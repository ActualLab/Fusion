using System.Runtime.Serialization;
using MemoryPack;
using MessagePack;
using static System.Console;

namespace Samples.MultiServerRpc;

public record ServerId(Symbol Id); // Used just to display the message with Server ID

public interface IChat : IComputeService
{
    [ComputeMethod]
    public Task<List<string>> GetRecentMessages(Symbol chatId, CancellationToken cancellationToken = default);
    [ComputeMethod]
    public Task<int> GetWordCount(Symbol chatId, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task Post(Chat_Post command, CancellationToken cancellationToken);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
// ReSharper disable once InconsistentNaming
public sealed partial record Chat_Post(
    [property: DataMember, MemoryPackOrder(0), Key(0)] Symbol ChatId,
    [property: DataMember, MemoryPackOrder(1), Key(1)] string Message
    ) : ICommand<Unit>;

public class Chat(ServerId serverId) : IChat
{
    private readonly object _lock = new();
    private readonly Dictionary<Symbol, List<string>> _chats = new();

    private ServerId ServerId { get; } = serverId;

    public virtual Task<List<string>> GetRecentMessages(Symbol chatId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult(_chats.GetValueOrDefault(chatId) ?? new());
    }

    public virtual async Task<int> GetWordCount(Symbol chatId, CancellationToken cancellationToken = default)
    {
        // Note that GetRecentMessages call here becomes a dependency of WordCount call,
        // and that's why it gets invalidated automatically.
        var messages = await GetRecentMessages(chatId, cancellationToken).ConfigureAwait(false);
        return messages
            .Select(m => m.Split(" ", StringSplitOptions.RemoveEmptyEntries).Length)
            .Sum();
    }

    public virtual Task Post(Chat_Post command, CancellationToken cancellationToken)
    {
        var chatId = command.ChatId;
        WriteLine($"{ServerId.Id}: got {command}");
        lock (_lock) {
            var posts = _chats.GetValueOrDefault(chatId) ?? new(); // We can't update the list itself (it's shared), but can re-create it
            posts.Add(command.Message);
            if (posts.Count > 10)
                posts.RemoveAt(0);
            _chats[chatId] = posts;
        }
        using var _1 = Invalidation.Begin();
        _ = GetRecentMessages(chatId, default); // No need to invalidate GetWordCount
        return Task.CompletedTask;
    }
}

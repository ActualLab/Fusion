using System.Text;
using Newtonsoft.Json;
using ActualLab.IO;

namespace ActualLab.Caching;

public abstract class FileSystemCacheBase<TKey, TValue> : AsyncCacheBase<TKey, TValue>
    where TKey : notnull
{
#if !NETSTANDARD2_0
    private const int BufferSize = -1;
#else
    private const int MinAllowedBufferSize = 128;
    private const int BufferSize = MinAllowedBufferSize;
#endif

    public override async ValueTask<Option<TValue>> TryGet(TKey key, CancellationToken cancellationToken = default)
    {
        try {
#pragma warning disable CA2007
            await using var fileStreamWrapper = OpenFile(GetFileName(key), false, cancellationToken)
                .ToAsyncDisposableAdapter()
                .ConfigureAwait(false);
#pragma warning restore CA2007
            var fileStream = fileStreamWrapper.Target;
            var pairs = Deserialize(await GetText(fileStream, cancellationToken).ConfigureAwait(false));
            return pairs is not null && pairs.TryGetValue(key, out var v) ? v : Option<TValue>.None;
        }
        catch (IOException) {
            return default;
        }
    }

    protected override async ValueTask Set(TKey key, Option<TValue> value, CancellationToken cancellationToken = default)
    {
        try {
            // The logic here is more complex than it seems to make sure the update is atomic,
            // i.e. the file is locked for modifications between read & write operations.
            var fileName = GetFileName(key);
            var newText = (string?) null;
            var fileStreamWrapper = OpenFile(fileName, true, cancellationToken)
                .ToAsyncDisposableAdapter()
                .ConfigureAwait(false);
#pragma warning disable CA2007
            await using (var _ = fileStreamWrapper) {
#pragma warning restore CA2007
                var fileStream = fileStreamWrapper.Target;
                var originalText = await GetText(fileStream, cancellationToken).ConfigureAwait(false);
                var pairs =
                    Deserialize(originalText)
                    ?? new Dictionary<TKey, TValue>();
                if (value.IsSome(out var v))
                    pairs[key] = v;
                else
                    pairs.Remove(key);
                newText = Serialize(pairs);
                await SetText(fileStream, newText, cancellationToken).ConfigureAwait(false);
            }
            if (newText is null)
                File.Delete(fileName);
        }
        catch (IOException) {}
    }

    protected abstract FilePath GetFileName(TKey key);

    protected virtual FileStream? OpenFile(FilePath fileName, bool forWrite,
        CancellationToken cancellationToken)
    {
        if (!forWrite)
            return File.Exists(fileName) ? File.OpenRead(fileName) : null;

        var dir = Path.GetDirectoryName(fileName);
        Directory.CreateDirectory(dir!);
        return File.Open(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    protected virtual async Task<string?> GetText(FileStream? fileStream, CancellationToken cancellationToken)
    {
        if (fileStream is null)
            return null;
        try {
            fileStream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize, true);
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            return text.NullIfEmpty();
        }
        catch (IOException) {
            return null;
        }
    }

    protected virtual async ValueTask SetText(FileStream? fileStream, string? text, CancellationToken cancellationToken)
    {
        if (fileStream is null)
            return;
        fileStream.Seek(0, SeekOrigin.Begin);
#pragma warning disable CA2007
        await using var writerWrapper = new StreamWriter(fileStream, Encoding.UTF8, BufferSize, true)
            .ToAsyncDisposableAdapter()
            .ConfigureAwait(false);
#pragma warning restore CA2007
        var writer = writerWrapper.Target!;
        await writer.WriteAsync(text ?? "").ConfigureAwait(false);
        fileStream.SetLength(fileStream.Position);
    }

    protected virtual Dictionary<TKey, TValue>? Deserialize(string? source)
        => source is null
            ? null
            : JsonConvert.DeserializeObject<Dictionary<TKey, TValue>>(source);

    protected virtual string? Serialize(Dictionary<TKey, TValue>? source)
        => source is null || source.Count == 0
            ? null
            : JsonConvert.SerializeObject(source);
}

public class FileSystemCache<TKey, TValue>(
    FilePath cacheDirectory,
    string? extension = null,
    Func<TKey, string>? keyToFileNameConverter = null
    ) : FileSystemCacheBase<TKey, TValue>
    where TKey : notnull
{
    // ReSharper disable once StaticMemberInGenericType
    protected static readonly string DefaultFileExtension = ".tmp";
    protected static readonly Func<TKey, string> DefaultKeyToFileNameConverter =
        key => FilePath.GetHashedName(key?.ToString() ?? "0_0");

    public string CacheDirectory { get; } = cacheDirectory;
    public string FileExtension { get; } = extension ?? DefaultFileExtension;
    public Func<TKey, string> KeyToFileNameConverter { get; } = keyToFileNameConverter ?? DefaultKeyToFileNameConverter;

    public void Clear()
    {
        if (!Directory.Exists(CacheDirectory))
            return;
        var names = Directory.EnumerateFiles(
            CacheDirectory, "*" + FileExtension,
            SearchOption.TopDirectoryOnly);
        foreach (var name in names)
            File.Delete(name);
    }

    protected override FilePath GetFileName(TKey key)
        => CacheDirectory & new FilePath(KeyToFileNameConverter(key) + FileExtension);
}

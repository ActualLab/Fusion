using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Samples.TodoApp.Abstractions;

namespace Samples.TodoApp.Services.Db;

[Table("Todos")]
public class DbTodo
{
    [Key] public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public bool IsDone { get; set; }

    public DbTodo() { }
    public DbTodo(string folder, TodoItem item)
    {
        Key = ComposeKey(folder, item.Id);
        UpdateFrom(item);
    }

    public void UpdateFrom(TodoItem item)
    {
        Title = item.Title;
        IsDone = item.IsDone;
    }

    public TodoItem ToModel()
        => new(SplitKey(Key).Id, Title, IsDone);

    public static string ComposeKey(string folder, Ulid id)
        => $"{folder}/{id.ToString()}";

    public static (string Folder, Ulid Id) SplitKey(string key)
    {
        var lastSlashIndex = key.LastIndexOf('/');
        if (lastSlashIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(key));

        var folder = key[..lastSlashIndex];
        var id = Ulid.Parse(key[(lastSlashIndex + 1)..], CultureInfo.InvariantCulture);
        return (folder, id);
    }
}

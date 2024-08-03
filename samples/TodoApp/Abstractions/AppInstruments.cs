using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Templates.TodoApp.Abstractions;

public static class AppInstruments
{
    public static readonly ActivitySource ActivitySource = new("Templates.TodoApp");
    public static readonly Meter Meter = new("Templates.TodoApp");
}

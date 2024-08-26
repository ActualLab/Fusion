using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Samples.TodoApp.Abstractions;

public static class AppInstruments
{
    public static readonly ActivitySource ActivitySource = new("Samples.TodoApp");
    public static readonly Meter Meter = new("Samples.TodoApp");
}

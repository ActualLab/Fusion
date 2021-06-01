using System;
using System.Reactive;
using System.Threading;
using Stl.CommandR;

namespace Stl.Tests.CommandR.Services
{
    public record LogCommand : CommandBase<Unit>
    {
        public string Message { get; set; } = "";
    }

    public record DivCommand : CommandBase<double>
    {
        public double Divisible { get; set; }
        public double Divisor { get; set; }
    }

    public record RecSumCommand : CommandBase<double>
    {
        public static AsyncLocal<object> Tag { get; } = new();

        public double[] Arguments { get; set; } = Array.Empty<double>();
        public bool Isolate { get; set; }
    }

    public record RecAddUsersCommand : CommandBase<Unit>
    {
        public User[] Users { get; set; } = Array.Empty<User>();
    }
}

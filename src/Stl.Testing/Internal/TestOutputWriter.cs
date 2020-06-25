using System;
using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace Stl.Testing.Internal
{
    public class TestOutputWriter : TextWriter
    {
        protected static readonly string EnvNewLine = Environment.NewLine;
        protected static readonly char LastEnvNewLineChar = EnvNewLine[^1];

        protected StringBuilder Prefix = new StringBuilder();
        public ITestOutputHelper TestOutput { get; }
        public override Encoding Encoding { get; } = Encoding.UTF8;

        public TestOutputWriter(ITestOutputHelper testOutput) 
            => TestOutput = testOutput;

        public override void Write(char value)
        {
            if (value == LastEnvNewLineChar)
                Write(value.ToString());
            else
                Prefix.Append(value);
        }

        public override void Write(string? value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Prefix.Append(value);
            if (!value.Contains(LastEnvNewLineChar))
                return;
            var lines = Prefix.ToString().Split(EnvNewLine);
            foreach (var line in lines[..^1])
                TestOutput.WriteLine(line);
            Prefix.Clear();
            Prefix.Append(lines[^1]);
        }
    }        

}

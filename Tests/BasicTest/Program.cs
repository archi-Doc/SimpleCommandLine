// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace ConsoleApp1
{
    public class BaseOptions
    {
        [SimpleOption("directory", ShortName = "d")]
        public string Directory { get; set; } = string.Empty;
    }

    public class TestOptions : BaseOptions
    {
        [SimpleOption("name", Required = true)]
        public string Name { get; set; } = string.Empty;

        [SimpleOption("number", ShortName = "n")]
        public int Number { get; set; } = 10;

        [SimpleOption("op5", Required = false)]
        public TestOptions5 Options5 { get; set; } = new TestOptions5() with { File = "A1", };
    }

    [SimpleCommand("test")]
    public class TestCommand : ISimpleCommandAsync<TestOptions>
    {
        public async Task RunAsync(TestOptions option, string[] args)
        {
            Console.WriteLine("Test command:");
            Console.WriteLine($"{option.Name}, {option.Number}, {option.Options5}");
        }
    }

    [SimpleCommand("test2")]
    public class TestCommand2 : ISimpleCommand
    {
        public void Run(string[] args)
        {
            Console.WriteLine("Test command2:");
        }
    }

    public class TestOptions3 : BaseOptions
    {
        [SimpleOption("text", ShortName = "t", Required = true)]
        public string Text { get; set; } = string.Empty;

        [SimpleOption("options")]
        public TestOptions Options { get; set; } = default!;

        [SimpleOption("options3b")]
        public TestOptions3b Options3b { get; set; } = default!;
    }

    public class TestOptions3b
    {
        [SimpleOption("name", ShortName = "n", Required = true)]
        public string Name { get; set; } = string.Empty;
    }

    [SimpleCommand("test3")]
    public class TestCommand3 : ISimpleCommand<TestOptions3>
    {
        public void Run(TestOptions3 option, string[] args)
        {
            Console.WriteLine("Test command3:");
            Console.WriteLine($"Test: {option.Text}");
            Console.WriteLine($"Option: {option.Options.Directory} - {option.Options.Number}");
            Console.WriteLine($"Option3b: {option.Options3b.Name}");
        }
    }

    public record TestOptions4
    {
        [SimpleOption("name", ShortName = "n", Required = false)]
        public string Name { get; set; } = string.Empty;

        [SimpleOption("id", Required = false)]
        public int Id { get; init; } = 99;

        [SimpleOption("op5", Required = false)]
        public TestOptions5 Options5 { get; set; } = new TestOptions5() with { File = "A", };
    }

    public record TestOptions5
    {
        [SimpleOption("file", ShortName = "f", Required = false)]
        public string File { get; set; } = string.Empty;
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var commandTypes = new Type[]
            {
                typeof(TestCommand),
                typeof(TestCommand2),
                typeof(TestCommand3),
            };

            var op4 = new TestOptions4() { Id = 1, Name = "A", Options5 = new() { File = "X", } };
            var b = SimpleParser.TryParseOptions<TestOptions4>(string.Empty, out var op4b);
            b = SimpleParser.TryParseOptions<TestOptions4>(string.Empty, out op4b, op4);

            // await SimpleParser.ParseAndRunAsync(commandTypes, args);
            await SimpleParser.ParseAndRunAsync(commandTypes, "test abc -ab 23");

            // await SimpleParser.ParseAndRunAsync(commandTypes, "test3 -t aa -options3b [-name2 ya -name tst] "); // -options {-n 99}
            // await SimpleParser.ParseAndRunAsync(commandTypes, "-n 12 -op5 [-file \"jj\"]"); // -options {-n 99}
            // await SimpleParser.ParseAndRunAsync(commandTypes, "test3 -text aaa -options3b -encodedCommand ewB9AA== -inputFormat xml -outputFormat text");

            // await SimpleParser.ParseAndRunAsync(commandTypes, "help");
        }
    }
}

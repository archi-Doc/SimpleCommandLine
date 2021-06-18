// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace ConsoleApp1
{
    /// <summary>
    /// A class that stores command options.
    /// </summary>
    public class TestOptions
    {
        [SimpleOption("number", "n", "test number")] // Annotate SimpleOptionAttribute and specify a long/short option name.
        public int Number { get; set; } = 10; // Set a default value.

        [SimpleOption("text", "t", "test text.", Required = true)] // Set Required to true if you want to make the option required.
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// A class that process the command function.
    /// </summary>
    [SimpleCommand("test", "Test command.")] // Annotate SimpleCommandAttribute and specify a command name.
    public class TestCommand : ISimpleCommandAsync<TestOptions> // Implementation of either ISimpleCommandAsync<T> or ISimpleCommand<T> is required.
    {
        public async Task Run(TestOptions option, string[] args)
        {
            Console.WriteLine("Test command");
            Console.WriteLine($"Number is {option.Number}");
            Console.WriteLine($"Text is {option.Text}");
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var commandTypes = new Type[]
            {
                typeof(TestCommand),
            };

            await SimpleParser.ParseAndRunAsync(commandTypes, "-Number 1 -Text \"tes\"");

            var p = new SimpleParser(commandTypes);
            p.ShowVersion();
            p.ShowHelp();
        }
    }
}

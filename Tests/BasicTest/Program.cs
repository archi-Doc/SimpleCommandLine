// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace ConsoleApp1
{
    public class BaseOptions
    {
        [SimpleOption("directory", "d")]
        public string Directory { get; set; } = string.Empty;
    }

    public class TestOptions : BaseOptions
    {
        [SimpleOption("number", "n")]
        public int Number { get; set; } = 10;
    }

    [SimpleCommand("test")]
    public class TestCommand : ISimpleCommandAsync<TestOptions>
    {
        public async Task Run(TestOptions option, string[] args)
        {
            Console.WriteLine("Test command:");
            Console.WriteLine($"Number is {option.Number}");
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

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var commandTypes = new Type[]
            {
                typeof(TestCommand),
                typeof(TestCommand2),
            };

            // await SimpleParser.ParseAndRunAsync(commandTypes, args);
            await SimpleParser.ParseAndRunAsync(commandTypes, "help");
        }
    }
}

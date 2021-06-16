// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

namespace ConsoleApp1
{
    [SimpleCommand("test", Runner)]
    public class TestCommand : ISimpleCommand
    {
        [SimpleOption("number", "n")]
        public int N { get; set; } = 10;

        public void Run()
        {
            Console.WriteLine("Test command");
            Console.WriteLine($"N is {this.N}");
        }

        public async Task Run(string[] args)
        {
            await Task.Delay(4000);
            Console.WriteLine("Test command");
            Console.WriteLine($"N is {this.N}");
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

            var commands = new ISimpleCommand[]
            {
                new TestCommand(),
            };

            await SimpleParser.ParseAndRunAsync(commandTypes, "");

            var p = SimpleParser.Parse(commandTypes, "");

            /*var p = SimpleParser.Parse(commandTypes, args);
            p.Run();
            p.ShowHelp();*/
        }
    }
}

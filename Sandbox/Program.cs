// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

namespace ConsoleApp1
{
    [SimpleCommand("test")]//, typeof(TestRunner))]
    public class TestCommand// : ISimpleCommand
    {
        [SimpleOption("number", "n")]
        public int N { get; set; } = 10;

        public async Task Run(string[] args)
        {
            //  new TestRunner(this);
            Console.WriteLine("Test command");
            await Task.Delay(4000);
            Console.WriteLine($"N is {this.N}");
        }
    }

    [SimpleCommand("test")]//, typeof(TestRunner))]
    public class TestOptions// : ISimpleCommand
    {
        [SimpleOption("number", "n")]
        public int N { get; set; } = 10;

        /*public async Task Run(string[] args)
        {
            //  new TestRunner(this);
            Console.WriteLine("Test command");
            await Task.Delay(4000);
            Console.WriteLine($"N is {this.N}");
        }*/
    }

    [SimpleCommand("test2")]
    public class TestCommand2 : ISimpleCommand<TestOptions>
    {
        public async Task Run(TestOptions options, string[] args)
        {
            if (args.Length == 0)
            {
                return;
            }

            switch(args[0])
            {
                case "a":
                    await Test1(options);
                    break;
            }
        }

        public async Task Test1(TestOptions options)
        {
        }
    }

    public class TestRunner : ISimpleCommand<TestOptions>
    {
        public Task Run(TestOptions option, string[] args)
        {
            return Task.CompletedTask;
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

            /*var commands = new ISimpleCommand[]
            {
                new TestCommand(),
            };*/

            await SimpleParser.ParseAndRunAsync(commandTypes, "");

            var p = SimpleParser.Parse(commandTypes, "-help");
            await p.RunAsync();

            /*var p = SimpleParser.Parse(commandTypes, args);
            p.Run();
            p.ShowHelp();*/
        }
    }
}

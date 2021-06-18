// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace ConsoleApp1
{
    [SimpleCommand("test")]
    public class ObsoleteCommand
    {
        [SimpleOption("number", "n")]
        public int N { get; set; } = 10;

        public async Task Run(string[] args)
        {
            Console.WriteLine("Test command");
            await Task.Delay(4000);
            Console.WriteLine($"N is {this.N}");
        }
    }

    public class TestOptions
    {
        [SimpleOption("number", "n")]
        public int N { get; set; } = 10;
    }

    [SimpleCommand("test")]
    public class TestCommand : ISimpleCommandAsync<TestOptions>
    {
        public async Task Run(TestOptions options, string[] args)
        {
            Console.WriteLine("test command");
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
            await Task.Delay(1000);
        }
    }

    [SimpleCommand("derived")]
    public class DerivedCommand : TestCommand
    {
        public DerivedCommand()
        {
        }

        public new async Task Run(TestOptions options, string[] args)
        {
            Console.WriteLine("derived command");
        }
    }

    [SimpleCommand("sync")]
    public class SyncCommand : ISimpleCommand
    {
        public void Run(string[] args)
        {
        }

        public void Run(object option, string[] args)
        {
        }
    }

    [SimpleCommand("test2")]
    public class TestCommand2 : ISimpleCommandAsync
    {
        public async Task Run(string[] args)
        {
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
                typeof(DerivedCommand),
                typeof(SyncCommand),
            };

            await RunArg("");

            var p = new SimpleParser(commandTypes);
            p.Parse("-help");
            await p.RunAsync();

            p.Parse("");
            p.Run();

            /*var p = SimpleParser.Parse(commandTypes, args);
            p.Run();
            p.ShowHelp();*/

            async Task RunArg(string arg)
            {
                Console.WriteLine(arg);
                await SimpleParser.ParseAndRunAsync(commandTypes, arg);
            }
        }
    }
}

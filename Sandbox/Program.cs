// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using DryIoc;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace ConsoleApp1
{
    public interface ICommandService
    {
        void Enter(string directory);

        void Exit();
    }

    public class CommandService : ICommandService
    {
        public void Enter(string directory)
        {
        }

        public void Exit()
        {
        }
    }

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
        [SimpleOption("directory", null, "base directory for storing application data")]
        public string Directory { get; set; } = string.Empty;

        [SimpleOption("mode", null, "mode(receive, transfer)")]
        public string Mode { get; private set; } = "receive";

        [SimpleOption("port", null, "local port number to transfer packets")]
        public int Port { get; } = 2000;

        [SimpleOption("targetip", null, "target ip address", Required = true)]
        public string TargetIp { get; } = string.Empty;

        [SimpleOption("targetport", null, "target port number")]
        public int TargetPort { get; } = 1000;

        [SimpleOption("receiver", null, "true if the node is receiver")]
        public bool Receiveer { get; } = true;

        [SimpleOption("n", null, "test N")]
        public int N { get; } = 4;
    }

    [SimpleCommand("test")]
    public class TestCommand : ISimpleCommandAsync<TestOptions>
    {
        public TestCommand(ICommandService commandService)
        {
            this.CommandService = commandService;
        }

        public async Task Run(TestOptions options, string[] args)
        {
            Console.WriteLine("test command");
        }

        public ICommandService CommandService { get; }
    }

    [SimpleCommand("derived")]
    public class DerivedCommand : TestCommand
    {
        public DerivedCommand(ICommandService commandService)
            : base(commandService)
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

            var container = new Container();
            container.Register<ICommandService, CommandService>(Reuse.Singleton);
            foreach (var x in commandTypes)
            {
                container.Register(x, Reuse.Singleton);
            }

            container.ValidateAndThrow();

            var parserOptions = SimpleParserOptions.Standard with
            {
                ServiceProvider = container,
                RequireStrictCommandName = true,
                RequireStrictOptionName = true
            };

            // await RunArg("", parserOptions);

            var p = new SimpleParser(commandTypes, parserOptions);

            p.Parse("test -mode receive -port 12211 -targetip 3.18.216.240 -targetport 49152");
            await p.RunAsync();

            /*var p = SimpleParser.Parse(commandTypes, args);
            p.Run();
            p.ShowHelp();*/

#pragma warning disable CS8321 // Local function is declared but never used
            async Task RunArg(string arg, SimpleParserOptions options)
#pragma warning restore CS8321 // Local function is declared but never used
            {
                Console.WriteLine(arg);
                await SimpleParser.ParseAndRunAsync(commandTypes!, arg, options);
            }

            container.Dispose();
        }
    }
}

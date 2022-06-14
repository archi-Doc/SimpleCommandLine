// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using DryIoc;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8321 // Local function is declared but never used

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

    public enum TestEnum
    {
        Yes,
        No,
        Hanbun,
    }

    public record TestOptions
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
        public bool Receiver { get; } = true;

        [SimpleOption("n", null, "test N")]
        public int N { get; init; } = 4;

        [SimpleOption("enum", null, "test enum", Required = true)]
        public TestEnum Enum { get; } = TestEnum.Yes;
    }

    [SimpleCommand("test", "description")]
    public class TestCommand : ISimpleCommandAsync<TestOptions>
    {
        public TestCommand(ICommandService commandService)
        {
            this.CommandService = commandService;
        }

        public async Task Run(TestOptions options, string[] args)
        {
            Console.WriteLine("test command");
            Console.WriteLine();

            this.Options = options with { };
            this.Options.Directory = "10";

            Console.WriteLine($"Directory: {Options.Directory}");
            Console.WriteLine($"Mode: {Options.Mode}");
            Console.WriteLine($"Port: {Options.Port}");
            Console.WriteLine($"TargetIp: {Options.TargetIp}");
            Console.WriteLine($"TargetPort: {Options.TargetPort}");
            Console.WriteLine($"Receiver: {Options.Receiver}");
            Console.WriteLine($"N: {Options.N}");
            Console.WriteLine($"Enum: {Options.Enum}");
        }

        public ICommandService CommandService { get; }

        public TestOptions Options { get; private set; } = default!;
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

    [SimpleCommand("nest", IsSubcommand = true)]
    public class SyncCommand : ISimpleCommand
    {
        public void Run(string[] args)
        {
            SimpleParser.ParseAndRun(new[] { typeof(TestCommand2) }, args);
        }
    }

    [SimpleCommand("test2")]
    public class TestCommand2 : ISimpleCommandAsync
    {
        public async Task Run(string[] args)
        {
            Console.WriteLine("Test2");
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
                RequireStrictOptionName = true,
                DoNotDisplayUsage = true,
                DisplayCommandListAsHelp = true,
            };

            // await RunArg("", parserOptions);

            // await SimpleParser.ParseAndRunAsync(commandTypes, args, parserOptions); // Main process

            var p = new SimpleParser(commandTypes, parserOptions);

            p.Parse("test -mode receive -port 12211 -targetip 127.0.0.1 -targetport 1000 -enum hanbun 333");
            // p.Parse("nest test2 222 -name \"ABC\"");
            await p.RunAsync();
            Console.WriteLine();

            /*var p = SimpleParser.Parse(commandTypes, args);
            p.Run();
            p.ShowHelp();*/

            async Task RunArg(string arg, SimpleParserOptions options)
            {
                Console.WriteLine(arg);
                await SimpleParser.ParseAndRunAsync(commandTypes!, arg, options);
            }

            p.ShowHelp();

            container.Dispose();
        }
    }
}

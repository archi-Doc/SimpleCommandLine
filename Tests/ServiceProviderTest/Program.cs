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
            Console.WriteLine("Enter");
        }

        public void Exit()
        {
            Console.WriteLine("Exit");
        }
    }

    public class TestOptions
    {
        [SimpleOption("number", "n")]
        public int Number { get; set; } = 10;
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
            this.CommandService.Enter(string.Empty);

            Console.WriteLine("Test command");
            await Task.Delay(1000);
            Console.WriteLine($"N is {options.Number}");

            this.CommandService.Exit();
        }

        public ICommandService CommandService { get; }
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
                RequireStrictCommandName = false,
                RequireStrictOptionName = true
            };

            await SimpleParser.ParseAndRunAsync(commandTypes, args, parserOptions);

            container.Dispose();
        }
    }
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using DryIoc;
using SimpleCommandLine;
using Tinyhand;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8321 // Local function is declared but never used
#pragma warning disable SA1602 // Enumeration items should be documented

namespace ConsoleApp1;

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
    [SimpleOption("number", ShortName = "n")]
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
    [SimpleOption("directory", Description = "base directory for storing application data")]
    public string Directory { get; set; } = string.Empty;

    [SimpleOption("mode", Description = "mode(receive, transfer)")]
    public string Mode { get; private set; } = "receive";

    [SimpleOption("port", Description = "local port number to transfer packets", Required = true, ReadFromEnvironment = true)]
    public int Port { get; } = 2000;

    [SimpleOption("targetip", Description = "target ip address", ReadFromEnvironment = true)]
    public string TargetIp { get; } = "test"; // string.Empty;

    [SimpleOption("targetport", Description = "target port number")]
    public int TargetPort { get; } = 1000;

    [SimpleOption("receiver", Description = "true if the node is receiver")]
    public bool Receiver { get; } = true;

    [SimpleOption("n", Description = "test N")]
    public int N { get; init; } = 4;

    [SimpleOption("enum", Description = "test enum", Required = true)]
    public TestEnum Enum { get; } = TestEnum.Yes;

    [SimpleOption("sub", Description = "sub option")]
    public TestSubOptions Sub { get; } = default!;
}

[TinyhandObject(AddAlternateKey = true)]
public partial record TestSubOptions
{
    [Key(0)]
    [SimpleOption("name", ShortName = "n", Required = true)]
    public string Name { get; set; } = string.Empty;
}

[SimpleCommand("test", Description = "description", Alias = "t")]
public class TestCommand : ISimpleCommandAsync<TestOptions>
{
    public TestCommand(ICommandService commandService)
    {
        this.CommandService = commandService;
    }

    public async Task RunAsync(TestOptions options, string[] args)
    {
        Console.WriteLine("test command");
        Console.WriteLine();

        this.Options = options with { };
        this.Options.Directory = "10";

        Console.WriteLine($"Directory: {this.Options.Directory}");
        Console.WriteLine($"Mode: {this.Options.Mode}");
        Console.WriteLine($"Port: {this.Options.Port}");
        Console.WriteLine($"TargetIp: {this.Options.TargetIp} {this.Options.TargetIp.Length}");
        Console.WriteLine($"TargetPort: {this.Options.TargetPort}");
        Console.WriteLine($"Receiver: {this.Options.Receiver}");
        Console.WriteLine($"N: {this.Options.N}");
        Console.WriteLine($"Enum: {this.Options.Enum}");
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

    public new async Task RunAsync(TestOptions options, string[] args)
    {
        Console.WriteLine("derived command");
    }
}

[SimpleCommand("nested-command", IsSubcommand = true)]
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
    public async Task RunAsync(string[] args)
    {
        Console.WriteLine("Test2");
    }
}

[SimpleCommand("test3")]
public partial class TestCommand3 : ISimpleCommandAsync<TestCommand3.Options>
{
    [TinyhandObject(ImplicitMemberNameAsKey = true)]
    public partial record class Options
    {
        [SimpleOption("A", Description = "AA")]
        public int A { get; set; }

        [SimpleOption("B", Description = "BB")]
        public double B { get; set; }

        [SimpleOption("C", Description = "CC")]
        public TestSubOptions SubOptions { get; set; } = new();
    }

    public async Task RunAsync(Options options, string[] args)
    {
        Console.WriteLine($"Test3: {options}");
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var args2 = SimpleParserHelper.GetCommandLineArguments();
        Console.WriteLine($"Arguments: {args2}");

        var commandTypes = new Type[]
        {
            typeof(TestCommand),
            typeof(TestCommand2),
            typeof(TestCommand3),
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
            AutoAlias = true,
        };

        var options = new TestOptions();
        // var b = SimpleParser.TryParseOptions<TestOptions>("test  -targetip '127.0.0.1' \"testdir\" -targetport 123 -enum hanbun", out options);
        // b = SimpleParser.TryParseOptions<TestOptions>("-enum Yes", out options, options);

        // await RunArg("", parserOptions);

        // await SimpleParser.ParseAndRunAsync(commandTypes, args, parserOptions); // Main process

        var p = new SimpleParser(commandTypes, parserOptions);

        var op = TinyhandSerializer.DeserializeFromString<TestCommand3.Options>("A=2 B = 3.2 C=\"abc\"");

        await p.ParseAndRunAsync("--help test3");
        await p.ParseAndRunAsync("test3 A=2 B = 3.2 C=\"abc\"");

        /* 
        await p.ParseAndRunAsync("test -targetip ttt A=2 B = 3");
        await p.ParseAndRunAsync("test help");
        p.Parse("t -mode receive -targetport 1000 -enum hanbun -n 999"); // -port 12211 -targetip 127.0.0.1
        await p.RunAsync();*/

        /*var p = SimpleParser.Parse(commandTypes, args);
        p.Run();
        p.ShowHelp();*/

        async Task RunArg(string arg, SimpleCommandLine.SimpleParserOptions options)
        {
            Console.WriteLine(arg);
            await SimpleParser.ParseAndRunAsync(commandTypes!, arg, options);
        }

        container.Dispose();
    }
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Arc;
using Arc.Unit;
using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
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

public enum TestEnum
{
    Yes,
    No,
    Hanbun,
}

public record class TestOptions
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
    public TestSubOptions(string name)
    {
        this.Name = name;
    }

    [Key(0)]
    [SimpleOption("name", ShortName = "n", Required = true)]
    public string Name { get; set; } = string.Empty;
}

[TinyhandObject(AddAlternateKey = true)]
public partial record TestClass : IStringConvertible<TestClass>
{
    public TestClass(string name)
    {
        this.Name = name;
    }

    static int IStringConvertible<TestClass>.MaxStringLength => 16;

    [Key(0)]
    public string Name { get; set; } = string.Empty;

    public static bool TryParse(ReadOnlySpan<char> source, out TestClass? @object, out int read, IConversionOptions? conversionOptions)
    {
        @object = new(source.ToString());
        read = source.Length;
        return true;
    }

    int IStringConvertible<TestClass>.GetStringLength()
    {
        return this.Name.Length;
    }

    bool IStringConvertible<TestClass>.TryFormat(Span<char> destination, out int written, IConversionOptions? conversionOptions)
    {
        var span = this.Name.AsSpan();
        span.CopyTo(destination);
        written = span.Length;
        return true;
    }
}

[SimpleCommand("test", Description = "description", Alias = "t")]
public class TestCommand : ISimpleCommand<TestOptions>
{
    public TestCommand(ICommandService commandService)
    {
        this.CommandService = commandService;
    }

    public async Task Execute(TestOptions options, string[] args, CancellationToken cancellationToken)
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

    public new async Task Execute(TestOptions options, string[] args, CancellationToken cancellationToken)
    {
        Console.WriteLine("derived command");
    }
}

[SimpleCommand("nested-command", IsSubcommand = true)]
public class SyncCommand : ISimpleCommand
{
    public async Task Execute(string[] args, CancellationToken cancellationToken)
    {
        await SimpleParser.ParseAndExecute(new[] { typeof(TestCommand2) }, args);
    }
}

[SimpleCommand("test2")]
public class TestCommand2 : ISimpleCommand
{
    public async Task Execute(string[] args, CancellationToken cancellationToken)
    {
        Console.WriteLine("Test2");
    }
}

[SimpleCommand("test3")]
public class TestCommand3 : ISimpleCommand<TestCommand3.Options>
{
    public record class Options
    {
        [SimpleOption("A", Description = "AA")]
        public int A { get; set; }

        [SimpleOption("B", Description = "BB")]
        public double B { get; set; }

        [SimpleOption("C", Description = "CC")]
        public TestSubOptions SubOptions { get; set; } = TestSubOptions.UnsafeConstructor();

        [SimpleOption("Class1", Description = "DD")]
        public TestClass Class1 { get; set; } = TestClass.UnsafeConstructor();
    }

    private readonly IConsoleService consoleService;

    public TestCommand3(IConsoleService consoleService)
    {
        this.consoleService = consoleService;
    }

    public async Task Execute(Options options, string[] args, CancellationToken cancellationToken)
    {
        this.consoleService.WriteLine($"Test3: {options}", ConsoleColor.Red);
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

        var builder = new UnitBuilder();
        builder.Configure(context =>
        {
            context.AddSingleton<IConsoleService, ConsoleService>();
            context.AddSingleton<ICommandService, CommandService>();

            foreach (var x in commandTypes)
            {
                context.AddCommand(x);
            }
        });

        var container = new Container(rules => rules.WithMicrosoftDependencyInjectionRules());
        builder.SetServiceProviderFactory(services => container.WithDependencyInjectionAdapter(services));
        var unit = builder.Build();

        /*var container = new Container(rules => rules.WithMicrosoftDependencyInjectionRules());

        container.Register<IConsoleService, ConsoleService>(Reuse.Singleton);
        container.Register<ICommandService, CommandService>(Reuse.Singleton);
        foreach (var x in commandTypes)
        {
            container.Register(x, Reuse.Singleton);
        }

        container.ValidateAndThrow();*/

        var parserOptions = SimpleParserOptions.Standard with
        {
            ServiceProvider = unit.Context.ServiceProvider,
            RequireStrictCommandName = true,
            RequireStrictOptionName = true,
            DoNotDisplayUsage = true,
            AutoAlias = true,
        };

        var options = new TestOptions();
        // var b = SimpleParser.TryParseOptions<TestOptions>("test  -targetip '127.0.0.1' \"testdir\" -targetport 123 -enum hanbun", out options);
        // b = SimpleParser.TryParseOptions<TestOptions>("-enum Yes", out options, options);

        // await SimpleParser.ParseAndExecute(commandTypes, args, parserOptions); // Main process

        var p = new SimpleParser(commandTypes, parserOptions);

        /*await p.ParseAndExecute("test3 help");
        await p.ParseAndExecute("test3 -help");
        await p.ParseAndExecute("help test3");*/

        await p.ParseAndExecute("test3 -abc");
        await p.ParseAndExecute("test3 -A 2 -B 3.2 -C {Name=abc1} -Class1 {Name=asdf}");
        await p.ParseAndExecute("test3 -A 2 -B 3.2 -C {Name=abc2} -Class1 qwer");
        await p.ParseAndExecute("test3 -A 2 -B 3.2 -C {Name=abc3} -Class1 {123}");

        /*await p.ParseAndExecute("test -targetip ttt -A 2 -B 3");
        await p.ParseAndExecute("test help");
        p.Parse("t -mode receive -targetport 1000 -enum hanbun -n 999"); // -port 12211 -targetip 127.0.0.1
        await p.Execute();*/

        /*var p = SimpleParser.Parse(commandTypes, args);
        p.Execute();
        p.ShowHelp();*/

        container.Dispose();
    }
}

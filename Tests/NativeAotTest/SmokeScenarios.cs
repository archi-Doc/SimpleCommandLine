// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;
using SimpleCommandLine;
using Tinyhand;

namespace NativeAotTest;

public static class SmokeScenarios
{
    public static async Task<int> Run()
    {
        var count = 0;
        var settings = SimpleParserOptions.Standard with { SuppressConsoleOutput = true, ReadCommandFromEnvironment = false };
        var builder = new SimpleParserBuilder()
            .AddCommand<OptionsCommand, Options>()
            .AddCommand<PlainCommand>()
            .AddCommand<InjectedCommand>()
            .AddOptions<NestedOptions>()
            .AddOptions<SerializedOptions>();
        var services = new ServiceCollection();
        var injected = new InjectedCommand("injected");
        services.AddSingleton(injected);
        using var provider = services.BuildServiceProvider();
        var parser = builder.Build(settings with { ServiceProvider = provider });

        Check(parser.Parse("run -name first -number 12 -field 3 -day friday -mode second -base 7 -hidden secret -nested {-value 9}"), "parse options");
        var options = (Options)parser.CurrentCommand!.OptionClass.OptionInstance!;
        Check(options.Number == 12 && options.Field == 3 && options.Day == DayOfWeek.Friday, "nullable and enum");
        Check(options.Mode == Mode.Second, "application enum metadata");
        Check(options.BaseValue == 7 && options.HiddenValue == "secret", "inherited private members and getter-only backing field");
        Check(options.Nested.Value == 9, "nested options");
        Check(parser.CurrentCommand.OptionClass.Options.Find(x => x.LongName == "hidden")!.PropertyInfo is not null, "private property metadata");
        using var cancellation = new CancellationTokenSource();
        await parser.Execute(cancellation.Token);
        var command = (OptionsCommand)parser.CurrentCommand.CommandInstance;
        Check(ReferenceEquals(command.Received, options) && command.Token == cancellation.Token, "explicit interface execution and token");

        Check(parser.Parse("run -name second -nested {-value 1} -nested {-value 2}"), "repeated nested option");
        options = (Options)parser.CurrentCommand!.OptionClass.OptionInstance!;
        Check(options.Nested.Value == 2, "last nested value wins");
        Check(options.Number is null && options.Day is null && options.BaseValue == 1, "new parse resets defaults");
        Check(!parser.Parse("run -name third -nested {-value 1} -nested {-value invalid}"), "invalid repeated nested value");
        Check(parser.CurrentCommand is null, "failed parse clears command");
        Check(!parser.Parse("run"), "required option");
        Check(parser.Parse("run -name recovered"), "recovery after required error");
        Check(!parser.Parse("run -name x -number invalid"), "invalid nullable value");
        Check(!parser.Parse("run -name x -day invalid"), "invalid enum value");
        Check(parser.Parse("run -name x -number -5"), "negative number");
        Check(parser.Parse("run -name x -serialized {Value=42}"), "Tinyhand serialized value");
        options = (Options)parser.CurrentCommand!.OptionClass.OptionInstance!;
        Check(options.Serialized.Value == 42, "Tinyhand generated formatter");

        Check(parser.Parse("plain extra"), "command without options");
        await parser.Execute(cancellation.Token);
        var plain = (PlainCommand)parser.CurrentCommand!.CommandInstance;
        Check(plain.Args is ["extra"] && plain.Token == cancellation.Token, "plain command execution");
        Check(parser.Parse("injected"), "DI command parse");
        await parser.Execute(cancellation.Token);
        Check(injected.Executed && injected.Tag == "injected", "DI command without default constructor");

        Check(parser.Parse("help run") && parser.HelpCommandName == "run", "help dispatch");
        Check(parser.Parse("version") && parser.VersionRequested, "version dispatch");
        var oldOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            var helpParser = builder.Build(settings with { SuppressConsoleOutput = false, ServiceProvider = provider });
            helpParser.ShowHelp();
            helpParser.ShowVersion("smoke");
        }
        finally
        {
            Console.SetOut(oldOutput);
        }

        var help = output.ToString();
        Check(help.Contains("Usage:") && help.Contains("-hidden") && help.Contains("{NestedOptions}"), "help with private and nested options");
        Check(help.Contains("smoke "), "version output");

        Check(builder.TryParseOptions<Options>("-name standalone -nested {-value 8}", out var parsed), "standalone options");
        Check(parsed!.Nested.Value == 8, "standalone nested value");
        Check(builder.TryParseOptions("-number 4 -name updated", out var updated, parsed) && ReferenceEquals(parsed, updated), "update existing instance");
        Check(updated!.Number == 4 && updated.Nested.Value == 8, "update preserves unspecified nested value");

        var unregistered = new SimpleParserBuilder().AddCommand<OptionsCommand, Options>();
        ExpectInvalid(() => unregistered.Build(), "AddOptions", "unregistered nested options fail clearly");
        Check(new SimpleParserBuilder().AddCommand<PlainCommand>().AddCommand<PlainCommand>().Build(settings).NameToCommand.Count == 1, "idempotent command registration");
        var snapshotBuilder = new SimpleParserBuilder().AddCommand<PlainCommand>();
        var snapshot = snapshotBuilder.Build(settings);
        snapshotBuilder.AddCommand<OptionsCommand, Options>();
        Check(snapshot.NameToCommand.Count == 1, "builder snapshot");

        ExpectInvalid(() => new SimpleParserBuilder().AddCommand<IndexerCommand, IndexerOptions>().Build(settings), "indexer", "reject indexed properties");
        ExpectInvalid(() => new SimpleParserBuilder().AddCommand<CircularCommand, CircularOptions>().Build(settings), "Circular", "reject circular options");

        var primitiveBuilder = new SimpleParserBuilder();
        Check(primitiveBuilder.TryParseOptions<PrimitiveOptions>("-sbyte -12 -byte 34 -short -1234 -ushort 5678 -int -90000 -uint 90000 -long -1234567890123 -ulong 1234567890123 -bool true -float 1.5 -double -2.25 -decimal 3.75 -char x", out var primitives), "primitive conversion");
        Check(primitives!.SByte == -12 && primitives.Byte == 34 && primitives.Short == -1234 && primitives.UShort == 5678, "small integer values");
        Check(primitives.Int == -90000 && primitives.UInt == 90000 && primitives.Long == -1234567890123 && primitives.ULong == 1234567890123, "large integer values");
        Check(primitives.Bool && primitives.Float == 1.5f && primitives.Double == -2.25 && primitives.Decimal == 3.75m && primitives.Char == 'x', "other primitive values");

        var unitBuilder = new UnitBuilder();
        unitBuilder.Configure(context => GroupCommand.ConfigureGroup(context).AddCommand(typeof(PlainCommand)));
        var unit = unitBuilder.Build();
        var group = new GroupCommand(unit.Context);
        await group.Execute([], cancellation.Token);
        var child = (PlainCommand)group.Parser.CurrentCommand!.CommandInstance;
        Check(child.Token == cancellation.Token, "group forwards cancellation token");
        Check(group.Parser.NameToCommand.Count == 1, "group only contains its own registered commands");
        var outer = new SimpleParserBuilder().AddCommand<GroupCommand>().Build(settings with { ServiceProvider = unit.Context.ServiceProvider });
        Check(outer.Parse("group plain"), "outer group dispatch");
        await outer.Execute(cancellation.Token);
        var resolvedGroup = (GroupCommand)outer.CurrentCommand!.CommandInstance;
        Check(((PlainCommand)resolvedGroup.Parser.CurrentCommand!.CommandInstance).Token == cancellation.Token, "DI group forwards cancellation token");
        return count;

        void Check(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"Failed: {description}");
            }

            count++;
        }

        void ExpectInvalid(Action action, string message, string description)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException ex)
            {
                Check(ex.Message.Contains(message, StringComparison.OrdinalIgnoreCase), description);
                return;
            }

            throw new InvalidOperationException($"Failed: {description} (no exception)");
        }
    }

    private class BaseOptions
    {
        [SimpleOption("base")]
        public int BaseValue { get; } = 1;

        [SimpleOption("hidden")]
        private string Hidden { get; set; } = "default";

        public string HiddenValue => this.Hidden;
    }

    private enum Mode
    {
        First,
        Second,
    }

    private sealed class Options : BaseOptions
    {
        [SimpleOption("name", Required = true)]
        public string Name { get; set; } = string.Empty;

        [SimpleOption("number")]
        public int? Number { get; private set; }

        [SimpleOption("field")]
        public int Field = 0;

        [SimpleOption("day")]
        public DayOfWeek? Day { get; set; }

        [SimpleOption("mode")]
        public Mode? Mode { get; set; }

        [SimpleOption("nested")]
        public NestedOptions Nested { get; set; } = new();

        [SimpleOption("serialized")]
        public SerializedOptions Serialized { get; set; } = new("default");
    }

    private sealed class NestedOptions
    {
        [SimpleOption("value")]
        public int Value { get; set; }
    }

    [SimpleCommand("run", Alias = "r")]
    private sealed class OptionsCommand : ISimpleCommand<Options>
    {
        public Options? Received { get; private set; }

        public CancellationToken Token { get; private set; }

        Task ISimpleCommand<Options>.Execute(Options options, string[] args, CancellationToken cancellationToken)
        {
            this.Received = options;
            this.Token = cancellationToken;
            return Task.CompletedTask;
        }
    }

    [SimpleCommand("plain")]
    private sealed class PlainCommand : ISimpleCommand
    {
        public string[]? Args { get; private set; }

        public CancellationToken Token { get; private set; }

        Task ISimpleCommand.Execute(string[] args, CancellationToken cancellationToken)
        {
            this.Args = args;
            this.Token = cancellationToken;
            return Task.CompletedTask;
        }
    }

    [SimpleCommand("injected")]
    private sealed class InjectedCommand : ISimpleCommand
    {
        public InjectedCommand(string tag) => this.Tag = tag;

        public string Tag { get; }

        public bool Executed { get; private set; }

        public Task Execute(string[] args, CancellationToken cancellationToken)
        {
            this.Executed = true;
            return Task.CompletedTask;
        }
    }

    [SimpleCommand("group", IsSubcommand = true)]
    private sealed class GroupCommand : SimpleCommandGroup<GroupCommand>
    {
        public GroupCommand(UnitContext context)
            : base(new SimpleParserBuilder().AddCommand<PlainCommand>().AddCommand<InjectedCommand>(), context, "plain")
        {
        }
    }

    private sealed class IndexerOptions
    {
        [SimpleOption("index")]
        public int this[int index] { get => index; set { } }
    }

    [SimpleCommand("indexer")]
    private sealed class IndexerCommand : ISimpleCommand<IndexerOptions>
    {
        public Task Execute(IndexerOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CircularOptions
    {
        [SimpleOption("self")]
        public CircularOptions? Self { get; set; }
    }

    [SimpleCommand("circular")]
    private sealed class CircularCommand : ISimpleCommand<CircularOptions>
    {
        public Task Execute(CircularOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PrimitiveOptions
    {
        [SimpleOption("sbyte")] public sbyte SByte { get; set; }
        [SimpleOption("byte")] public byte Byte { get; set; }
        [SimpleOption("short")] public short Short { get; set; }
        [SimpleOption("ushort")] public ushort UShort { get; set; }
        [SimpleOption("int")] public int Int { get; set; }
        [SimpleOption("uint")] public uint UInt { get; set; }
        [SimpleOption("long")] public long Long { get; set; }
        [SimpleOption("ulong")] public ulong ULong { get; set; }
        [SimpleOption("bool")] public bool Bool { get; set; }
        [SimpleOption("float")] public float Float { get; set; }
        [SimpleOption("double")] public double Double { get; set; }
        [SimpleOption("decimal")] public decimal Decimal { get; set; }
        [SimpleOption("char")] public char Char { get; set; }
    }
}

[TinyhandObject(AddAlternateKey = true)]
public partial class SerializedOptions
{
    public SerializedOptions(string tag) => this.Tag = tag;

    [IgnoreMember]
    public string Tag { get; }

    [Key(0)]
    [SimpleOption("value")]
    public int Value { get; set; }
}

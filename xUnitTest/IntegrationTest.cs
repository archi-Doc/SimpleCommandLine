// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class EnvOptions
{
    [SimpleOption(EnvironmentTest.LongName, ShortName = EnvironmentTest.ShortName, ReadFromEnvironment = true)]
    public string Text { get; set; } = "default";

    [SimpleOption("plain")]
    public string Plain { get; set; } = "plain-default";
}

[SimpleCommand("env-command")]
public class EnvCommand : ISimpleCommand<EnvOptions>
{
    public Task Execute(EnvOptions option, string[] args, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

[SimpleCommand("env-plain")]
public class EnvPlainCommand : ISimpleCommand
{
    public Task Execute(string[] args, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

[SimpleCommand("env-alias", Alias = "ea")]
public class EnvAliasCommand : ISimpleCommand
{
    public Task Execute(string[] args, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

/// <summary>
/// Tests that depend on process-wide environment variables.
/// </summary>
public class EnvironmentTest
{
    internal const string LongName = "SimpleCommandLine_TestLongName";
    internal const string ShortName = "SimpleCommandLine_TestShortName";

    private static readonly Type[] CommandTypes = [typeof(EnvPlainCommand), typeof(EnvAliasCommand), typeof(EnvCommand)];

    private static readonly SimpleParserOptions Options = SimpleParserOptions.Standard with { SuppressConsoleOutput = true };

    [Fact]
    public void ReadOptionFromEnvironmentTest()
    {
        var parser = new SimpleParser([typeof(EnvCommand)], Options with { ReadCommandFromEnvironment = false });

        try
        {
            // The long name is used as the environment variable name.
            Environment.SetEnvironmentVariable(LongName, "from-long");
            parser.Parse("env-command").IsTrue();
            GetOptions(parser).Text.Is("from-long");

            // The short name takes precedence.
            Environment.SetEnvironmentVariable(ShortName, "from-short");
            parser.Parse("env-command").IsTrue();
            GetOptions(parser).Text.Is("from-short");

            // An explicit value on the command line wins over the environment.
            parser.Parse($"env-command -{LongName} explicit").IsTrue();
            GetOptions(parser).Text.Is("explicit");

            // An option without ReadFromEnvironment is not read from the environment.
            Environment.SetEnvironmentVariable("plain", "from-env");
            parser.Parse("env-command").IsTrue();
            GetOptions(parser).Plain.Is("plain-default");
        }
        finally
        {
            Environment.SetEnvironmentVariable(LongName, null);
            Environment.SetEnvironmentVariable(ShortName, null);
            Environment.SetEnvironmentVariable("plain", null);
        }

        // Without the environment variables the default value is kept.
        parser.Parse("env-command").IsTrue();
        GetOptions(parser).Text.Is("default");

        static EnvOptions GetOptions(SimpleParser parser)
            => (EnvOptions)parser.CurrentCommand!.OptionClass.OptionInstance!;
    }

    [Fact]
    public void ReadCommandFromEnvironmentTest()
    {
        var parser = new SimpleParser(CommandTypes, Options);
        try
        {
            // The command name comes from the environment variable.
            Environment.SetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable, "env-command");
            parser.Parse(string.Empty).IsTrue();
            parser.CurrentCommand!.CommandName.Is("env-command");

            // An alias in the environment variable (empty arguments must not throw).
            Environment.SetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable, "ea");
            parser.Parse(string.Empty).IsTrue();
            parser.CurrentCommand!.CommandName.Is("env-alias");

            // An unknown value falls back to the default command.
            Environment.SetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable, "no-such-command");
            parser.Parse(string.Empty).IsTrue();
            parser.CurrentCommand!.CommandName.Is("env-plain");

            // A command name on the command line wins over the environment.
            Environment.SetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable, "env-command");
            parser.Parse("env-alias").IsTrue();
            parser.CurrentCommand!.CommandName.Is("env-alias");
        }
        finally
        {
            Environment.SetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable, null);
        }
    }

    [Fact]
    public void ExplicitHelpAndVersionTakePriorityOverEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable, "env-command");
            var parser = new SimpleParser(CommandTypes, Options);
            Assert.True(parser.Parse("help env-alias"));
            Assert.Equal("env-alias", parser.HelpCommandName);
            Assert.Null(parser.CurrentCommand);
            Assert.True(parser.Parse("version"));
            Assert.True(parser.VersionRequested);
            Assert.Null(parser.CurrentCommand);
            Assert.True(parser.Parse(string.Empty));
            Assert.Equal("env-command", parser.CurrentCommand!.CommandName);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SimpleParser.CommandEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void InvalidEnvironmentValueFailsCommandParsing()
    {
        const string Name = "SimpleCommandLine_ReviewNumber";
        var previous = Environment.GetEnvironmentVariable(Name);
        try
        {
            Environment.SetEnvironmentVariable(Name, "invalid");
            var parser = new SimpleParserBuilder().AddCommand<EnvironmentNumberCommand, EnvironmentNumberOptions>()
                .Build(Options with { ReadCommandFromEnvironment = false });
            Assert.False(parser.Parse("number"));
            Assert.Null(parser.CurrentCommand);
            Assert.True(parser.Parse($"number -{Name} 7"));
            Assert.Equal(7, ((EnvironmentNumberOptions)parser.CurrentCommand!.OptionClass.OptionInstance!).Number);

            // The standalone API intentionally tolerates invalid optional values.
            Assert.True(new SimpleParserBuilder().TryParseOptions<EnvironmentNumberOptions>(string.Empty, out var relaxed));
            Assert.Equal(0, relaxed.Number);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, previous);
        }
    }

    public class EnvironmentNumberOptions
    {
        [SimpleOption("SimpleCommandLine_ReviewNumber", ReadFromEnvironment = true)]
        public int Number { get; set; }
    }

    [SimpleCommand("number")]
    public class EnvironmentNumberCommand : ISimpleCommand<EnvironmentNumberOptions>
    {
        public Task Execute(EnvironmentNumberOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

public class SubOptions
{
    [SimpleOption("known")]
    public int Known { get; set; }
}

[SimpleCommand("sub", IsSubcommand = true)]
public class SubCommand : ISimpleCommand<SubOptions>
{
    public static string[]? ReceivedArgs { get; set; }

    public Task Execute(SubOptions option, string[] args, CancellationToken cancellationToken)
    {
        ReceivedArgs = args;
        return Task.CompletedTask;
    }
}

[SimpleCommand("injected")]
public class InjectedCommand : ISimpleCommand
{
    public InjectedCommand(string tag)
    {
        this.Tag = tag;
    }

    public static string? ReceivedTag { get; set; }

    public string Tag { get; }

    public Task Execute(string[] args, CancellationToken cancellationToken)
    {
        ReceivedTag = this.Tag;
        return Task.CompletedTask;
    }
}

public class IntegrationTest
{
    private static readonly SimpleParserOptions Options = SimpleParserOptions.Standard with
    {
        SuppressConsoleOutput = true,
        ReadCommandFromEnvironment = false,
    };

    [Fact]
    public async Task SubcommandTest()
    {
        // A subcommand accepts unknown option names even with RequireStrictOptionName.
        var parser = new SimpleParser([typeof(SubCommand)], Options with { RequireStrictOptionName = true });

        parser.Parse("sub -known 1 -unknown 2").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        SubCommand.ReceivedArgs!.SequenceEqual(["-unknown", "2"]).IsTrue();

        // 'help' is not intercepted for a subcommand (it is forwarded instead).
        parser.Parse("sub help").IsTrue();
        parser.HelpCommandName.IsNull();
    }

    [Fact]
    public async Task ServiceProviderTest()
    {
        // A command instance is resolved from the service provider (no parameterless constructor needed).
        var parserOptions = Options with { ServiceProvider = new TestServiceProvider() };
        var parser = new SimpleParser([typeof(InjectedCommand)], parserOptions);

        InjectedCommand.ReceivedTag = null;
        parser.Parse(string.Empty).IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        InjectedCommand.ReceivedTag.Is("injected");
    }

    [Fact]
    public void StaticPresetTest()
    {
        SimpleParserOptions.Standard.RequireStrictCommandName.IsFalse();
        SimpleParserOptions.Standard.RequireStrictOptionName.IsFalse();
        SimpleParserOptions.StrictCommandName.RequireStrictCommandName.IsTrue();
        SimpleParserOptions.StrictOptionName.RequireStrictOptionName.IsTrue();

        // The delimiter length is carried over correctly by 'with'.
        var options = SimpleParserOptions.Standard with { ArgumentDelimiter = "###", };
        options.ArgumentDelimiter.Is("###");
        SimpleParserHelper.ProcessArgument("###a b###", options, ArgumentProcessing.AsIs).Is("a b");

        var single = SimpleParserOptions.Standard with { ArgumentDelimiter = "#", };
        SimpleParserHelper.ProcessArgument("#ab#", single, ArgumentProcessing.AsIs).Is("ab");

        // The default delimiter is unaffected.
        SimpleParserHelper.ProcessArgument("\"\"\"a b\"\"\"", SimpleParserOptions.Standard, ArgumentProcessing.AsIs).Is("a b");
    }

    [Fact]
    public async Task ParseAndExecuteTest()
    {
        // The static helpers parse and execute in one call.
        SubCommand.ReceivedArgs = null;
        await SimpleParser.ParseAndExecute([typeof(SubCommand)], "sub abc", Options, TestContext.Current.CancellationToken);
        SubCommand.ReceivedArgs!.SequenceEqual(["abc"]).IsTrue();

        SubCommand.ReceivedArgs = null;
        await SimpleParser.ParseAndExecute([typeof(SubCommand)], ["sub", "def"], Options, TestContext.Current.CancellationToken);
        SubCommand.ReceivedArgs!.SequenceEqual(["def"]).IsTrue();
    }

    private class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(InjectedCommand) ? new InjectedCommand("injected") : null;
    }
}

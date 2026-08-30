// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class NestedOptions
{
    [SimpleOption("text", ShortName = "t")]
    public string Text { get; set; } = string.Empty;

    [SimpleOption("value")]
    public int Value { get; set; }
}

public class BaseOptions
{
    [SimpleOption("base", ShortName = "b", Description = "Base option")]
    public string Base { get; set; } = "base-default";
}

public class ParserOptions : BaseOptions
{
    [SimpleOption("number", ShortName = "n")]
    public int Number { get; set; } = 1;

#pragma warning disable SA1401 // Fields should be private
    [SimpleOption("flag")]
    public bool Flag = true;
#pragma warning restore SA1401

    [SimpleOption("nested")]
    public NestedOptions Nested { get; set; } = new();
}

[SimpleCommand("parser-command", Alias = "pc", Description = "Parser test")]
public class ParserCommand : ISimpleCommand<ParserOptions>
{
    public static ParserOptions? ReceivedOptions { get; set; }

    public static string[]? ReceivedArgs { get; set; }

    public Task Execute(ParserOptions option, string[] args, CancellationToken cancellationToken)
    {
        ReceivedOptions = option;
        ReceivedArgs = args;
        return Task.CompletedTask;
    }
}

[SimpleCommand("plain-command")]
public class PlainCommand : ISimpleCommand
{
    public static string[]? ReceivedArgs { get; set; }

    public Task Execute(string[] args, CancellationToken cancellationToken)
    {
        ReceivedArgs = args;
        return Task.CompletedTask;
    }
}

public class RequiredOptions
{
    [SimpleOption("first", Required = true)]
    public string First { get; set; } = string.Empty;

    [SimpleOption("second", Required = true)]
    public int Second { get; set; }

    [SimpleOption("third")]
    public int Third { get; set; }
}

[SimpleCommand("required-command")]
public class RequiredCommand : ISimpleCommand<RequiredOptions>
{
    public static RequiredOptions? ReceivedOptions { get; set; }

    public Task Execute(RequiredOptions option, string[] args, CancellationToken cancellationToken)
    {
        ReceivedOptions = option;
        return Task.CompletedTask;
    }
}

public class ParserTest
{
    private static readonly Type[] CommandTypes = [typeof(ParserCommand), typeof(PlainCommand), typeof(RequiredCommand)];

    private static readonly SimpleParserOptions StandardOptions = SimpleParserOptions.Standard with
    {
        SuppressConsoleOutput = true,
        ReadCommandFromEnvironment = false, // Keep the tests hermetic.
    };

    [Fact]
    public async Task DefaultCommandTest()
    {
        // The first registered command is the default command.
        var parser = new SimpleParser(CommandTypes, StandardOptions);
        parser.DefaultCommandName.Is("parser-command");

        ParserCommand.ReceivedOptions = null;
        parser.Parse("-number 5").IsTrue();
        parser.CurrentCommand!.CommandName.Is("parser-command");
        await parser.Execute(TestContext.Current.CancellationToken);

        ParserCommand.ReceivedOptions!.Number.Is(5);
        ParserCommand.ReceivedOptions.Base.Is("base-default"); // Inherited option keeps its default.
        ParserCommand.ReceivedOptions.Flag.IsTrue(); // Field option keeps its default.
    }

    [Fact]
    public async Task CommandNameAndAliasTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        // Command name.
        PlainCommand.ReceivedArgs = null;
        parser.Parse("plain-command abc def").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        PlainCommand.ReceivedArgs!.SequenceEqual(["abc", "def"]).IsTrue();

        // The command name is case insensitive.
        parser.Parse("PLAIN-COMMAND").IsTrue();
        parser.CurrentCommand!.CommandName.Is("plain-command");

        // Alias.
        parser.Parse("pc -number 3").IsTrue();
        parser.CurrentCommand!.CommandName.Is("parser-command");
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedOptions!.Number.Is(3);
    }

    [Fact]
    public void AutoAliasTest()
    {
        var parser = new SimpleParser([typeof(PlainCommand)], StandardOptions with { AutoAlias = true });

        // 'plain-command' -> 'pc'
        parser.Parse("pc").IsTrue();
        parser.CurrentCommand!.CommandName.Is("plain-command");
    }

    [Fact]
    public async Task OptionTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        // Long name, short name, inherited option, field option.
        parser.Parse("parser-command -number 7 -b hello -flag false").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedOptions!.Number.Is(7);
        ParserCommand.ReceivedOptions.Base.Is("hello");
        ParserCommand.ReceivedOptions.Flag.IsFalse();

        // The option name is case insensitive.
        parser.Parse("parser-command -NUMBER 8").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedOptions!.Number.Is(8);

        // A quoted value keeps its spaces.
        parser.Parse("parser-command -b \"a b c\"").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedOptions!.Base.Is("a b c");

        // Options are reset for every Parse() call.
        parser.Parse("parser-command").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedOptions!.Number.Is(1);
        ParserCommand.ReceivedOptions.Base.Is("base-default");
    }

    [Fact]
    public async Task NestedOptionTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        parser.Parse("parser-command -nested {-text \"a b\" -value 9}").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedOptions!.Nested.Text.Is("a b");
        ParserCommand.ReceivedOptions.Nested.Value.Is(9);

        // The nested instance is reset for every Parse() call.
        parser.Parse("parser-command").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedOptions!.Nested.Text.Is(string.Empty);
        ParserCommand.ReceivedOptions.Nested.Value.Is(0);
    }

    [Fact]
    public async Task RemainingArgumentsTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        parser.Parse("parser-command -number 2 abc \"d e\"").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedArgs!.SequenceEqual(["abc", "d e"]).IsTrue();

        // No remaining arguments.
        parser.Parse("parser-command -number 2").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedArgs!.Length.Is(0);

        // An unknown option is treated as a remaining argument.
        parser.Parse("parser-command -unknown").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        ParserCommand.ReceivedArgs!.SequenceEqual(["-unknown"]).IsTrue();
    }

    [Fact]
    public async Task RequiredOptionTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        // A missing required value is an error.
        parser.Parse("required-command").IsFalse();
        parser.CurrentCommand.IsNull();

        // Required options can omit their names (OmitOptionNamesForRequiredOptions).
        parser.Parse("required-command abc 5").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        RequiredCommand.ReceivedOptions!.First.Is("abc");
        RequiredCommand.ReceivedOptions.Second.Is(5);

        // Named required options.
        parser.Parse("required-command -first x -second 6").IsTrue();
        await parser.Execute(TestContext.Current.CancellationToken);
        RequiredCommand.ReceivedOptions!.First.Is("x");
        RequiredCommand.ReceivedOptions.Second.Is(6);

        // Omitting the names can be disabled.
        var strict = new SimpleParser(CommandTypes, StandardOptions with { OmitOptionNamesForRequiredOptions = false });
        strict.Parse("required-command abc 5").IsFalse();
    }

    [Fact]
    public void StrictOptionNameTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions with { RequireStrictOptionName = true });

        parser.Parse("parser-command -number 1").IsTrue();
        parser.Parse("parser-command -unknown 1").IsFalse();
    }

    [Fact]
    public void StrictCommandNameTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions with { RequireStrictCommandName = true });

        parser.DefaultCommandName.IsNull();
        parser.Parse("-number 1").IsFalse(); // No command name.
        parser.Parse("parser-command -number 1").IsTrue();
    }

    [Fact]
    public void HelpAndVersionTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        parser.Parse("help").IsTrue();
        parser.HelpCommand.Is(string.Empty);

        parser.Parse("help plain-command").IsTrue();
        parser.HelpCommand.Is("plain-command");

        parser.Parse("parser-command help").IsTrue();
        parser.HelpCommand.Is("parser-command");

        parser.Parse("version").IsTrue();
        parser.VersionCommand.IsTrue();

        // The state is reset for every Parse() call.
        parser.Parse("parser-command").IsTrue();
        parser.HelpCommand.IsNull();
        parser.VersionCommand.IsFalse();

        // ShowHelp()/ShowVersion() do not throw (the output is suppressed).
        parser.ShowHelp();
        parser.ShowHelp("parser-command");
        parser.ShowVersion();
        parser.ShowVersion("prefix");
        parser.ShowCommandList();
    }

    [Fact]
    public void TryGetCommandAndOptionTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        parser.TryGetCommand("parser-command", out var command).IsTrue();
        command!.Description.Is("Parser test");
        parser.TryGetCommand("no-such-command", out _).IsFalse();

        parser.TryGetOption("parser-command", "base", out var option).IsTrue();
        option!.Description.Is("Base option");
        option.ShortName.Is("b");
        parser.TryGetOption("parser-command", "no-such-option", out _).IsFalse();
        parser.TryGetOption("no-such-command", "base", out _).IsFalse();
    }

    [Fact]
    public void SeparatorStopsParsingTest()
    {
        var parser = new SimpleParser(CommandTypes, StandardOptions);

        // Arguments after '|' belong to the next command.
        parser.Parse("parser-command -number 4 | -number 9").IsTrue();
        ((ParserOptions)parser.CurrentCommand!.OptionClass.OptionInstance!).Number.Is(4);
    }

    [Fact]
    public async Task TryParseOptionsTest()
    {
        // A fresh instance.
        SimpleParser.TryParseOptions<NestedOptions>("-text abc -value 3", out var options).IsTrue();
        options!.Text.Is("abc");
        options.Value.Is(3);

        // An existing instance is updated (the untouched member keeps its value).
        var original = new NestedOptions { Text = "keep", Value = 100, };
        SimpleParser.TryParseOptions<NestedOptions>("-value 4", out var updated, original).IsTrue();
        ReferenceEquals(updated, original).IsTrue();
        updated!.Text.Is("keep");
        updated.Value.Is(4);

        // The string[] overload.
        SimpleParser.TryParseOptions<NestedOptions>(["-value", "7"], out options).IsTrue();
        options!.Value.Is(7);

        await Task.CompletedTask;
    }

    [Fact]
    public void InvalidCommandTypeTest()
    {
        // No SimpleCommandAttribute.
        Assert.Throws<InvalidOperationException>(() => new SimpleParser([typeof(NoAttributeCommand)], StandardOptions));

        // Does not implement ISimpleCommand.
        Assert.Throws<InvalidOperationException>(() => new SimpleParser([typeof(NoInterfaceCommand)], StandardOptions));

        // Duplicate command name.
        Assert.Throws<InvalidOperationException>(() => new SimpleParser([typeof(ParserCommand), typeof(DuplicateNameCommand)], StandardOptions));

        // Circular dependency of option classes.
        Assert.Throws<InvalidOperationException>(() => new SimpleParser([typeof(CircularCommand)], StandardOptions));

        // Duplicate long option name.
        Assert.Throws<InvalidOperationException>(() => new SimpleParser([typeof(DuplicateOptionCommand)], StandardOptions));
    }

    [SimpleCommand("parser-command")]
    private class DuplicateNameCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class NoAttributeCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("no-interface")]
    private class NoInterfaceCommand
    {
    }

    private class CircularOptions
    {
        [SimpleOption("self")]
        public CircularOptions? Self { get; set; }
    }

    [SimpleCommand("circular")]
    private class CircularCommand : ISimpleCommand<CircularOptions>
    {
        public Task Execute(CircularOptions option, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private class DuplicateOptions
    {
        [SimpleOption("same")]
        public int A { get; set; }

        [SimpleOption("same")]
        public int B { get; set; }
    }

    [SimpleCommand("duplicate-option")]
    private class DuplicateOptionCommand : ISimpleCommand<DuplicateOptions>
    {
        public Task Execute(DuplicateOptions option, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

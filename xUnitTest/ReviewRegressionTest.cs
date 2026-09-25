// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class ReviewRegressionTest
{
    private static readonly SimpleParserOptions Settings = SimpleParserOptions.Standard with
    {
        ReadCommandFromEnvironment = false,
        SuppressConsoleOutput = true,
    };

    [Theory]
    [InlineData("two words")]
    [InlineData("")]
    [InlineData("  padded  ")]
    [InlineData("a,b|c")]
    [InlineData("\"literal quotes\"")]
    [InlineData("a\\\"b")]
    public void ArrayArgumentsPreserveValues(string value)
    {
        var parser = new SimpleParserBuilder().AddCommand<TextCommand, TextOptions>().Build(Settings);
        Assert.True(parser.Parse(["text", "-text", value]));
        Assert.Equal(value, ((TextOptions)parser.CurrentCommand!.OptionSet.Instance!).Text);
        Assert.Empty(parser.CurrentCommand.OptionSet.RemainingArguments!);

        Assert.True(SimpleParser.TryParseOptions<TextOptions>(["-text", value], out var options));
        Assert.Equal(value, options.Text);
        Assert.True(new SimpleParserBuilder().TryParseOptions<TextOptions>(["-text", value], out var registered));
        Assert.Equal(value, registered.Text);
    }

    [Fact]
    public async Task CommandGroupsPreserveArgumentBoundaries()
    {
        var parent = new SimpleParserBuilder().AddCommand<ForwardCommand>().Build(Settings);
        var child = new SimpleParserBuilder().AddCommand<TextCommand, TextOptions>().Build(Settings);
        Assert.True(parent.Parse("forward text -text 'two words' '' '\"quoted\"'"));
        var args = parent.CurrentCommand!.OptionSet.RemainingArguments!;
        await child.ParseAndExecute(args, TestContext.Current.CancellationToken);
        Assert.Equal("two words", ((TextOptions)child.CurrentCommand!.OptionSet.Instance!).Text);
        Assert.Equal([string.Empty, "\"quoted\""], child.CurrentCommand.OptionSet.RemainingArguments!);
    }

    [Theory]
    [InlineData("\"don't split\" tail", "\"don't split\"")]
    [InlineData("'a \"quote' tail", "'a \"quote'")]
    [InlineData("{ -text \"\"\"a \" b } c\"\"\" } tail", "{ -text \"\"\"a \" b } c\"\"\" }")]
    public void NestedQuotesKeepTokenBoundaries(string line, string first)
        => Assert.Equal([first, "tail"], line.SplitArguments());

    [Theory]
    [InlineData("#")]
    [InlineData("###")]
    public void CustomDelimiterInsideBraces(string delimiter)
    {
        var nested = "{-text " + delimiter + "a } | ' b" + delimiter + "}";
        Assert.Equal([nested, "tail"], (nested + " tail").SplitArguments(delimiter));
    }

    [Fact]
    public void QuotedPrimitiveOptionsAreConverted()
    {
        var parser = new SimpleParserBuilder().AddCommand<ScalarCommand, ScalarOptions>().Build(Settings);
        Assert.True(parser.Parse("scalar -number '42' -day \"Friday\" -flag 'true' -letter 'x'"));
        var options = (ScalarOptions)parser.CurrentCommand!.OptionSet.Instance!;
        Assert.Equal(42, options.Number);
        Assert.Equal(DayOfWeek.Friday, options.Day);
        Assert.True(options.Flag);
        Assert.Equal('x', options.Letter);
    }

    [Fact]
    public void EmptyDelimiterDisablesTripleQuotes()
    {
        var parser = new SimpleParserBuilder().AddCommand<ForwardCommand>()
            .Build(Settings with { ArgumentDelimiter = string.Empty });
        Assert.True(parser.Parse("forward \"\"\"value\"\"\""));
        Assert.Equal([string.Empty, "value", string.Empty], parser.CurrentCommand!.OptionSet.RemainingArguments!);
    }

    [Fact]
    public void CommandSeparatorCannotBecomeAnOptionValue()
    {
        var parser = new SimpleParserBuilder().AddCommand<TextCommand, TextOptions>().Build(Settings);
        Assert.False(parser.Parse("text -text | -text second"));
        Assert.Null(parser.CurrentCommand);
        Assert.True(parser.Parse("text -text '|'"));
        Assert.Equal("|", ((TextOptions)parser.CurrentCommand!.OptionSet.Instance!).Text);
    }

    [Fact]
    public void OverriddenOptionsUseTheDerivedMemberOnce()
    {
        var builder = new SimpleParserBuilder();
        Assert.True(builder.TryParseOptions<InheritedOptions>("hello", out var inherited));
        Assert.Equal("hello", inherited.Text);
        Assert.True(builder.TryParseOptions<RenamedOptions>("-renamed world", out var renamed));
        Assert.Equal("world", renamed.Text);
    }

    [Fact]
    public void PartialOverridesReplaceTheBaseOption()
    {
        var builder = new SimpleParserBuilder();
        Assert.True(builder.TryParseOptions<SetterOverrideOptions>("-text value", out var setter));
        Assert.Equal("VALUE", setter.Text);
        Assert.True(builder.TryParseOptions<GetterOverrideOptions>("value", out var getter));
        Assert.Equal("value!", getter.Text);
        Assert.True(builder.TryParseOptions<AbstractOverrideOptions>("-text value", out var concrete));
        Assert.Equal("value", concrete.Text);

        Assert.True(builder.TryParseOptions<RenamedSetterOptions>("-renamed value -text ignored", out var renamed));
        Assert.Equal("value?", renamed.Text);
        var parser = builder.AddCommand<RenamedSetterCommand, RenamedSetterOptions>().Build(Settings);
        var option = Assert.Single(parser.NameToCommand["renamed-setter"].OptionSet.Options);
        Assert.Equal("renamed", option.LongName);
        Assert.Equal(typeof(BaseOptions), option.PropertyInfo!.DeclaringType);

        Assert.True(SimpleParser.TryParseOptions<SetterOverrideOptions>("-text reflection", out var reflected));
        Assert.Equal("REFLECTION", reflected.Text);
    }

    [Fact]
    public void HelpRequestsUseCanonicalCommandNames()
    {
        var parser = new SimpleParserBuilder().AddCommand<TextCommand, TextOptions>().Build(Settings);
        Assert.True(parser.Parse("TEXT help"));
        Assert.Equal("text", parser.HelpCommandName);
        Assert.True(parser.Parse("help TEXT"));
        Assert.Equal("text", parser.HelpCommandName);
        Assert.False(parser.Parse("TEXT -text"));
        Assert.Equal("text", parser.HelpCommandName);
    }

    [Fact]
    public void RemovedDefaultCommandIsReported()
    {
        var parser = new SimpleParserBuilder().AddCommand<FirstDefault>().AddCommand<TextCommand, TextOptions>().Build(Settings);
        Assert.True(parser.NameToCommand.Remove("first"));
        Assert.False(parser.Parse(string.Empty));
        Assert.Equal(string.Empty, parser.HelpCommandName);
        Assert.True(parser.Parse("text"));
    }

    [Fact]
    public void OriginalCommandLineReflectsTheLatestInput()
    {
        var parser = new SimpleParserBuilder().AddCommand<TextCommand, TextOptions>().Build(Settings);
        Assert.Equal(string.Empty, parser.OriginalCommandLine);
        Assert.True(parser.Parse(["text", "-text", "two words"]));
        Assert.Equal("text -text two words", parser.OriginalCommandLine);
        Assert.True(parser.Parse("text  -text 'raw'"));
        Assert.Equal("text  -text 'raw'", parser.OriginalCommandLine);
        Assert.True(parser.Parse(Array.Empty<string>()));
        Assert.Equal(string.Empty, parser.OriginalCommandLine);
        Assert.Throws<ArgumentNullException>(() => parser.Parse((string[])null!));
    }

    [Fact]
    public void RepeatedReflectionRegistrationIsIdempotentWithAliases()
    {
        var parser = new SimpleParser([typeof(AliasCommand), typeof(AliasCommand)], Settings);
        Assert.Single(parser.NameToCommand);
        Assert.Single(parser.AliasToCommand);
    }

    [Fact]
    public void AutomaticAliasMetadataMatchesDispatch()
    {
        var parser = new SimpleParserBuilder().AddCommand<TextCommand, TextOptions>().Build(Settings with { GenerateAliases = true });
        Assert.True(parser.Parse("t"));
        Assert.Equal("t", parser.CurrentCommand!.Alias);
    }

    [Fact]
    public void OnlyTheSelectedDefaultCommandIsMarked()
    {
        var builder = new SimpleParserBuilder().AddCommand<FirstDefault>().AddCommand<SecondDefault>();
        var parser = builder.Build(Settings);
        Assert.Equal("first", parser.DefaultCommandName);
        Assert.Single(parser.NameToCommand.Values, x => x.IsDefault);
        parser = builder.Build(Settings with { RequireCommandName = true });
        Assert.DoesNotContain(parser.NameToCommand.Values, x => x.IsDefault);
    }

    [Fact]
    public void FailedOptionsConstructionDoesNotExecuteACommand()
    {
        var parser = new SimpleParserBuilder().AddCommand<BrokenCommand, BrokenOptions>().Build(Settings);
        Assert.False(parser.Parse("broken"));
        Assert.Null(parser.CurrentCommand);
    }

    [Theory]
    [InlineData("-count", "-1")]
    [InlineData("--count", "-.5")]
    public void ArgumentRemovalAcceptsNegativeValues(string name, string value)
    {
        string[] args = ["unrelated", name, value, "tail"];
        Assert.True(SimpleParserHelper.TryGetAndRemoveOptionValue(ref args, "count", out var result));
        Assert.Equal(value, result);
        Assert.Equal(["unrelated", "tail"], args);
    }

    [Fact]
    public void ArgumentRemovalStopsAtCommandSeparator()
    {
        string[] args = ["-text", "|", "-text", "second"];
        Assert.False(SimpleParserHelper.TryGetAndRemoveOptionValue(ref args, "text", out _));
        Assert.Equal(["-text", "|", "-text", "second"], args);
        args = ["first", "|", "-text", "second"];
        Assert.False(SimpleParserHelper.TryGetAndRemoveOptionValue(ref args, "text", out _));
    }

    public class TextOptions
    {
        [SimpleOption("text")]
        public string Text { get; set; } = "default";
    }

    public class ScalarOptions
    {
        [SimpleOption("number")]
        public int Number { get; set; }

        [SimpleOption("day")]
        public DayOfWeek Day { get; set; }

        [SimpleOption("flag")]
        public bool Flag { get; set; }

        [SimpleOption("letter")]
        public char Letter { get; set; }
    }

    public class BaseOptions
    {
        [SimpleOption("text", IsRequired = true)]
        public virtual string Text { get; set; } = string.Empty;
    }

    public class InheritedOptions : BaseOptions
    {
        public override string Text { get; set; } = string.Empty;
    }

    public class RenamedOptions : BaseOptions
    {
        [SimpleOption("renamed", IsRequired = true)]
        public override string Text { get; set; } = string.Empty;
    }

    public class SetterOverrideOptions : BaseOptions
    {
        public override string Text
        {
            set => base.Text = value.ToUpperInvariant();
        }
    }

    public class GetterOverrideOptions : BaseOptions
    {
        public override string Text => base.Text + "!";
    }

    public class RenamedSetterOptions : BaseOptions
    {
        [SimpleOption("renamed", IsRequired = true)]
        public override string Text
        {
            set => base.Text = value + "?";
        }
    }

    public abstract class AbstractTextOptions
    {
        [SimpleOption("text")]
        public abstract string Text { get; set; }
    }

    public class AbstractOverrideOptions : AbstractTextOptions
    {
        public override string Text { get; set; } = string.Empty;
    }

    [SimpleCommand("renamed-setter")]
    public class RenamedSetterCommand : ISimpleCommand<RenamedSetterOptions>
    {
        public Task Execute(RenamedSetterOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class BrokenOptions
    {
        public BrokenOptions() => throw new InvalidOperationException("Cannot construct options.");
    }

    [SimpleCommand("text")]
    public class TextCommand : ISimpleCommand<TextOptions>
    {
        public Task Execute(TextOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("scalar")]
    public class ScalarCommand : ISimpleCommand<ScalarOptions>
    {
        public Task Execute(ScalarOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("broken")]
    public class BrokenCommand : ISimpleCommand<BrokenOptions>
    {
        public Task Execute(BrokenOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("forward", IsCommandGroup = true)]
    public class ForwardCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("alias", Alias = "a")]
    public class AliasCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("first", IsDefault = true)]
    public class FirstDefault : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("second", IsDefault = true)]
    public class SecondDefault : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

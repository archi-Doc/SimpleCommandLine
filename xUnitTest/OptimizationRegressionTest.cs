// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class OptimizationRegressionTest
{
    private static readonly SimpleParserOptions Settings = SimpleParserOptions.Standard with
    {
        ReadCommandFromEnvironment = false,
        SuppressConsoleOutput = true,
    };

    [Theory]
    [InlineData("-name-", "name-")]
    [InlineData("--name-", "name-")]
    public void TrailingHyphensArePartOfOptionNames(string token, string name)
    {
        var parser = new SimpleParserBuilder().AddCommand<HyphenCommand, HyphenOptions>().Build(Settings);
        Assert.True(parser.Parse(["hyphen", token, "value"]));
        Assert.Equal("value", ((HyphenOptions)parser.CurrentCommand!.OptionSet.Instance!).Name);
        string[] args = [token, "value", "tail"];
        Assert.True(SimpleParserHelper.TryGetAndRemoveOptionValue(ref args, name, out var value));
        Assert.Equal("value", value);
        Assert.Equal(["tail"], args);
    }

    [Theory]
    [InlineData("help-")]
    [InlineData("version-")]
    [InlineData("-help-")]
    public void TrailingHyphensDoNotRequestBuiltInOutput(string argument)
    {
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.ForwardCommand>().Build(Settings);
        Assert.True(parser.Parse(argument));
        Assert.NotNull(parser.CurrentCommand);
        Assert.Null(parser.HelpCommandName);
        Assert.False(parser.IsVersionRequested);
        Assert.Equal([argument], parser.CurrentCommand.OptionSet.RemainingArguments!);
    }

    [Theory]
    [InlineData("||")]
    [InlineData(",,")]
    [InlineData("}")]
    public void CustomDelimitersTakePrecedenceOverSeparators(string delimiter)
    {
        var wrapped = delimiter + "a | b" + delimiter;
        Assert.Equal([wrapped], wrapped.SplitArguments(delimiter));
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.TextCommand, ReviewRegressionTest.TextOptions>()
            .Build(Settings with { ArgumentDelimiter = delimiter });
        Assert.True(parser.Parse("text -text " + wrapped));
        Assert.Equal("a | b", ((ReviewRegressionTest.TextOptions)parser.CurrentCommand!.OptionSet.Instance!).Text);
    }

    [Fact]
    public void FailedNestedValuesDoNotReplaceExistingDefaults()
    {
        var builder = new SimpleParserBuilder().AddOptionsType<ReviewRegressionTest.ScalarOptions>();
        Assert.True(builder.TryParseOptions<NestedOptions>("-nested {-number 7 -day invalid}", out var options));
        Assert.Equal(11, options.Nested.Number);
    }

    [Fact]
    public void RemainingArgumentsAreIndependentAcrossParses()
    {
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.TextCommand, ReviewRegressionTest.TextOptions>().Build(Settings);
        Assert.True(parser.Parse("text first -text value second"));
        var first = parser.CurrentCommand!.OptionSet.RemainingArguments!;
        Assert.True(parser.Parse("text third"));
        Assert.Equal(["first", "second"], first);
        Assert.Equal(["third"], parser.CurrentCommand!.OptionSet.RemainingArguments!);
        Assert.False(parser.Parse("text stale -text"));
        Assert.Null(parser.NameToCommand["text"].OptionSet.RemainingArguments);
        Assert.True(parser.Parse("text"));
        Assert.Empty(parser.CurrentCommand!.OptionSet.RemainingArguments!);
    }

    [Fact]
    public void TokenBuffersHandleRepeatedGrowthAndCommandBoundaries()
    {
        var tokens = Enumerable.Range(0, 300).Select(x => x.ToString()).ToArray();
        var deep = new string('{', 300) + "'a | b'" + new string('}', 300);
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.ForwardCommand>().Build(Settings);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(tokens, string.Join(' ', tokens).SplitArguments());
            Assert.Equal([deep, "tail"], (deep + " tail").SplitArguments());
            Assert.True(parser.Parse("forward 'a | b' | " + deep));
            Assert.Equal(["a | b"], parser.CurrentCommand!.OptionSet.RemainingArguments!);
            Assert.True(parser.Parse("forward |"));
            Assert.Empty(parser.CurrentCommand!.OptionSet.RemainingArguments!);
        }
    }

    [Theory]
    [InlineData("|", 2)]
    [InlineData("||", 3)]
    [InlineData(" | | ", 3)]
    public void SplittingCommandsPreservesEmptySegments(string input, int count)
        => Assert.Equal(Enumerable.Repeat(string.Empty, count), input.SplitCommandLines());

    [Fact]
    public void SplittingCommandsPreservesTrailingAndQuotedSegments()
    {
        Assert.Empty(" \t ".SplitCommandLines());
        Assert.Equal(["one", "", "'two | three'", ""], "one||'two | three'|".SplitCommandLines());
        Assert.Equal(["##a|b##", "tail"], "##a|b## | tail".SplitCommandLines("##"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void LowLevelParsingRejectsInvalidOffsets(int offset)
    {
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.TextCommand, ReviewRegressionTest.TextOptions>().Build(Settings);
        var options = parser.NameToCommand["text"].OptionSet;
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Parse(["value"], offset, false));
        Assert.Throws<ArgumentNullException>(() => options.Parse(null!, 0, false));
        Assert.True(options.Parse(["ignored"], 1, false));
        Assert.Empty(options.RemainingArguments!);
    }

    [Theory]
    [InlineData("'a\nb'", "a b")]
    [InlineData("'a\\\"b'", "a\"b")]
    [InlineData("'a\r\nb'", "a b")]
    public void NormalizedStringsRetainReplacementCharacters(string raw, string expected)
    {
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.TextCommand, ReviewRegressionTest.TextOptions>().Build(Settings);
        Assert.True(parser.Parse("text -text " + raw));
        Assert.Equal(expected, ((ReviewRegressionTest.TextOptions)parser.CurrentCommand!.OptionSet.Instance!).Text);
    }

    [Fact]
    public void SuppressedHelpDoesNotCreateHelpOnlyInstances()
    {
        var parser = new SimpleParserBuilder().AddCommand<ThrowingHelpCommand, ThrowingHelpOptions>().Build(Settings);
        parser.ShowHelp();
        parser.ShowHelp("throwing-help");
        parser.ShowVersion("prefix");
        parser.ShowCommandList();
        Assert.Throws<ArgumentOutOfRangeException>(() => parser.ShowCommandList(-1));
    }

    [Fact]
    public void LowLevelReflectionCommandConstructorAndMissingDefaultsAreCovered()
    {
        var parser = new SimpleParserBuilder().Build(Settings);
        var command = new SimpleParser.Command(parser, typeof(ReviewRegressionTest.ForwardCommand), new SimpleCommandAttribute(string.Empty));
        Assert.True(command.IsDefault);
        Assert.NotNull(command.CommandInstance);
        Assert.Throws<InvalidOperationException>(() => new SimpleParser.Command(parser, typeof(NoDefaultConstructorCommand), new SimpleCommandAttribute("missing")));
    }

    [Fact]
    public void InvalidPositionalRequiredValuesFail()
    {
        var parser = new SimpleParserBuilder().AddCommand<RequiredNumberCommand, RequiredNumberOptions>().Build(Settings);
        Assert.False(parser.Parse("number invalid"));
        Assert.Null(parser.CurrentCommand);
        Assert.True(parser.Parse("number '42'"));
        Assert.Equal(42, ((RequiredNumberOptions)parser.CurrentCommand!.OptionSet.Instance!).Number);
    }

    [Theory]
    [InlineData("first -third third second tail")]
    [InlineData("-second second first third tail")]
    public void PositionalRequiredOptionsSkipValuesAlreadyAssignedByName(string input)
    {
        var builder = new SimpleParserBuilder();
        Assert.True(builder.TryParseOptions<OrderedOptions>(input, out var options));
        Assert.Equal("first", options.First);
        Assert.Equal("second", options.Second);
        Assert.Equal("third", options.Third);
        Assert.Equal("optional", options.Optional);
    }

    [Fact]
    public void StringTrimmingRetainsUnchangedInstancesAndEscapedQuotes()
    {
        var text = new string('x', 100);
        Assert.Same(text, text.TrimQuotes());
        Assert.Same(text, text.TrimQuotesAndBraces());
        Assert.Equal("'a'b'", "'a'b'".TrimQuotes());
        Assert.Equal("a\\'b", "'a\\'b'".TrimQuotes());
        Assert.Equal(string.Empty, SimpleParserHelper.ExtractArguments("\"unterminated"));
    }

    public class ThrowingHelpOptions
    {
        public ThrowingHelpOptions() => throw new InvalidOperationException("Help must not construct options when suppressed.");

        [SimpleOption("text")]
        public string Text { get; set; } = string.Empty;
    }

    [SimpleCommand("throwing-help")]
    public class ThrowingHelpCommand : ISimpleCommand<ThrowingHelpOptions>
    {
        public ThrowingHelpCommand() => throw new InvalidOperationException("Help must not construct commands when suppressed.");

        public Task Execute(ThrowingHelpOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class NoDefaultConstructorCommand : ISimpleCommand
    {
        public NoDefaultConstructorCommand(int value)
        {
        }

        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class RequiredNumberOptions
    {
        [SimpleOption("number", IsRequired = true)]
        public int Number { get; set; }
    }

    public class OrderedOptions
    {
        [SimpleOption("first", IsRequired = true)]
        public string First { get; set; } = string.Empty;

        [SimpleOption("optional")]
        public string Optional { get; set; } = "optional";

        [SimpleOption("second", IsRequired = true)]
        public string Second { get; set; } = string.Empty;

        [SimpleOption("third", IsRequired = true)]
        public string Third { get; set; } = string.Empty;
    }

    [SimpleCommand("number")]
    public class RequiredNumberCommand : ISimpleCommand<RequiredNumberOptions>
    {
        public Task Execute(RequiredNumberOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class HyphenOptions
    {
        [SimpleOption("name-")]
        public string Name { get; set; } = string.Empty;
    }

    public class NestedOptions
    {
        [SimpleOption("nested")]
        public ReviewRegressionTest.ScalarOptions Nested { get; set; } = new() { Number = 11 };
    }

    [SimpleCommand("hyphen")]
    public class HyphenCommand : ISimpleCommand<HyphenOptions>
    {
        public Task Execute(HyphenOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

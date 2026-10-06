// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Linq;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class HelperReviewTest
{
    [Theory]
    [InlineData("program\t-option value", "-option value")]
    [InlineData("program\r\n-option value\t", "-option value")]
    [InlineData(" \tprogram -option value  ", "-option value")]
    [InlineData(" \t\"program with spaces\"\t-option value  ", "-option value")]
    [InlineData("program\u2003-option value", "-option value")]
    [InlineData(" \tprogram \r\n", "")]
    [InlineData(" \t\"program with spaces\"  ", "")]
    [InlineData(" \t\"unterminated path", "")]
    [InlineData(" \r\n\t", "")]
    public void ExecutableExtractionHandlesWhitespace(string input, string expected)
        => Assert.Equal(expected, SimpleParserHelper.ExtractArguments(input));

    [Theory]
    [InlineData("  alpha\t-beta,1  | gamma\n'delta | epsilon'  ", "alpha -beta 1", "gamma 'delta | epsilon'")]
    [InlineData("{ nested {-text 'a|b'} }|\"a,b\"", "{ nested {-text 'a|b'} }", "\"a,b\"")]
    [InlineData("\"\"\"a\n|b\"\"\"|'c\\'|d'", "\"\"\"a\n|b\"\"\"", "'c\\'|d'")]
    [InlineData("left\"right\"|{nested}tail", "left \"right\"", "{nested} tail")]
    public void CommandSplittingNormalizesOnlyUnenclosedSeparators(string input, string first, string second)
        => Assert.Equal([first, second], input.SplitCommandLines());

    [Theory]
    [InlineData("#", "#first|second# | tail", "#first|second#", "tail")]
    [InlineData("||", "||first|second|| | tail", "||first|second||", "tail")]
    [InlineData(",,", ",,first|second,, | tail", ",,first|second,,", "tail")]
    [InlineData("}", "}first|second} | tail", "}first|second}", "tail")]
    public void CommandSplittingHonorsCustomDelimiters(string delimiter, string input, string first, string second)
        => Assert.Equal([first, second], input.SplitCommandLines(delimiter));

    [Fact]
    public void PipeUsedAsAnUnclosedDelimiterRemainsLiteral()
    {
        Assert.Equal(["|"], "|".SplitCommandLines("|"));
        Assert.Equal(["|one| |two|"], "|one| |two|".SplitCommandLines("|"));
    }

    [Fact]
    public void CommandSplittingHandlesLargeInputsAndEmptySegments()
    {
        var values = Enumerable.Range(0, 300).Select(x => x.ToString()).ToArray();
        var normalized = string.Join(' ', values);
        var nested = new string('{', 300) + "'quoted | text'" + new string('}', 300);
        var input = "|" + string.Join(",\t", values) + "||" + nested + "|";

        Assert.Equal([string.Empty, normalized, string.Empty, nested, string.Empty], input.SplitCommandLines());
    }

    [Theory]
    [InlineData("\r\n", ArgumentProcessing.RemoveNewlines, "")]
    [InlineData("\r\r", ArgumentProcessing.ReplaceNewlinesWithSpace, "")]
    [InlineData("\n\n", ArgumentProcessing.ReplaceNewlinesWithSpace, "  ")]
    [InlineData("\\'\\\"\\", ArgumentProcessing.RemoveNewlines, "'\"\\")]
    [InlineData("\\'\\\"\\", ArgumentProcessing.ReplaceNewlinesWithSpace, "'\"\\")]
    [InlineData("\\'\\\"\\", ArgumentProcessing.AsIs, "\\'\\\"\\")]
    public void ArgumentProcessingHandlesEmptyResultsAndTrailingBackslashes(string input, ArgumentProcessing processing, string expected)
        => Assert.Equal(expected, SimpleParserHelper.ProcessArgument(input, SimpleParserOptions.Standard, processing));

    [Fact]
    public void TripleQuoteTrimmingPreservesInteriorWhitespace()
    {
        const string Input = " \"\"\" a\n b \"\"\" ";
        Assert.Equal(" a\n b ", Input.TrimQuotes());
        Assert.Equal(" a\n b ", Input.TrimQuotesAndBraces());
    }

    [Theory]
    [InlineData(" - -- - ")]
    [InlineData("-----------------------------------------")]
    public void AliasGenerationIgnoresEmptyWords(string commandName)
        => Assert.Equal(string.Empty, SimpleParserHelper.CreateAliasFromCommandName(commandName));

    [Fact]
    public void MissingAndInvalidEnvironmentVariablesLeaveArgumentsUnchanged()
    {
        var name = "SimpleCommandLine_HelperReviewTest_" + Guid.NewGuid().ToString("N");
        string[] args = ["original"];
        var originalArgs = args;
        var commandLine = "original";
        Assert.Equal(string.Empty, SimpleParserHelper.AppendEnvironmentVariable(ref args, name));
        Assert.Equal(string.Empty, SimpleParserHelper.AppendEnvironmentVariable(ref args, null!));
        Assert.Same(originalArgs, args);
        Assert.Equal(string.Empty, SimpleParserHelper.AppendEnvironmentVariable(ref commandLine, name));
        Assert.Equal(string.Empty, SimpleParserHelper.AppendEnvironmentVariable(ref commandLine, null!));
        Assert.Equal("original", commandLine);
    }

    [Fact]
    public void CurrentProcessArgumentsAreExtractedAndCached()
    {
        var actual = SimpleParserHelper.GetCommandLineArguments();
        Assert.Equal(SimpleParserHelper.ExtractArguments(Environment.CommandLine), actual);
        Assert.Same(actual, SimpleParserHelper.GetCommandLineArguments());
    }
}

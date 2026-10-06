// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

[Collection("Console")]
public class ParserCoverageTest
{
    [Theory]
    [InlineData("sbyte", "128")]
    [InlineData("byte", "256")]
    [InlineData("short", "32768")]
    [InlineData("ushort", "65536")]
    [InlineData("int", "2147483648")]
    [InlineData("uint", "4294967296")]
    [InlineData("long", "9223372036854775808")]
    [InlineData("ulong", "18446744073709551616")]
    [InlineData("float", "invalid")]
    [InlineData("double", "invalid")]
    [InlineData("decimal", "79228162514264337593543950336")]
    [InlineData("char", "")]
    public void FailedScalarConversionsClearThePreviousCommand(string name, string value)
    {
        var parser = new SimpleParserBuilder().AddCommand<AllTypeCommand, AllTypeOptions>()
            .Build(SimpleParserOptions.Standard with { ReadCommandFromEnvironment = false, SuppressConsoleOutput = true });
        Assert.True(parser.Parse(["all-type", "-int", "42"]));
        Assert.False(parser.Parse(["all-type", "-" + name, value]));
        Assert.Null(parser.CurrentCommand);
        Assert.Equal("all-type", parser.HelpCommandName);
        Assert.True(parser.Parse(["all-type"]));
        Assert.Equal(-5, ((AllTypeOptions)parser.CurrentCommand!.OptionSet.Instance!).Int);
    }

    [Theory]
    [InlineData("-text first | -text second")]
    [InlineData("-text first | 'unterminated")]
    public void StandaloneParsingStopsAtTheFirstCommand(string line)
    {
        Assert.True(SimpleParser.TryParseOptions<ReviewRegressionTest.TextOptions>(line, out var reflected));
        Assert.Equal("first", reflected.Text);
        Assert.True(new SimpleParserBuilder().TryParseOptions<ReviewRegressionTest.TextOptions>(line, out var registered));
        Assert.Equal("first", registered.Text);
    }

    [Fact]
    public void DiagnosticsStayCachedUntilTheNextParse()
    {
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.TextCommand, ReviewRegressionTest.TextOptions>()
            .Build(SimpleParserOptions.Standard with { ReadCommandFromEnvironment = false });
        string[] args = ["text", "-text", "original"];
        Assert.True(parser.Parse(args));
        Assert.Equal("text -text original", parser.OriginalCommandLine);
        args[2] = "changed";
        Assert.Equal("text -text original", parser.OriginalCommandLine);
        Assert.Equal("original", ((ReviewRegressionTest.TextOptions)parser.CurrentCommand!.OptionSet.Instance!).Text);
        Assert.True(parser.Parse(args));
        Assert.Equal("text -text changed", parser.OriginalCommandLine);
        Assert.True(parser.Parse("text  -text 'raw'"));
        Assert.Equal("text  -text 'raw'", parser.OriginalCommandLine);
    }

    [Theory]
    [InlineData(8, 19)]
    [InlineData(9, 19)]
    [InlineData(7, 3)]
    [InlineData(3, int.MaxValue)]
    public void CommandColumnsIncludeEachNameExactlyOnce(int count, int width)
    {
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.FirstDefault>().Build();
        var command = parser.NameToCommand["first"];
        parser.NameToCommand.Clear();
        var names = Enumerable.Range(0, count).Select(i => $"{i:D2}-" + new string('x', 90)).ToArray();
        foreach (var name in names)
        {
            parser.NameToCommand.Add(name, command);
        }

        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            parser.ShowCommandList(width);
        }
        finally
        {
            Console.SetOut(previous);
        }

        var actual = output.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(count, actual.Length);
        Array.Sort(actual, StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            Assert.StartsWith(actual[i], names[i], StringComparison.Ordinal);
            Assert.InRange(actual[i].Length, 2, Math.Min(width, names[i].Length));
        }
    }

    [Fact]
    public void HelpHandlesAnOptionsConstructorThatFails()
    {
        var parser = new SimpleParserBuilder().AddCommand<BrokenCommand, BrokenOptions>()
            .Build(SimpleParserOptions.Standard with { DisplayUsage = false });
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            parser.ShowHelp("broken");
        }
        finally
        {
            Console.SetOut(previous);
        }

        Assert.Contains("(Optional)", output.ToString());
    }

    public class BrokenOptions
    {
        public BrokenOptions() => throw new InvalidOperationException("Cannot construct options.");

        [SimpleOption("text")]
        public string? Text { get; set; }
    }

    [SimpleCommand("broken")]
    public class BrokenCommand : ISimpleCommand<BrokenOptions>
    {
        public Task Execute(BrokenOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

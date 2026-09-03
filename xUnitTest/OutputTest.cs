// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

[Collection("Console")]
public class OutputTest
{
    [Fact]
    public void ShowHelpUsesThePreviouslyRequestedCommand()
    {
        var parser = CreateParser();
        Assert.True(parser.Parse("help text"));
        var output = Capture(() => parser.ShowHelp());
        Assert.Contains("-text", output);
        Assert.DoesNotContain("first", output);
    }

    [Fact]
    public async Task ExecuteDisplaysHelpAndVersion()
    {
        var parser = CreateParser();
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            Assert.True(parser.Parse("help"));
            await parser.Execute(TestContext.Current.CancellationToken);
            Assert.Contains("Commands:", output.ToString());
            output.GetStringBuilder().Clear();
            Assert.True(parser.Parse("version"));
            await parser.Execute(TestContext.Current.CancellationToken);
            Assert.NotEmpty(output.ToString());
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    [Fact]
    public void ErrorsAndCommandListsAreWritten()
    {
        var parser = CreateParser();
        Assert.False(parser.Parse("text -text"));
        var output = Capture(() => parser.ShowHelp());
        Assert.Contains("Error: text -text", output);
        Assert.Contains("No corresponding value", output);

        output = Capture(() => parser.ShowCommandList());
        Assert.Contains("text", output);
        Assert.Contains("first", output);
        Assert.Equal(Environment.NewLine, Capture(() => new SimpleParserBuilder().Build().ShowCommandList()));
        Assert.Equal(Environment.NewLine, Capture(() => parser.ShowCommandList(0)));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NegativeColumnWidthIsRejected(int width)
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateParser().ShowCommandList(width));

    [Fact]
    public void HelpDisplaysDefaultsAndDescribesEachNestedTypeOnce()
    {
        var parser = new SimpleParserBuilder().AddCommand<DisplayCommand, DisplayOptions>()
            .AddOptions<ReviewRegressionTest.TextOptions>()
            .Build(SimpleParserOptions.Standard with { ReadCommandFromEnvironment = false });
        Assert.True(parser.Parse("display -required supplied -write written"));
        var options = (DisplayOptions)parser.CurrentCommand!.OptionClass.OptionInstance!;
        Assert.Equal("written", options.Written);
        Assert.NotNull(options.First);
        Assert.NotNull(options.Second);
        Assert.NotSame(options.First, options.Second);
        Assert.True(parser.TryGetOption("display", "write", out var writeOption));
        Assert.Null(writeOption.ShortName);
        Assert.False(writeOption.Parse("ignored", null, false));

        var output = Capture(() => parser.ShowHelp("display"));
        Assert.Contains("(Required: a value)", output);
        Assert.Contains("(Default: 3)", output);
        Assert.Contains("(Default: \"example\")", output);
        Assert.Contains("(Optional)", output);
        Assert.Equal(1, output.Split("{TextOptions}" + Environment.NewLine).Length - 1);
    }

    [Fact]
    public void OutputUsesTheConfiguredConsoleService()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConsoleService, ConsoleService>();
        using var provider = services.BuildServiceProvider();
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.FirstDefault>()
            .Build(SimpleParserOptions.Standard with { ServiceProvider = provider, ReadCommandFromEnvironment = false, DisplayCommandListAsHelp = true });
        Assert.Contains("first", Capture(() => parser.ShowHelp()));
        Assert.Contains("first", Capture(() => parser.ShowCommandList()));
    }

    public class DisplayOptions
    {
        [SimpleOption("required", Required = true, DefaultValueText = "a value")]
        public string Required { get; set; } = string.Empty;

        [SimpleOption("number", DefaultValueText = "3")]
        public int Number { get; set; }

        [SimpleOption("example", DefaultValueText = "example")]
        public string Example { get; set; } = string.Empty;

        [SimpleOption("write", ShortName = " ")]
        public string Write { set => this.Written = value; }

        public string Written { get; private set; } = string.Empty;

        [SimpleOption("first")]
        public ReviewRegressionTest.TextOptions? First { get; set; }

        [SimpleOption("second")]
        public ReviewRegressionTest.TextOptions? Second { get; set; }
    }

    [SimpleCommand("display")]
    public class DisplayCommand : ISimpleCommand<DisplayOptions>
    {
        public Task Execute(DisplayOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static SimpleParser CreateParser()
        => new SimpleParserBuilder()
            .AddCommand<ReviewRegressionTest.TextCommand, ReviewRegressionTest.TextOptions>()
            .AddCommand<ReviewRegressionTest.FirstDefault>()
            .Build(SimpleParserOptions.Standard with { ReadCommandFromEnvironment = false });

    private static string Capture(Action action)
    {
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            action();
            return output.ToString();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }
}

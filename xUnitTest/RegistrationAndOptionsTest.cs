// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Arc.Unit;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class RegistrationAndOptionsTest
{
    private static readonly SimpleParserOptions Settings = SimpleParserOptions.Standard with
    {
        ReadCommandFromEnvironment = false,
        SuppressConsoleOutput = true,
    };

    [Fact]
    public void AmbiguousReflectionInterfacesAreRejected()
        => Assert.Throws<InvalidOperationException>(() => new SimpleParser([typeof(AmbiguousCommand)], Settings));

    [Fact]
    public void DuplicateExplicitAliasesAreRejected()
        => Assert.Throws<InvalidOperationException>(() => new SimpleParserBuilder()
            .AddCommand<ReviewRegressionTest.AliasCommand>().AddCommand<DuplicateAlias>().Build(Settings));

    [Fact]
    public void AutomaticAliasCannotHideACommandName()
    {
        var parser = new SimpleParserBuilder().AddCommand<ReviewRegressionTest.TextCommand, ReviewRegressionTest.TextOptions>()
            .AddCommand<ShortCommand>().Build(Settings with { AutoAlias = true });
        Assert.True(parser.Parse("t"));
        Assert.IsType<ShortCommand>(parser.CurrentCommand!.CommandInstance);
        Assert.Equal(string.Empty, parser.NameToCommand["text"].Alias);
    }

    [Fact]
    public void DuplicateShortOptionNamesAreRejected()
        => Assert.Throws<InvalidOperationException>(() => new SimpleParserBuilder().TryParseOptions<DuplicateOptions>(string.Empty, out _));

    [Fact]
    public void MissingConstructorsAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new SimpleParserBuilder().AddCommand<NoConstructor>().Build(Settings));
        Assert.Throws<InvalidOperationException>(() => new SimpleParserBuilder().TryParseOptions<NoOptionsConstructor>(string.Empty, out _));
    }

    [Fact]
    public void MissingDependencyRegistrationHasADescriptiveError()
    {
        var parser = new SimpleParserBuilder().AddCommand<NoConstructor>().Build(Settings with { ServiceProvider = new EmptyProvider() });
        Assert.True(parser.Parse("no-constructor"));
        var error = Assert.Throws<InvalidOperationException>(() => parser.CurrentCommand!.CommandInstance);
        Assert.Contains("service provider", error.Message);
    }

    private class EmptyProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    [Fact]
    public void StandaloneRequiredValuesAreCheckedForNewAndExistingInstances()
    {
        var builder = new SimpleParserBuilder();
        Assert.False(builder.TryParseOptions<RequiredOptions>(string.Empty, out _));
        Assert.False(SimpleParser.TryParseOptions<RequiredOptions>(["-first", "present"], out _));
        var existing = new RequiredOptions { First = "previous", Second = 5 };
        Assert.False(builder.TryParseOptions(string.Empty, out _, existing));
        Assert.True(builder.TryParseOptions(["replacement", "8"], out var updated, existing));
        Assert.Same(existing, updated);
        Assert.Equal(8, updated.Second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyOptionNamesAreRejected(string? name)
        => Assert.Throws<ArgumentNullException>(() => new SimpleOptionAttribute(name!));

    [Fact]
    public void StrictPlainCommandRejectsUnknownOptions()
    {
        var parser = new SimpleParserBuilder().AddCommand<ShortCommand>().Build(Settings with { RequireStrictOptionName = true });
        Assert.True(parser.RequireStrictOptionName);
        Assert.False(parser.Parse("t -unknown"));
        Assert.Null(parser.CurrentCommand);
    }

    [Fact]
    public async Task LegacyGroupSupportsAFullDefaultCommandLine()
    {
        var builder = new UnitBuilder();
        builder.Configure(context => LegacyGroup.ConfigureGroup(context, typeof(ParentGroup))
            .AddCommand(typeof(ReviewRegressionTest.TextCommand)));
        var unit = builder.Build();
        using var provider = (IDisposable)unit.Context.ServiceProvider;
        var group = new LegacyGroup(unit.Context);
        await group.Execute([], TestContext.Current.CancellationToken);
        Assert.Equal("two words", ((ReviewRegressionTest.TextOptions)group.Parser.CurrentCommand!.OptionClass.OptionInstance!).Text);
        Assert.Single(unit.Context.GetCommandTypes(typeof(ParentGroup)));
    }

    [SimpleCommand("ambiguous")]
    public class AmbiguousCommand : ISimpleCommand, ISimpleCommand<ReviewRegressionTest.TextOptions>
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task Execute(ReviewRegressionTest.TextOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("duplicate", Alias = "a")]
    public class DuplicateAlias : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("t")]
    public class ShortCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("no-constructor")]
    public class NoConstructor : ISimpleCommand
    {
        public NoConstructor(int value)
        {
        }

        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class NoOptionsConstructor
    {
        public NoOptionsConstructor(int value)
        {
        }
    }

    public class DuplicateOptions
    {
        [SimpleOption("first", ShortName = "x")]
        public string First { get; set; } = string.Empty;

        [SimpleOption("second", ShortName = "x")]
        public string Second { get; set; } = string.Empty;
    }

    [SimpleCommand("parent", IsSubcommand = true)]
    public class ParentGroup : SimpleCommandGroup<ParentGroup>
    {
        public ParentGroup(UnitContext context)
            : base(context, parserOptions: Settings)
        {
        }
    }

    [SimpleCommand("legacy", IsSubcommand = true)]
    public class LegacyGroup : SimpleCommandGroup<LegacyGroup>
    {
        public LegacyGroup(UnitContext context)
            : base(context, "text -text 'two words'", Settings)
        {
        }
    }
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

[Collection("Console")]
public class NativeAotTest
{
    [Fact]
    public async Task RegisteredTypesWorkInManagedRuntime()
        => Assert.True(await global::NativeAotTest.SmokeScenarios.Run() > 0);

    [Fact]
    public async Task UnitRegistrationWorksInManagedRuntime()
        => Assert.True(await global::NativeAotTest.UnitIntegrationScenarios.Run() > 0);

    [Fact]
    public async Task ReflectionSupportsExplicitInterfaces()
    {
        var parser = new SimpleParser([typeof(ExplicitCommand)], SimpleParserOptions.Standard with { ReadCommandFromEnvironment = false });
        Assert.True(parser.Parse("-value 2"));
        await parser.Execute(TestContext.Current.CancellationToken);
        Assert.Equal(2, ((ExplicitCommand)parser.CurrentCommand!.CommandInstance).Value);
    }

    [Fact]
    public void NonPublicSetterIsUsed()
    {
        Assert.True(SimpleParser.TryParseOptions<PrivateSetterOptions>("-value 3", out var options));
        Assert.Equal(6, options.Value);
    }

    [Fact]
    public void RepeatedNestedOptionsAreParsed()
    {
        var parser = new SimpleParser([typeof(ParserCommand)], SimpleParserOptions.Standard with { ReadCommandFromEnvironment = false, SuppressConsoleOutput = true });
        Assert.True(parser.Parse("-nested {-value 1} -nested {-value 2}"));
        Assert.Equal(2, ((ParserOptions)parser.CurrentCommand!.OptionClass.OptionInstance!).Nested.Value);
        Assert.False(parser.Parse("-nested {-value 1} -nested {-value invalid}"));
    }

    private sealed class PrivateSetterOptions
    {
        [SimpleOption("value")]
        public int Value { get; private set => field = value * 2; }
    }

    [SimpleCommand("explicit")]
    private sealed class ExplicitCommand : ISimpleCommand<NestedOptions>
    {
        public int Value { get; private set; }

        Task ISimpleCommand<NestedOptions>.Execute(NestedOptions options, string[] args, CancellationToken cancellationToken)
        {
            this.Value = options.Value;
            return Task.CompletedTask;
        }
    }
}

[CollectionDefinition("Console", DisableParallelization = true)]
public class ConsoleCollection
{
}

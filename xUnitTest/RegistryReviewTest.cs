// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class RegistryReviewTest
{
    private static readonly SimpleParserOptions Settings = SimpleParserOptions.Standard with
    {
        ReadCommandFromEnvironment = false,
        SuppressConsoleOutput = true,
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObjectOptionsWorkWithExplicitRegistration(bool splitArguments)
    {
        var builder = new SimpleParserBuilder();
        object? parsed;
        Assert.True(splitArguments
            ? builder.TryParseOptions<object>([], out parsed)
            : builder.TryParseOptions<object>(string.Empty, out parsed));
        Assert.IsType<object>(parsed);

        var existing = new object();
        Assert.True(builder.TryParseOptions([], out var updated, existing));
        Assert.Same(existing, updated);
    }

    [Fact]
    public async Task ObjectCommandOptionsReachTypedExecution()
    {
        var parser = new SimpleParserBuilder().AddCommand<ObjectCommand, object>().Build(Settings);
        Assert.True(parser.Parse("object"));
        await parser.Execute(TestContext.Current.CancellationToken);
        var command = Assert.IsType<ObjectCommand>(parser.CurrentCommand!.CommandInstance);
        Assert.IsType<object>(command.Received);
        Assert.Same(parser.CurrentCommand.OptionSet.Instance, command.Received);
    }

    [Fact]
    public void RepeatedBuildsHaveIndependentStateAndRetainRegistrationOrder()
    {
        var builder = new SimpleParserBuilder().AddCommand<NumberCommand, NumberOptions>().AddCommand<ObjectCommand, object>();
        var first = builder.Build(Settings);
        builder.AddCommand<NumberCommand, NumberOptions>();
        var second = builder.Build(Settings);

        Assert.Equal(new[] { "number", "object" }, second.NameToCommand.Keys);
        Assert.True(first.Parse("number -value 1"));
        Assert.True(second.Parse("number -value 2"));
        Assert.Equal(1, Assert.IsType<NumberOptions>(first.CurrentCommand!.OptionSet.Instance).Value);
        Assert.Equal(2, Assert.IsType<NumberOptions>(second.CurrentCommand!.OptionSet.Instance).Value);
        Assert.NotSame(first.CurrentCommand.CommandInstance, second.CurrentCommand.CommandInstance);

        builder.AddCommand<PlainCommand>();
        var third = builder.Build(Settings);
        Assert.Equal(new[] { "number", "object", "plain" }, third.NameToCommand.Keys);
        Assert.Equal(2, first.NameToCommand.Count);
        Assert.Equal(2, second.NameToCommand.Count);
    }

    [Fact]
    public void AddingNestedOptionsRefreshesARegistryAfterFailedBuild()
    {
        var builder = new SimpleParserBuilder().AddCommand<NestedCommand, NestedOptions>();
        Assert.Throws<InvalidOperationException>(() => builder.Build(Settings));
        builder.AddOptionsType<NumberOptions>();

        var parser = builder.Build(Settings);
        Assert.True(parser.Parse("nested -number {-value 7}"));
        Assert.Equal(7, Assert.IsType<NestedOptions>(parser.CurrentCommand!.OptionSet.Instance).Number.Value);
    }

    [Fact]
    public void ConflictingCommandRegistrationDoesNotPreserveUnregisteredOptions()
    {
        var builder = new SimpleParserBuilder().AddCommand<AmbiguousCommand>();
        Assert.Throws<InvalidOperationException>(() => builder.AddCommand<AmbiguousCommand, NumberOptions>());
        builder.AddCommand<NestedCommand, NestedOptions>();
        var error = Assert.Throws<InvalidOperationException>(() => builder.Build(Settings));
        Assert.Contains(nameof(NumberOptions), error.Message);

        builder.AddOptionsType<NumberOptions>();
        Assert.True(builder.Build(Settings).Parse("nested -number {-value 9}"));
    }

    [Fact]
    public void SharedRegistryUsesRequestedOrderAndRejectsUnknownTypes()
    {
        var builder = new UnitBuilder();
        builder.Configure(context =>
        {
            context.AddCommand<NumberCommand, NumberOptions>();
            context.AddCommand<PlainCommand>();
        });
        var unit = builder.Build();
        using var provider = (ServiceProvider)unit.Context.ServiceProvider;
        var registry = provider.GetRequiredService<SimpleCommandRegistry>();
        var parser = registry.CreateParser(new[] { typeof(PlainCommand), typeof(NumberCommand), typeof(PlainCommand) }.Select(x => x), Settings);
        Assert.Equal(new[] { "plain", "number" }, parser.NameToCommand.Keys);
        Assert.Equal("plain", parser.DefaultCommandName);
        Assert.Throws<ArgumentNullException>(() => registry.CreateParser(null!));
        Assert.Throws<InvalidOperationException>(() => registry.CreateParser([typeof(ObjectCommand)], Settings));
    }

    [Fact]
    public void NullCommandNamesHaveAnArgumentError()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new SimpleCommandAttribute(null!));
        Assert.Equal("commandName", error.ParamName);
        Assert.Equal(string.Empty, new SimpleCommandAttribute(" \t ").CommandName);
    }

    [Fact]
    public async Task ReflectionDispatchesInheritedExplicitCommandsWithCancellation()
    {
        var parser = new SimpleParser([typeof(InheritedCommand)], Settings);
        Assert.True(parser.Parse(new[] { "inherited", "two words" }));
        using var cancellation = new CancellationTokenSource();
        await parser.Execute(cancellation.Token);
        var command = Assert.IsType<InheritedCommand>(parser.CurrentCommand!.CommandInstance);
        Assert.Equal(new[] { "two words" }, command.Received);
        Assert.Equal(cancellation.Token, command.Token);
    }

    public class NumberOptions
    {
        [SimpleOption("value")]
        public int Value { get; set; }
    }

    public class NestedOptions
    {
        [SimpleOption("number")]
        public NumberOptions Number { get; set; } = new();
    }

    [SimpleCommand("object")]
    public class ObjectCommand : ISimpleCommand<object>
    {
        public object? Received { get; private set; }

        public Task Execute(object options, string[] args, CancellationToken cancellationToken)
        {
            this.Received = options;
            return Task.CompletedTask;
        }
    }

    [SimpleCommand("number")]
    public class NumberCommand : ISimpleCommand<NumberOptions>
    {
        public Task Execute(NumberOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("nested")]
    public class NestedCommand : ISimpleCommand<NestedOptions>
    {
        public Task Execute(NestedOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("plain")]
    public class PlainCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("ambiguous")]
    public class AmbiguousCommand : ISimpleCommand, ISimpleCommand<NumberOptions>
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task Execute(NumberOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public class ExplicitCommand : ISimpleCommand
    {
        public string[]? Received { get; private set; }

        public CancellationToken Token { get; private set; }

        Task ISimpleCommand.Execute(string[] args, CancellationToken cancellationToken)
        {
            this.Received = args;
            this.Token = cancellationToken;
            return Task.CompletedTask;
        }
    }

    [SimpleCommand("inherited")]
    public class InheritedCommand : ExplicitCommand
    {
    }
}

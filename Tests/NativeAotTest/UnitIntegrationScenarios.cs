// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;
using SimpleCommandLine;

namespace NativeAotTest;

public static class UnitIntegrationScenarios
{
    public static async Task<int> Run()
    {
        var checks = 0;
        var settings = SimpleParserOptions.Standard with { SuppressConsoleOutput = true, ReadCommandFromEnvironment = false };
        IUnitConfigurationContext? retainedContext = null;
        SimpleCommandGroupBuilder? retainedGroup = null;
        var builder = new UnitBuilder();
        builder.Configure(context =>
        {
            retainedContext = context;
            context.AddScoped<ScopeState>();
            Check(context.AddCommand<RunCommand, RunOptions>(), "first command registration");
            Check(!context.AddCommand<RunCommand, RunOptions>(ServiceLifetime.Singleton), "duplicate command registration");
            Check(context.Services.Single(x => x.ServiceType == typeof(RunCommand)).Lifetime == ServiceLifetime.Scoped, "duplicate registration preserves lifetime");
            Check(context.AddCommand<DatabaseCommand>(), "parent registration");
            Check(context.AddCommand<SingletonCommand>(ServiceLifetime.Singleton), "singleton registration");
            Check(context.AddCommand<TransientCommand>(ServiceLifetime.Transient), "transient registration");
            Check(context.AddSubcommand<Subcommand>(), "subcommand registration");
            Check(context.AddSubcommand<RunCommand, RunOptions>(), "same command in separate list");
        });
        // Registration from another module/configuration delegate shares the same metadata.
        builder.Configure(context =>
        {
            context.AddOptionType<NestedOptions>();
            retainedGroup = context.GetSimpleCommandGroup<DatabaseCommand>();
            Check(retainedGroup.AddCommand<RunCommand, RunOptions>(), "same command in a group");
            Check(retainedGroup.AddCommand<InnerCommand>(), "nested group registration");
            context.GetSimpleCommandGroup<InnerCommand>().AddCommand<RunCommand, RunOptions>();
        });
        var unit = builder.Build();
        using var provider = (ServiceProvider)unit.Context.ServiceProvider;
        using var scope = provider.CreateScope();
        var scopedSettings = settings with { ServiceProvider = scope.ServiceProvider };
        var registry = provider.GetRequiredService<SimpleCommandRegistry>();
        Check(ReferenceEquals(registry, scope.ServiceProvider.GetRequiredService<SimpleCommandRegistry>()), "one registry per unit");

        var parser = unit.Context.CreateSimpleParser(scopedSettings);
        Check(parser.NameToCommand.Keys.SequenceEqual(["run", "db", "singleton", "transient"]), "Arc.Unit owns command ordering and membership");
        Check(parser.DefaultCommandName == "run", "Arc.Unit order chooses default command");
        Check(parser.Parse("-value 10 -nested {-text example}"), "single registration parses root and nested options");
        using var cancellation = new CancellationTokenSource();
        await parser.Execute(cancellation.Token);
        var run = (RunCommand)parser.CurrentCommand!.CommandInstance;
        var parsedOptions = (RunOptions)parser.CurrentCommand.OptionClass.OptionInstance!;
        Check(ReferenceEquals(run, scope.ServiceProvider.GetRequiredService<RunCommand>()), "command uses scoped DI registration");
        Check(run.Received?.Value == 10 && run.Received.Nested.Text == "example" && run.Token == cancellation.Token, "typed command execution");
        Check(ReferenceEquals(run.State, scope.ServiceProvider.GetRequiredService<ScopeState>()), "scoped constructor dependency");

        var independent = unit.Context.CreateSimpleParser(scopedSettings);
        Check(independent.CurrentCommand is null, "parsers do not share parse state");
        Check(independent.Parse("run -value 20"), "second parser parses independently");
        Check(parsedOptions.Value == 10, "options instances are not shared");
        parser.TryGetOption("run", "value", out var option);
        independent.TryGetOption("run", "value", out var otherOption);
        option!.Description = "changed";
        Check(otherOption!.Description != "changed", "option descriptions are not shared");

        var childParser = unit.Context.CreateSimpleParser<DatabaseCommand>(scopedSettings);
        Check(childParser.NameToCommand.Keys.SequenceEqual(["run", "inner"]), "group membership is isolated");
        var subParser = unit.Context.CreateSimpleSubcommandParser(scopedSettings);
        Check(subParser.NameToCommand.Keys.SequenceEqual(["sub", "run"]), "separate subcommand list is respected");
        Check(subParser.Parse("sub"), "subcommand parses");
        await subParser.Execute(cancellation.Token);

        Check(parser.Parse("db run -value 30"), "group dispatch");
        await parser.Execute(cancellation.Token);
        var database = (DatabaseCommand)parser.CurrentCommand!.CommandInstance;
        Check(ReferenceEquals(database.Registry, registry), "group uses the shared registry");
        Check(ReferenceEquals(database.Parser.CurrentCommand!.CommandInstance, run), "child resolves in the parent's scope");
        Check(run.Received!.Value == 30 && run.Token == cancellation.Token, "group forwards options and cancellation");
        Check(parser.Parse("db inner run -value 40"), "nested group dispatch");
        await parser.Execute(cancellation.Token);
        var inner = (InnerCommand)database.Parser.CurrentCommand!.CommandInstance;
        Check(ReferenceEquals(inner.Parser.CurrentCommand!.CommandInstance, run), "nested groups preserve the DI scope");
        Check(run.Received!.Value == 40 && run.Token == cancellation.Token, "nested group execution");
        Check(parser.Parse("db"), "default group command");
        await parser.Execute(cancellation.Token);
        Check(run.Received!.Value == 0, "default group command uses new option values");

        using var secondScope = provider.CreateScope();
        var anotherScope = unit.Context.CreateSimpleParser(settings with { ServiceProvider = secondScope.ServiceProvider });
        Check(anotherScope.Parse("run"), "another scope parses");
        await anotherScope.Execute(cancellation.Token);
        var otherRun = (RunCommand)anotherScope.CurrentCommand!.CommandInstance;
        Check(!ReferenceEquals(run, otherRun) && !ReferenceEquals(run.State, otherRun.State), "scoped commands and dependencies differ between scopes");
        Check(ReferenceEquals(scope.ServiceProvider.GetRequiredService<SingletonCommand>(), secondScope.ServiceProvider.GetRequiredService<SingletonCommand>()), "singleton lifetime");
        Check(!ReferenceEquals(scope.ServiceProvider.GetRequiredService<TransientCommand>(), scope.ServiceProvider.GetRequiredService<TransientCommand>()), "transient lifetime");

        ExpectInvalid(() => retainedContext!.AddCommand<LateCommand>(), "registration is complete", "late command registration rejected");
        ExpectInvalid(() => retainedContext!.AddOptionType<UnregisteredOptions>(), "registration is complete", "late options registration rejected");
        ExpectInvalid(() => retainedGroup!.AddCommand<LateCommand>(), "registration is complete", "retained group cannot modify frozen registry");
        ExpectInvalid(() => retainedContext!.GetSimpleCommandGroup<LateCommand>(), "registration is complete", "late group creation rejected");
        Check(registry.CreateParser(unit.Context.Commands, scopedSettings).NameToCommand.Count == 4, "frozen registry is unchanged");

        var otherBuilder = new UnitBuilder();
        otherBuilder.Configure(context => context.AddCommand<Subcommand>());
        var otherUnit = otherBuilder.Build();
        using var otherProvider = (ServiceProvider)otherUnit.Context.ServiceProvider;
        Check(otherUnit.Context.CreateSimpleParser(settings).NameToCommand.Keys.SequenceEqual(["sub"]), "different units have independent registrations");
        Check(!ReferenceEquals(registry, otherProvider.GetRequiredService<SimpleCommandRegistry>()), "registries are not global");
        ExpectInvalid(() => otherProvider.GetRequiredService<SimpleCommandRegistry>().CreateParser([typeof(RunCommand)], settings), "not registered", "metadata does not leak between units");

        var rawBuilder = new UnitBuilder();
        rawBuilder.Configure(context =>
        {
            context.AddCommand(typeof(Subcommand));
            Check(!context.AddCommand<Subcommand>(), "generic registration supplements an existing Arc.Unit entry");
            context.AddCommand(typeof(LateCommand));
        });
        var rawUnit = rawBuilder.Build();
        using var rawProvider = (ServiceProvider)rawUnit.Context.ServiceProvider;
        var rawRegistry = rawProvider.GetRequiredService<SimpleCommandRegistry>();
        Check(rawRegistry.CreateParser([typeof(Subcommand)], settings).Parse("sub"), "existing raw registration gains typed metadata");
        ExpectInvalid(() => rawUnit.Context.CreateSimpleParser(settings), "generic", "raw-only registrations fail clearly");

        var conflicting = new SimpleParserBuilder().AddCommand<MultiCommand>();
        ExpectInvalid(() => conflicting.AddCommand<MultiCommand, RunOptions>(), "different options type", "conflicting shared metadata rejected");
        var missingBuilder = new UnitBuilder();
        var missing = missingBuilder.Build();
        using var missingProvider = (ServiceProvider)missing.Context.ServiceProvider;
        ExpectInvalid(() => missing.Context.CreateSimpleParser(settings), "registry", "missing integration registration fails clearly");

        return checks;

        void Check(bool success, string description)
        {
            if (!success)
            {
                throw new InvalidOperationException($"Failed: {description}");
            }

            checks++;
        }

        void ExpectInvalid(Action action, string message, string description)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException ex)
            {
                Check(ex.Message.Contains(message, StringComparison.OrdinalIgnoreCase), description);
                return;
            }

            throw new InvalidOperationException($"Failed: {description} (no exception)");
        }
    }

    private sealed class ScopeState
    {
    }

    private sealed class RunOptions
    {
        [SimpleOption("value")]
        public int Value { get; set; }

        [SimpleOption("nested")]
        public NestedOptions Nested { get; set; } = new();
    }

    private sealed class NestedOptions
    {
        [SimpleOption("text")]
        public string Text { get; set; } = string.Empty;
    }

    [SimpleCommand("run")]
    private sealed class RunCommand : ISimpleCommand<RunOptions>
    {
        public RunCommand(ScopeState state) => this.State = state;

        public ScopeState State { get; }

        public RunOptions? Received { get; private set; }

        public CancellationToken Token { get; private set; }

        public Task Execute(RunOptions options, string[] args, CancellationToken cancellationToken)
        {
            this.Received = options;
            this.Token = cancellationToken;
            return Task.CompletedTask;
        }
    }

    [SimpleCommand("db", IsSubcommand = true)]
    private sealed class DatabaseCommand : SimpleCommandGroup<DatabaseCommand>
    {
        public DatabaseCommand(SimpleCommandRegistry registry, UnitContext context, IServiceProvider serviceProvider)
            : base(registry, context, "run", SimpleParserOptions.Standard with { ServiceProvider = serviceProvider, SuppressConsoleOutput = true, ReadCommandFromEnvironment = false })
        {
            this.Registry = registry;
        }

        public SimpleCommandRegistry Registry { get; }
    }

    [SimpleCommand("inner", IsSubcommand = true)]
    private sealed class InnerCommand : SimpleCommandGroup<InnerCommand>
    {
        public InnerCommand(SimpleCommandRegistry registry, UnitContext context, IServiceProvider serviceProvider)
            : base(registry, context, "run", SimpleParserOptions.Standard with { ServiceProvider = serviceProvider, SuppressConsoleOutput = true, ReadCommandFromEnvironment = false })
        {
        }
    }

    [SimpleCommand("sub")]
    private sealed class Subcommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("singleton")]
    private sealed class SingletonCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("transient")]
    private sealed class TransientCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [SimpleCommand("late")]
    private sealed class LateCommand : ISimpleCommand
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UnregisteredOptions
    {
    }

    [SimpleCommand("multi")]
    private sealed class MultiCommand : ISimpleCommand, ISimpleCommand<RunOptions>
    {
        public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task Execute(RunOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

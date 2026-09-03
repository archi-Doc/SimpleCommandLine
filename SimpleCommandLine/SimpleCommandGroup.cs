// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCommandLine;

/// <summary>
/// A base class for a command which dispatches its arguments to a group of subcommands.
/// </summary>
/// <typeparam name="TCommand">The type of the derived command group.</typeparam>
public abstract class SimpleCommandGroup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand> : ISimpleCommand
    where TCommand : SimpleCommandGroup<TCommand>
{
    /// <summary>
    /// Registers <typeparamref name="TCommand"/> with its parent and returns its own <see cref="CommandGroup"/>,
    /// to which the subcommands are then added.
    /// </summary>
    /// <param name="context">The unit configuration context.</param>
    /// <param name="parentCommandType">The type of the parent command. Use <see langword="null"/> to register at the top level.</param>
    /// <param name="lifetime">The service lifetime of the command.</param>
    /// <returns>The command group of <typeparamref name="TCommand"/>.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Arc.Unit 0.46 GetCommandGroup uses the type as a dictionary key and registers it with DI. The public constructors required by DI are preserved.")]
    [UnconditionalSuppressMessage("Trimming", "IL2087", Justification = "Arc.Unit 0.46 GetCommandGroup uses the type as a key and AddCommand registers a DI ServiceDescriptor. Only public constructors are required; command dispatch is handled separately by SimpleParserBuilder.")]
    public static CommandGroup ConfigureGroup(IUnitConfigurationContext context, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type? parentCommandType = null, ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var commandType = typeof(TCommand);

        // Add a command type to the parent.
        CommandGroup group;
        if (parentCommandType != null)
        {
            group = context.GetCommandGroup(parentCommandType);
        }
        else
        {
            group = context.GetSubcommandGroup();
        }

        group.AddCommand(commandType, lifetime);

        // Get the command group.
        group = context.GetCommandGroup(commandType);
        return group;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCommandGroup{TCommand}"/> class.
    /// </summary>
    /// <param name="context">The unit context which provides the subcommand types and the service provider.</param>
    /// <param name="defaultArgument">The argument used when no argument is given. Use <see langword="null"/> to show the command list instead.</param>
    /// <param name="parserOptions">
    /// The options of the inner parser. Use <see langword="null"/> for the defaults of a command group
    /// (a strict command and option name, no usage text, and the command list as help).
    /// </param>
    [RequiresUnreferencedCode(PreservedType.ReflectionWarning)]
    public SimpleCommandGroup(UnitContext context, string? defaultArgument = null, SimpleParserOptions? parserOptions = null)
        : this(context, defaultArgument, parserOptions, static (types, options) => new SimpleParser(types, options))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCommandGroup{TCommand}"/> class using explicitly registered types for trimming and NativeAOT.
    /// </summary>
    /// <param name="parserBuilder">The builder containing all subcommands and nested options types.</param>
    /// <param name="context">The unit context containing the group's command types and services.</param>
    /// <param name="defaultArgument">The default subcommand, or null to show the command list.</param>
    /// <param name="parserOptions">Options for the inner parser, or null for the group defaults.</param>
    public SimpleCommandGroup(SimpleParserBuilder parserBuilder, UnitContext context, string? defaultArgument = null, SimpleParserOptions? parserOptions = null)
        : this(context, defaultArgument, parserOptions, (types, options) => parserBuilder.Build(options, types))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCommandGroup{TCommand}"/> class using the unit's shared command registry.
    /// </summary>
    /// <param name="registry">The shared registry, supplied by dependency injection after generic command registration.</param>
    /// <param name="context">The unit context supplying this group's child command types.</param>
    /// <param name="defaultArgument">The default subcommand, or null to show the command list.</param>
    /// <param name="parserOptions">Parser options. Supply a scoped ServiceProvider to resolve child commands in the same scope.</param>
    public SimpleCommandGroup(SimpleCommandRegistry registry, UnitContext context, string? defaultArgument = null, SimpleParserOptions? parserOptions = null)
        : this(context, defaultArgument, parserOptions, (types, options) => registry.CreateParser(types, options))
    {
    }

    private SimpleCommandGroup(UnitContext context, string? defaultArgument, SimpleParserOptions? parserOptions, Func<IEnumerable<Type>, SimpleParserOptions, SimpleParser> createParser)
    {
        this.createParser = createParser;
        this.commandTypes = context.GetCommandTypes(typeof(TCommand));

        if (parserOptions != null)
        {
            this.ParserOptions = parserOptions with { ServiceProvider = parserOptions.ServiceProvider ?? context.ServiceProvider, };
        }
        else
        {
            this.ParserOptions = SimpleParserOptions.Standard with
            {
                ServiceProvider = context.ServiceProvider,
                RequireStrictCommandName = true,
                RequireStrictOptionName = true,
                DisplayUsage = false,
                DisplayCommandListAsHelp = true,
            };
        }

        this.defaultArgument = defaultArgument;
    }

    /// <summary>
    /// Parses the arguments and executes the specified subcommand.<br/>
    /// The default argument is used when the arguments are empty.
    /// </summary>
    /// <param name="args">The arguments specifying the subcommand and its options.</param>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    public Task Execute(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 && this.defaultArgument != null)
        {// Default argument
            args = [this.defaultArgument,];
        }

        return this.Parser.ParseAndExecute(args, cancellationToken);
    }

    /// <summary>
    /// Gets the options of the inner parser.
    /// </summary>
    public SimpleParserOptions ParserOptions { get; }

    /// <summary>
    /// Gets the parser for the subcommands, creating it on the first access.
    /// </summary>
    public SimpleParser Parser
    {
        get
        {
            this.parser ??= this.createParser(this.commandTypes, this.ParserOptions);
            return this.parser;
        }
    }

    private readonly Type[] commandTypes;
    private readonly Func<IEnumerable<Type>, SimpleParserOptions, SimpleParser> createParser;
    private readonly string? defaultArgument;
    private SimpleParser? parser;
}

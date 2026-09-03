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
/// Dispatches remaining arguments to an Arc.Unit command group's cached parser.
/// </summary>
/// <typeparam name="TCommand">The type of the derived command group.</typeparam>
/// <remarks>Annotate the derived class with <see cref="SimpleCommandAttribute"/> and set <see cref="SimpleCommandAttribute.IsSubcommand"/> to true.</remarks>
public abstract class SimpleCommandGroup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand> : ISimpleCommand
    where TCommand : SimpleCommandGroup<TCommand>
{
    /// <summary>
    /// Registers this command in Arc.Unit and returns its child group for legacy configuration.
    /// </summary>
    /// <param name="context">The unit configuration context.</param>
    /// <param name="parentCommandType">The parent command type, or null to register in Arc.Unit's separate subcommand list.</param>
    /// <param name="lifetime">The service lifetime of the command.</param>
    /// <returns>The command group of <typeparamref name="TCommand"/>.</returns>
    /// <remarks>This method does not populate <see cref="SimpleCommandRegistry"/>. Use the generic <see cref="UnitCommandExtensions"/> for shared registration.</remarks>
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
    /// <param name="defaultArgument">The raw command line used when the argument array is empty, or null to pass empty input to the parser.</param>
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
    /// <param name="defaultArgument">The raw command line used for an empty argument array, or null to leave the input empty.</param>
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
    /// <param name="defaultArgument">The raw command line used for an empty argument array, or null to leave the input empty.</param>
    /// <param name="parserOptions">Options, or null for group defaults. Supply a scoped service provider to share the parent's scope.</param>
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
    /// Parses pre-split arguments and executes a child command, or processes the default command line for empty input.
    /// </summary>
    /// <param name="args">The arguments specifying the subcommand and its options.</param>
    /// <param name="cancellationToken">The token forwarded to the child command.</param>
    /// <returns>A task that represents the command execution.</returns>
    public Task Execute(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 && this.defaultArgument != null)
        {// Default argument
            return this.Parser.ParseAndExecute(this.defaultArgument, cancellationToken);
        }

        return this.Parser.ParseAndExecute(args, cancellationToken);
    }

    /// <summary>
    /// Gets the inner parser's options, with the unit's service provider as the fallback.
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

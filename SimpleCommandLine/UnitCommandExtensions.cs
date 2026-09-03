// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCommandLine;

/// <summary>
/// Integrates Arc.Unit command registration and DI with SimpleCommandLine's NativeAOT metadata.
/// </summary>
public static class UnitCommandExtensions
{
    /// <summary>
    /// Registers a top-level command without options in Arc.Unit and the shared parser registry.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="context">The configuration context.</param>
    /// <param name="lifetime">The DI service lifetime, used if the service is not already registered.</param>
    /// <returns>True if newly added to the top-level command list; otherwise false.</returns>
    public static bool AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(
        this IUnitConfigurationContext context, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ISimpleCommand
        => GetConfigurationGroup(context, false).AddCommand<TCommand>(lifetime);

    /// <summary>
    /// Registers a top-level command and its root options metadata. Register nested options with AddOptionType.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TOptions">The root options type.</typeparam>
    /// <param name="context">The configuration context.</param>
    /// <param name="lifetime">The DI service lifetime, used if the service is not already registered.</param>
    /// <returns>True if newly added to the top-level command list; otherwise false.</returns>
    public static bool AddCommand<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(
        this IUnitConfigurationContext context, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ISimpleCommand<TOptions>
        where TOptions : new()
        => GetConfigurationGroup(context, false).AddCommand<TCommand, TOptions>(lifetime);

    /// <summary>
    /// Registers a command without options in Arc.Unit's separate subcommand list and the shared parser registry.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="context">The configuration context.</param>
    /// <param name="lifetime">The DI service lifetime, used if the service is not already registered.</param>
    /// <returns>True if newly added to the subcommand list; otherwise false.</returns>
    public static bool AddSubcommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(
        this IUnitConfigurationContext context, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ISimpleCommand
        => GetConfigurationGroup(context, true).AddCommand<TCommand>(lifetime);

    /// <summary>
    /// Registers a command and its root options in Arc.Unit's separate subcommand list and the shared parser registry.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TOptions">The root options type.</typeparam>
    /// <param name="context">The configuration context.</param>
    /// <param name="lifetime">The DI service lifetime, used if the service is not already registered.</param>
    /// <returns>True if newly added to the subcommand list; otherwise false.</returns>
    public static bool AddSubcommand<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(
        this IUnitConfigurationContext context, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ISimpleCommand<TOptions>
        where TOptions : new()
        => GetConfigurationGroup(context, true).AddCommand<TCommand, TOptions>(lifetime);

    /// <summary>
    /// Preserves a nested options type and its inherited/non-public members in this unit's shared registry.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="context">The configuration context.</param>
    public static void AddOptionType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(this IUnitConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.GetCustomContext<SimpleCommandConfiguration>().Builder.AddOptions<TOptions>();
    }

    /// <summary>
    /// Gets a command's child group for combined Arc.Unit and parser registration.
    /// Register the parent command itself with AddCommand or AddSubcommand to choose where it belongs.
    /// </summary>
    /// <typeparam name="TCommand">The parent command type.</typeparam>
    /// <param name="context">The configuration context.</param>
    /// <returns>A builder for the parent's child commands.</returns>
    [UnconditionalSuppressMessage("Trimming", "IL2087", Justification = "Arc.Unit 0.46 GetCommandGroup uses the type as a dictionary key and registers it with DI. The public constructors required by DI are preserved.")]
    public static SimpleCommandGroupBuilder GetSimpleCommandGroup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(this IUnitConfigurationContext context)
        where TCommand : ISimpleCommand
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = context.GetCustomContext<SimpleCommandConfiguration>();
        _ = configuration.Builder; // Reject late configuration before modifying Arc.Unit's groups.
        return new SimpleCommandGroupBuilder(configuration, context.GetCommandGroup(typeof(TCommand)));
    }

    /// <summary>
    /// Creates an independent parser for Arc.Unit's top-level command list using the shared registry.
    /// </summary>
    /// <param name="context">The built unit context.</param>
    /// <param name="parserOptions">Parser options. ServiceProvider defaults to the unit's provider; supply a scoped provider when needed.</param>
    /// <returns>A new parser.</returns>
    public static SimpleParser CreateSimpleParser(this UnitContext context, SimpleParserOptions? parserOptions = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return GetRegistry(context).CreateParser(context.Commands, WithServiceProvider(context, parserOptions));
    }

    /// <summary>
    /// Creates an independent parser for the children of the specified command using the shared registry.
    /// </summary>
    /// <typeparam name="TCommand">The parent command type.</typeparam>
    /// <param name="context">The built unit context.</param>
    /// <param name="parserOptions">Parser options. ServiceProvider defaults to the unit's provider.</param>
    /// <returns>A new parser for this group only.</returns>
    public static SimpleParser CreateSimpleParser<TCommand>(this UnitContext context, SimpleParserOptions? parserOptions = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return GetRegistry(context).CreateParser(context.GetCommandTypes(typeof(TCommand)), WithServiceProvider(context, parserOptions));
    }

    /// <summary>
    /// Creates an independent parser for Arc.Unit's separate subcommand list using the shared registry.
    /// </summary>
    /// <param name="context">The built unit context.</param>
    /// <param name="parserOptions">Parser options. ServiceProvider defaults to the unit's provider.</param>
    /// <returns>A new parser.</returns>
    public static SimpleParser CreateSimpleSubcommandParser(this UnitContext context, SimpleParserOptions? parserOptions = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        return GetRegistry(context).CreateParser(context.Subcommands, WithServiceProvider(context, parserOptions));
    }

    private static SimpleCommandGroupBuilder GetConfigurationGroup(IUnitConfigurationContext context, bool subcommands)
    {
        ArgumentNullException.ThrowIfNull(context);
        var configuration = context.GetCustomContext<SimpleCommandConfiguration>();
        _ = configuration.Builder;
        return new SimpleCommandGroupBuilder(configuration, subcommands ? context.GetSubcommandGroup() : context.GetCommandGroup());
    }

    private static SimpleCommandRegistry GetRegistry(UnitContext context)
        => context.ServiceProvider.GetService<SimpleCommandRegistry>()
            ?? throw new InvalidOperationException("No SimpleCommandLine registry is available. Use the generic context.AddCommand/AddSubcommand methods during UnitBuilder.Configure.");

    private static SimpleParserOptions WithServiceProvider(UnitContext context, SimpleParserOptions? options)
    {
        options ??= SimpleParserOptions.Standard;
        return options with { ServiceProvider = options.ServiceProvider ?? context.ServiceProvider };
    }
}

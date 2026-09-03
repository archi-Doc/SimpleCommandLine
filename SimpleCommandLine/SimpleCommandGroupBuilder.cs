// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCommandLine;

/// <summary>
/// Registers commands in an Arc.Unit command group and in the unit's shared NativeAOT metadata registry.
/// Obtain a group using <see cref="UnitCommandExtensions.GetSimpleCommandGroup{TCommand}"/>.
/// </summary>
public sealed class SimpleCommandGroupBuilder
{
    internal SimpleCommandGroupBuilder(SimpleCommandConfiguration configuration, CommandGroup group)
    {
        this.configuration = configuration;
        this.group = group;
    }

    /// <summary>
    /// Registers a command without options in this group and preserves its metadata.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="lifetime">The DI service lifetime, used if the service is not already registered.</param>
    /// <returns>True if the command was newly added to this group; false if it already belonged to the group.</returns>
    public bool AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ISimpleCommand
    {
        this.configuration.Builder.AddCommand<TCommand>();
        return this.AddCommandType(typeof(TCommand), lifetime);
    }

    /// <summary>
    /// Registers a command and its root options metadata. Nested options types must be registered separately.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TOptions">The root options type.</typeparam>
    /// <param name="lifetime">The DI service lifetime, used if the service is not already registered.</param>
    /// <returns>True if the command was newly added to this group; false if it already belonged to the group.</returns>
    public bool AddCommand<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ISimpleCommand<TOptions>
        where TOptions : new()
    {
        this.configuration.Builder.AddCommand<TCommand, TOptions>();
        return this.AddCommandType(typeof(TCommand), lifetime);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "Arc.Unit 0.46 CommandGroup.AddCommand stores the type and registers a DI ServiceDescriptor. Public constructors are preserved; command execution uses registered typed delegates.")]
    private bool AddCommandType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type, ServiceLifetime lifetime)
        => this.group.AddCommand(type, lifetime);

    private readonly SimpleCommandConfiguration configuration;
    private readonly CommandGroup group;
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCommandLine;

/// <summary>
/// Registers a group's commands with Arc.Unit, dependency injection, and the shared parser registry.
/// </summary>
/// <remarks>Obtain this builder with <see cref="UnitCommandExtensions.GetSimpleCommandGroup{TCommand}"/> and finish registration during unit configuration.</remarks>
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
    /// <returns><see langword="true"/> if newly added to this group; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Registration is finalized, or the command has conflicting options metadata.</exception>
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
    /// <returns><see langword="true"/> if newly added to this group; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException">Registration is finalized, or the command has conflicting options metadata.</exception>
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

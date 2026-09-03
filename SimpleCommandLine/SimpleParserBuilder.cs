// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SimpleCommandLine;

/// <summary>
/// Registers commands and preserves their options metadata for trimming and NativeAOT.
/// </summary>
/// <remarks>
/// Register nested types with <see cref="AddOptions{TOptions}"/>. Each <see cref="Build(SimpleParserOptions)"/> uses a snapshot;
/// later registrations do not change existing parsers. Configure a builder from one thread at a time.
/// </remarks>
public sealed class SimpleParserBuilder
{
    /// <summary>
    /// Registers a command without options.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The command is already registered with an options type.</exception>
    public SimpleParserBuilder AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>()
        where TCommand : ISimpleCommand
    {
        this.AddRegistration(new SimpleCommandRegistration(
            typeof(TCommand),
            null,
            typeof(ISimpleCommand),
            static (command, options, args, cancellationToken) => ((ISimpleCommand)command).Execute(args, cancellationToken)));
        return this;
    }

    /// <summary>
    /// Registers a command and its root options type. Nested options types require separate registration.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TOptions">The root options type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The command is already registered with a different options type or without options.</exception>
    public SimpleParserBuilder AddCommand<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>()
        where TCommand : ISimpleCommand<TOptions>
        where TOptions : new()
    {
        this.AddOptions<TOptions>();
        this.AddRegistration(new SimpleCommandRegistration(
            typeof(TCommand),
            typeof(TOptions),
            typeof(ISimpleCommand<>),
            static (command, options, args, cancellationToken) => ((ISimpleCommand<TOptions>)command).Execute((TOptions)options!, args, cancellationToken)));
        return this;
    }

    /// <summary>
    /// Preserves an options type, including its inherited and non-public members.
    /// Call this for every nested options type, including types used only by another options type.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <returns>This builder.</returns>
    public SimpleParserBuilder AddOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>()
    {
        this.AddOptionType(typeof(TOptions));
        return this;
    }

    /// <summary>
    /// Creates an independent parser from the registrations in insertion order.
    /// </summary>
    /// <param name="parserOptions">The parser options, or null for the defaults.</param>
    /// <returns>A new parser.</returns>
    /// <exception cref="InvalidOperationException">A registration is invalid or a nested options type is missing.</exception>
    public SimpleParser Build(SimpleParserOptions? parserOptions = null)
        => this.Build(parserOptions, this.commands.Keys);

    /// <summary>
    /// Parses options without a command. The root type is registered automatically; register nested types first.
    /// </summary>
    /// <typeparam name="TOptions">The root options type.</typeparam>
    /// <param name="commandLine">The command line.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="instanceToUpdate">An existing instance to update, or null to create one.</param>
    /// <returns><see langword="true"/> if an instance is available and required values are supplied; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Uses <see cref="SimpleParserOptions.Standard"/>. Unknown names and invalid optional values are ignored.
    /// Unspecified values on an existing instance are retained, but required values must be supplied on each call.
    /// An existing instance may be partially updated on failure.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The options type is invalid or a nested type is not registered.</exception>
    public bool TryParseOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(
        string commandLine, [MaybeNullWhen(false)] out TOptions options, TOptions? instanceToUpdate = default)
    {
        this.AddOptions<TOptions>();
        return SimpleParser.TryParseOptionsCore(commandLine, out options, instanceToUpdate, CreateResolver(this.optionTypes));
    }

    /// <summary>
    /// Parses pre-split arguments without joining or unquoting their values. The root type is registered automatically.
    /// </summary>
    /// <typeparam name="TOptions">The root options type.</typeparam>
    /// <param name="args">The arguments, with one value per array element.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="instanceToUpdate">An existing instance to update, or null to create one.</param>
    /// <returns><see langword="true"/> if an instance is available and required values are supplied; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// Uses <see cref="SimpleParserOptions.Standard"/>. Register nested types first. Unknown names and invalid optional values are ignored.
    /// Required values must be supplied on each call; an existing instance may be partially updated on failure.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The options type is invalid or a nested type is not registered.</exception>
    public bool TryParseOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(
        string[] args, [MaybeNullWhen(false)] out TOptions options, TOptions? instanceToUpdate = default)
    {
        this.AddOptions<TOptions>();
        return SimpleParser.TryParseOptionsCore(args, out options, instanceToUpdate, CreateResolver(this.optionTypes), false);
    }

    internal SimpleParser Build(SimpleParserOptions? parserOptions, IEnumerable<Type> commandTypes)
        => this.CreateRegistry().CreateParser(commandTypes, parserOptions);

    internal SimpleCommandRegistry CreateRegistry() => new(this.commands, this.optionTypes);

    private static Func<Type, PreservedType> CreateResolver(Dictionary<Type, PreservedType> types)
        => type => types.TryGetValue(type, out var preserved)
            ? preserved
            : throw new InvalidOperationException($"Options type '{type}' is not registered. Call SimpleParserBuilder.AddOptions<{type.Name}>() before building the parser.");

    private void AddOptionType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        if (type == typeof(object) || !this.optionTypes.TryAdd(type, new PreservedType(type)))
        {
            return;
        }

        if (type.BaseType is { } baseType)
        {
            this.AddOptionType(baseType);
        }
    }

    private void AddRegistration(SimpleCommandRegistration registration)
    {
        if (!this.commands.TryAdd(registration.CommandType, registration) &&
            this.commands[registration.CommandType].OptionType != registration.OptionType)
        {
            throw new InvalidOperationException($"Command type '{registration.CommandType}' is already registered with a different options type.");
        }
    }

    private readonly Dictionary<Type, SimpleCommandRegistration> commands = new();
    private readonly Dictionary<Type, PreservedType> optionTypes = new();
}

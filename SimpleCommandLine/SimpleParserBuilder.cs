// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SimpleCommandLine;

/// <summary>
/// Registers the command and option types whose reflection metadata must be preserved for trimming and NativeAOT.
/// Register every nested options type with <see cref="AddOptions{TOptions}"/> before building a parser.
/// </summary>
public sealed class SimpleParserBuilder
{
    /// <summary>
    /// Registers a command without options.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <returns>This builder.</returns>
    public SimpleParserBuilder AddCommand<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand>()
        where TCommand : ISimpleCommand
    {
        this.commands.TryAdd(typeof(TCommand), new SimpleCommandRegistration(
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
    public SimpleParserBuilder AddCommand<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>()
        where TCommand : ISimpleCommand<TOptions>
        where TOptions : new()
    {
        this.AddOptions<TOptions>();
        this.commands.TryAdd(typeof(TCommand), new SimpleCommandRegistration(
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
    /// Creates a parser from a snapshot of the registrations.
    /// </summary>
    /// <param name="parserOptions">The parser options, or null for the defaults.</param>
    /// <returns>A new parser.</returns>
    public SimpleParser Build(SimpleParserOptions? parserOptions = null)
        => this.Build(parserOptions, this.commands.Keys);

    /// <summary>
    /// Parses options without a command. The root type is registered automatically; register nested types first.
    /// </summary>
    /// <typeparam name="TOptions">The root options type.</typeparam>
    /// <param name="commandLine">The command line.</param>
    /// <param name="options">The parsed options.</param>
    /// <param name="instanceToUpdate">An existing instance to update, or null to create one.</param>
    /// <returns>True if options are created and required values are present. As with SimpleParser.TryParseOptions, invalid optional values are ignored.</returns>
    public bool TryParseOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(
        string commandLine, [MaybeNullWhen(false)] out TOptions options, TOptions? instanceToUpdate = default)
    {
        this.AddOptions<TOptions>();
        return SimpleParser.TryParseOptionsCore(commandLine, out options, instanceToUpdate, CreateResolver(this.optionTypes));
    }

    internal SimpleParser Build(SimpleParserOptions? parserOptions, IEnumerable<Type> commandTypes)
    {
        var registrations = commandTypes.Select(type => this.commands.TryGetValue(type, out var registration)
            ? registration
            : throw new InvalidOperationException($"Command type '{type}' is not registered. Call SimpleParserBuilder.AddCommand first.")).ToArray();
        return new SimpleParser(registrations, parserOptions, CreateResolver(new Dictionary<Type, PreservedType>(this.optionTypes)));
    }

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

    private readonly Dictionary<Type, SimpleCommandRegistration> commands = new();
    private readonly Dictionary<Type, PreservedType> optionTypes = new();
}

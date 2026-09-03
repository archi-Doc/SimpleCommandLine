// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace SimpleCommandLine;

/// <summary>
/// Stores immutable command registrations and preserved options metadata for creating parsers.
/// </summary>
/// <remarks>Arc.Unit registers one instance per unit. Parsers have separate parse state; their command instances may be shared by DI.</remarks>
public sealed class SimpleCommandRegistry
{
    internal SimpleCommandRegistry(Dictionary<Type, SimpleCommandRegistration> commands, Dictionary<Type, PreservedType> optionTypes)
    {
        this.commands = commands.ToFrozenDictionary();
        this.optionTypes = optionTypes.ToFrozenDictionary();
    }

    /// <summary>
    /// Creates a parser for the specified registered commands, preserving their order.
    /// </summary>
    /// <param name="commandTypes">The command types, usually supplied by Arc.Unit's command lists.</param>
    /// <param name="parserOptions">The parser options, or null for <see cref="SimpleParserOptions.Standard"/>. Supply a service provider when needed.</param>
    /// <returns>A new parser with independent options and parse state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="commandTypes"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A type is not registered, or the selected commands or options are invalid.</exception>
    public SimpleParser CreateParser(IEnumerable<Type> commandTypes, SimpleParserOptions? parserOptions = null)
    {
        ArgumentNullException.ThrowIfNull(commandTypes);
        var registrations = commandTypes.Select(type => this.commands.TryGetValue(type, out var registration)
            ? registration
            : throw new InvalidOperationException($"Command type '{type}' is not registered. Use SimpleParserBuilder.AddCommand or the generic IUnitConfigurationContext.AddCommand/AddSubcommand methods during configuration.")).ToArray();
        return new SimpleParser(registrations, parserOptions, this.ResolveType);
    }

    private PreservedType ResolveType(Type type)
        => this.optionTypes.TryGetValue(type, out var preserved)
            ? preserved
            : throw new InvalidOperationException($"Options type '{type}' is not registered. Call SimpleParserBuilder.AddOptions<{type.Name}>() or IUnitConfigurationContext.AddOptionType<{type.Name}>() during configuration.");

    private readonly FrozenDictionary<Type, SimpleCommandRegistration> commands;
    private readonly FrozenDictionary<Type, PreservedType> optionTypes;
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace SimpleCommandLine;

/// <summary>
/// The part of <see cref="SimpleParser"/> that <see cref="SimpleParser.OptionClass"/> and <see cref="SimpleParser.Option"/> depend on,
/// so that options can also be parsed without a parser instance (see <see cref="SimpleParser.TryParseOptions{TOptions}(string, out TOptions, TOptions)"/>).
/// </summary>
internal interface ISimpleParser
{
    /// <summary>
    /// Resolves an options type with preserved reflection metadata.
    /// </summary>
    /// <param name="type">The options type.</param>
    /// <returns>The preserved type.</returns>
    PreservedType ResolveType(System.Type type);

    /// <summary>
    /// Adds an error message to be displayed in a help message.
    /// </summary>
    /// <param name="message">The error message.</param>
    void AddErrorMessage(string message);

    /// <summary>
    /// Registers a nested options class so that its options are described once at the end of a help message.
    /// </summary>
    /// <param name="optionClass">The nested options class.</param>
    void AddOptionClassUsage(SimpleParser.OptionClass optionClass);

    /// <summary>
    /// Gets the parser options.
    /// </summary>
    SimpleParserOptions ParserOptions { get; }
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Names and configures a class implementing <see cref="ISimpleCommand"/> or <see cref="ISimpleCommand{TOptions}"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SimpleCommandAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the command (case insensitive).
    /// </summary>
    public string CommandName { get; }

    /// <summary>
    /// Gets or sets an alternate name for the command [the default is <see cref="string.Empty"/>: no alias].
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this command is a default candidate. The default is false.
    /// </summary>
    /// <remarks>An empty name also marks a candidate. The first candidate wins; otherwise, the first command is used. Strict command names disable the default.</remarks>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the description shown in a help message.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this command forwards unknown options and help to its own parser. The default is false.
    /// </summary>
    public bool IsSubcommand { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCommandAttribute"/> class.
    /// </summary>
    /// <param name="commandName">The command name, trimmed of surrounding whitespace. An empty name makes it a default candidate.</param>
    public SimpleCommandAttribute(string commandName)
    {
        this.CommandName = commandName.Trim();
    }
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Marks a class as a command and specifies its name and other properties.<br/>
/// The class must implement <see cref="ISimpleCommand"/> or <see cref="ISimpleCommand{TOptions}"/>.
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
    /// Gets or sets a value indicating whether this command is executed when the command name
    /// is not specified [the default is <see langword="false"/>].<br/>
    /// An empty command name implies <see langword="true"/>. If no command declares it, the first registered command becomes the default.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Gets or sets the description shown in a help message.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this command is a subcommand [the default is <see langword="false"/>].<br/>
    /// A subcommand accepts unknown option names and forwards them, so that it can dispatch them to its own parser.
    /// </summary>
    public bool IsSubcommand { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCommandAttribute"/> class.
    /// </summary>
    /// <param name="commandName">The name of the command. An empty name makes it the default command.</param>
    public SimpleCommandAttribute(string commandName)
    {
        this.CommandName = commandName.Trim();
    }
}

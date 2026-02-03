// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Specifies the command name and other properties of the simple command.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SimpleCommandAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    public string CommandName { get; }

    /// <summary>
    /// Gets or sets an alternate name for the command.
    /// </summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether this command will be executed if the command name is not specified.
    /// </summary>
    public bool Default { get; set; }

    /// <summary>
    /// Gets or sets the description of the command.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the command is a subcommand or not (subcommand accepts unknown option names).
    /// </summary>
    public bool IsSubcommand { get; set; } = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCommandAttribute"/> class.
    /// </summary>
    /// <param name="commandName">The name of the command.</param>
    public SimpleCommandAttribute(string commandName)
    {
        this.CommandName = commandName.Trim();
    }
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Specifies the long/short option name and other properties.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class SimpleOptionAttribute : Attribute
{
    /// <summary>
    /// Gets the long option name.
    /// </summary>
    public string LongName { get; }

    /// <summary>
    /// Gets or sets the short option name. Set <see langword="null"/> if you don't want to use short name.
    /// </summary>
    public string? ShortName { get; set; }

    /// <summary>
    /// Gets or sets the description of the command-line option.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the default value text of the command-line option.
    /// </summary>
    public string? DefaultValueText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this command-line option is required [the default is false].
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether or not to read from the environment variable if the option is not set [the default is false].
    /// </summary>
    public bool GetEnvironmentVariable { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleOptionAttribute"/> class.
    /// </summary>
    /// <param name="longName">The long command-line name.</param>
    public SimpleOptionAttribute(string longName)
    {
        if (string.IsNullOrWhiteSpace(longName))
        {
            throw new ArgumentNullException(nameof(longName));
        }

        this.LongName = longName;
    }
}

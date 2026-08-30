// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Marks a field or property of an options class as a command-line option
/// and specifies its name and other properties.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public class SimpleOptionAttribute : Attribute
{
    /// <summary>
    /// Gets the long option name, specified as <c>-name</c> on the command line (case insensitive).
    /// </summary>
    public string LongName { get; }

    /// <summary>
    /// Gets or sets the short option name [the default is <see langword="null"/>: no short name].<br/>
    /// The long name takes precedence when both resolve to different options.
    /// </summary>
    public string? ShortName { get; set; }

    /// <summary>
    /// Gets or sets the description shown in a help message.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text shown as the default value in a help message
    /// [the default is <see langword="null"/>: the actual value of a new instance is shown].
    /// </summary>
    public string? DefaultValueText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a value is required for this option [the default is <see langword="false"/>].<br/>
    /// The option name may be omitted unless <see cref="SimpleParserOptions.OmitOptionNamesForRequiredOptions"/> is disabled.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value is read from the environment variable
    /// named after <see cref="ShortName"/> or <see cref="LongName"/> when the option is not specified
    /// [the default is <see langword="false"/>].
    /// </summary>
    public bool ReadFromEnvironment { get; set; }

    /// <summary>
    /// Gets or sets how the argument is normalized
    /// [the default is <see cref="ArgumentProcessing.ReplaceNewlinesWithSpace"/>].
    /// </summary>
    public ArgumentProcessing ArgumentProcessing { get; set; } = ArgumentProcessing.ReplaceNewlinesWithSpace;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleOptionAttribute"/> class.
    /// </summary>
    /// <param name="longName">The long option name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> is empty or whitespace.</exception>
    public SimpleOptionAttribute(string longName)
    {
        if (string.IsNullOrWhiteSpace(longName))
        {
            throw new ArgumentNullException(nameof(longName));
        }

        this.LongName = longName;
    }
}

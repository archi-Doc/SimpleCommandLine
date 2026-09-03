// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Maps an instance field or property to a named command-line option.
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
    /// Gets or sets help-only default text, or null to use the member's default value for optional options.
    /// </summary>
    /// <remarks>Does not set the parsed value. Required options display this text as a hint.</remarks>
    public string? DefaultValueText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a value is required for this option [the default is <see langword="false"/>].<br/>
    /// The option name may be omitted unless <see cref="SimpleParserOptions.OmitOptionNamesForRequiredOptions"/> is disabled.
    /// </summary>
    /// <remarks>A default member value does not satisfy this requirement; supply a value in the input or environment.</remarks>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value is read from the environment variable
    /// named after <see cref="ShortName"/> or <see cref="LongName"/> when the option is not specified
    /// [the default is <see langword="false"/>].
    /// </summary>
    public bool ReadFromEnvironment { get; set; }

    /// <summary>
    /// Gets or sets raw-value normalization; defaults to <see cref="ArgumentProcessing.ReplaceNewlinesWithSpace"/>.
    /// </summary>
    /// <remarks>Pre-split array values are kept verbatim. Nested expressions are processed by their own options parser.</remarks>
    public ArgumentProcessing ArgumentProcessing { get; set; } = ArgumentProcessing.ReplaceNewlinesWithSpace;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleOptionAttribute"/> class.
    /// </summary>
    /// <param name="longName">The long option name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="longName"/> is null, empty, or whitespace.</exception>
    public SimpleOptionAttribute(string longName)
    {
        if (string.IsNullOrWhiteSpace(longName))
        {
            throw new ArgumentNullException(nameof(longName));
        }

        this.LongName = longName;
    }
}

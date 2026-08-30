// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

public record SimpleParserOptions
{
    public static SimpleParserOptions Standard { get; } = new SimpleParserOptions();

    /// <summary>
    /// Gets the parser option which requires to specify the command name (no default command).
    /// </summary>
    public static SimpleParserOptions StrictCommandName { get; } = Standard with { RequireStrictCommandName = true };

    /// <summary>
    /// Gets the parser option which requires the strict option name (unregistered options will result in an error).
    /// </summary>
    public static SimpleParserOptions StrictOptionName { get; } = Standard with { RequireStrictOptionName = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleParserOptions"/> class.
    /// </summary>
    protected internal SimpleParserOptions()
    {
    }

    /// <summary>
    /// Gets an <seealso cref="IServiceProvider"/> that is used to create command instances.
    /// </summary>
    public IServiceProvider? ServiceProvider { get; init; }

    /// <summary>
    /// Gets a value indicating whether or not to require to specify the command name (no default command).
    /// </summary>
    public bool RequireStrictCommandName { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether or not to requires the strict option name (unregistered options will result in an error).
    /// </summary>
    public bool RequireStrictOptionName { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether or not to display the usage text in a help message.
    /// </summary>
    public bool DoNotDisplayUsage { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether or not to display a list of commands as help.
    /// </summary>
    public bool DisplayCommandListAsHelp { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether or not to omit specifying option names for required options.
    /// </summary>
    public bool OmitOptionNamesForRequiredOptions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether or not to to automatically create an alias from the command name.<br/>
    /// The alias will consist of the initials of the words separated by hyphens (for example, 'remove-file' becomes 'rf').
    /// </summary>
    public bool AutoAlias { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether or not to read the command name from the environment variable <see cref="SimpleParser.CommandString"/>.
    /// </summary>
    public bool ReadCommandFromEnvironment { get; init; } = true;

    /// <summary>
    /// Gets the argument delimiter string used to separate arguments.<br/>
    /// The default value is <see cref="SimpleParser.TripleQuotes"/>.
    /// </summary>
    public string ArgumentDelimiter
    {
        get => this.argumentDelimiter;
        init
        {
            this.argumentDelimiter = value ?? string.Empty;

            // int.MaxValue disables the delimiter check (the length comparison never succeeds).
            this.twoDelimitersLength = this.argumentDelimiter.Length == 0 ? int.MaxValue : this.argumentDelimiter.Length * 2;
        }
    }

    /// <summary>
    /// Gets a value indicating whether messages should not be written to the console.
    /// </summary>
    public bool SuppressConsoleOutput { get; init; } = false;

    /// <summary>
    /// Gets twice the length of <see cref="ArgumentDelimiter"/> (the minimum length of an enclosed argument).
    /// </summary>
    internal int TwoDelimitersLength => this.twoDelimitersLength;

    private string argumentDelimiter = SimpleParser.TripleQuotes;
    private int twoDelimitersLength = SimpleParser.TripleQuotes.Length * 2;
}

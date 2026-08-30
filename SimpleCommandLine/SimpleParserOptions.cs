// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Options that control how <see cref="SimpleParser"/> parses arguments and displays messages.<br/>
/// Create a variant with a <c>with</c> expression (for example <c>SimpleParserOptions.Standard with { AutoAlias = true, }</c>).
/// </summary>
public record SimpleParserOptions
{
    /// <summary>
    /// Gets the default options.
    /// </summary>
    public static SimpleParserOptions Standard { get; } = new SimpleParserOptions();

    /// <summary>
    /// Gets the options which require the command name to be specified (no default command).
    /// </summary>
    public static SimpleParserOptions StrictCommandName { get; } = Standard with { RequireStrictCommandName = true };

    /// <summary>
    /// Gets the options which require a valid option name (an unregistered option results in an error).
    /// </summary>
    public static SimpleParserOptions StrictOptionName { get; } = Standard with { RequireStrictOptionName = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleParserOptions"/> class.
    /// </summary>
    protected internal SimpleParserOptions()
    {
    }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> used to resolve command instances.<br/>
    /// When <see langword="null"/>, each command type is instantiated with its parameterless constructor.
    /// </summary>
    public IServiceProvider? ServiceProvider { get; init; }

    /// <summary>
    /// Gets a value indicating whether the command name is required (no default command) [the default is <see langword="false"/>].
    /// </summary>
    public bool RequireStrictCommandName { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether an unregistered option name results in an error [the default is <see langword="false"/>].
    /// </summary>
    public bool RequireStrictOptionName { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether the usage text is omitted from a help message [the default is <see langword="false"/>].
    /// </summary>
    public bool DoNotDisplayUsage { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether help displays a single-line list of command names
    /// instead of the detailed description of each command [the default is <see langword="false"/>].
    /// </summary>
    public bool DisplayCommandListAsHelp { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether the name of a required option may be omitted [the default is <see langword="true"/>].<br/>
    /// A value without an option name is assigned to the first required option that is not set yet.
    /// </summary>
    public bool OmitOptionNamesForRequiredOptions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether an alias is created automatically from the command name [the default is <see langword="false"/>].<br/>
    /// The alias consists of the initials of the words separated by hyphens (for example, 'remove-file' becomes 'rf').
    /// </summary>
    public bool AutoAlias { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether the command name is read from the environment variable
    /// <see cref="SimpleParser.CommandString"/> when it is not specified in the arguments [the default is <see langword="true"/>].
    /// </summary>
    public bool ReadCommandFromEnvironment { get; init; } = true;

    /// <summary>
    /// Gets the delimiter that encloses an argument containing spaces or newlines (for example, <c>"""a b"""</c>).<br/>
    /// The default value is a triple quote. An empty string disables the delimiter.
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
    /// Gets a value indicating whether help, version and error messages are not written
    /// to the console [the default is <see langword="false"/>].
    /// </summary>
    public bool SuppressConsoleOutput { get; init; } = false;

    /// <summary>
    /// Gets twice the length of <see cref="ArgumentDelimiter"/> (the minimum length of an enclosed argument).
    /// </summary>
    internal int TwoDelimitersLength => this.twoDelimitersLength;

    private string argumentDelimiter = SimpleParser.TripleQuotes;
    private int twoDelimitersLength = SimpleParser.TripleQuotes.Length * 2;
}

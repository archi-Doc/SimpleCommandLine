// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;

namespace SimpleCommandLine;

/// <summary>
/// Configures argument parsing, command resolution, and parser output.
/// </summary>
/// <remarks>Create a variant with <c>SimpleParserOptions.Standard with { GenerateAliases = true }</c>.</remarks>
public record SimpleParserOptions
{
    /// <summary>
    /// Gets the default options.
    /// </summary>
    public static SimpleParserOptions Standard { get; } = new SimpleParserOptions();

    /// <summary>
    /// Gets the standard options with <see cref="RequireCommandName"/> enabled (no default command).
    /// </summary>
    public static SimpleParserOptions CommandNameRequired { get; } = Standard with { RequireCommandName = true };

    /// <summary>
    /// Gets the standard options with <see cref="RejectUnknownOptionNames"/> enabled (an unregistered option results in an error).
    /// </summary>
    public static SimpleParserOptions UnknownOptionNamesRejected { get; } = Standard with { RejectUnknownOptionNames = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleParserOptions"/> class.
    /// </summary>
    protected internal SimpleParserOptions()
    {
    }

    /// <summary>
    /// Gets the provider for command instances and <see cref="Arc.Unit.IConsoleService"/>, or null to use constructors and standard output.
    /// </summary>
    public IServiceProvider? ServiceProvider { get; init; }

    /// <summary>
    /// Gets a value indicating whether the command name is required (no default command) [the default is <see langword="false"/>].
    /// </summary>
    public bool RequireCommandName { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether unknown option names cause errors, except for command groups (<see cref="SimpleCommandAttribute.IsCommandGroup"/>). The default is false.
    /// </summary>
    public bool RejectUnknownOptionNames { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether the usage text is displayed in a help message [the default is <see langword="true"/>].
    /// </summary>
    public bool DisplayUsage { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether help for all commands uses a single-line name list. The default is false.
    /// </summary>
    public bool DisplayCommandListAsHelp { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether the name of a required option may be omitted [the default is <see langword="true"/>].<br/>
    /// A value without an option name is assigned to the first required option that is not set yet.
    /// </summary>
    public bool AllowPositionalRequiredOptions { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether an alias is created automatically from the command name [the default is <see langword="false"/>].<br/>
    /// Uses hyphen-separated initials, such as <c>remove-file</c> to <c>rf</c>; conflicting names or aliases are skipped.
    /// </summary>
    public bool GenerateAliases { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether the command name is read from the environment variable
    /// <see cref="SimpleParser.CommandEnvironmentVariableName"/> when no command, help, or version request is recognized. The default is true.
    /// </summary>
    public bool ReadCommandFromEnvironment { get; init; } = true;

    /// <summary>
    /// Gets the delimiter that encloses an argument containing spaces or newlines (for example, <c>"""a b"""</c>).<br/>
    /// Defaults to triple quotes. An empty string disables this delimiter; single and double quotes remain active.
    /// </summary>
    /// <remarks>Use a delimiter without whitespace. It takes precedence over commas, pipes, and braces when it matches.</remarks>
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
    /// Gets a value indicating whether parser output, including console-service output, is suppressed. The default is false.
    /// </summary>
    /// <remarks>Skips help/version/list formatting and help-only instance creation. Command execution and command output are unaffected.</remarks>
    public bool SuppressConsoleOutput { get; init; } = false;

    /// <summary>
    /// Gets twice the length of <see cref="ArgumentDelimiter"/> (the minimum length of an enclosed argument).
    /// </summary>
    internal int TwoDelimitersLength => this.twoDelimitersLength;

    private string argumentDelimiter = SimpleParser.TripleQuotes;
    private int twoDelimitersLength = SimpleParser.TripleQuotes.Length * 2;
}

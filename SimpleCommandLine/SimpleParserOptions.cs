﻿// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

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
}

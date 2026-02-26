// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace SimpleCommandLine;

/// <summary>
/// Specifies how to normalize a string argument.
/// </summary>
public enum ArgumentProcessing
{
    /// <summary>
    /// Unwrap, then replace newline characters with spaces.
    /// </summary>
    UnwrapAndReplaceNewlinesWithSpace = 0,

    /// <summary>
    /// Leave the input as-is (no unwrapping and no newline handling).
    /// </summary>
    AsIs = 1,

    /// <summary>
    /// Unwrap only: if the string starts and ends with the argument delimiters, <br/>
    /// remove those delimiters; otherwise leave the input unchanged.
    /// </summary>
    UnwrapOnly = 2,

    /// <summary>
    /// Unwrap, then remove newline characters from the result.
    /// </summary>
    UnwrapAndRemoveNewlines = 3,
}

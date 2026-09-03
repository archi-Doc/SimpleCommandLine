// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace SimpleCommandLine;

/// <summary>
/// Specifies how newlines and escape sequences in a raw command-line argument are handled.<br/>
/// The surrounding delimiter or quotes are always removed, whichever value is used.
/// Pre-split string-array values are kept verbatim and do not use this normalization.
/// </summary>
public enum ArgumentProcessing
{
    /// <summary>
    /// Remove '\r', replace '\n' with a space, and unescape <c>\'</c> and <c>\"</c>.
    /// </summary>
    ReplaceNewlinesWithSpace = 0,

    /// <summary>
    /// Keep newlines and escape sequences as they are.
    /// </summary>
    AsIs = 1,

    /// <summary>
    /// Remove '\r' and '\n', and unescape <c>\'</c> and <c>\"</c>.
    /// </summary>
    RemoveNewlines = 3,
}

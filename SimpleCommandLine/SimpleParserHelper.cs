// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Arc;

#pragma warning disable SA1124 // Do not use regions

namespace SimpleCommandLine;

/// <summary>
/// Helper methods for handling command lines and arguments.
/// </summary>
public static class SimpleParserHelper
{
    #region FieldAndProperty

    private const int InitialStackSize = 32;
    private const int NestingStackSize = 16;
    private const int DefaultArgumentCapacity = 16;
    private const int SeparatorMark = -1; // Marks a range as the separator string.

    private static readonly IFormatProvider DefautFormatProvider = CultureInfo.InvariantCulture;
    private static readonly object BoxedTrue = true;
    private static readonly object BoxedFalse = false;
    private static string? commandlineArguments;

    /// <summary>
    /// Gets the converters used to parse an argument into a primitive type.<br/>
    /// A converter returns <see langword="null"/> when the argument cannot be converted (no exception is thrown).
    /// </summary>
    internal static Dictionary<Type, Func<ReadOnlySpan<char>, object?>> TypeConverters { get; } = new(16)
    {
        { typeof(bool), static x => bool.TryParse(x, out var v) ? (v ? BoxedTrue : BoxedFalse) : null },
        { typeof(sbyte), static x => sbyte.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(byte), static x => byte.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(short), static x => short.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(ushort), static x => ushort.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(int), static x => int.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(uint), static x => uint.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(long), static x => long.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(ulong), static x => ulong.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(float), static x => float.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(double), static x => double.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(decimal), static x => decimal.TryParse(x, DefautFormatProvider, out var v) ? v : null },
        { typeof(char), static x => x.Length > 0 ? x[0] : null },
    };

    #endregion

    /// <summary>
    /// Joins a collection of strings with space separators.
    /// </summary>
    /// <param name="values">The collection of strings to join.</param>
    /// <returns>A single string containing all values joined with spaces.</returns>
    public static string JoinWithSpace(this IEnumerable<string> values)
    {
        return string.Join(' ', values);
    }

    /// <summary>
    /// Trims whitespace and removes the surrounding braces or quotes from the input string.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The trimmed string, unwrapped if it is enclosed in braces or in unescaped quotes.</returns>
    public static string TrimQuotesAndBraces(this string input)
        => TrimQuotesAndBraces(input.AsSpan()).ToString();

    /// <summary>
    /// Trims whitespace and removes the surrounding braces or quotes from the input span.
    /// </summary>
    /// <param name="input">The input span.</param>
    /// <returns>The trimmed span, unwrapped if it is enclosed in braces or in unescaped quotes.</returns>
    public static ReadOnlySpan<char> TrimQuotesAndBraces(this ReadOnlySpan<char> input)
    {
        var span = input.Trim();
        if (span.Length < 2)
        {
            return span;
        }

        if (span[0] == SimpleParser.OpenBrace && span[^1] == SimpleParser.CloseBrace)
        {// {A B}
            return span.Slice(1, span.Length - 2).Trim(); // Removes spaces again to avoid misdetection as indentation.
        }

        return TrimQuotes(span);
    }

    /// <summary>
    /// Trims whitespace and removes the surrounding triple quotes, double quotes or single quotes from the input string.<br/>
    /// The quotes are kept if the text contains an unescaped quote of the same kind.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The trimmed and unquoted string.</returns>
    public static string TrimQuotes(this string input)
        => TrimQuotes(input.AsSpan()).ToString();

    /// <summary>
    /// Trims whitespace and removes the surrounding triple quotes, double quotes or single quotes from the input span.<br/>
    /// The quotes are kept if the text contains an unescaped quote of the same kind.
    /// </summary>
    /// <param name="input">The input span.</param>
    /// <returns>The trimmed and unquoted span.</returns>
    public static ReadOnlySpan<char> TrimQuotes(this ReadOnlySpan<char> input)
    {
        var span = input.Trim();
        if (span.Length < 2)
        {
            return span;
        }

        if (span.Length >= 6 && span.StartsWith(SimpleParser.TripleQuotes) && span.EndsWith(SimpleParser.TripleQuotes))
        {
            return span.Slice(3, span.Length - 6);
        }

        if (span[0] == SimpleParser.Quote && span[^1] == SimpleParser.Quote)
        {// "A B"
            for (var i = 1; i < span.Length - 1; i++)
            {// Check escaped quote
                if (span[i] == SimpleParser.Quote && span[i - 1] != '\\')
                {
                    return span;
                }
            }

            return span.Slice(1, span.Length - 2).Trim(); // Removes spaces again to avoid misdetection as indentation.
        }
        else if (span[0] == SimpleParser.SingleQuote && span[^1] == SimpleParser.SingleQuote)
        {// 'A B'
            for (var i = 1; i < span.Length - 1; i++)
            {// Check escaped quote
                if (span[i] == SimpleParser.SingleQuote && span[i - 1] != '\\')
                {
                    return span;
                }
            }

            return span.Slice(1, span.Length - 2).Trim(); // Removes spaces again to avoid misdetection as indentation.
        }

        return span;
    }

    /// <summary>
    /// Removes the surrounding double quotes from the text (whitespace is not trimmed).
    /// </summary>
    /// <param name="text">The text to unwrap.</param>
    /// <returns>The unwrapped text, the original text if it is not double-quoted, or <see langword="null"/> if the input is <see langword="null"/>.</returns>
    public static string? UnwrapDoubleQuote(string? text)
    {
        if (text is null)
        {
            return null;
        }

        if (text.Length >= 2 && text[0] == SimpleParser.Quote && text[^1] == SimpleParser.Quote)
        {
            return text[1..^1];
        }
        else
        {
            return text;
        }
    }

    /// <summary>
    /// Gets the leading command name of a command line without parsing it.
    /// </summary>
    /// <param name="commandLine">The command line.</param>
    /// <returns>The first word of the command line, or <see cref="string.Empty"/> if it is blank or starts with an option.</returns>
    public static string PeekCommand(ReadOnlySpan<char> commandLine)
    {
        if (commandLine.Length == 0)
        {
            return string.Empty;
        }

        var span = commandLine;
        var start = 0;
        var end = 0;
        while (span.Length > start && char.IsWhiteSpace(span[start]))
        {// Skip space
            start++;
        }

        if (start >= span.Length ||
            span[start] == SimpleParser.OptionPrefix)
        {
            return string.Empty;
        }

        // start < span.Length;
        end = start + 1; // end <= span.Length
        while (span.Length > end && !char.IsWhiteSpace(span[end]))
        {// Skip non-space
            end++;
        }

        return span[start..end].ToString();
    }

    /// <summary>
    /// Gets the arguments of the current process (the executable path is removed).<br/>
    /// The result is cached after the first call.
    /// </summary>
    /// <returns>The arguments, or <see cref="string.Empty"/> if there is none.</returns>
    public static string GetCommandLineArguments()
    {
        return commandlineArguments is not null ?
            commandlineArguments :
            (commandlineArguments = ExtractArguments(Environment.CommandLine));
    }

    /// <summary>
    /// Removes the leading executable path (quoted or not) from a command line.
    /// </summary>
    /// <param name="commandLine">The command line (<see cref="Environment.CommandLine"/> style).</param>
    /// <returns>The arguments, or <see cref="string.Empty"/> if there is none.</returns>
    public static string ExtractArguments(string commandLine)
    {
        if (commandLine.Length == 0)
        {
            return string.Empty;
        }

        if (commandLine[0] != '"')
        {// Path arguments
            var firstSpace = commandLine.IndexOf(' ');
            if (firstSpace < 0)
            {// Path
                return string.Empty;
            }
            else
            {// arguments
                return commandLine.Substring(firstSpace + 1).Trim();
            }
        }

        var quotePosition = commandLine.IndexOf('"', 1);
        if (quotePosition < 0)
        {// "Path
            return string.Empty;
        }

        return commandLine.Substring(quotePosition + 1).Trim();
    }

    /// <summary>
    /// Creates an alias from a command name by concatenating the initials of the hyphen-separated words<br/>
    /// (for example, 'remove-file' becomes 'rf').
    /// </summary>
    /// <param name="commandName">The command name.</param>
    /// <returns>The alias.</returns>
    public static string CreateAliasFromCommand(string commandName)
    {
        var span = commandName.AsSpan();
        if (span.IsEmpty)
        {
            return string.Empty;
        }

        char[]? rented = null;
        var destination = span.Length <= InitialStackSize ?
            stackalloc char[InitialStackSize] :
            (rented = ArrayPool<char>.Shared.Rent(span.Length)).AsSpan();

        var count = 0;
        while (!span.IsEmpty)
        {// Split by '-', trim and pick the first character of each word.
            ReadOnlySpan<char> word;
            var index = span.IndexOf('-');
            if (index < 0)
            {
                word = span;
                span = default;
            }
            else
            {
                word = span.Slice(0, index);
                span = span.Slice(index + 1);
            }

            word = word.Trim();
            if (!word.IsEmpty)
            {
                destination[count++] = word[0];
            }
        }

        var alias = count == 0 ? string.Empty : new string(destination.Slice(0, count));
        if (rented is not null)
        {
            ArrayPool<char>.Shared.Return(rented);
        }

        return alias;
    }

    /// <summary>
    /// Appends the value of the specified environment variable to the arguments.<br/>
    /// The arguments are left unchanged if the variable is not set.
    /// </summary>
    /// <param name="args">The arguments to append to.</param>
    /// <param name="variableName">The name of the environment variable.</param>
    /// <returns>The value of the environment variable, or <see cref="string.Empty"/> if it is not set.</returns>
    public static string AppendEnvironmentVariable(ref string[] args, string variableName)
    {
        try
        {
            var v = Environment.GetEnvironmentVariable(variableName);
            if (v != null)
            {
                Array.Resize(ref args, args.Length + 1);
                args[args.Length - 1] = v;
                return v;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    /// <summary>
    /// Appends the value of the specified environment variable to the command line, separated by a space.<br/>
    /// The command line is left unchanged if the variable is not set.
    /// </summary>
    /// <param name="args">The command line to append to.</param>
    /// <param name="variableName">The name of the environment variable.</param>
    /// <returns>The value of the environment variable, or <see cref="string.Empty"/> if it is not set.</returns>
    public static string AppendEnvironmentVariable(ref string args, string variableName)
    {
        try
        {
            var v = Environment.GetEnvironmentVariable(variableName);
            if (v != null)
            {
                args += " " + v;
                return v;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    /// <summary>
    /// Gets the value of the specified option from an array of arguments.<br/>
    /// The name/value pair is removed from the array when it is found.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="optionName">The option name, without the leading '-' (case insensitive).</param>
    /// <param name="value">When this method returns, contains the value of the option; otherwise, <see cref="string.Empty"/>.</param>
    /// <returns><see langword="true"/> if the option and its value are found.</returns>
    public static bool TryGetAndRemoveArgument(ref string[] args, string optionName, out string value)
    {
        value = string.Empty;
        var nameSpan = optionName.AsSpan();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == SimpleParser.CommandSeparatorString)
            {
                break;
            }

            if (!arg.IsOptionName())
            {
                continue;
            }

            if (arg.AsSpan().Trim(SimpleParser.OptionPrefix).Equals(nameSpan, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || args[i + 1] == SimpleParser.CommandSeparatorString)
                {// No value
                    return false;
                }
                else if (args[i + 1].IsOptionName())
                {// -argument
                    continue;
                }

                value = args[i + 1];
                var remaining = new string[args.Length - 2];
                args.AsSpan(0, i).CopyTo(remaining);
                args.AsSpan(i + 2).CopyTo(remaining.AsSpan(i));
                args = remaining;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes the surrounding braces from the input string.
    /// </summary>
    /// <param name="text">The input string.</param>
    /// <returns>The string without the surrounding braces, or the original string if it is not enclosed in braces.</returns>
    public static string UnwrapBraces(this string text)
    {
        if (text.Length >= 2 && text.StartsWith(SimpleParser.OpenBrace) && text.EndsWith(SimpleParser.CloseBrace))
        {
            return text.Substring(1, text.Length - 2);
        }

        return text;
    }

    /// <summary>
    /// Splits the input string at whitespace, discarding empty entries.<br/>
    /// Quotes and braces are not taken into account (use <see cref="SplitArguments"/> to split a command line).
    /// </summary>
    /// <param name="text">The input string.</param>
    /// <returns>An array of the separated strings.</returns>
    public static string[] SplitAtSpace(this string text) => text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Determines whether the text is an option name (starts with '-').<br/>
    /// A negative number such as "-1" or "-.5" is a value, not an option name.
    /// </summary>
    /// <param name="text">The text to examine.</param>
    /// <returns><see langword="true"/> if the text is an option name.</returns>
    public static bool IsOptionName(this ReadOnlySpan<char> text)
    {
        if (text.IsEmpty || text[0] != SimpleParser.OptionPrefix)
        {
            return false;
        }

        if (text.Length == 1)
        {// "-"
            return true;
        }

        var c = text[1];
        return !char.IsAsciiDigit(c) && c != '.';
    }

    /// <summary>
    /// Splits a command line at the separator <see cref="SimpleParser.CommandSeparator"/> ('|') into individual command lines.<br/>
    /// A separator inside quotes or braces is not treated as a separator.
    /// </summary>
    /// <param name="commandLine">The command line.</param>
    /// <param name="delimiter">The argument delimiter (<see cref="SimpleParser.DefaultArgumentDelimiter"/> if empty).</param>
    /// <returns>An array of command lines.</returns>
    public static string[] SplitCommandLines(this string commandLine, ReadOnlySpan<char> delimiter = default)
    {
        var args = commandLine.SplitArguments(delimiter);
        StringBuilder? sb = default;
        List<string> list = new();

        foreach (var x in args)
        {
            if (x == SimpleParser.CommandSeparatorString)
            {
                if (sb is null)
                {
                    list.Add(string.Empty);
                }
                else
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb ??= new();
                if (sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(x);
            }
        }

        if (sb is not null)
        {
            list.Add(sb.ToString());
        }

        return list.ToArray();
    }

    /// <summary>
    /// Normalizes an argument: removes the surrounding delimiter or quotes, unescapes <c>\'</c> and <c>\"</c>,
    /// and handles newlines according to <paramref name="argumentProcessing"/>.
    /// </summary>
    /// <param name="argument">The argument.</param>
    /// <param name="parserOptions">The parser options which provide the argument delimiter.</param>
    /// <param name="argumentProcessing">Specifies how newlines are handled.</param>
    /// <returns>The normalized argument (the original instance if nothing has changed).</returns>
    public static string ProcessArgument(string argument, SimpleParserOptions parserOptions, ArgumentProcessing argumentProcessing)
    {
        var span = argument.AsSpan();

        // Unwrap ', ", """
        if (span.Length >= parserOptions.TwoDelimitersLength && span.StartsWith(parserOptions.ArgumentDelimiter) && span.EndsWith(parserOptions.ArgumentDelimiter))
        {
            var length = parserOptions.ArgumentDelimiter.Length;
            span = span.Slice(length, span.Length - length - length);
        }
        else if (span.Length >= 2 && span[0] == SimpleParser.Quote && span[^1] == SimpleParser.Quote)
        {
            span = span.Slice(1, span.Length - 2);
        }
        else if (span.Length >= 2 && span[0] == SimpleParser.SingleQuote && span[^1] == SimpleParser.SingleQuote)
        {
            span = span.Slice(1, span.Length - 2);
        }

        if (argumentProcessing == ArgumentProcessing.RemoveNewlines)
        {
            var subtraction = 0;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == '\r' || span[i] == '\n')
                {
                    subtraction++;
                }
                else if (span[i] == '\\' && i + 1 < span.Length &&
                    (span[i + 1] == '\'' || span[i + 1] == '\"'))
                {// Unescape \' \"
                    i++;
                    subtraction++;
                }
            }

            if (subtraction == 0)
            {
                goto Exit;
            }

            var resultLength = span.Length - subtraction;
            return string.Create(resultLength, span, static (dest, src) =>
            {
                var position = 0;
                for (var i = 0; i < src.Length; i++)
                {
                    if (src[i] == '\r' || src[i] == '\n')
                    {
                    }
                    else if (src[i] == '\\' && i + 1 < src.Length &&
                    (src[i + 1] == '\'' || src[i + 1] == '\"'))
                    {// Unescape \' \"
                        dest[position++] = src[i + 1];
                        i++;
                    }
                    else
                    {
                        dest[position++] = src[i];
                    }
                }
            });
        }
        else if (argumentProcessing == ArgumentProcessing.ReplaceNewlinesWithSpace)
        {
            var subtraction = 0;
            var replacement = false;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] == '\r')
                {// Remove \r
                    subtraction++;
                }
                else if (span[i] == '\n')
                {// \n -> Space (the length does not change)
                    replacement = true;
                }
                else if (span[i] == '\\' && i + 1 < span.Length &&
                    (span[i + 1] == '\'' || span[i + 1] == '\"'))
                {// Unescape \' \"
                    i++;
                    subtraction++;
                }
            }

            if (subtraction == 0 && !replacement)
            {
                goto Exit;
            }

            var resultLength = span.Length - subtraction;
            return string.Create(resultLength, span, static (dest, src) =>
            {
                var position = 0;
                for (var i = 0; i < src.Length; i++)
                {
                    if (src[i] == '\r')
                    {// Remove \r
                    }
                    else if (src[i] == '\n')
                    {// \n -> Space
                        dest[position++] = ' ';
                    }
                    else if (src[i] == '\\' && i + 1 < src.Length &&
                    (src[i + 1] == '\'' || src[i + 1] == '\"'))
                    {// Unescape \' \"
                        dest[position++] = src[i + 1];
                        i++;
                    }
                    else
                    {
                        dest[position++] = src[i];
                    }
                }
            });
        }

Exit:
// If the value has changed, create a new string; otherwise, return the original string.
        if (span.Length == argument.Length)
        {
            return argument;
        }
        else
        {
            return span.ToString();
        }
    }

    /// <summary>
    /// Splits a command line into arguments, honoring quotes ('), double quotes ("), the argument delimiter (""") and brackets ({}).
    /// </summary>
    /// <param name="commandLine">The command line.</param>
    /// <param name="delimiter">The argument delimiter (<see cref="SimpleParser.DefaultArgumentDelimiter"/> if empty).</param>
    /// <returns>An array of arguments.</returns>
    public static string[] SplitArguments(this ReadOnlySpan<char> commandLine, ReadOnlySpan<char> delimiter = default)
        => SplitArgumentsCore(commandLine, delimiter.IsEmpty ? SimpleParser.DefaultArgumentDelimiter : delimiter);

    internal static string[] SplitParserArguments(string commandLine, SimpleParserOptions parserOptions)
        => SplitArgumentsCore(commandLine, parserOptions.ArgumentDelimiter);

    private static string[] SplitArgumentsCore(ReadOnlySpan<char> commandLine, ReadOnlySpan<char> delimiter)
    {
        if (commandLine.IsEmpty)
        {
            return [];
        }

        var ranges = new RangeList(stackalloc int[DefaultArgumentCapacity * 2]);
        var start = 0;
        var position = 0;
        var nextPosition = 0;
        var enclosed = new CharStack(stackalloc char[NestingStackSize]);

        while (position < commandLine.Length)
        {
            var currentChar = commandLine[position];
            var lastChar = position > 0 ? commandLine[position - 1] : (char)0;
            if (enclosed.Count == 0)
            {
                if (char.IsWhiteSpace(currentChar))
                {// A B
                    nextPosition = position + 1;
                    goto AddString;
                }
                else if (currentChar == SimpleParser.CommandSeparator ||
                    currentChar == SimpleParser.ArgumentSeparator)
                {// A|B
                    nextPosition = position;
                    goto AddString;
                }
                else if (!delimiter.IsEmpty && commandLine.Slice(position).StartsWith(delimiter))
                {// Delimiter """A B"""
                    enclosed.Push(SimpleParser.DelimiterChar);
                    nextPosition = position + delimiter.Length;
                    goto AddString;
                }
                else if (currentChar == SimpleParser.OpenBrace ||
                    (currentChar == SimpleParser.Quote && lastChar != '\\') ||
                    (currentChar == SimpleParser.SingleQuote && lastChar != '\\'))
                {// { or " (not \") or ' (not \')
                    enclosed.Push(currentChar);
                    nextPosition = position + 1;
                    goto AddString;
                }
                else if (currentChar == SimpleParser.CloseBrace)
                {// }
                    nextPosition = position + 1;
                    goto AddString;
                }
            }
            else
            {
                var peek = enclosed.Peek();

                if (!delimiter.IsEmpty && (peek == SimpleParser.OpenBrace || peek == SimpleParser.DelimiterChar) &&
                    commandLine.Slice(position).StartsWith(delimiter))
                {// """
                    if (peek == SimpleParser.DelimiterChar)
                    {// """abc"""
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            nextPosition = position + delimiter.Length;
                            position = nextPosition;
                            goto AddString;
                        }
                    }
                    else
                    {// { """A
                        enclosed.Push(SimpleParser.DelimiterChar);
                    }

                    position += delimiter.Length;
                    continue;
                }
                else if (currentChar == SimpleParser.Quote && lastChar != '\\')
                {// " (not \")
                    if (peek == SimpleParser.Quote)
                    {// "-arg {-test "A"} "
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            nextPosition = ++position;
                            goto AddString;
                        }
                    }
                    else if (peek == SimpleParser.OpenBrace)
                    {
                        enclosed.Push(currentChar);
                    }
                }
                else if (currentChar == SimpleParser.SingleQuote && lastChar != '\\')
                {// ' (not \')
                    if (peek == SimpleParser.SingleQuote)
                    {// '-arg {-test "A"} '
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            nextPosition = ++position;
                            goto AddString;
                        }
                    }
                    else if (peek == SimpleParser.OpenBrace)
                    {
                        enclosed.Push(currentChar);
                    }
                }
                else if (currentChar == SimpleParser.CloseBrace)
                {// }
                    if (peek == SimpleParser.OpenBrace)
                    {// {-test "A"}
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            nextPosition = ++position;
                            goto AddString;
                        }
                    }
                }
                else if (currentChar == SimpleParser.OpenBrace)
                {
                    if (peek == SimpleParser.OpenBrace)
                    {
                        enclosed.Push(currentChar);
                    }
                }
            }

            position++;
            continue;

AddString:
            if (start < position)
            {
                AddTrimmed(ref ranges, commandLine, start, position);
            }

            if (currentChar == SimpleParser.CommandSeparator)
            {
                ranges.Add(0, SeparatorMark);
                position++;
                nextPosition++;
            }
            else if (currentChar == SimpleParser.ArgumentSeparator)
            {
                position++;
                nextPosition++;
            }

            start = position;
            position = nextPosition;
        }

        if (start < position && position <= commandLine.Length)
        {
            AddTrimmed(ref ranges, commandLine, start, position);
        }

        // Materialize the arguments (the exact size is known, so no intermediate list is needed).
        if (ranges.Count == 0)
        {
            return [];
        }

        var result = new string[ranges.Count];
        for (var i = 0; i < result.Length; i++)
        {
            var (rangeStart, rangeLength) = ranges.Get(i);
            result[i] = rangeLength == SeparatorMark ?
                SimpleParser.CommandSeparatorString :
                commandLine.Slice(rangeStart, rangeLength).ToString();
        }

        return result;

        static void AddTrimmed(ref RangeList ranges, ReadOnlySpan<char> commandLine, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(commandLine[start]))
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(commandLine[end - 1]))
            {
                end--;
            }

            if (start < end)
            {
                ranges.Add(start, end - start);
            }
        }
    }

    /// <summary>
    /// A list of (start, length) ranges backed by a stack-allocated buffer.<br/>
    /// It grows on the heap when the number of arguments exceeds the initial capacity.
    /// </summary>
    private ref struct RangeList
    {
        public RangeList(Span<int> initialBuffer)
        {
            this.buffer = initialBuffer;
        }

        /// <summary>
        /// Gets the number of ranges.
        /// </summary>
        public readonly int Count => this.count >> 1;

        public readonly (int Start, int Length) Get(int index) => (this.buffer[index << 1], this.buffer[(index << 1) + 1]);

        public void Add(int start, int length)
        {
            if (this.count == this.buffer.Length)
            {
                this.Grow();
            }

            this.buffer[this.count++] = start;
            this.buffer[this.count++] = length;
        }

        private void Grow()
        {
            var array = new int[this.buffer.Length * 2];
            this.buffer.CopyTo(array);
            this.buffer = array;
        }

        private Span<int> buffer;
        private int count;
    }

    /// <summary>
    /// A stack of characters backed by a stack-allocated buffer.<br/>
    /// It grows on the heap in the (pathological) case where the nesting is deeper than the initial buffer.
    /// </summary>
    private ref struct CharStack
    {
        public CharStack(Span<char> initialBuffer)
        {
            this.buffer = initialBuffer;
        }

        public readonly int Count => this.count;

        public readonly char Peek() => this.buffer[this.count - 1];

        public void Push(char c)
        {
            if (this.count == this.buffer.Length)
            {
                this.Grow();
            }

            this.buffer[this.count++] = c;
        }

        public void Pop() => this.count--;

        private void Grow()
        {
            var array = new char[this.buffer.Length * 2];
            this.buffer.CopyTo(array);
            this.buffer = array;
        }

        private Span<char> buffer;
        private int count;
    }
}

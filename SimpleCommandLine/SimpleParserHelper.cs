// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Arc;

#pragma warning disable SA1124 // Do not use regions

namespace SimpleCommandLine;

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
    /// Removes surrounding quotes or brackets from the input string.
    /// </summary>
    /// <param name="input">The input string to trim quotes or brackets from.</param>
    /// <returns>
    /// The input string without surrounding quotes or brackets, or the original string if no unescaped surrounding quotes or brackets are found.
    /// </returns>
    public static string TrimQuotesAndBracket(this string input)
        => TrimQuotesAndBracket(input.AsSpan()).ToString();

    /// <summary>
    /// Removes surrounding quotes or brackets from the input <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    /// <param name="input">The input span to trim quotes or brackets from.</param>
    /// <returns>
    /// The input span without surrounding quotes or brackets, or the original span if no unescaped surrounding quotes or brackets are found.
    /// </returns>
    public static ReadOnlySpan<char> TrimQuotesAndBracket(this ReadOnlySpan<char> input)
    {
        var span = input.Trim();
        if (span.Length < 2)
        {
            return span;
        }

        if (span[0] == SimpleParser.OpenBracket && span[^1] == SimpleParser.CloseBracket)
        {// {A B}
            return span.Slice(1, span.Length - 2).Trim(); // Removes spaces again to avoid misdetection as indentation.
        }

        return TrimQuotes(span);
    }

    /// <summary>
    /// Removes surrounding double or single quotes from the input string.
    /// </summary>
    /// <param name="input">The input string to trim quotes from.</param>
    /// <returns>
    /// The input string without surrounding quotes, or the original string if no unescaped surrounding quotes are found.
    /// </returns>
    public static string TrimQuotes(this string input)
        => TrimQuotes(input.AsSpan()).ToString();

    /// <summary>
    /// Removes surrounding double or single quotes from the input <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    /// <param name="input">The input span to trim quotes from.</param>
    /// <returns>
    /// The input span without surrounding quotes, or the original span if no unescaped surrounding quotes are found.
    /// </returns>
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
    /// Tries to unwrap a double-quoted text by removing the surrounding quotes.
    /// </summary>
    /// <param name="text">The text to unwrap.</param>
    /// <returns>The unwrapped text, or null if the input text is null.</returns>
    public static string? TryUnwrapDoubleQuote(string? text)
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

    public static string PeekCommand(ReadOnlySpan<char> commandline)
    {
        if (commandline.Length == 0)
        {
            return string.Empty;
        }

        var span = commandline;
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

    public static string GetCommandLineArguments()
    {
        return commandlineArguments is not null ?
            commandlineArguments :
            (commandlineArguments = ParseArguments(Environment.CommandLine));
    }

    public static string ParseArguments(string commandLine)
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
    /// <param name="command">The command name.</param>
    /// <returns>The alias.</returns>
    public static string CreateAliasFromCommand(string command)
    {
        var span = command.AsSpan();
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
    /// Adds the specified environment variable to the arguments.<br/>
    /// The return value is the environment variable.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="variable">The name of the environment variable.</param>
    /// <returns>The environment variable.</returns>
    public static string AddEnvironmentVariable(ref string[] args, string variable)
    {
        try
        {
            var v = Environment.GetEnvironmentVariable(variable);
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
    /// Adds the specified environment variable to the arguments.<br/>
    /// The return value is the environment variable.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="variable">The name of the environment variable.</param>
    /// <returns>The environment variable.</returns>
    public static string AddEnvironmentVariable(ref string args, string variable)
    {
        try
        {
            var v = Environment.GetEnvironmentVariable(variable);
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
    /// Get the value of a specified argument from an array of arguments.<br/>
    /// The corresponding name/value is removed from the array.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> if found.</returns>
    public static bool TryGetAndRemoveArgument(ref string[] args, string name, out string value)
    {
        value = string.Empty;
        var nameSpan = name.AsSpan();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith(SimpleParser.OptionPrefix))
            {
                continue;
            }

            if (arg.AsSpan(1).Equals(nameSpan, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {// No value
                    return false;
                }
                else if (args[i + 1].StartsWith(SimpleParser.OptionPrefix))
                {// -argument
                    continue;
                }

                value = args[i + 1];
                for (var j = i; j < args.Length; j++)
                {
                    if (j + 2 < args.Length)
                    {
                        args[j] = args[j + 2];
                    }
                }

                Array.Resize(ref args, args.Length - 2);
                return true;
            }
        }

        return false;
    }

    public static string UnwrapBracket(this string text)
    {
        if (text.Length >= 2 && text.StartsWith(SimpleParser.OpenBracket) && text.EndsWith(SimpleParser.CloseBracket))
        {
            return text.Substring(1, text.Length - 2);
        }

        return text;
    }

    public static string[] SplitAtSpace(this string text) => text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Determines whether the text is an option name (starts with '-').<br/>
    /// A negative number such as "-1" or "-.5" is a value, not an option name.
    /// </summary>
    /// <param name="text">The text to examine.</param>
    /// <returns><see langword="true"/> if the text is an option name.</returns>
    public static bool IsOptionString(this ReadOnlySpan<char> text)
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

    public static string[] SeparateArguments(this string arg, ReadOnlySpan<char> delimiter = default)
    {
        var args = arg.FormatArguments(delimiter);
        StringBuilder? sb = default;
        List<string> list = new();

        foreach (var x in args)
        {
            if (x == SimpleParser.SeparatorString)
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

    public static string ProcessArgument(string arg, SimpleParserOptions parserOptions, ArgumentProcessing argumentProcessing)
    {
        var span = arg.AsSpan();

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
        if (span.Length == arg.Length)
        {
            return arg;
        }
        else
        {
            return span.ToString();
        }
    }

    /// <summary>
    /// Splits a command line into arguments, honoring quotes ('), double quotes ("), the argument delimiter (""") and brackets ({}).
    /// </summary>
    /// <param name="span">The command line.</param>
    /// <param name="delimiter">The argument delimiter (<see cref="SimpleParser.DefaultDelimiter"/> if empty).</param>
    /// <returns>An array of arguments.</returns>
    public static string[] FormatArguments(this ReadOnlySpan<char> span, ReadOnlySpan<char> delimiter = default)
    {
        if (span.IsEmpty)
        {
            return [];
        }

        if (delimiter.IsEmpty)
        {
            delimiter = SimpleParser.DefaultDelimiter;
        }

        var ranges = new RangeList(stackalloc int[DefaultArgumentCapacity * 2]);
        var start = 0;
        var position = 0;
        var nextPosition = 0;
        var enclosed = new CharStack(stackalloc char[NestingStackSize]);

        while (position < span.Length)
        {
            var currentChar = span[position];
            var lastChar = position > 0 ? span[position - 1] : (char)0;
            if (enclosed.Count == 0)
            {
                if (char.IsWhiteSpace(currentChar))
                {// A B
                    nextPosition = position + 1;
                    goto AddString;
                }
                else if (currentChar == SimpleParser.Separator ||
                    currentChar == SimpleParser.Separator2)
                {// A|B
                    nextPosition = position;
                    goto AddString;
                }
                else if (span.Slice(position).StartsWith(delimiter))
                {// Delimiter """A B"""
                    enclosed.Push(SimpleParser.DelimiterChar);
                    nextPosition = position + delimiter.Length;
                    goto AddString;
                }
                else if (currentChar == SimpleParser.OpenBracket ||
                    (currentChar == SimpleParser.Quote && lastChar != '\\') ||
                    (currentChar == SimpleParser.SingleQuote && lastChar != '\\'))
                {// { or " (not \") or ' (not \')
                    enclosed.Push(currentChar);
                    nextPosition = position + 1;
                    goto AddString;
                }
                else if (currentChar == SimpleParser.CloseBracket)
                {// }
                    nextPosition = position + 1;
                    goto AddString;
                }
            }
            else
            {
                var peek = enclosed.Peek();

                if (span.Slice(position).StartsWith(delimiter))
                {// """
                    if (peek == SimpleParser.DelimiterChar)
                    {// """abc"""
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            position += delimiter.Length;
                            nextPosition = position;
                            goto AddString;
                        }
                    }
                    else
                    {// { """A
                        enclosed.Push(currentChar);
                    }
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
                    else if (peek != SimpleParser.DelimiterChar)
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
                    else if (peek != SimpleParser.DelimiterChar)
                    {
                        enclosed.Push(currentChar);
                    }
                }
                else if (currentChar == SimpleParser.CloseBracket)
                {// }
                    if (peek == SimpleParser.OpenBracket)
                    {// {-test "A"}
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            nextPosition = ++position;
                            goto AddString;
                        }
                    }
                }
                else if (currentChar == SimpleParser.OpenBracket)
                {
                    if (peek == SimpleParser.OpenBracket)
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
                AddTrimmed(ref ranges, span, start, position);
            }

            if (currentChar == SimpleParser.Separator)
            {
                ranges.Add(0, SeparatorMark);
                position++;
                nextPosition++;
            }
            else if (currentChar == SimpleParser.Separator2)
            {
                position++;
                nextPosition++;
            }

            start = position;
            position = nextPosition;
        }

        if (start < position && position <= span.Length)
        {
            AddTrimmed(ref ranges, span, start, position);
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
                SimpleParser.SeparatorString :
                span.Slice(rangeStart, rangeLength).ToString();
        }

        return result;

        static void AddTrimmed(ref RangeList ranges, ReadOnlySpan<char> span, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(span[start]))
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(span[end - 1]))
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

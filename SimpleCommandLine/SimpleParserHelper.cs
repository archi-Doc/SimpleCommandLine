// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#pragma warning disable SA1124 // Do not use regions

namespace SimpleCommandLine;

public static class SimpleParserHelper
{
    #region FieldAndProperty

    private static readonly IFormatProvider DefautFormatProvider = CultureInfo.InvariantCulture;
    private static string? commandlineArguments;

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

        return input;
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

    public static string CreateAliasFromCommand(string command)
    {
        var words = command.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var alias = string.Empty;
        foreach (var x in words)
        {
            if (x.Length > 0)
            {
                alias += x[0];
            }
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

    public static bool IsOptionString(this ReadOnlySpan<char> text) => text.StartsWith(SimpleParser.OptionPrefix);

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

    public static string ProcessArgument(string input, ArgumentProcessing argumentProcessing)
    {
        return string.Empty;
    }

    public static string[] FormatArguments(this ReadOnlySpan<char> span, ReadOnlySpan<char> delimiter = default)
    {
        const char DelimiterChar = 'd';
        var list = new List<string>();

        var start = 0;
        var position = 0;
        var nextPosition = 0;
        var enclosed = new Stack<char>();
        if (delimiter.IsEmpty)
        {
            delimiter = SimpleParser.DefaultDelimiter;
        }

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
                else if (currentChar == SimpleParser.Separator)
                {// A|B
                    nextPosition = position;
                    goto AddString;
                }
                else if (span.Slice(position).StartsWith(delimiter))
                {// Delimiter """A B"""
                    enclosed.Push(DelimiterChar);
                    nextPosition = position + 3;
                    goto AddString;
                }
                else if (currentChar == SimpleParser.OpenBracket ||
                    (currentChar == SimpleParser.Quote && lastChar != '\\') ||
                    (currentChar == SimpleParser.SingleQuote && lastChar != '\\'))
                {// { or " (not \") or '" (not \')
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
                    if (peek == DelimiterChar)
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
                    else if (peek == DelimiterChar)
                    {
                    }
                    else
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
                    else if (peek == DelimiterChar)
                    {
                    }
                    else
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
            { // Add string
                var s = span[start..position].ToString().Trim();
                if (s.Length > 0)
                {
                    list.Add(s);
                }
            }

            if (currentChar == SimpleParser.Separator)
            {
                list.Add(SimpleParser.SeparatorString);
                position++;
                nextPosition++;
            }

            start = position;
            position = nextPosition;
        }

        if (start < position && position <= span.Length)
        { // Add string
            var s = span[start..position].ToString().Trim();
            if (s.Length > 0)
            {
                list.Add(s);
            }
        }

        return list.ToArray();
    }

    internal static void InitializeTypeConverter(ISimpleParser parser)
    {
        parser.TypeConverters.Add(typeof(bool), static x =>
        {
            var st = x.ToLower();
            if (st == "true")
            {
                return true;
            }
            else if (st == "false")
            {
                return false;
            }
            else
            {
                throw new InvalidOperationException();
            }
        });

        parser.TypeConverters.Add(typeof(string), x =>
        {
            var span = x.AsSpan();
            if (span.Length >= parser.ParserOptions.TwoDelimitersLength && span.StartsWith(parser.ParserOptions.ArgumentDelimiter) && span.EndsWith(parser.ParserOptions.ArgumentDelimiter))
            {
                var length = parser.ParserOptions.ArgumentDelimiter.Length;
                return x.Substring(length, span.Length - length - length);
            }
            else if (span.Length >= 2 && span[0] == SimpleParser.Quote && span[^1] == SimpleParser.Quote)
            {
                return x.Substring(1, span.Length - 2);
            }
            else if (span.Length >= 2 && span[0] == SimpleParser.SingleQuote && span[^1] == SimpleParser.SingleQuote)
            {
                return x.Substring(1, span.Length - 2);
            }

            return x;
        });

        parser.TypeConverters.Add(typeof(sbyte), static x => sbyte.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(byte), static x => byte.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(short), static x => short.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(ushort), static x => ushort.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(int), static x => int.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(uint), static x => uint.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(long), static x => long.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(ulong), static x => ulong.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(float), static x => float.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(double), static x => double.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(decimal), static x => decimal.Parse(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(char), static x => char.Parse(x));

        /*parser.TypeConverters.Add(typeof(sbyte), static x => Convert.ToSByte(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(byte), static x => Convert.ToByte(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(short), static x => Convert.ToInt16(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(ushort), static x => Convert.ToUInt16(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(int), static x => Convert.ToInt32(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(uint), static x => Convert.ToUInt32(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(long), static x => Convert.ToInt64(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(ulong), static x => Convert.ToUInt64(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(float), static x => Convert.ToSingle(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(double), static x => Convert.ToDouble(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(decimal), static x => Convert.ToDecimal(x, DefautFormatProvider));
        parser.TypeConverters.Add(typeof(char), static x => Convert.ToChar(x, DefautFormatProvider));*/
    }

    /*public static string[] FormatArguments(this string arg)
    {
        var span = arg.AsSpan();
        var list = new List<string>();

        var start = 0;
        var end = 0;
        var enclosed = new Stack<char>();
        var addStringIncrement = true;
        while (end < span.Length)
        {
            var c = span[end];
            var b = end > 0 ? span[end - 1] : (char)0;
            if (enclosed.Count == 0)
            {
                if (char.IsWhiteSpace(c))
                {
                    goto AddString;
                }
                else if ((c == '\"' && b != '\\') || c == SimpleParser.OpenBracket)
                {
                    enclosed.Push(c);
                    addStringIncrement = false;
                    goto AddString;
                }
                else if (c == SimpleParser.CloseBracket)
                {
                    goto AddString;
                }
            }
            else
            {
                if (c == '\"' && b != '\\')
                {
                    if (enclosed.Peek() == '\"')
                    {// "-arg {-test "A"} "
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            end++;
                            goto AddString;
                        }
                    }
                    else
                    {
                        enclosed.Push(c);
                    }
                }
                else if (c == SimpleParser.CloseBracket)
                {
                    if (enclosed.Peek() == SimpleParser.OpenBracket)
                    {// [-test "A"]
                        enclosed.Pop();
                        if (enclosed.Count == 0)
                        {
                            end++;
                            goto AddString;
                        }
                    }
                }
                else if (c == SimpleParser.OpenBracket)
                {
                    enclosed.Push(c);
                }
            }

            end++;
            continue;

AddString:
            if (start < end)
            { // Add string
                var s = span[start..end].ToString().Trim();
                if (s.Length > 0)
                {
                    list.Add(s);
                }
            }

            start = end + (addStringIncrement ? 1 : 0);
            end++;
        }

        if (start < end && end <= span.Length)
        { // Add string
            var s = span[start..end].ToString().Trim();
            if (s.Length > 0)
            {
                list.Add(s);
            }
        }

        return list.ToArray();
    }*/
}

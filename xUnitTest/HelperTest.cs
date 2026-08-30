// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Linq;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class HelperTest
{
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("{abc}", "abc")]
    [InlineData("  {a b}  ", "a b")]
    [InlineData("{a b", "{a b")]
    [InlineData("\"abc\"", "abc")]
    [InlineData("{}", "")]
    [InlineData("{", "{")]
    public void TrimQuotesAndBracketTest(string input, string expected)
        => SimpleParserHelper.TrimQuotesAndBraces(input).Is(expected);

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("{abc}", "abc")]
    [InlineData("{}", "")]
    [InlineData("{abc", "{abc")]
    [InlineData("abc}", "abc}")]
    [InlineData("", "")]
    public void UnwrapBracketTest(string input, string expected)
        => input.UnwrapBraces().Is(expected);

    [Theory]
    [InlineData("\"abc\"", "abc")]
    [InlineData("abc", "abc")]
    [InlineData("\"abc", "\"abc")]
    [InlineData("\"", "\"")]
    public void TryUnwrapDoubleQuoteTest(string input, string expected)
        => SimpleParserHelper.UnwrapDoubleQuote(input).Is(expected);

    [Fact]
    public void TryUnwrapDoubleQuoteNullTest()
        => SimpleParserHelper.UnwrapDoubleQuote(null).IsNull();

    [Fact]
    public void SplitAndJoinTest()
    {
        "a b  c".SplitAtSpace().SequenceEqual(["a", "b", "c"]).IsTrue();
        "  ".SplitAtSpace().SequenceEqual([]).IsTrue();
        new[] { "a", "b" }.JoinWithSpace().Is("a b");
        Array.Empty<string>().JoinWithSpace().Is(string.Empty);
    }

    [Fact]
    public void TryGetAndRemoveArgumentTest()
    {
        // The name/value pair is removed from the array.
        string[] args = ["-a", "1", "-b", "2"];
        SimpleParserHelper.TryGetAndRemoveArgument(ref args, "a", out var value).IsTrue();
        value.Is("1");
        args.SequenceEqual(["-b", "2"]).IsTrue();

        // The last pair.
        args = ["-b", "2", "-a", "1"];
        SimpleParserHelper.TryGetAndRemoveArgument(ref args, "A", out value).IsTrue(); // Case insensitive
        value.Is("1");
        args.SequenceEqual(["-b", "2"]).IsTrue();

        // Not found.
        args = ["-b", "2"];
        SimpleParserHelper.TryGetAndRemoveArgument(ref args, "a", out value).IsFalse();
        value.Is(string.Empty);
        args.SequenceEqual(["-b", "2"]).IsTrue();

        // No value.
        args = ["-b", "2", "-a"];
        SimpleParserHelper.TryGetAndRemoveArgument(ref args, "a", out value).IsFalse();

        // The next argument is an option.
        args = ["-a", "-b", "2"];
        SimpleParserHelper.TryGetAndRemoveArgument(ref args, "a", out value).IsFalse();
    }

    [Fact]
    public void AddEnvironmentVariableTest()
    {
        const string Name = "SimpleCommandLine_HelperTest";
        Environment.SetEnvironmentVariable(Name, "value");
        try
        {
            string[] args = ["-a"];
            SimpleParserHelper.AppendEnvironmentVariable(ref args, Name).Is("value");
            args.SequenceEqual(["-a", "value"]).IsTrue();

            var arg = "-a";
            SimpleParserHelper.AppendEnvironmentVariable(ref arg, Name).Is("value");
            arg.Is("-a value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, null);
        }

        // A missing environment variable leaves the arguments unchanged.
        string[] args2 = ["-a"];
        SimpleParserHelper.AppendEnvironmentVariable(ref args2, Name).Is(string.Empty);
        args2.SequenceEqual(["-a"]).IsTrue();
    }

    [Theory]
    // Unwrapping (applies to every ArgumentProcessing).
    [InlineData("abc", ArgumentProcessing.AsIs, "abc")]
    [InlineData("\"a b\"", ArgumentProcessing.AsIs, "a b")]
    [InlineData("'a b'", ArgumentProcessing.AsIs, "a b")]
    [InlineData("\"\"\"a b\"\"\"", ArgumentProcessing.AsIs, "a b")]
    // AsIs keeps newlines and escape sequences.
    [InlineData("a\r\nb", ArgumentProcessing.AsIs, "a\r\nb")]
    [InlineData("a\\\"b", ArgumentProcessing.AsIs, "a\\\"b")]
    // ReplaceNewlinesWithSpace: \r is removed, \n becomes a space.
    [InlineData("a\r\nb", ArgumentProcessing.ReplaceNewlinesWithSpace, "a b")]
    [InlineData("a\nb", ArgumentProcessing.ReplaceNewlinesWithSpace, "a b")]
    [InlineData("a\\\"b", ArgumentProcessing.ReplaceNewlinesWithSpace, "a\"b")]
    [InlineData("a\\'b", ArgumentProcessing.ReplaceNewlinesWithSpace, "a'b")]
    [InlineData("abc", ArgumentProcessing.ReplaceNewlinesWithSpace, "abc")]
    // RemoveNewlines: both \r and \n are removed.
    [InlineData("a\r\nb", ArgumentProcessing.RemoveNewlines, "ab")]
    [InlineData("a\nb", ArgumentProcessing.RemoveNewlines, "ab")]
    [InlineData("a\\\"b", ArgumentProcessing.RemoveNewlines, "a\"b")]
    [InlineData("abc", ArgumentProcessing.RemoveNewlines, "abc")]
    public void ProcessArgumentTest(string input, ArgumentProcessing processing, string expected)
        => SimpleParserHelper.ProcessArgument(input, SimpleParserOptions.Standard, processing).Is(expected);

    [Fact]
    public void SeparateArgumentsTest()
    {
        "a b".SplitCommandLines().SequenceEqual(["a b"]).IsTrue();
        "a | b | c".SplitCommandLines().SequenceEqual(["a", "b", "c"]).IsTrue();
        "|a".SplitCommandLines().SequenceEqual([string.Empty, "a"]).IsTrue();
    }

    [Fact]
    public void FormatArgumentsIsAllocationFriendlyTest()
    {
        // An empty command line returns an empty array (no allocation).
        SimpleParserHelper.SplitArguments(string.Empty).Length.Is(0);
        SimpleParserHelper.SplitArguments("   ").Length.Is(0);

        // Deeply nested brackets do not overflow the internal stack buffer.
        var nested = new string('{', 100) + "a" + new string('}', 100);
        var result = SimpleParserHelper.SplitArguments(nested);
        result.Length.Is(1);
        result[0].Is(nested);

        // Many arguments exceed the initial capacity of the internal range buffer.
        var many = string.Join(' ', Enumerable.Range(0, 100).Select(x => x.ToString()));
        SimpleParserHelper.SplitArguments(many).Length.Is(100);
    }
}

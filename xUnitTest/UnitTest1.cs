using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;
using Xunit;

namespace xUnitTest;

public class TestOptions
{
    [SimpleOption("A")]
    public int A { get; set; }

    [SimpleOption("B")]
    public int B { get; set; }
}

public class NameOptions
{
    /// <summary>
    /// Gets or sets the number. Its long name collides with the short name of <see cref="Value"/>.
    /// </summary>
    [SimpleOption("n", ShortName = "x")]
    public int N { get; set; }

    [SimpleOption("value", ShortName = "n")]
    public int Value { get; set; }

    [SimpleOption("nullable", ShortName = "u")]
    public int? Nullable { get; set; }

    [SimpleOption("day", ShortName = "d")]
    public DayOfWeek? Day { get; set; }
}

public class DelimiterOptions
{
    [SimpleOption("text")]
    public string Text { get; set; } = string.Empty;
}

[SimpleCommand("delimiter")]
public class DelimiterCommand : ISimpleCommand<DelimiterOptions>
{
    public static DelimiterOptions? Result { get; set; }

    public Task Execute(DelimiterOptions option, string[] args, CancellationToken cancellationToken)
    {
        Result = option;
        return Task.CompletedTask;
    }
}

public class UnitTest1
{
    private const string CommandSeparator = SimpleParser.CommandSeparatorString;

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("\"abc\"", "abc")]
    [InlineData("'abc'", "abc")]
    [InlineData("  \"abc\"  ", "abc")]
    [InlineData("  'abc'  ", "abc")]
    [InlineData("\"a\\\"bc\"", "a\\\"bc")] // Escaped quote inside, should not trim
    [InlineData("'a\\'bc'", "a\\'bc")]   // Escaped single quote inside, should not trim
    [InlineData("\"abc", "\"abc")]
    [InlineData("abc\"", "abc\"")]
    [InlineData("'abc", "'abc")]
    [InlineData("abc'", "abc'")]
    [InlineData("", "")]
    [InlineData(" ", "")]
    [InlineData("\"", "\"")]
    [InlineData("'", "'")]
    [InlineData("\"a\"b\"", "\"a\"b\"")]
    [InlineData("'a'b'", "'a'b'")]
    public void TrimQuotesTest(string input, string expected)
    {
        var result = SimpleParserHelper.TrimQuotes(input);
        result.Is(expected);
    }

    [Fact]
    public void SeparatorTest()
    {
        Test(string.Empty, []);
        Test("a | b", ["a", CommandSeparator, "b"]);
        Test("|a|b|", [CommandSeparator, "a", CommandSeparator, "b", CommandSeparator]);
        Test("ab | \"cd|ef\"|{gh|ij}||", ["ab", CommandSeparator, "\"cd|ef\"", CommandSeparator, "{gh|ij}", CommandSeparator, CommandSeparator]);

        TestOptions options;
        SimpleParser.TryParseOptions("", out options!).IsTrue();
        options.IsNotNull();
        options.A.Is(0);
        options.B.Is(0);

        SimpleParser.TryParseOptions("-A 1 | -B 2", out options!).IsTrue();
        options.IsNotNull();
        options.A.Is(1);
        options.B.Is(0);

        "".SplitCommandLines().SequenceEqual([]).IsTrue();
        "| ".SplitCommandLines().SequenceEqual([string.Empty]).IsTrue();
        "-A 1 | -B 2".SplitCommandLines().SequenceEqual(["-A 1", "-B 2"]).IsTrue();
    }

    [Fact]
    public void OptionNameTest()
    {
        // A short name may collide with the long name of another option (the long name takes precedence).
        NameOptions options;
        SimpleParser.TryParseOptions("-n 1 -x 2 -value 3", out options!).IsTrue();
        options.N.Is(2); // -x (short) and -n (long) both point to N.
        options.Value.Is(3);

        // Nullable value types.
        options.Nullable.IsNull();
        options.Day.IsNull();
        SimpleParser.TryParseOptions("-nullable 7 -d friday", out options!).IsTrue();
        options.Nullable.Is((int?)7);
        options.Day.Is((DayOfWeek?)DayOfWeek.Friday);

        // An invalid value leaves the default value (and does not throw).
        SimpleParser.TryParseOptions("-n abc", out options!).IsTrue();
        options.N.Is(0);
    }

    [Fact]
    public async Task ArgumentDelimiterTest()
    {
        var parserOptions = SimpleParserOptions.Standard with
        {
            ArgumentDelimiter = "#",
            SuppressConsoleOutput = true,
        };

        // The delimiter length is honored even when it differs from the default (""").
        await SimpleParser.ParseAndExecute([typeof(DelimiterCommand)], "delimiter -text #a b#", parserOptions, TestContext.Current.CancellationToken);
        DelimiterCommand.Result!.Text.Is("a b");
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("test", "t")]
    [InlineData("remove-file", "rf")]
    [InlineData("-remove--file-", "rf")]
    [InlineData("a-b-c-d", "abcd")]
    public void CreateAliasTest(string command, string expected)
        => SimpleParserHelper.CreateAliasFromCommand(command).Is(expected);

    [Fact]
    public void PeekCommandTest()
    {
        SimpleParserHelper.PeekCommand("").Is("");
        SimpleParserHelper.PeekCommand(" ").Is("");
        SimpleParserHelper.PeekCommand("cmd").Is("cmd");
        SimpleParserHelper.PeekCommand(" cmd  ").Is("cmd");
        SimpleParserHelper.PeekCommand("1").Is("1");
        SimpleParserHelper.PeekCommand("-option").Is("");
        SimpleParserHelper.PeekCommand("-option 123").Is("");
        SimpleParserHelper.PeekCommand("cmd -option 123").Is("cmd");
        SimpleParserHelper.PeekCommand(" cmd -option 123").Is("cmd");
    }

    [Fact]
    public void FormatTest()
    {
        Test(string.Empty, []);
        Test("test", ["test",]);
        Test("test -abc", ["test", "-abc",]);

        Test("  -n 99 ", ["-n", "99",]);
        Test("test -abc", ["test", "-abc",]);
        Test("  -test  1 23  ", ["-test", "1", "23",]);
        Test("-text \"abc\"", ["-text", "\"abc\"",]);

        Test(""" -text"a \"b c" """, ["-text", "\"a \\\"b c\""]);
        Test("-text \"a b c\" -options {} ", ["-text", "\"a b c\"", "-options", "{}",]);
        Test("-options {}} ", ["-options", "{}", "}",]);
        Test("-options {{} ", ["-options", "{{}",]);
        Test("-options {-z\"AA\"} ", ["-options", "{-z\"AA\"}",]);
        Test("-options {-z\"{A}B\"}", ["-options", "{-z\"{A}B\"}",]);
        Test("-ns {-node \"[3.18.216.240]:49152(1)\"}", ["-ns", "{-node \"[3.18.216.240]:49152(1)\"}"]);
        Test("-options {-text \"message\"} -string \"{options2}\"", ["-options", "{-text \"message\"}", "-string", "\"{options2}\"",]);

        Test("-options \"a}b\" ", ["-options", "\"a}b\"",]);
        Test("-options \"a{}b\" ", ["-options", "\"a{}b\"",]);
        Test("-options \"a{}{b\" ", ["-options", "\"a{}{b\"",]);
        Test("""" """a""" """", ["\"\"\"a\"\"\"",]);
        Test(""""-text """Triple quotes{}""" -options {} """", ["-text", "\"\"\"Triple quotes{}\"\"\"", "-options", "{}",]);
        Test("""""""-text """a""" """""" """Triple quotes{}""" """"""", ["-text", "\"\"\"a\"\"\"", "\"\"\"\"\"\"", "\"\"\"Triple quotes{}\"\"\"",]);
        // Test(""""-text """Triple quotes""" -options {} """");

        SimpleParserHelper.ExtractArguments("").Is("");
        SimpleParserHelper.ExtractArguments("A").Is("");
        SimpleParserHelper.ExtractArguments("\"").Is("");
        SimpleParserHelper.ExtractArguments("A\"").Is("");
        SimpleParserHelper.ExtractArguments("\"AB").Is("");
        SimpleParserHelper.ExtractArguments("\"AB\"").Is("");
        SimpleParserHelper.ExtractArguments("\"AB\"c").Is("c");
        SimpleParserHelper.ExtractArguments("\"AB\" c").Is("c");
        SimpleParserHelper.ExtractArguments("AB c").Is("c");
    }

    private void Test(string args, string[] test)
    {
        var result = SimpleParserHelper.SplitArguments(args);
        result.IsStructuralEqual(test);
    }
}

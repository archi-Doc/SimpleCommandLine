using System.Linq;
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

public class UnitTest1
{
    private const string Separator = SimpleParser.SeparatorString;

    [Fact]
    public void SeparatorTest()
    {
        Test(string.Empty, []);
        Test("a | b", ["a", Separator, "b"]);
        Test("|a|b|", [Separator, "a", Separator, "b", Separator]);
        Test("ab | \"cd|ef\"|{gh|ij}||", ["ab", Separator, "\"cd|ef\"", Separator, "{gh|ij}", Separator, Separator]);

        TestOptions options;
        SimpleParser.TryParseOptions("", out options!).IsTrue();
        options.IsNotNull();
        options.A.Is(0);
        options.B.Is(0);

        SimpleParser.TryParseOptions("-A 1 | -B 2", out options!).IsTrue();
        options.IsNotNull();
        options.A.Is(1);
        options.B.Is(0);

        "".SeparateArguments().SequenceEqual([]).IsTrue();
        "| ".SeparateArguments().SequenceEqual([string.Empty]).IsTrue();
        "-A 1 | -B 2".SeparateArguments().SequenceEqual(["-A 1", "-B 2"]).IsTrue();
    }

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
        Test(""""" """abc "d"""" """"test"""" """"", ["\"\"\"abc \"d\"\"\"\"", "\"\"\"\"test\"\"\"\"",]);
        // Test(""""-text """Triple quotes""" -options {} """");

        SimpleParserHelper.ParseArguments("").Is("");
        SimpleParserHelper.ParseArguments("A").Is("");
        SimpleParserHelper.ParseArguments("\"").Is("");
        SimpleParserHelper.ParseArguments("A\"").Is("");
        SimpleParserHelper.ParseArguments("\"AB").Is("");
        SimpleParserHelper.ParseArguments("\"AB\"").Is("");
        SimpleParserHelper.ParseArguments("\"AB\"c").Is("c");
        SimpleParserHelper.ParseArguments("\"AB\" c").Is("c");
        SimpleParserHelper.ParseArguments("AB c").Is("c");
    }

    private void Test(string args, string[] test)
    {
        var result = SimpleParserHelper.FormatArguments(args);
        result.IsStructuralEqual(test);
    }
}

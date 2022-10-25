using SimpleCommandLine;
using Xunit;

namespace xUnitTest
{
    public class UnitTest1
    {
        [Fact]
        public void FormatTest()
        {
            Test(string.Empty, new string[] { });
            Test("test", new string[] { "test", });
            Test("test -abc", new string[] { "test", "-abc", });

            Test("  -n 99 ", new string[] { "-n", "99", });
            Test("test -abc", new string[] { "test", "-abc", });
            Test("  -test  1 23  ", new string[] { "-test", "1", "23", });
            Test("-text \"abc\"", new string[] { "-text", "\"abc\"", });

            Test(""" -text"a \"b c" """, new string[] { "-text", "\"a \\\"b c\"" });
            Test("-text \"a b c\" -options [] ", new string[] { "-text", "\"a b c\"", "-options", "[]", });
            Test("-options []] ", new string[] { "-options", "[]", "]", });
            Test("-options [[] ", new string[] { "-options", "[[]", });
            Test("-options [-z\"AA\"] ", new string[] { "-options", "[-z\"AA\"]", });
            Test("-options [-z\"[A]B\"]", new string[] { "-options", "[-z\"[A]B\"]",  });
            Test("-ns [-node \"[3.18.216.240]:49152(1)\"]", new string[] { "-ns", "[-node \"[3.18.216.240]:49152(1)\"]" });
            Test("-options [-text \"message\"] -string \"[options2]\"", new string[] { "-options", "[-text \"message\"]", "-string", "\"[options2]\"", });

            Test("-options \"a]b\" ", new string[] { "-options", "\"a]b\"", });
            Test("-options \"a[]b\" ", new string[] { "-options", "\"a[]b\"", });
            Test("-options \"a[][b\" ", new string[] { "-options", "\"a[][b\"", });
            Test("""" """a""" """", new string[] { "\"\"\"a\"\"\"", });
            Test(""""-text """Triple quotes[]""" -options [] """", new string[] { "-text", "\"\"\"Triple quotes[]\"\"\"", "-options", "[]", });
            Test("""""""-text """a""" """""" """Triple quotes[]""" """"""", new string[] { "-text", "\"\"\"a\"\"\"", "\"\"\"\"\"\"", "\"\"\"Triple quotes[]\"\"\"", });
            Test(""""" """abc "d"""" """"test"""" """"", new string[] { "\"\"\"abc \"d\"\"\"\"", "\"\"\"\"test\"\"\"\"", });
            // Test(""""-text """Triple quotes""" -options [] """");
        }

        private void Test(string args, string[] test)
        {
            var result = SimpleParserExtensions.FormatArguments(args);
            result.IsStructuralEqual(test);
        }
    }
}

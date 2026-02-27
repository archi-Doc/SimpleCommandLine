// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Text;
using SimpleCommandLine;

#pragma warning disable SA1516 // Elements should be separated by blank line

var delimiter = "\"\"\"";
Console.WriteLine("Arguments test");

Test(string.Empty);
Test("test");
Test("-n 99");
Test("-test 12");
Test("  -test  1 23  ");
Test("-text \" abc \"");
Test("-text\"a \\\"b c\"");
Test("-text \"a b c\" -options {} ");
Test(""""-text """Triple quotes""" -options {} """");
Test("-options {abd");
Test("-options {}} ");
Test("-options {{} ");
Test("-options {-z\"AA\"} ");
Test("-options {-z\"{A}B\"}");
Test("-options {-z \"AA\"}");
Test("-ns {-node \"[3.18.216.240]:49152(1)\"}");
Test("-node [1.3.4.5]:023");

Test("-a \"A\" -b 'b b' -c \"CCC'cc'\" -d 'DDD \"dd d\"'");
Test("-options \"--env lpargs='-pass 1'\"");

Test("A | B|C");
Test("A | \"B|C|\"|D|{E}|{FG|}");

Test($"{delimiter}A\"B\"C{delimiter}");

Test($"{delimiter}A\r\nB\nC{delimiter}");
static void Test(string arg)
{
    var sb = new StringBuilder();
    Test2(sb, arg, null);
    Console.WriteLine(sb.ToString());
}

static void Test2(StringBuilder sb, string arg, string[]? formatted)
{
    var result = formatted ?? arg.FormatArguments();
    var prefix = formatted is null ? string.Empty : "  /  ";
    sb.Append($"{prefix}{arg}  ->  {string.Join(',', result)}");
    foreach (var x in result)
    {
        if (x.Length >= 2 && x.StartsWith('{') && arg.EndsWith('}'))
        {
            var result2 = x.Substring(1, x.Length - 2).FormatArguments();
            if (result2.Length > 1)
            {
                Test2(sb, x, result2);
            }
        }
    }
}

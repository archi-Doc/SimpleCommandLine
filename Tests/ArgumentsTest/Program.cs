// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Text;
using SimpleCommandLine;

Console.WriteLine("Arguments test");

Test(string.Empty);
Test("test");
Test("-n 99");
Test("-test 12");
Test("  -test  1 23  ");
Test("-text \"abc\"");
Test("-text\"a \\\"b c\"");
Test("-text \"a b c\" -options [] ");
Test("-options []] ");
Test("-options [[] ");
Test("-options [-z\"AA\"] ");
Test("-options [-z\"[A]B\"]");
Test("-options [-z \"AA\"]");
Test("-ns [-node \"[3.18.216.240]:49152(1)\"]");

static void Test(string arg)
{
    var sb = new StringBuilder();
    Test2(sb, arg, null);
    Console.WriteLine(sb.ToString());
}

static void Test2(StringBuilder sb, string arg, string[]? formatted)
{
    var result = formatted ?? arg.FormatArguments();
    sb.Append($"{arg} = {string.Join(',', result)} | ");
    foreach (var x in result)
    {
        if (x.Length >= 2 && x.StartsWith('[') && arg.EndsWith(']'))
        {
            var result2 = x.Substring(1, x.Length - 2).FormatArguments();
            if (result2.Length > 1)
            {
                Test2(sb, x, result2);
            }
        }
    }
}

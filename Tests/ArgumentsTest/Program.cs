// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using SimpleCommandLine;

Console.WriteLine("Arguments test");

Test(string.Empty);
Test("test");
Test("-n 99");
Test("-test 12");
Test("  -test  1 23  ");
Test("-text \"abc\"");
Test("-text\"a b c\"");
Test("-text \"a b c\" -options {} ");
Test("-options {}} ");
Test("-options {{} ");
Test("-options {-z\"AA\"} ");
Test("-options \"{-z\"AA\"}\"");

static void Test(string arg)
{
    Console.WriteLine($"{arg,-20}: {string.Join(',', arg.FormatArguments())}");
}

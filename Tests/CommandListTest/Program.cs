// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace CommandListTest;

public class TestCommandBase : ISimpleCommand
{
    public void Run(string[] args)
    {
    }
}

[SimpleCommand("test")]
public class TestCommand : ISimpleCommandAsync
{
    public async Task RunAsync(string[] args)
    {
        Console.WriteLine("Test command:");
    }
}

[SimpleCommand("test2")]
public class TestCommand2 : TestCommandBase;

[SimpleCommand("A")]
public class TestCommandA : TestCommandBase;

[SimpleCommand("1234567890")]
public class TestCommand123 : TestCommandBase;

[SimpleCommand("ZXCVBNMasdfghj1234567890")]
public class TestCommandZ : TestCommandBase;

[SimpleCommand("change-vault-password")]
public class TestCommandCap : TestCommandBase;

[SimpleCommand("show-incoming-relay")]
public class TestCommand3 : TestCommandBase;

[SimpleCommand("list-authority")]
public class TestCommand4 : TestCommandBase;

[SimpleCommand("show-authority")]
public class TestCommand5 : TestCommandBase;

[SimpleCommand("list-vault")]
public class TestCommand6 : TestCommandBase;

[SimpleCommand("show-vault")]
public class TestCommand7 : TestCommandBase;

[SimpleCommand("benchmark")]
public class TestCommand8 : TestCommandBase;

public class Program
{
    public static async Task Main(string[] args)
    {
        var commandTypes = new Type[]
        {
            typeof(TestCommand),
            typeof(TestCommand2),
            typeof(TestCommandA),
            typeof(TestCommand123),
            typeof(TestCommandZ),
            typeof(TestCommandCap),
            typeof(TestCommand3),
            typeof(TestCommand4),
            typeof(TestCommand5),
            typeof(TestCommand6),
            typeof(TestCommand7),
            typeof(TestCommand8),
        };

        var parser = new SimpleParser(commandTypes);
        parser.ShowCommandList(25);
    }
}

// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace CommandListTest;

[SimpleCommand("test")]
public class TestCommand : ISimpleCommandAsync
{
    public async Task RunAsync(string[] args)
    {
        Console.WriteLine("Test command:");
    }
}

[SimpleCommand("test2")]
public class TestCommand2 : ISimpleCommandAsync
{
    public async Task RunAsync(string[] args)
    {
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var commandTypes = new Type[]
        {
            typeof(TestCommand),
            typeof(TestCommand2),
        };

        var parser = new SimpleParser(commandTypes);
        parser.ShowCommandList();
    }
}

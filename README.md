## SimpleCommandLine

![Nuget](https://img.shields.io/nuget/v/SimpleCommandLine) ![Build and Test](https://github.com/archi-Doc/SimpleCommandLine/workflows/Build%20and%20Test/badge.svg)

Simple command-line parser for .NET console applications.



## Table of Contents

- [Requirements](#requirements)
- [Quick Start](#quick-start)



## Requirements

**.NET 5** or later



## Quick Start

Install SimpleCommandLine using Package Manager Console.

```
Install-Package SimpleCommandLine
```

This is a small sample code to use SimpleCommandLine.

```csharp
// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using SimpleCommandLine;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace ConsoleApp1;

public class TestOptions
{// Option class stores command options. Default constructor is required.
    [SimpleOption("number", ShortName = "n", Description = "test number")] // Annotate SimpleOptionAttribute and specify a long/short option name and description.
    public int Number { get; set; } = 10; // Set a default value.

    [SimpleOption("text", ShortName = "t", Description = "test text.", Required = true)] // Set Required property to true if you want to make the option required.
    public string Text { get; set; } = string.Empty;
}

[SimpleCommand("test", Description = "Test command.")] // Annotate SimpleCommandAttribute and specify a command name and description.
public class TestCommand : ISimpleCommandAsync<TestOptions> // Implementation of either ISimpleCommandAsync<T> or ISimpleCommand<T> is required.
{// Command class handles the command function.
    public async Task RunAsync(TestOptions option, string[] args)
    {// RunAsync() method will be called if you specify "test" command-line argument.
        // TestOption class is parsed from command-line arguments.
        // args is the remaining arguments.

        Console.WriteLine("Test command:");
        Console.WriteLine($"Number is {option.Number}");
        Console.WriteLine($"Text is {option.Text}");
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        // An array of command types.
        // Command type must have SimpleCommandAttribute and implement ISimpleCommandAsync<T> or ISimpleCommand<T>.
        var commandTypes = new Type[]
        {
            typeof(TestCommand),
        };

        // Parse arguments and call the appropriate command method.
        await SimpleParser.ParseAndRunAsync(commandTypes, args); // If you do not specify a text option with a valid value, an error will occur.
        Console.WriteLine();

        // You can manually create a parser and parse an argument string.
        var p = new SimpleParser(commandTypes);
        p.Parse("-number 1 -text sample");
        await p.RunAsync();
        Console.WriteLine();

        p.ShowVersion(); // Show application version (1.0.0)
        Console.WriteLine();

        p.ShowHelp(); // Show help text.
        Console.WriteLine();
    }
}
```


## SimpleCommandLine

![Nuget](https://img.shields.io/nuget/v/SimpleCommandLine) ![Build and Test](https://github.com/archi-Doc/SimpleCommandLine/workflows/Build%20and%20Test/badge.svg)

Simple command-line parser for .NET console applications.



## Table of Contents

- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Commands](#commands)
- [Options](#options)
- [Parser Options](#parser-options)



## Requirements

**.NET 10** or later



## Quick Start

Install SimpleCommandLine using Package Manager Console.

```
Install-Package SimpleCommandLine
```

This is a small sample code to use SimpleCommandLine.

```csharp
// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;

namespace ConsoleApp1;

public class TestOptions
{// Option class stores command options. Default constructor is required.
    [SimpleOption("number", ShortName = "n", Description = "test number")] // Annotate SimpleOptionAttribute and specify a long/short option name and description.
    public int Number { get; set; } = 10; // Set a default value.

    [SimpleOption("text", ShortName = "t", Description = "test text", Required = true)] // Set Required property to true if you want to make the option required.
    public string Text { get; set; } = string.Empty;
}

[SimpleCommand("test", Description = "Test command.")] // Annotate SimpleCommandAttribute and specify a command name and description.
public class TestCommand : ISimpleCommand<TestOptions> // Implementation of either ISimpleCommand or ISimpleCommand<TOptions> is required.
{// Command class handles the command function.
    public async Task Execute(TestOptions options, string[] args, CancellationToken cancellationToken)
    {// Execute() method will be called if you specify "test" command-line argument.
     // TestOptions class is parsed from command-line arguments.
     // args is the remaining arguments.

        Console.WriteLine("Test command:");
        Console.WriteLine($"Number is {options.Number}");
        Console.WriteLine($"Text is {options.Text}");
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        // An array of command types.
        // Command type must have SimpleCommandAttribute and implement ISimpleCommand or ISimpleCommand<TOptions>.
        var commandTypes = new Type[]
        {
            typeof(TestCommand),
        };

        // Parse arguments and call the appropriate command method.
        await SimpleParser.ParseAndExecute(commandTypes, args); // If you do not specify a text option with a valid value, an error will occur.
        Console.WriteLine();

        // You can manually create a parser and parse a command line.
        var p = new SimpleParser(commandTypes);
        p.Parse("-number 1 -text example");
        await p.Execute();
        Console.WriteLine();

        p.ShowVersion("QuickStart"); // Show application version (1.0.0)
        Console.WriteLine();

        p.ShowHelp(); // Show help text.
        Console.WriteLine();

        if (p.TryGetOption("test", "text", out var option))
        {// You can modify the documentation by changing the options registered with SimpleParser.
            option.Description = "Modified";
        }

        p.ShowHelp(); // Show help text.
        Console.WriteLine();
    }
}
```



## Commands

A command is a class annotated with `SimpleCommandAttribute` which implements one of the following interfaces.

```csharp
// A command with an options class.
public interface ISimpleCommand<TOptions>
    where TOptions : new()
{
    Task Execute(TOptions options, string[] args, CancellationToken cancellationToken);
}

// A command without options.
public interface ISimpleCommand
{
    Task Execute(string[] args, CancellationToken cancellationToken);
}
```

`args` receives the arguments which were not consumed as an option.

| `SimpleCommandAttribute` | Description |
| --- | --- |
| `CommandName` | The name of the command (case insensitive). An empty name makes it the default command. |
| `Alias` | An alternate name for the command. |
| `IsDefault` | The command is executed when the command name is not specified. If no command declares it, the first registered command becomes the default. |
| `Description` | The description shown in a help message. |
| `IsSubcommand` | The command accepts unknown option names and forwards them, so that it can dispatch them to its own parser. |

`help` and `version` are handled by the parser: `app.exe help`, `app.exe help <command>`, `app.exe <command> help` and `app.exe version`.

A single parse stops at the separator `|`. To run several commands with one command line, split it first.

```csharp
foreach (var x in "first -number 1 | second".SplitCommandLines())
{
    await SimpleParser.ParseAndExecute(commandTypes, x);
}
```



## Options

An options class is a plain class with a parameterless constructor. Each field or property annotated with `SimpleOptionAttribute` becomes an option.

| `SimpleOptionAttribute` | Description |
| --- | --- |
| `LongName` | The long option name, specified as `-name` (case insensitive). |
| `ShortName` | The short option name. |
| `Description` | The description shown in a help message. |
| `DefaultValueText` | The text shown as the default value in a help message. |
| `Required` | A value is required for this option. Its name may be omitted, so `app.exe test example` sets the first required option. |
| `ReadFromEnvironment` | The value is read from the environment variable named after the short or long name when the option is not specified. |
| `ArgumentProcessing` | How newlines and escape sequences in the value are handled. |

Supported option types are `string`, `bool`, `sbyte`/`byte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`, `float`/`double`/`decimal`, `char`, any `enum`, their nullable counterparts, and a nested options class.

```csharp
public class NestedOptions
{
    [SimpleOption("host")]
    public string Host { get; set; } = string.Empty;

    [SimpleOption("port")]
    public int Port { get; set; }
}

public class TestOptions
{
    [SimpleOption("count")]
    public int? Count { get; set; } // A nullable value type is supported.

    [SimpleOption("server")]
    public NestedOptions Server { get; set; } = new(); // A nested options class is enclosed in braces.
}
```

```
app.exe test -count 3 -server {-host localhost -port 100}
```

A value containing spaces is enclosed in `"`, `'` or `"""` (the argument delimiter), and a negative number is treated as a value rather than an option name.

```
app.exe test -text "a b c" -number -5
```



## Parser Options

`SimpleParserOptions` controls the behavior of the parser. Create a variant with a `with` expression.

```csharp
var parserOptions = SimpleParserOptions.Standard with
{
    RequireStrictCommandName = true,
    AutoAlias = true,
};

await SimpleParser.ParseAndExecute(commandTypes, args, parserOptions);
```

| Property | Default | Description |
| --- | --- | --- |
| `ServiceProvider` | `null` | Resolves command instances. When `null`, the parameterless constructor is used. |
| `RequireStrictCommandName` | `false` | The command name is required (no default command). |
| `RequireStrictOptionName` | `false` | An unregistered option name results in an error. |
| `DisplayUsage` | `true` | The usage text is displayed in a help message. |
| `DisplayCommandListAsHelp` | `false` | Help displays a single-line list of command names. |
| `OmitOptionNamesForRequiredOptions` | `true` | The name of a required option may be omitted. |
| `AutoAlias` | `false` | An alias is created from the initials of the hyphen-separated words ('remove-file' becomes 'rf'). |
| `ReadCommandFromEnvironment` | `true` | The command name is read from the `Command` environment variable when it is not specified. |
| `ArgumentDelimiter` | `"""` | The delimiter that encloses an argument containing spaces or newlines. |
| `SuppressConsoleOutput` | `false` | Help, version and error messages are not written to the console. |

To parse a command line into an options class without registering a command, use `SimpleParser.TryParseOptions()`.

```csharp
if (SimpleParser.TryParseOptions<TestOptions>("-number 1 -text example", out var options))
{
    Console.WriteLine(options.Number);
}
```

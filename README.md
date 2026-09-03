## SimpleCommandLine

![Nuget](https://img.shields.io/nuget/v/SimpleCommandLine) ![Build and Test](https://github.com/archi-Doc/SimpleCommandLine/workflows/Build%20and%20Test/badge.svg)

Simple command-line parser for .NET console applications.

- Declare commands and options with attributes; no builder code.
- Parse a `string[]` or a raw command line string.
- Generates the help and version messages for you.
- Supports required options, nested option classes, environment variables, aliases and subcommands.



## Table of Contents

- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [NativeAOT and Trimming](#nativeaot-and-trimming)
- [Commands](#commands)
- [Options](#options)
- [Option Types](#option-types)
- [Argument Syntax](#argument-syntax)
- [Parser Options](#parser-options)
- [Parser API](#parser-api)
- [Arc.Unit Integration](#arcunit-integration)
- [Command Groups](#command-groups)
- [Helper Methods](#helper-methods)
- [License](#license)



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

        p.ShowVersion("QuickStart"); // Show application version.
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

`ShowHelp()` writes a message like this.

```
Usage: QuickStart <Command> -option value...

Commands:
  test (default)

test Test command.
  -number, -n <Int32>    test number (Default: 10)
  -text, -t <String>     test text (Required)
```



## NativeAOT and Trimming

Use `SimpleParserBuilder`, or the [Arc.Unit integration](#arcunit-integration), when publishing with NativeAOT or trimming. Both share the same registration implementation: they preserve the reflection metadata used to create options and read or write their members, and dispatch commands through their interfaces without reflection invocation.

```csharp
var builder = new SimpleParserBuilder()
    .AddCommand<TestCommand, TestOptions>(); // Registers the command and its root options.

var parser = builder.Build();
await parser.ParseAndExecute(args);
```

Use `AddCommand<TCommand>()` for a command without options. For nested options, register **each nested options type** with `AddOptions<TOptions>()`. Base classes and their non-public members are preserved automatically. Missing nested registrations produce an `InvalidOperationException` naming the type at parser construction time.

```csharp
var builder = new SimpleParserBuilder()
    .AddCommand<TestCommand, TestOptions>()
    .AddOptions<NestedOptions>();

var parser = builder.Build(SimpleParserOptions.Standard with
{
    RequireStrictOptionName = true,
});

// Parse without a command; the root options type is registered automatically.
builder.TryParseOptions<TestOptions>("-number 1 -text example", out var options);
```

`Build()` creates a snapshot, so later registrations do not change an existing parser. Attributes, aliases, help/version output, required options, nullable values, enums, inherited/non-public members, explicit interface implementations and Tinyhand-generated option serialization work with this API. `TryParseOptions` retains the existing helper's behavior: invalid optional values are ignored; use a parser's `Parse()` to reject conversion errors.

The `IEnumerable<Type>` constructor, static `ParseAndExecute` overloads and static `TryParseOptions` methods remain available for untrimmed applications. They use runtime type discovery and are annotated with `RequiresUnreferencedCode`; migrate those calls to the builder or the generic Arc.Unit registration methods for trimmed/NativeAOT applications.

With Arc.Unit, register each command once using the generic context/group methods and inject the unit's `SimpleCommandRegistry` into command groups. There is no separate parser-builder registration in the group constructor:

```csharp
public DbCommand(SimpleCommandRegistry registry, UnitContext context)
    : base(registry, context, "list")
{
}
```

See [Command Groups](#command-groups) for the complete registration example. Service providers and custom serializers must themselves support NativeAOT. The legacy command-group constructor uses runtime discovery and requires migration too. The constructor taking a standalone `SimpleParserBuilder` remains available.

The library enables `IsAotCompatible` analysis. Publish and run the sample on Windows x64 with:

```powershell
dotnet publish QuickStart/QuickStart.csproj -c Release -r win-x64 -p:PublishAot=true -o artifacts/quickstart
./artifacts/quickstart/QuickStart.exe test -text example
```

The smoke-test project treats compiler and trimming warnings as errors and exercises parsing, command execution, DI, groups, help, Tinyhand serialization and failure/recovery paths. It runs in CI on Windows x64 and Linux x64:

```powershell
dotnet publish Tests/NativeAotTest/NativeAotTest.csproj -c Release -r win-x64 -o artifacts/native-aot
./artifacts/native-aot/NativeAotTest.exe --require-aot
```

For Linux, use `-r linux-x64` and run `./artifacts/native-aot/NativeAotTest --require-aot`. NativeAOT requires the platform's native build tools; see Microsoft's [NativeAOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/).



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
| `IsDefault` | The command is executed when the command name is not specified. |
| `Description` | The description shown in a help message. |
| `IsSubcommand` | The command accepts unknown option names and forwards them, so that it can dispatch them to its own parser. |

The command name may be omitted on the command line. The default command is the first command which declares `IsDefault`, or the first registered command if none does. Set `RequireStrictCommandName` to disable the default command.

An instance is created with the parameterless constructor of the command type, or resolved from `ServiceProvider` if one is set.

### Help and Version

The parser handles these before dispatching to a command.

| Command line | Result |
| --- | --- |
| `app.exe help` | The usage, the command list and the options of every command. |
| `app.exe help <command>` | The options of the specified command. |
| `app.exe <command> help` | The same as above. |
| `app.exe version` | The version of the entry assembly. |

`Parse()` sets `HelpCommandName` or `VersionRequested` instead of `CurrentCommand`, and `Execute()` writes the message. Call `ShowHelp()`, `ShowVersion()` or `ShowCommandList()` to write them yourself.

### Running Multiple Commands

A single parse stops at the separator `|`. To run several commands with one command line, split it first.

```csharp
foreach (var x in "first -number 1 | second".SplitCommandLines())
{
    await SimpleParser.ParseAndExecute(commandTypes, x);
}
```



## Options

An options class is a plain class with a parameterless constructor. Each field or property annotated with `SimpleOptionAttribute` becomes an option, including inherited and non-public ones.

| `SimpleOptionAttribute` | Description |
| --- | --- |
| `LongName` | The long option name, specified as `-name` (case insensitive). |
| `ShortName` | The short option name. The long name takes precedence when both match. |
| `Description` | The description shown in a help message. |
| `DefaultValueText` | The text shown as the default value in a help message. When omitted, the actual value of a new instance is shown. |
| `Required` | A value is required for this option. |
| `ReadFromEnvironment` | The value is read from an environment variable when the option is not specified. |
| `ArgumentProcessing` | How newlines and escape sequences in the value are handled. |

### Required Options

A required option which is not set is an error, and `Parse()` returns `false`. Its name may be omitted, so a value without an option name is assigned to the first required option that is not set yet.

```
app.exe test example    # The same as: app.exe test -text example
```

Set `OmitOptionNamesForRequiredOptions` to `false` to always require the name.

### Environment Variables

With `ReadFromEnvironment`, an option that is not specified on the command line falls back to the environment variable named after its short name, then its long name.

```csharp
[SimpleOption("api-key", ShortName = "API_KEY", ReadFromEnvironment = true)]
public string ApiKey { get; set; } = string.Empty;
```

The command name itself is read from the `Command` environment variable when it is not specified on the command line (`ReadCommandFromEnvironment`).

### Argument Processing

`ArgumentProcessing` controls how the value of a `string` option is normalized. The surrounding delimiter or quotes are always removed.

| Value | Description |
| --- | --- |
| `ReplaceNewlinesWithSpace` | Removes `\r`, replaces `\n` with a space, and unescapes `\'` and `\"`. The default. |
| `RemoveNewlines` | Removes `\r` and `\n`, and unescapes `\'` and `\"`. |
| `AsIs` | Keeps newlines and escape sequences as they are. |



## Option Types

| Type | Note |
| --- | --- |
| `string` | Normalized according to `ArgumentProcessing`. |
| `bool` | `true` / `false` (case insensitive). |
| `sbyte` `byte` `short` `ushort` `int` `uint` `long` `ulong` | Parsed with the invariant culture. |
| `float` `double` `decimal` | Parsed with the invariant culture. |
| `char` | The first character of the value. |
| `enum` | By member name (case insensitive) or by its numeric value. |
| `Nullable<T>` | Any of the above, such as `int?` or `DayOfWeek?`. |
| An options class | A nested options class, enclosed in braces. |

A value which cannot be converted is an error, and `Parse()` returns `false`.

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

A nested options class is described in its own block of the help message.

```
test Test command.
  -count <Int32?>             (Optional)
  -server {NestedOptions}

{NestedOptions}
  -host <String>     (Default: "")
  -port <Int32>      (Default: 0)
```



## Argument Syntax

| Syntax | Description |
| --- | --- |
| `-name value` | An option and its value. `--name` is also accepted. |
| `-n value` | The short name of an option. |
| `value` | A value without an option name: assigned to a required option, otherwise passed to the command as a remaining argument. |
| `"a b"` `'a b'` | A value containing spaces. |
| `"""a b"""` | The argument delimiter (`ArgumentDelimiter`). Useful for a value which itself contains quotes. |
| `{...}` | A nested options class. |
| `-5` `-.5` | A negative number is a value, not an option name. |
| `,` | Separates arguments, like whitespace. |
| `\|` | Separates a command line into multiple command lines. |

Unknown option names are passed to the command as remaining arguments. Set `RequireStrictOptionName` to make them an error instead.



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

`SimpleParserOptions.Standard`, `StrictCommandName` and `StrictOptionName` are ready-made instances.



## Parser API

`SimpleParser.ParseAndExecute()` is a shortcut for `Parse()` followed by `Execute()`. Keep a parser instance to inspect the result.

```csharp
var parser = new SimpleParser(commandTypes, parserOptions);
if (parser.Parse(args))
{
    await parser.Execute(cancellationToken);
}
```

| Member | Description |
| --- | --- |
| `Parse(string)` / `Parse(string[])` | Parses the arguments. Returns `false` on an error. |
| `Execute(CancellationToken)` | Executes the parsed command, or writes the help or version message. |
| `ParseAndExecute(...)` | Both of the above. Also available as a static method. |
| `CurrentCommand` | The command of the last successful parse. |
| `DefaultCommandName` | The name of the default command. |
| `HelpCommandName` / `VersionRequested` | Set when help or version was requested. |
| `OriginalCommandLine` | The arguments passed to the last `Parse()`. |
| `NameToCommand` / `AliasToCommand` | The registered commands, keyed by name or alias. |
| `TryGetCommand(name, out command)` | Looks up a command (case insensitive). |
| `TryGetOption(commandName, optionName, out option)` | Looks up an option by its long name (case insensitive). |
| `ShowHelp(commandName)` / `ShowVersion(prefix)` | Writes a help or version message. |
| `ShowCommandList(maxColumnWidth)` | Writes the command names in columns sized to the console width. |
| `AddErrorMessage(message)` | Adds a message to be displayed by `ShowHelp()`. |

`Description` of a `Command` or an `Option` is settable, so a help message can be localized at runtime.

To parse a command line into an options class without registering a command, use `SimpleParser.TryParseOptions()`. Pass an instance as the third argument to update it instead of creating a new one.

```csharp
if (SimpleParser.TryParseOptions<TestOptions>("-number 1 -text example", out var options))
{
    Console.WriteLine(options.Number);
}
```



## Arc.Unit Integration

Import `SimpleCommandLine` to enable generic registration extensions on `IUnitConfigurationContext`. A single registration adds the command to Arc.Unit's command list and DI services, and records its NativeAOT metadata in the unit's shared registry.

```csharp
using Arc.Unit;
using SimpleCommandLine;

var unitBuilder = new UnitBuilder();
unitBuilder.Configure(context =>
{
    context.AddCommand<TestCommand, TestOptions>();
    // Register every nested options type used by these commands, if any:
    context.AddOptionType<NestedOptions>();
});

var unit = unitBuilder.Build();
var parser = unit.Context.CreateSimpleParser();
await parser.ParseAndExecute(args);
```

Use `context.AddCommand<TCommand>()` for commands without options. Both overloads accept a `ServiceLifetime` (default: `Scoped`) and return whether the command was newly added to the selected list. Arc.Unit keeps the first DI registration, including its lifetime or a pre-registered service instance. Repeating a generic registration is safe; choosing a different options type for the same command throws an error.

| Registration | Parser creation | Command list |
| --- | --- | --- |
| `context.AddCommand<TCommand, TOptions>()` | `unit.Context.CreateSimpleParser()` | `UnitContext.Commands` |
| `context.AddSubcommand<TCommand, TOptions>()` | `unit.Context.CreateSimpleSubcommandParser()` | `UnitContext.Subcommands` |
| `context.GetSimpleCommandGroup<TParent>().AddCommand<TCommand, TOptions>()` | `unit.Context.CreateSimpleParser<TParent>()` | `UnitContext.GetCommandTypes(typeof(TParent))` |

Each registration also has a `TCommand`-only overload. Register the parent command itself in its desired list before obtaining its group. Child groups select commands from Arc.Unit's membership lists; command metadata can be shared across groups without adding those commands to other lists.

The integration uses `IUnitCustomContext` to collect metadata across configuration delegates and modules. After `Configure` completes, it registers an immutable `SimpleCommandRegistry` singleton with the unit's service provider. Registrations must finish during `Configure`; modifying a retained context/group after it has been finalized throws an error. Different units have independent registries.

`CreateSimpleParser` creates a fresh parser each time. Parse results, options instances and mutable help descriptions are independent. Command instances follow their DI lifetime; a parser caches each resolved command instance for its own lifetime, just as a standalone parser does.

For scoped services, supply the current scope's provider:

```csharp
using Microsoft.Extensions.DependencyInjection;

using var scope = unit.Context.ServiceProvider.CreateScope();
var parser = unit.Context.CreateSimpleParser(SimpleParserOptions.Standard with
{
    ServiceProvider = scope.ServiceProvider,
});
await parser.ParseAndExecute(args);
```

Without an explicit provider, the unit's provider is used. When a command group must resolve its children in the same DI scope, inject `IServiceProvider` into the group and pass it through `parserOptions.ServiceProvider` to the base constructor.

The existing Arc.Unit `AddCommand(typeof(...))` / `AddSubcommand(typeof(...))` methods do not capture parser metadata. Replace them with the generic extensions for this integration. If an existing raw registration is later supplemented with a generic registration, its DI registration is retained and its parser metadata is added. Raw-only commands or missing nested option types produce a descriptive error when creating the parser.



## Command Groups

`SimpleCommandGroup<TCommand>` dispatches its arguments to a group of subcommands, using `Arc.Unit` for the registration and the service provider.

```csharp
[SimpleCommand("db", IsSubcommand = true)] // A group must be a subcommand.
public class DbCommand : SimpleCommandGroup<DbCommand>
{
    public static void Configure(IUnitConfigurationContext context)
    {
        context.AddCommand<DbCommand>(); // Register the parent in the top-level command list.
        var group = context.GetSimpleCommandGroup<DbCommand>();
        group.AddCommand<DbListCommand>(); // One registration for membership, DI and NativeAOT metadata.
    }

    public DbCommand(SimpleCommandRegistry registry, UnitContext context)
        : base(registry, context, "list")
    {// "list" is executed when no subcommand is specified.
    }
}

[SimpleCommand("list")]
public class DbListCommand : ISimpleCommand
{
    public Task Execute(string[] args, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

In `Main`, configure the unit and create the top-level parser:

```csharp
var builder = new UnitBuilder();
builder.Configure(DbCommand.Configure);
var unit = builder.Build();
await unit.Context.CreateSimpleParser().ParseAndExecute(args);
```

```
app.exe db list
```

To nest a group, register it as a child with `context.GetSimpleCommandGroup<TParent>().AddCommand<TChildGroup>()`, then register its children through `context.GetSimpleCommandGroup<TChildGroup>()`. The inner parser uses `RequireStrictCommandName`, `RequireStrictOptionName`, `DisplayUsage = false` and `DisplayCommandListAsHelp` unless you pass your own `SimpleParserOptions`.

The older `ConfigureGroup(context, parentCommandType)` API remains available for existing manually configured parsers. The generic context/group extensions above provide the shared-registry integration.



## Helper Methods

`SimpleParserHelper` contains the utilities used by the parser.

| Method | Description |
| --- | --- |
| `GetCommandLineArguments()` | The arguments of the current process, with the executable path removed. |
| `ExtractArguments(commandLine)` | Removes the leading executable path from a command line. |
| `PeekCommand(commandLine)` | The leading command name of a command line, without parsing it. |
| `SplitArguments(commandLine)` | Splits a command line into arguments, honoring quotes and braces. |
| `SplitCommandLines(commandLine)` | Splits a command line at `\|` into multiple command lines. |
| `SplitAtSpace(text)` / `JoinWithSpace(values)` | Splits at whitespace / joins with a space. |
| `TrimQuotes(text)` / `TrimQuotesAndBraces(text)` | Removes the surrounding quotes or braces. |
| `UnwrapDoubleQuote(text)` / `UnwrapBraces(text)` | Removes the surrounding double quotes or braces, without trimming whitespace. |
| `ProcessArgument(argument, parserOptions, processing)` | Normalizes an argument the way the parser does. |
| `IsOptionName(text)` | Whether the text is an option name rather than a value. |
| `CreateAliasFromCommand(commandName)` | The initials of the hyphen-separated words. |
| `TryGetAndRemoveArgument(ref args, optionName, out value)` | Takes an option and its value out of an argument array. |
| `AppendEnvironmentVariable(ref args, variableName)` | Appends the value of an environment variable to the arguments. |



## License

MIT License. See [LICENSE](LICENSE).

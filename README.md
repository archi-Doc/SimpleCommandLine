# SimpleCommandLine

![NuGet](https://img.shields.io/nuget/v/SimpleCommandLine) ![Build and Test](https://github.com/archi-Doc/SimpleCommandLine/actions/workflows/test.yml/badge.svg)

A command-line parser for .NET console applications.

- Describe commands and options with attributes.
- Parse raw command lines or pre-split argument arrays.
- Use typed registration for trimming and NativeAOT.
- Share command registration with Arc.Unit and dependency injection.
- Support required and nested options, environment variables, aliases, help, and version output.

## Contents

- [Requirements and Installation](#requirements-and-installation)
- [Quick Start](#quick-start)
- [Commands](#commands)
- [Options](#options)
- [Argument Syntax](#argument-syntax)
- [Parser Options](#parser-options)
- [Parser API and State](#parser-api-and-state)
- [Standalone Options](#standalone-options)
- [NativeAOT and Trimming](#nativeaot-and-trimming)
- [Arc.Unit Integration](#arcunit-integration)
- [Command Groups](#command-groups)
- [Helper Methods](#helper-methods)
- [Tests and Coverage](#tests-and-coverage)
- [License](#license)

## Requirements and Installation

Targets **.NET 10**. Install the package in a .NET 10 or later application:

```shell
dotnet add package SimpleCommandLine
```

## Quick Start

Use this as `Program.cs` in a console application:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;

var parser = new SimpleParserBuilder()
    .AddCommand<GreetCommand, GreetOptions>()
    .Build();

await parser.ParseAndExecute(args);

public class GreetOptions
{
    [SimpleOption("name", ShortName = "n", Required = true)]
    public string Name { get; set; } = string.Empty;

    [SimpleOption("count", ShortName = "c")]
    public int Count { get; set; } = 1;
}

[SimpleCommand("greet", Alias = "g", Description = "Print a greeting.")]
public class GreetCommand : ISimpleCommand<GreetOptions>
{
    public Task Execute(GreetOptions options, string[] args, CancellationToken cancellationToken)
    {
        for (var i = 0; i < options.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine($"Hello, {options.Name}!");
        }

        return Task.CompletedTask;
    }
}
```

```shell
dotnet run -- greet -name "Ada Lovelace" -count 2
dotnet run -- g Ada
dotnet run -- help greet
dotnet run -- version
```

`GreetCommand` is the default command because it is registered first. With the default settings, `greet Ada` and `greet -name Ada` are equivalent: `Ada` supplies the first unset required option.

## Commands

A command class has `[SimpleCommand(...)]` and implements either:

- `ISimpleCommand<TOptions>`: `Task Execute(TOptions options, string[] args, CancellationToken cancellationToken)`.
- `ISimpleCommand`: `Task Execute(string[] args, CancellationToken cancellationToken)`.

`args` contains unconsumed arguments. Explicit interface implementations are supported. The parser forwards cancellation tokens; command implementations must observe them. Register commands with options using `AddCommand<TCommand, TOptions>()`, and commands without options using `AddCommand<TCommand>()`.

| `SimpleCommandAttribute` member | Meaning |
| --- | --- |
| `CommandName` | Case-insensitive name, trimmed of surrounding whitespace. An empty name is a default candidate. |
| `Alias` | An explicit case-insensitive alias; empty by default. |
| `IsDefault` | Marks a default candidate; false by default. |
| `Description` | Text shown in help. |
| `IsSubcommand` | Accepts unknown options even in strict mode and leaves command-specific help for the child parser. |

The first default candidate wins; otherwise, the first registered command is used. `RequireStrictCommandName` disables the default. Command names take precedence over aliases. With `AutoAlias`, hyphen-separated initials become aliases, such as `remove-file` to `rf`; conflicts with command names or existing aliases are skipped.

The provider in `ServiceProvider` is asked for each command instance on first access. If it returns null, the parser uses a public parameterless constructor. The parser caches the instance, even for a transient DI registration.

### Help and Version

| Input | Result |
| --- | --- |
| `help` or `-help` | Help for all commands. |
| `help greet` | Help for the full command name `greet`. |
| `greet help` or `greet -help` | Help for `greet`. |
| `version` or `-version` | Entry assembly version output. |
| `h` or `-h` | General help when `AutoAlias` is enabled. |

Registered commands and aliases are resolved before built-in help/version names. A registered `help` option takes precedence over `greet -help`; `greet help` still requests help. Subcommands receive command-specific help as remaining arguments.

`Parse()` returns true for help/version requests and sets `HelpCommandName` or `VersionRequested`, leaving `CurrentCommand` null. `Execute()` writes the requested output. Explicit help/version requests take precedence over the `Command` environment variable.

## Options

Options classes normally have public parameterless constructors. Annotated instance fields and properties are discovered, including inherited and non-public members. Setters may be non-public; getter-only auto-properties use their backing fields. Getter-only computed properties and indexers are rejected. Virtual overrides replace their base option metadata while retaining its position in required-option ordering.

| `SimpleOptionAttribute` member | Meaning |
| --- | --- |
| `LongName` | Required nonblank name, used as `-name` or `--name`; case insensitive. |
| `ShortName` | Optional short name; blank names are ignored. Long names take precedence over short names. |
| `Description` | Text shown in help. |
| `DefaultValueText` | Help-only text; does not assign a value. Required options display it as a hint. |
| `Required` | Requires a successfully supplied value on each parse. |
| `ReadFromEnvironment` | Reads from the environment when no value was successfully supplied. |
| `ArgumentProcessing` | Normalizes raw values; defaults to `ReplaceNewlinesWithSpace`. |

Set ordinary defaults in member initializers or the options constructor. Repeated valid occurrences use the last value. Every recognized option needs a value, including boolean options such as `-enabled true`.

### Required Values and Environment Variables

An unset required option fails parsing even if its member has an initializer. Unnamed values supply the first unset required option in base-to-derived declaration order. Set `OmitOptionNamesForRequiredOptions = false` to require option names. An empty string is a valid value for a required string option.

For `ReadFromEnvironment`, the short-name environment variable is checked first, then the long name if the short variable is absent. A successfully parsed input value takes precedence. An invalid supplied value still makes ordinary `Parse()` fail even if the environment provides a valid fallback.

```csharp
[SimpleOption("api-key", ShortName = "API_KEY", ReadFromEnvironment = true)]
public string ApiKey { get; set; } = string.Empty;
```

With `ReadCommandFromEnvironment` enabled, the `Command` environment variable selects a command name or alias when the input does not select one. An unknown environment value falls back to normal default-command behavior. Invalid environment option values fail `Parse()`.

### Supported Types

| Type | Conversion |
| --- | --- |
| `string` | Raw values use `ArgumentProcessing`; array values are kept verbatim. |
| `bool` | `true` or `false`, case insensitive. |
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` | Invariant-culture numbers. |
| `float`, `double`, `decimal` | Invariant-culture numbers. |
| `char` | The first character of a nonempty value. |
| Enum | Case-insensitive member names or numeric values. |
| Nullable value types | Nullable forms of the supported numeric, boolean, character, and enum types. |
| Options class | A nested expression, usually enclosed in braces. |

Numeric enum values need not name a declared member. Conversion failures make ordinary `Parse()` return false. Unsupported types are treated as nested options types, not converted using general-purpose type converters.

### Nested Options and Tinyhand

Register every nested type with `AddOptions<TOptions>()`. This standalone program parses an endpoint:

```csharp
using System;
using SimpleCommandLine;

var builder = new SimpleParserBuilder().AddOptions<EndpointOptions>();
if (builder.TryParseOptions<NetworkOptions>("-server {-host localhost -port 100}", out var options))
{
    Console.WriteLine($"{options.Server.Host}:{options.Server.Port}");
}

public class NetworkOptions
{
    [SimpleOption("server")]
    public EndpointOptions Server { get; set; } = new();
}

public class EndpointOptions
{
    [SimpleOption("host")]
    public string Host { get; set; } = string.Empty;

    [SimpleOption("port")]
    public int Port { get; set; }
}
```

Each explicit nested occurrence creates a fresh value instead of merging into the previous instance. Unspecified nested members retain their existing instance, or are initialized when possible. Nested types are described once each in help. Circular type dependencies are rejected.

For types registered with Tinyhand, nested values first use Tinyhand string parsing or deserialization, then fall back to option parsing if no object is returned. Tinyhand reconstruction can also create registered nested types without public parameterless constructors. These types still need `AddOptions<TOptions>()` for trimming and NativeAOT; root command options retain the `new()` constraint of `ISimpleCommand<TOptions>`.

## Argument Syntax

### Raw Command Lines

| Syntax | Meaning |
| --- | --- |
| `-name value`, `--name value` | An option and its value. |
| `value` | An unnamed required value or a remaining argument. |
| `"a b"`, `'a b'` | One value containing spaces. |
| `"""a "quoted" value"""` | A value enclosed by the default argument delimiter. |
| `{...}` | A nested options expression. |
| `-5`, `-.5` | Negative values, not option names. |
| `,` | An argument separator, like whitespace. |
| `\|` | Ends the current command outside quotes or braces. |

Quotes and the configured delimiter are removed before scalar conversion, including numeric and enum conversion. Quote characters preceded by a backslash do not open or close single/double-quoted text. This is the library's syntax, not a shell grammar.

| `ArgumentProcessing` | Raw-value handling after removing enclosing quotes/delimiters |
| --- | --- |
| `ReplaceNewlinesWithSpace` | Removes `\r`, replaces `\n` with a space, and unescapes `\'` and `\"`. |
| `RemoveNewlines` | Removes `\r` and `\n`, and unescapes `\'` and `\"`. |
| `AsIs` | Preserves newlines and escapes. |

Nested brace expressions retain their syntax until their own parser handles it. Remaining scalar arguments use `ReplaceNewlinesWithSpace`. Setting `ArgumentDelimiter = string.Empty` disables the extra delimiter; single and double quotes remain active.

### Pre-split Argument Arrays

Each `string[]` element is already one argument. Spaces, empty values, commas, quotes, escapes, and newlines inside a value are kept verbatim. Option-name detection still applies, and a standalone `|` still ends the command:

```csharp
parser.Parse(new[] { "greet", "-name", "Ada Lovelace", "-count", "2" });
```

Nested expressions belong in one element, such as `"{-host 'two words' -port 100}"`. Do not add shell-style enclosing quotes to ordinary array values. If you intentionally have command-line fragments, join them explicitly and use the string overload. Earlier versions joined arrays automatically.

### Multiple Commands

A parse handles only the input before the first command separator. Split raw text to execute several commands using an existing parser:

```csharp
foreach (var line in "greet Ada | greet Grace".SplitCommandLines())
{
    await parser.ParseAndExecute(line);
}
```

## Parser Options

Use `SimpleParserOptions.Standard with { ... }` to customize parsing. `StrictCommandName` and `StrictOptionName` are presets that enable their respective flags independently.

| Property | Default | Meaning |
| --- | --- | --- |
| `ServiceProvider` | `null` | Resolves command instances and an optional `IConsoleService`. |
| `RequireStrictCommandName` | `false` | Requires a command name or alias; disables the default command. |
| `RequireStrictOptionName` | `false` | Rejects unknown option names, except for subcommands. |
| `DisplayUsage` | `true` | Includes usage text in help. |
| `DisplayCommandListAsHelp` | `false` | Uses a single-line name list for general help; command-specific help remains detailed. |
| `OmitOptionNamesForRequiredOptions` | `true` | Allows unnamed required values. |
| `AutoAlias` | `false` | Creates nonconflicting command initials and enables the help alias `h`. |
| `ReadCommandFromEnvironment` | `true` | Uses the `Command` variable when no command/help/version request is recognized. |
| `ArgumentDelimiter` | `"""` | Extra raw-value delimiter; an empty string disables it. |
| `SuppressConsoleOutput` | `false` | Suppresses parser output through both the console and `IConsoleService`. Command output is unaffected. |

## Parser API and State

Build a parser once and use it sequentially. Each parse clears the previous command, help/version flags, and errors. A command parse creates fresh root options. Command instances and edited help descriptions remain cached.

| Member | Meaning |
| --- | --- |
| `Parse(string)` / `Parse(string[])` | Returns true for a valid command or help/version request; false for a parsing error. |
| `Execute(token)` | Executes the stored command or writes help/errors/version. Repeated calls repeat the operation. |
| `ParseAndExecute(input, token)` | Parses, then executes or displays output, including help after a parse error. |
| `CurrentCommand` | Result of the latest parse; null before parsing, on error, or for help/version. |
| `DefaultCommandName` | Selected default name; null when disabled or no commands exist. |
| `HelpCommandName` | Null for no help request, empty for all commands, or a specific command name. |
| `VersionRequested` | Whether the latest parse requested version output. |
| `OriginalCommandLine` | Raw input, or array elements joined with spaces for diagnostics. |
| `ParserOptions` / `RequireStrictOptionName` | Parser configuration and the strict-option flag. |
| `NameToCommand` / `AliasToCommand` | Case-insensitive command lookups. |
| `TryGetCommand(name, out command)` | Looks up a full command name, not an alias. |
| `TryGetOption(commandName, longName, out option)` | Looks up a long option name under a full command name. |
| `ShowHelp(commandName)` | Writes help. Null uses `HelpCommandName`; empty or unknown names target all commands. |
| `ShowVersion(prefix)` | Writes version output with an optional prefix. |
| `ShowCommandList(maxColumnWidth)` | Writes columns, capped at 19 characters by default. Redirected output uses an 80-character width. Zero writes a blank line; negative widths throw. |
| `AddErrorMessage(message)` | Adds text to the next help output; the next parse clears it. |
| `AddOptionClassUsage(optionClass)` | Adds a nested type to the current help traversal, deduplicated by type. `ShowHelp` rebuilds this list. |

`SimpleParser.Command` exposes the cached `CommandInstance` and its `OptionClass`. The latter exposes `OptionInstance`, `DefaultInstance`, `Options`, and `RemainingArguments`. `SimpleParser.Option` exposes member metadata and editable `Description` / `DefaultValueText` for help customization.

The low-level `OptionClass.Parse` and `Option.Parse` methods process raw tokens, unlike `SimpleParser.Parse(string[])`. `OptionClass.Parse` updates its existing instance; `Option.Parse` assigns one value without updating `ValueIsSet`. Prefer the parser or builder APIs for normal use.

Parsing errors request help; `Parse()` itself does not print it. Registration errors and user-code exceptions can still throw. To display parser errors, call `parser.Parse(args)` followed by `await parser.Execute(token)`. To handle errors yourself, branch on the parse result and distinguish `CurrentCommand` from help/version requests.

Parsers and builders contain mutable state. Complete command execution before parsing again on the same instance. Use separate parsers for concurrent work; DI may still share command instances, so their own concurrency rules apply.

## Standalone Options

`SimpleParserBuilder.TryParseOptions<TOptions>` parses without a command and automatically registers the root type. Register nested types first. Both raw-string and pre-split-array overloads use `SimpleParserOptions.Standard`.

```csharp
var builder = new SimpleParserBuilder();
if (builder.TryParseOptions<GreetOptions>(new[] { "-name", "Ada Lovelace" }, out var options))
{
    Console.WriteLine(options.Name);
}
```

This API is intentionally permissive: unknown names and invalid optional values are ignored. Missing required values or instance-creation failure return false. Invalid type metadata or missing nested registration may throw. Use a command parser's `Parse()` when all conversion errors must fail.

Pass an existing instance as the third argument to update it. Unspecified members are retained, but required values must be supplied on every call. Updates are not transactional: the instance may already be partly changed when parsing returns false.

## NativeAOT and Trimming

`SimpleParserBuilder` uses typed interface dispatch and preserves reflection metadata needed for option creation and member access. Arc.Unit's generic registration extensions use the same registration implementation.

| Registration | Purpose |
| --- | --- |
| `AddCommand<TCommand>()` | Registers a command without typed options. |
| `AddCommand<TCommand, TOptions>()` | Registers a command and its root options type. |
| `AddOptions<TOptions>()` | Preserves an options type and its base types; call for every nested type. |
| `Build(parserOptions)` | Creates an independent parser from a registration snapshot, in insertion order. |

Repeated registration with the same command/options pairing is safe. A conflicting pairing throws. Later builder changes do not affect an existing parser. All nested types must be registered even when no value is supplied for them in a particular invocation.

The `SimpleParser(IEnumerable<Type>, ...)` constructor, public `SimpleParser.Command` constructor, static `SimpleParser.ParseAndExecute` / `TryParseOptions`, and legacy command-group constructor use runtime discovery. They carry `RequiresUnreferencedCode` and are intended for untrimmed applications. Migrate those calls to typed registration for trimming or NativeAOT. DI providers and serializers must also support the publishing mode.

The library enables `IsAotCompatible` analysis. Publish the QuickStart sample on Windows x64:

```powershell
dotnet publish QuickStart/QuickStart.csproj -c Release -r win-x64 -p:PublishAot=true -o artifacts/quickstart
./artifacts/quickstart/QuickStart.exe test -text example
```

The repository's QuickStart sample uses the command name `test`. For Linux, use `-r linux-x64` and run the executable without `.exe`. NativeAOT requires the platform's native toolchain; see Microsoft's [NativeAOT prerequisites](https://learn.microsoft.com/dotnet/core/deploying/native-aot/).

## Arc.Unit Integration

Import `SimpleCommandLine` to enable extensions on `IUnitConfigurationContext`. One generic registration adds Arc.Unit membership, DI registration, and parser metadata:

```csharp
using Arc.Unit;
using SimpleCommandLine;

var unitBuilder = new UnitBuilder();
unitBuilder.Configure(context => context.AddCommand<GreetCommand, GreetOptions>());
var unit = unitBuilder.Build();
await unit.Context.CreateSimpleParser().ParseAndExecute(args);
```

This snippet reuses the greeting types from Quick Start. Registration overloads accept `ServiceLifetime` (default: `Scoped`) and return whether the command was newly added to the selected list. An existing DI registration, lifetime, or service instance is retained.

| Registration | Parser creation | Membership |
| --- | --- | --- |
| `context.AddCommand<TCommand, TOptions>()` | `unit.Context.CreateSimpleParser()` | Top-level `Commands`. |
| `context.AddSubcommand<TCommand, TOptions>()` | `unit.Context.CreateSimpleSubcommandParser()` | Separate `Subcommands` list. |
| `context.GetSimpleCommandGroup<TParent>().AddCommand<TCommand, TOptions>()` | `unit.Context.CreateSimpleParser<TParent>()` | Children of `TParent`. |

Each registration has a `TCommand`-only overload. Use `context.AddOptionType<TOptions>()` for each nested type; this registers metadata, not an options instance in DI. Register a parent separately to choose its list. Sharing metadata across groups does not add a command to other membership lists.

The unit collects metadata through `IUnitCustomContext` and registers an immutable `SimpleCommandRegistry` singleton when configuration is finalized. Finish all registration during `UnitBuilder.Configure`. Changes through retained contexts or group builders after finalization throw. Different units have separate registries.

Every creation call returns a fresh parser. The registry can also create a selected parser with `registry.CreateParser(commandTypes, parserOptions)`. That direct call uses standard options and does not add the unit's provider automatically. UnitContext extension methods do add that provider as a fallback. `CreateSimpleParser<TParent>()` uses standard parser settings, not `SimpleCommandGroup` defaults.

For a specific DI scope, supply its provider:

```csharp
using Microsoft.Extensions.DependencyInjection;

using var scope = unit.Context.ServiceProvider.CreateScope();
var parser = unit.Context.CreateSimpleParser(SimpleParserOptions.Standard with
{
    ServiceProvider = scope.ServiceProvider,
});
await parser.ParseAndExecute(args);
```

Each parser caches its resolved commands. Do not reuse a parser after its DI scope has been disposed.

Arc.Unit's raw `AddCommand(typeof(...))` / `AddSubcommand(typeof(...))` calls do not record parser metadata. Generic registration can supplement a raw entry while retaining DI registration. Missing typed metadata or nested types produce an error when the parser is created.

## Command Groups

Derive from `SimpleCommandGroup<TCommand>` and set `IsSubcommand = true`. This complete example registers a parent and child once and keeps child resolution in the parent's DI scope:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;
using SimpleCommandLine;

var builder = new UnitBuilder();
builder.Configure(context =>
{
    context.AddCommand<DbCommand>();
    context.GetSimpleCommandGroup<DbCommand>().AddCommand<DbListCommand>();
});

var unit = builder.Build();
using var scope = unit.Context.ServiceProvider.CreateScope();
await unit.Context.CreateSimpleParser(SimpleParserOptions.Standard with
{
    ServiceProvider = scope.ServiceProvider,
}).ParseAndExecute(args);

[SimpleCommand("db", IsSubcommand = true)]
public class DbCommand : SimpleCommandGroup<DbCommand>
{
    public DbCommand(SimpleCommandRegistry registry, UnitContext context, IServiceProvider services)
        : base(registry, context, "list", SimpleParserOptions.Standard with
        {
            ServiceProvider = services,
            RequireStrictCommandName = true,
            RequireStrictOptionName = true,
            DisplayUsage = false,
            DisplayCommandListAsHelp = true,
        })
    {
    }
}

[SimpleCommand("list")]
public class DbListCommand : ISimpleCommand
{
    public Task Execute(string[] args, CancellationToken cancellationToken)
    {
        Console.WriteLine("List command");
        return Task.CompletedTask;
    }
}
```

Both `db list` and `db` execute the list command. `defaultArgument` is a raw command line, so it may include options. It is used only for an empty argument array; null leaves the input empty. Cancellation is forwarded to the selected child.

When `parserOptions` is null, group defaults require strict command and option names, disable usage text, and show a single-line list for general help. A supplied record replaces these defaults; only a missing service provider falls back to the unit's provider. The group's `Parser` is created lazily and cached.

For nested groups, register the child group with `context.GetSimpleCommandGroup<TParent>().AddCommand<TChildGroup>()`, then register that child's commands through its own group builder.

The constructor taking a standalone `SimpleParserBuilder` remains available. The legacy `ConfigureGroup(context, parentCommandType)` API registers Arc.Unit membership only: a null parent adds the command to the separate **subcommand list**, not the top-level list. It does not populate the shared registry. Prefer generic context/group extensions for shared registration.

## Helper Methods

| Method | Meaning |
| --- | --- |
| `GetCommandLineArguments()` | Cached process command line with the executable path removed. |
| `ExtractArguments(commandLine)` | Removes an executable path from Environment.CommandLine-style text. |
| `PeekCommand(commandLine)` | First whitespace-delimited word, or empty for blank input or a word starting with `-`; does not parse syntax. |
| `SplitArguments(commandLine, delimiter)` | Raw tokens with enclosing quotes/braces retained. An empty delimiter argument selects triple quotes. |
| `SplitCommandLines(commandLine, delimiter)` | Splits at unenclosed `\|` and rejoins each command's tokens with spaces. |
| `SplitAtSpace(text)` | Splits at whitespace without interpreting quotes or braces. |
| `JoinWithSpace(values)` | Joins with spaces without quoting; reparsing may lose argument boundaries. |
| `TrimQuotes(text)` / `TrimQuotesAndBraces(text)` | Trims surrounding whitespace and removes recognized wrappers; does not unescape values. |
| `UnwrapDoubleQuote(text)` / `UnwrapBraces(text)` | Removes a matching wrapper without trimming whitespace. |
| `ProcessArgument(argument, parserOptions, processing)` | Unwraps raw values and applies newline/escape handling. |
| `IsOptionName(text)` | Detects a leading `-`, except negative numeric forms. |
| `CreateAliasFromCommand(commandName)` | Hyphen-separated initials without checking registration conflicts. |
| `TryGetAndRemoveArgument(ref args, name, out value)` | Removes a matching option/value pair before the first command separator. |
| `AppendEnvironmentVariable(ref args, name)` | Appends one literal array element or unescaped text to a command-line string. |

## Tests and Coverage

From the repository root:

```powershell
dotnet build SimpleCommandLine.slnx -c Release
dotnet test --project xUnitTest/xUnitTest.csproj -c Release --coverage --coverage-settings xUnitTest/coverage.config --coverage-output-format cobertura --coverage-output "$PWD/artifacts/coverage/coverage.cobertura.xml"
```

Coverage includes `SimpleCommandLine.dll` and its generated code, excluding other assemblies. CI uploads the Cobertura report as `code-coverage`.

NativeAOT smoke tests treat compiler and trimming warnings as errors and run in CI on Windows x64 and Linux x64:

```powershell
dotnet publish Tests/NativeAotTest/NativeAotTest.csproj -c Release -r win-x64 -o artifacts/native-aot
./artifacts/native-aot/NativeAotTest.exe --require-aot
```

For Linux, use `-r linux-x64` and run `./artifacts/native-aot/NativeAotTest --require-aot`.

## License

MIT. See [LICENSE](LICENSE).

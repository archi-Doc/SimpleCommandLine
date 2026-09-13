# SimpleCommandLine Name Changes

Public API names were changed after version 0.46.0 to be clearer and more consistent.
This is a **source-breaking** change. Parsing behavior, command-line syntax, and help output are unchanged.
No `[Obsolete]` forwarding members are provided. Update your code with the tables below.

## Quick migration

1. Apply the replacements under [Safe find-and-replace](#safe-find-and-replace).
2. Review the members under [Context-dependent renames](#context-dependent-renames). The same old name maps to different new names depending on the owning type.
3. Build your project and fix the remaining errors (named arguments, string assertions on error messages).

## Safe find-and-replace

These old names are unique, so a whole-word replacement is safe.

| Old | New | Kind | Owner |
| --- | --- | --- | --- |
| `IsSubcommand` | `IsCommandGroup` | Property | `SimpleCommandAttribute`, `SimpleParser.Command` |
| `AddOptions<TOptions>()` | `AddOptionsType<TOptions>()` | Method | `SimpleParserBuilder` |
| `AddOptionType<TOptions>()` | `AddOptionsType<TOptions>()` | Extension method | `UnitCommandExtensions` (`IUnitConfigurationContext`) |
| `SimpleParser.OptionClass` | `SimpleParser.OptionSet` | Nested class | `SimpleParser` |
| `AddOptionClassUsage(OptionClass)` | `AddOptionSetUsage(OptionSet)` | Method | `SimpleParser` |
| `OptionTypeIdentifier` | `OptionsTypeIdentifier` | Property | `SimpleParser.OptionSet` |
| `OptionInstance` | `Instance` | Property | `SimpleParser.OptionSet` |
| `ValueIsSet` | `IsValueSet` | Property | `SimpleParser.Option` |
| `VersionRequested` | `IsVersionRequested` | Property | `SimpleParser` |
| `CommandEnvironmentVariable` | `CommandEnvironmentVariableName` | Constant | `SimpleParser` |
| `ConfigureGroup(...)` | `RegisterAndGetChildGroup(...)` | Static method | `SimpleCommandGroup<TSelf>` |
| `RequireStrictCommandName` | `RequireCommandName` | Property | `SimpleParserOptions` |
| `RequireStrictOptionName` | `RejectUnknownOptionNames` | Property | `SimpleParserOptions`, `SimpleParser` |
| `StrictCommandName` | `CommandNameRequired` | Static preset | `SimpleParserOptions` |
| `StrictOptionName` | `UnknownOptionNamesRejected` | Static preset | `SimpleParserOptions` |
| `OmitOptionNamesForRequiredOptions` | `AllowPositionalRequiredOptions` | Property | `SimpleParserOptions` |
| `AutoAlias` | `GenerateAliases` | Property | `SimpleParserOptions` |
| `TryGetAndRemoveArgument` | `TryGetAndRemoveOptionValue` | Static method | `SimpleParserHelper` |
| `UnwrapDoubleQuote` | `UnwrapDoubleQuotes` | Static method | `SimpleParserHelper` |
| `PeekCommand` | `PeekCommandName` | Static method | `SimpleParserHelper` |
| `CreateAliasFromCommand` | `CreateAliasFromCommandName` | Static method | `SimpleParserHelper` |
| `SplitAtSpace` | `SplitAtWhitespace` | Extension method | `SimpleParserHelper` |

Replace `RequireStrictCommandName` / `RequireStrictOptionName` **before** `StrictCommandName` / `StrictOptionName`, or use whole-word matching.

## Context-dependent renames

| Old | New | Owner | Notes |
| --- | --- | --- | --- |
| `Command.OptionClass` | `Command.OptionSet` | `SimpleParser.Command` | e.g. `parser.CurrentCommand!.OptionSet.Instance` |
| `Option.OptionClass` | `Option.NestedOptionSet` | `SimpleParser.Option` | Null unless the option is a nested options type. |
| `Command.OptionType` | `Command.OptionsType` | `SimpleParser.Command` | Type of the options class. |
| `OptionClass.OptionType` | `OptionSet.OptionsType` | `SimpleParser.OptionSet` | Type of the options class. |
| `Option.OptionType` | `Option.DeclaredType` | `SimpleParser.Option` | Declared type of the field or property (not the options class). |
| `SimpleOptionAttribute.Required` | `SimpleOptionAttribute.IsRequired` | Attribute property | `[SimpleOption("name", Required = true)]` → `[SimpleOption("name", IsRequired = true)]` |
| `Option.Required` | `Option.IsRequired` | `SimpleParser.Option` | |

Typical before/after:

```csharp
// Before
[SimpleCommand("db", IsSubcommand = true)]
var options = (MyOptions)parser.CurrentCommand!.OptionClass.OptionInstance!;
var builder = new SimpleParserBuilder().AddOptions<NestedOptions>();
var settings = SimpleParserOptions.Standard with { RequireStrictCommandName = true, AutoAlias = true };

// After
[SimpleCommand("db", IsCommandGroup = true)]
var options = (MyOptions)parser.CurrentCommand!.OptionSet.Instance!;
var builder = new SimpleParserBuilder().AddOptionsType<NestedOptions>();
var settings = SimpleParserOptions.Standard with { RequireCommandName = true, GenerateAliases = true };
```

## Parameter names

These matter only when you pass the argument by name.

| Member | Old parameter | New parameter |
| --- | --- | --- |
| `SimpleCommandGroup<TSelf>` constructors (all three) | `defaultArgument` | `defaultCommandLine` |
| `SimpleParserHelper.AppendEnvironmentVariable(ref string, string)` | `args` | `commandLine` |
| `SimpleParser.TryGetOption(string, string, out Option)` | `optionName` | `longOptionName` |
| `SimpleParser.AddOptionSetUsage(OptionSet)` | `optionClass` | `optionSet` |

## Type parameter names

No source changes are required; only XML documentation references (`cref`) may need updating.

| Member | Old | New |
| --- | --- | --- |
| `SimpleCommandGroup<T>` | `TCommand` | `TSelf` |
| `UnitCommandExtensions.GetSimpleCommandGroup<T>` | `TCommand` | `TParentCommand` |
| `UnitCommandExtensions.CreateSimpleParser<T>` | `TCommand` | `TParentCommand` |

## Error message text

Registration error messages now name the new method. Update tests that assert on this text:

- `Call SimpleParserBuilder.AddOptions<T>()` → `Call SimpleParserBuilder.AddOptionsType<T>()`
- `IUnitConfigurationContext.AddOptionType<T>()` → `IUnitConfigurationContext.AddOptionsType<T>()`

## Regex helpers

Whole-word replacements (for example, in VS Code with regex enabled), applied in this order:

```text
\bRequireStrictCommandName\b      -> RequireCommandName
\bRequireStrictOptionName\b       -> RejectUnknownOptionNames
\bStrictCommandName\b             -> CommandNameRequired
\bStrictOptionName\b              -> UnknownOptionNamesRejected
\bIsSubcommand\b                  -> IsCommandGroup
\bAddOptions(?=<)                 -> AddOptionsType
\bAddOptionType(?=<)              -> AddOptionsType
\bOptionClass\.OptionInstance\b   -> OptionSet.Instance
\bOptionInstance\b                -> Instance
\bValueIsSet\b                    -> IsValueSet
\bVersionRequested\b              -> IsVersionRequested
\bRequired = (true|false)\b       -> IsRequired = $1
```

After that, `OptionClass` and `OptionType` still need the context-dependent review above.

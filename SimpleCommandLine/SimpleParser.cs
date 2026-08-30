// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Arc;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;
using Tinyhand;

namespace SimpleCommandLine;

/// <summary>
/// A simple command-line parser.
/// </summary>
public class SimpleParser : ISimpleParser
{
    /// <summary>
    /// The command/option name which displays a help message.
    /// </summary>
    public const string HelpString = "help";

    /// <summary>
    /// The alias of <see cref="HelpString"/>, available when <see cref="SimpleParserOptions.AutoAlias"/> is enabled.
    /// </summary>
    public const string HelpAlias = "h";

    /// <summary>
    /// The command/option name which displays the version.
    /// </summary>
    public const string VersionString = "version";

    /// <summary>
    /// The character which separates a command line into multiple command lines.
    /// </summary>
    public const char Separator = '|';

    /// <summary>
    /// The character which separates arguments (like whitespace, but it does not produce a separator token).
    /// </summary>
    public const char Separator2 = ',';

    /// <summary>
    /// The string representation of <see cref="Separator"/>.
    /// </summary>
    public const string SeparatorString = "|";

    /// <summary>
    /// The name of the environment variable which holds the command name (see <see cref="SimpleParserOptions.ReadCommandFromEnvironment"/>).
    /// </summary>
    public const string CommandString = "Command";

    /// <summary>
    /// Gets the default argument delimiter (a triple quote).
    /// </summary>
    public static ReadOnlySpan<char> DefaultDelimiter => "\"\"\"";

    internal const string ExecuteMethodString = "Execute";
    internal const string IndentString = "  ";
    internal const string IndentString2 = "    ";
    internal const int DefaultWindowWidth = 80;
    internal const int MaxWindowWidth = 1024;
    internal const char OpenBracket = '{'; // '['
    internal const char CloseBracket = '}'; // ']'
    internal const char Quote = '\"';
    internal const string TripleQuotes = "\"\"\"";
    internal const char SingleQuote = '\'';
    internal const char OptionPrefix = '-';
    internal const char DelimiterChar = (char)0x1E; // Record separator

    private static readonly TinyhandSerializerOptions SerializerOptions = TinyhandSerializerOptions.ConvertToStrictString;

    private class HollowParser : ISimpleParser
    {
        public HollowParser(SimpleParserOptions parserOptions)
        {
            this.ParserOptions = parserOptions;
        }

        public SimpleParserOptions ParserOptions { get; }

        public void AddErrorMessage(string message)
        {
        }

        public void TryAddOptionClassUsage(OptionClass optionClass)
        {
        }
    }

    /// <summary>
    /// Parses the arguments into an options class, without registering any command.<br/>
    /// The arguments are joined with a space, so quoting is lost; use the <see cref="string"/> overload to preserve it.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options class.</typeparam>
    /// <param name="args">The arguments.</param>
    /// <param name="options">When this method returns, contains the parsed options.</param>
    /// <param name="original">The instance to update. Use <see langword="null"/> to create a new instance.</param>
    /// <returns><see langword="true"/> if the options are created (unknown option names are ignored).</returns>
    public static bool TryParseOptions<TOptions>(string[] args, [MaybeNullWhen(false)] out TOptions options, TOptions? original = default)
        => TryParseOptions(string.Join(' ', args), out options, original);

    /// <summary>
    /// Parses the arguments into an options class, without registering any command.
    /// </summary>
    /// <typeparam name="TOptions">The type of the options class.</typeparam>
    /// <param name="args">The arguments.</param>
    /// <param name="options">When this method returns, contains the parsed options.</param>
    /// <param name="original">The instance to update. Use <see langword="null"/> to create a new instance.</param>
    /// <returns>
    /// <see langword="true"/> if the options are created. Unknown option names and values that cannot be converted
    /// are ignored; only a missing required value results in <see langword="false"/>.
    /// </returns>
    public static bool TryParseOptions<TOptions>(string args, [MaybeNullWhen(false)] out TOptions options, TOptions? original = default)
    {
        var parser = new HollowParser(SimpleParserOptions.Standard);

        var optionClass = new OptionClass(parser, typeof(TOptions), null);
        if (original != null)
        {
            optionClass.optionInstance = original;
        }

        optionClass.Parse(args.FormatArguments(parser.ParserOptions.ArgumentDelimiter), 0, true);
        if (optionClass.FatalError)
        {
            options = default;
            return false;
        }

        options = (TOptions)optionClass.OptionInstance!;
        return options != null;
    }

    /// <summary>
    /// A registered command: the command type, its metadata and its options.
    /// </summary>
    public class Command
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Command"/> class.
        /// </summary>
        /// <param name="parser">The parser which owns this command.</param>
        /// <param name="commandType">The command type. It must implement <see cref="ISimpleCommand"/> or <see cref="ISimpleCommand{TOption}"/>.</param>
        /// <param name="attribute">The <see cref="SimpleCommandAttribute"/> of the command type.</param>
        /// <exception cref="InvalidOperationException">The command type is not a valid command.</exception>
        public Command(SimpleParser parser, Type commandType, SimpleCommandAttribute attribute)
        {
            const string MultipleInterfacesException = "Type {0} can implement only single ISimpleCommand interface.";

            this.Parser = parser;
            this.CommandType = commandType;
            this.CommandName = attribute.CommandName;
            this.Alias = attribute.Alias;
            this.Default = attribute.Default;
            this.Description = attribute.Description;
            this.IsSubcommand = attribute.IsSubcommand;

            if (this.CommandName == string.Empty)
            {
                this.Default = true;
            }

            foreach (var y in commandType.GetInterfaces())
            {
                if (y == typeof(ISimpleCommand))
                {
                    if (this.CommandInterface == null)
                    {
                        this.CommandInterface = y;
                    }
                    else
                    {
                        throw new InvalidOperationException(string.Format(MultipleInterfacesException, commandType.ToString()));
                    }
                }
                else if (y.IsGenericType)
                {
                    var z = y.GetGenericTypeDefinition();
                    if (z == typeof(ISimpleCommand<>))
                    {
                        if (this.CommandInterface == null)
                        {
                            this.CommandInterface = z;
                            this.OptionType = y.GetGenericArguments()[0];
                        }
                        else
                        {
                            throw new InvalidOperationException(string.Format(MultipleInterfacesException, commandType.ToString()));
                        }
                    }
                }
            }

            if (this.CommandInterface == null)
            {
                throw new InvalidOperationException($"Type \"{commandType.ToString()}\" must implement ISimpleCommand or ISimpleCommand<TOption>.");
            }

            var constructor = this.CommandType.GetConstructor(Type.EmptyTypes);
            if (this.Parser.ParserOptions.ServiceProvider == null && constructor == null)
            {
                throw new InvalidOperationException($"Default constructor (parameterless constructor) is required for type '{commandType.ToString()}'.");
            }

            this.constructorInvoker = constructor is null ? null : ConstructorInvoker.Create(constructor);

            var mi = this.FindMethod();
            if (mi == null)
            {// No Execute method
                throw new InvalidOperationException($"{ExecuteMethodString}() method is required in Type {this.CommandType.ToString()}.");
            }

            this.executeInvoker = MethodInvoker.Create(mi);
            this.hasOptionType = this.CommandInterface == typeof(ISimpleCommand<>);
            this.OptionClass = new OptionClass(this.Parser, this.OptionType, null);
        }

        /// <summary>
        /// Executes the command with the options and the remaining arguments of the last parse.
        /// </summary>
        /// <param name="cancellationToken">A token used to cancel the command execution.</param>
        /// <returns>A task that represents the command execution.</returns>
        public Task Execute(CancellationToken cancellationToken)
        {
            var args = this.OptionClass.RemainingArguments ?? Array.Empty<string>();
            var task = this.hasOptionType ?
                this.executeInvoker.Invoke(this.CommandInstance, this.OptionClass.OptionInstance, args, cancellationToken) : // Task Execute(Options option, string[] args, CancellationToken cancellationToken);
                this.executeInvoker.Invoke(this.CommandInstance, args, cancellationToken); // Task Execute(string[] args, CancellationToken cancellationToken);

            return (Task?)task ?? Task.CompletedTask;
        }

        /// <summary>
        /// Gets the parser which owns this command.
        /// </summary>
        public SimpleParser Parser { get; }

        /// <summary>
        /// Gets the type of the command class.
        /// </summary>
        public Type CommandType { get; }

        /// <summary>
        /// Gets the implemented interface: <see cref="ISimpleCommand"/> or the generic definition of <see cref="ISimpleCommand{TOption}"/>.
        /// </summary>
        public Type CommandInterface { get; }

        /// <summary>
        /// Gets the type of the options class, or <see langword="null"/> if the command has no options.
        /// </summary>
        public Type? OptionType { get; }

        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// Gets the alternate name of the command (<see cref="string.Empty"/> if there is none).
        /// </summary>
        public string Alias { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this command is executed when the command name is not specified.
        /// </summary>
        public bool Default { get; internal set; }

        /// <summary>
        /// Gets or sets the description shown in a help message.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets a value indicating whether this command is a subcommand (it accepts unknown option names).
        /// </summary>
        public bool IsSubcommand { get; }

        /// <summary>
        /// Gets the options of this command.
        /// </summary>
        public OptionClass OptionClass { get; }

        /// <summary>
        /// Gets the command instance, creating it on the first access
        /// (from <see cref="SimpleParserOptions.ServiceProvider"/> if available, otherwise from the parameterless constructor).
        /// </summary>
        public object CommandInstance
        {
            get
            {
                if (this.commandInstance is null)
                {
                    this.commandInstance = this.Parser.ParserOptions.ServiceProvider?.GetService(this.CommandType);
                    this.commandInstance ??= this.constructorInvoker is not null ?
                        this.constructorInvoker.Invoke() :
                        Activator.CreateInstance(this.CommandType)!;
                }

                return this.commandInstance;
            }
        }

        /// <summary>
        /// Appends the name, the description and the options of this command to a help message.
        /// </summary>
        /// <param name="sb">The destination.</param>
        internal void AppendCommand(StringBuilder sb)
        {
            sb.AppendLine($"{this.CommandName} {this.Description}");

            this.OptionClass.AppendOption(sb, false);
        }

        private readonly ConstructorInvoker? constructorInvoker;
        private readonly MethodInvoker executeInvoker;
        private readonly bool hasOptionType;
        private object? commandInstance;

        private MethodInfo? FindMethod()
        {
            var withOptions = this.CommandInterface == typeof(ISimpleCommand<>);
            foreach (var x in this.CommandType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (x.Name != ExecuteMethodString || x.ReturnType != typeof(Task))
                {
                    continue;
                }

                var parameters = x.GetParameters();
                if (withOptions)
                {// Task Execute(Options option, string[] args, CancellationToken cancellationToken);
                    if (parameters.Length == 3 &&
                        parameters[0].ParameterType == this.OptionType &&
                        parameters[1].ParameterType == typeof(string[]) &&
                        parameters[2].ParameterType == typeof(CancellationToken))
                    {
                        return x;
                    }
                }
                else
                {// Task Execute(string[] args, CancellationToken cancellationToken);
                    if (parameters.Length == 2 &&
                        parameters[0].ParameterType == typeof(string[]) &&
                        parameters[1].ParameterType == typeof(CancellationToken))
                    {
                        return x;
                    }
                }
            }

            return null;
        }
    }

    /// <summary>
    /// An options class: the set of <see cref="Option"/> declared by an options type, and the instance holding the parsed values.
    /// </summary>
    public class OptionClass
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OptionClass"/> class.
        /// </summary>
        /// <param name="parser">The parser which collects error messages.</param>
        /// <param name="optionType">The type of the options class. Use <see langword="null"/> for a command without options.</param>
        /// <param name="optionStack">The types being processed, used to detect a circular dependency.</param>
        /// <exception cref="InvalidOperationException">The options type is not valid (circular dependency, duplicate option name, and so on).</exception>
        internal OptionClass(ISimpleParser parser, Type? optionType, Stack<Type>? optionStack)
        {
            optionStack ??= new();
            if (optionType != null)
            {
                if (optionStack.Contains(optionType))
                {
                    var s = string.Join(SimpleParser.OptionPrefix, optionStack.Select(x => x.Name));
                    throw new InvalidOperationException($"Circular dependency of option classes is detected ({s}).");
                }
                else
                {
                    optionStack.Push(optionType);
                }
            }

            this.Parser = parser;
            if (optionType is not null)
            {
                this.OptionType = optionType;
                this.OptionTypeIdentifier = TinyhandTypeIdentifier.GetTypeIdentifier(optionType);
            }

            this.LongNameToOption = new(StringComparer.OrdinalIgnoreCase);
            this.ShortNameToOption = new(StringComparer.OrdinalIgnoreCase);
            if (this.OptionType is null)
            {
                this.Options = new(0);
            }
            else
            {
                var constructor = this.OptionType.GetConstructor(Type.EmptyTypes);
                if (constructor is null && !TinyhandTypeIdentifier.IsRegistered(this.OptionTypeIdentifier))
                {
                    throw new InvalidOperationException($"Default constructor (parameterless constructor) is required for type '{this.OptionType.ToString()}'.");
                }

                this.constructorInvoker = constructor is null || this.OptionType.IsAbstract ? null : ConstructorInvoker.Create(constructor);

                var members = GetOptionMembers(this.OptionType);
                this.Options = new(members.Length);
                foreach (var (memberInfo, optionAttribute) in members)
                {
                    var option = new Option(this.Parser, this.OptionType, memberInfo, optionAttribute, optionStack);
                    this.Options.Add(option);

                    if (!this.LongNameToOption.TryAdd(option.LongName, option))
                    {
                        throw new InvalidOperationException($"Long option name '{option.LongName}' ({this.OptionType.ToString()}) already exists.");
                    }

                    if (option.ShortName != null && !this.ShortNameToOption.TryAdd(option.ShortName, option))
                    {
                        throw new InvalidOperationException($"Short option name '{option.ShortName}' ({this.OptionType.ToString()}) already exists.");
                    }

                    this.hasRequiredOption |= option.Required;
                    this.hasEnvironmentOption |= option.ReadFromEnvironment;
                }
            }

            if (optionType != null)
            {
                optionStack.Pop();
            }
        }

        /// <summary>
        /// Parses the arguments and sets the values of <see cref="OptionInstance"/>.<br/>
        /// Arguments which are not consumed as an option are stored in <see cref="RemainingArguments"/>.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <param name="start">The index at which parsing starts.</param>
        /// <param name="acceptUnknownOptionName">
        /// <see langword="true"/> to ignore an unknown option name even when <see cref="SimpleParserOptions.RequireStrictOptionName"/> is enabled.
        /// </param>
        /// <returns><see langword="true"/> if the arguments are successfully parsed.</returns>
        public bool Parse(string[] args, int start, bool acceptUnknownOptionName)
        {
            var errorFlag = false;
            List<string>? remaining = null;
            var options = CollectionsMarshal.AsSpan(this.Options);
            var longLookup = this.LongNameToOption.GetAlternateLookup<ReadOnlySpan<char>>();
            var shortLookup = this.ShortNameToOption.GetAlternateLookup<ReadOnlySpan<char>>();

            foreach (var x in options)
            {
                x.Reset();
            }

            for (var n = start; n < args.Length; n++)
            {
                if (args[n].IsOptionString())
                {// -option
                    var name = args[n].AsSpan().Trim(SimpleParser.OptionPrefix);
                    if (!longLookup.TryGetValue(name, out var option))
                    {
                        shortLookup.TryGetValue(name, out option);
                    }

                    if (option != null)
                    {// Option found
                        if (n + 1 < args.Length)
                        {
                            if (!args[n + 1].IsOptionString())
                            {
                                n++;
                                if (option.Parse(args[n], this.OptionInstance, acceptUnknownOptionName))
                                {
                                    option.ValueIsSet = true;
                                }
                                else
                                {// Parse error
                                    this.Parser.AddErrorMessage($"Could not convert '{args[n]}' to Type '{option.OptionType.Name}' ({args[n - 1]} {args[n]})");
                                    errorFlag = true;
                                }
                            }
                            else
                            {
                                this.Parser.AddErrorMessage($"No corresponding value found for option '{option.LongName}'");
                                errorFlag = true;
                            }
                        }
                        else
                        {// The value for the option '' is required.
                            this.Parser.AddErrorMessage($"No corresponding value found for option '{option.LongName}'");
                            errorFlag = true;
                        }
                    }
                    else
                    {// Option not found
                        (remaining ??= new()).Add(args[n]);

                        if (this.Parser.ParserOptions.RequireStrictOptionName && !acceptUnknownOptionName)
                        {
                            if (this.OptionType == null)
                            {
                                this.Parser.AddErrorMessage($"Option '{name}' is invalid");
                            }
                            else
                            {
                                this.Parser.AddErrorMessage($"Option '{name}' is not found in Type: {this.OptionType.ToString()}");
                            }

                            errorFlag = true;
                        }
                    }
                }
                else if (args[n].Length == 1 && args[n][0] == SimpleParser.Separator)
                {// '|' Separator
                    break;
                }
                else
                {
                    if (this.hasRequiredOption &&
                        this.Parser.ParserOptions.OmitOptionNamesForRequiredOptions &&
                        FindUnsetRequiredOption(options) is { } option)
                    {
                        if (option.Parse(args[n], this.OptionInstance, acceptUnknownOptionName))
                        {
                            option.ValueIsSet = true;
                        }
                        else if (n > 0)
                        {// Parse error
                            this.Parser.AddErrorMessage($"Could not convert '{args[n]}' to Type '{option.OptionType.Name}' ({args[n - 1]} {args[n]})");
                            errorFlag = true;
                        }

                        continue;
                    }

                    (remaining ??= new()).Add(args[n]);
                }
            }

            if (this.hasEnvironmentOption)
            {
                this.ReadFromEnvironment(options, acceptUnknownOptionName);
            }

            foreach (var x in options)
            {
                if (x.ValueIsSet)
                {
                    continue;
                }

                if (x.Required)
                {// Value required.
                    this.Parser.AddErrorMessage($"Value is required for option '{x.LongName}' <{this.OptionType?.Name}>");
                    errorFlag = true;
                    this.FatalError = true;
                }

                if (x.OptionClass != null && this.OptionInstance is { } instance)
                {// Set instance.
                    x.OptionClass.optionInstance ??= x.GetValue(instance);
                    if (x.OptionClass.optionInstance != null)
                    {
                        x.ValueIsSet = true;
                    }
                    else if (x.OptionClass.OptionInstance is { } nested &&
                        x.SetValue(instance, nested))
                    {
                        x.ValueIsSet = true;
                    }
                }
            }

            if (errorFlag)
            {
                return false;
            }

            if (remaining is null)
            {
                this.RemainingArguments = Array.Empty<string>();
                return true;
            }

            var remainingArguments = new string[remaining.Count];
            for (var i = 0; i < remainingArguments.Length; i++)
            {
                remainingArguments[i] = SimpleParserHelper.ProcessArgument(remaining[i], this.Parser.ParserOptions, ArgumentProcessing.ReplaceNewlinesWithSpace);
            }

            this.RemainingArguments = remainingArguments;
            return true;

            static Option? FindUnsetRequiredOption(ReadOnlySpan<Option> options)
            {
                foreach (var x in options)
                {
                    if (x.Required && !x.ValueIsSet)
                    {
                        return x;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the type of the options class, or <see langword="null"/> if the command has no options.
        /// </summary>
        public Type? OptionType { get; }

        /// <summary>
        /// Gets the Tinyhand type identifier of <see cref="OptionType"/> (0 if it is not registered).
        /// </summary>
        public uint OptionTypeIdentifier { get; }

        /// <summary>
        /// Gets the options, in declaration order (base type first).
        /// </summary>
        public List<Option> Options { get; }

        /// <summary>
        /// Gets the options keyed by their long name (case insensitive).
        /// </summary>
        public Dictionary<string, Option> LongNameToOption { get; }

        /// <summary>
        /// Gets the options keyed by their short name (case insensitive).
        /// </summary>
        public Dictionary<string, Option> ShortNameToOption { get; }

        /// <summary>
        /// Gets the instance holding the parsed values, creating it on the first access.
        /// </summary>
        public object? OptionInstance => this.optionInstance ??= this.CreateInstance();

        /// <summary>
        /// Gets an untouched instance of the options class, used to display the default values in a help message.
        /// </summary>
        public object? DefaultInstance => this.defaultInstance ??= this.CreateInstance();

        /// <summary>
        /// Gets the arguments which were not consumed as an option, or <see langword="null"/> if <see cref="Parse"/> has not succeeded yet.
        /// </summary>
        public string[]? RemainingArguments { get; private set; }

        /// <summary>
        /// Gets the parser which collects error messages.
        /// </summary>
        internal ISimpleParser Parser { get; }

        /// <summary>
        /// Gets a value indicating whether the last parse failed because a required value is missing.
        /// </summary>
        internal bool FatalError { get; private set; }

        /// <summary>
        /// Appends the description of each option to a help message.
        /// </summary>
        /// <param name="sb">The destination.</param>
        /// <param name="addName"><see langword="true"/> to prepend the name of the options type.</param>
        internal void AppendOption(StringBuilder sb, bool addName)
        {
            if (addName)
            {
                sb.AppendLine($"{{{this.OptionType?.Name}}}");
            }

            var options = CollectionsMarshal.AsSpan(this.Options);
            if (options.Length == 0)
            {
                sb.AppendLine();
                return;
            }

            var maxWidth = 0;
            foreach (var x in options)
            {
                if (x.OptionText.Length > maxWidth)
                {
                    maxWidth = x.OptionText.Length;
                }
            }

            foreach (var x in options)
            {
                var padding = maxWidth - x.OptionText.Length;
                sb.Append(SimpleParser.IndentString);
                sb.Append(x.OptionText);
                for (var i = 0; i < padding; i++)
                {
                    sb.Append(' ');
                }

                sb.Append(SimpleParser.IndentString2);
                sb.Append(x.Description);

                if (x.Required)
                {
                    if (x.DefaultValueText is not null)
                    {
                        sb.Append($" (Required: {x.DefaultValueText})");
                    }
                    else
                    {
                        sb.Append(" (Required)");
                    }
                }
                else if (x.DefaultValueText is not null)
                {
                    if (x.OptionType == typeof(string))
                    {
                        sb.Append($" (Default: \"{x.DefaultValueText}\")");
                    }
                    else
                    {
                        sb.Append($" (Default: {x.DefaultValueText})");
                    }
                }
                else
                {
                    var value = x.GetValue(this.DefaultInstance);
                    if (value == null)
                    {
                        sb.Append(" (Optional)");
                    }
                    else if (x.OptionClass != null)
                    {
                    }
                    else if (value is string)
                    {
                        sb.Append($" (Default: \"{value}\")");
                    }
                    else
                    {
                        sb.Append($" (Default: {value})");
                    }
                }

                sb.AppendLine();

                if (x.OptionClass != null)
                {
                    this.Parser.TryAddOptionClassUsage(x.OptionClass);
                }
            }

            sb.AppendLine();
        }

        /// <summary>
        /// Replaces <see cref="OptionInstance"/> with a new instance, so that the values of a previous parse are discarded.
        /// </summary>
        internal void ResetOptionInstance() => this.optionInstance = this.CreateInstance();

        /// <summary>
        /// Gets the members annotated with <see cref="SimpleOptionAttribute"/> (base type -> derived type).<br/>
        /// The result is cached since reflection is relatively expensive.
        /// </summary>
        /// <param name="optionType">The option type.</param>
        /// <returns>An array of members and their attributes.</returns>
        private static (MemberInfo MemberInfo, SimpleOptionAttribute Attribute)[] GetOptionMembers(Type optionType)
            => OptionMembersCache.GetOrAdd(optionType, static type =>
            {
                var types = new List<Type>();
                for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
                {
                    types.Add(current);
                }

                var list = new List<(MemberInfo, SimpleOptionAttribute)>();
                for (var i = types.Count - 1; i >= 0; i--)
                {// Base type -> derived type
                    foreach (var x in types[i].GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (x.MemberType != MemberTypes.Field && x.MemberType != MemberTypes.Property)
                        {
                            continue;
                        }

                        if (x.GetCustomAttribute<SimpleOptionAttribute>(true) is { } attribute)
                        {
                            list.Add((x, attribute));
                        }
                    }
                }

                return list.ToArray();
            });

        /// <summary>
        /// Creates an instance of the option type (falls back to Tinyhand reconstruction when there is no parameterless constructor).
        /// </summary>
        /// <returns>A new instance, or <see langword="null"/> if the instance could not be created.</returns>
        private object? CreateInstance()
        {
            if (this.OptionType is null)
            {
                return null;
            }

            if (this.constructorInvoker is not null)
            {
                try
                {
                    return this.constructorInvoker.Invoke();
                }
                catch
                {
                }
            }

            return TinyhandTypeIdentifier.TryReconstruct(this.OptionTypeIdentifier);
        }

        private void ReadFromEnvironment(ReadOnlySpan<Option> options, bool acceptUnknownOptionName)
        {
            foreach (var x in options)
            {
                if (x.ValueIsSet || !x.ReadFromEnvironment)
                {
                    continue;
                }

                var env = x.ShortName is null ? null : Environment.GetEnvironmentVariable(x.ShortName);
                env ??= Environment.GetEnvironmentVariable(x.LongName);
                if (env is not null && x.Parse(env, this.OptionInstance, acceptUnknownOptionName))
                {
                    x.ValueIsSet = true;
                }
            }
        }

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401
        internal object? optionInstance;
#pragma warning restore SA1401
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter

        private static readonly ConcurrentDictionary<Type, (MemberInfo MemberInfo, SimpleOptionAttribute Attribute)[]> OptionMembersCache = new();

        private readonly ConstructorInvoker? constructorInvoker;
        private readonly bool hasRequiredOption;
        private readonly bool hasEnvironmentOption;
        private object? defaultInstance;
    }

    /// <summary>
    /// A single command-line option: the field or property it maps to, and the metadata of its <see cref="SimpleOptionAttribute"/>.
    /// </summary>
    public class Option
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Option"/> class.
        /// </summary>
        /// <param name="parser">The parser which collects error messages.</param>
        /// <param name="optionType">The type which declares the member.</param>
        /// <param name="memberInfo">The field or property annotated with <see cref="SimpleOptionAttribute"/>.</param>
        /// <param name="attribute">The <see cref="SimpleOptionAttribute"/> of the member.</param>
        /// <param name="optionStack">The types being processed, used to detect a circular dependency.</param>
        /// <exception cref="InvalidOperationException">The member is not a settable field or property.</exception>
        internal Option(ISimpleParser parser, Type optionType, MemberInfo memberInfo, SimpleOptionAttribute attribute, Stack<Type> optionStack)
        {
            this.Parser = parser;
            this.LongName = attribute.LongName.Trim();
            this.PropertyInfo = memberInfo as PropertyInfo;
            this.FieldInfo = memberInfo as FieldInfo;
            if (this.PropertyInfo is { } propertyInfo)
            {
                this.optionType = propertyInfo.PropertyType;
                this.FieldInfo = optionType.GetField($"<{propertyInfo.Name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (propertyInfo.GetSetMethod() is { } setMethod)
                {
                    this.setInvoker = MethodInvoker.Create(setMethod);
                }
                else if (this.FieldInfo is null && propertyInfo.GetSetMethod(true) is { } nonPublicSetMethod)
                {// Neither a public setter nor a backing field: fall back to the non-public setter.
                    this.setInvoker = MethodInvoker.Create(nonPublicSetMethod);
                }

                if (propertyInfo.GetGetMethod() is { } getMethod)
                {
                    this.getInvoker = MethodInvoker.Create(getMethod);
                }

                if (this.setInvoker is null && this.FieldInfo is null)
                {
                    throw new InvalidOperationException($"{optionType.Name}.{propertyInfo.Name} is a getter-only property and inaccessible.");
                }
            }
            else if (this.FieldInfo is { } fieldInfo)
            {
                this.optionType = fieldInfo.FieldType;
            }
            else
            {
                throw new InvalidOperationException($"'{memberInfo.Name}' ({optionType.Name}) must be a field or a property.");
            }

            // Nullable value types (int?, TestEnum?, ...) are handled as their underlying type.
            var underlyingType = Nullable.GetUnderlyingType(this.optionType) ?? this.optionType;
            if (underlyingType.IsEnum)
            {// Enum
                this.enumType = underlyingType;
            }
            else if (underlyingType == typeof(string))
            {// String
            }
            else if (SimpleParserHelper.TypeConverters.TryGetValue(underlyingType, out var converter))
            {// Primitive types
                this.converter = converter;
            }
            else
            {// Option class
                this.OptionClass = new OptionClass(this.Parser, this.optionType, optionStack);
            }

            if (attribute.ShortName != null)
            {
                this.ShortName = attribute.ShortName.Trim();
                if (this.ShortName.Length == 0)
                {
                    this.ShortName = null;
                }
            }

            this.Description = attribute.Description;
            this.Required = attribute.Required;
            this.ReadFromEnvironment = attribute.ReadFromEnvironment;
            this.DefaultValueText = attribute.DefaultValueText;
            this.ArgumentProcessing = attribute.ArgumentProcessing;

            var typeName = underlyingType == this.optionType ? underlyingType.Name : underlyingType.Name + "?";
            this.OptionText = this.OptionClass is null ?
                $"-{this.LongName}{(this.ShortName is null ? string.Empty : ", -" + this.ShortName)} <{typeName}>" :
                $"-{this.LongName}{(this.ShortName is null ? string.Empty : ", -" + this.ShortName)} {{{typeName}}}";
        }

        /// <summary>
        /// Converts the argument to the type of this option and sets it on the specified instance.
        /// </summary>
        /// <param name="arg">The value of the option.</param>
        /// <param name="instance">The instance of the options class to set the value on.</param>
        /// <param name="acceptUnknownOptionName">
        /// <see langword="true"/> to ignore an unknown option name while parsing a nested options class.
        /// </param>
        /// <returns><see langword="true"/> if the value is converted and set.</returns>
        public bool Parse(string arg, object? instance, bool acceptUnknownOptionName)
        {
            if (instance == null)
            {
                return false;
            }

            object value;
            if (this.OptionClass is not null)
            {
                arg = SimpleParserHelper.ProcessArgument(arg, this.Parser.ParserOptions, this.ArgumentProcessing);
                var typeIdentifier = this.OptionClass.OptionTypeIdentifier;
                if (typeIdentifier != 0 && TinyhandTypeIdentifier.IsRegistered(typeIdentifier))
                {
                    var obj = TinyhandTypeIdentifier.TryParseOrDeserializeFromString(typeIdentifier, arg, SerializerOptions);
                    if (obj is not null)
                    {
                        this.OptionClass.optionInstance = obj;
                    }
                }

                if (this.OptionClass.optionInstance is null)
                {
                    if (arg.Length >= 2 && arg.StartsWith(SimpleParser.OpenBracket) && arg.EndsWith(SimpleParser.CloseBracket))
                    {
                        arg = arg.Substring(1, arg.Length - 2);
                    }

                    var ret = this.OptionClass.Parse(arg.FormatArguments(this.Parser.ParserOptions.ArgumentDelimiter), 0, acceptUnknownOptionName);
                    if (!ret || this.OptionClass.OptionInstance == null)
                    {
                        return false;
                    }
                }

                value = this.OptionClass.OptionInstance!;
            }
            else if (this.enumType is not null)
            {// Enum
                if (!Enum.TryParse(this.enumType, arg, true, out var result) || result is null)
                {
                    return false;
                }

                value = result;
            }
            else if (this.converter is not null)
            {// Primitive types
                if (this.converter(arg) is not { } converted)
                {
                    return false;
                }

                value = converted;
            }
            else
            {// String
                value = SimpleParserHelper.ProcessArgument(arg, this.Parser.ParserOptions, this.ArgumentProcessing);
            }

            return this.SetValue(instance, value);
        }

        /// <summary>
        /// Sets the value on the member (through the public setter, or the backing field if there is none).
        /// </summary>
        /// <param name="instance">The instance of the options class.</param>
        /// <param name="value">The value to set.</param>
        /// <returns><see langword="true"/> if the value is set.</returns>
        internal bool SetValue(object instance, object value)
        {
            if (this.setInvoker is not null)
            {// Set property
                this.setInvoker.Invoke(instance, value);
            }
            else if (this.FieldInfo is not null)
            {// Set field
                this.FieldInfo.SetValue(instance, value);
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the property this option maps to, or <see langword="null"/> if it maps to a field.
        /// </summary>
        public PropertyInfo? PropertyInfo { get; }

        /// <summary>
        /// Gets the field this option maps to (the backing field when <see cref="PropertyInfo"/> is an auto-property),
        /// or <see langword="null"/> if there is none.
        /// </summary>
        public FieldInfo? FieldInfo { get; }

        /// <summary>
        /// Gets the long option name.
        /// </summary>
        public string LongName { get; }

        /// <summary>
        /// Gets the short option name, or <see langword="null"/> if there is none.
        /// </summary>
        public string? ShortName { get; }

        /// <summary>
        /// Gets or sets the description shown in a help message.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the text shown as the default value in a help message.<br/>
        /// When <see langword="null"/>, the value of <see cref="OptionClass.DefaultInstance"/> is shown instead.
        /// </summary>
        public string? DefaultValueText { get; set; }

        /// <summary>
        /// Gets the name and type of this option as shown in a help message (for example, <c>-count, -c &lt;Int32&gt;</c>).
        /// </summary>
        public string OptionText { get; }

        /// <summary>
        /// Gets a value indicating whether a value is required for this option.
        /// </summary>
        public bool Required { get; }

        /// <summary>
        /// Gets a value indicating whether the value is read from the environment variable
        /// named after <see cref="ShortName"/> or <see cref="LongName"/> when the option is not specified.
        /// </summary>
        public bool ReadFromEnvironment { get; }

        /// <summary>
        /// Gets the way the argument of this option is normalized.
        /// </summary>
        public ArgumentProcessing ArgumentProcessing { get; }

        /// <summary>
        /// Gets a value indicating whether a value has been set during the last parse.
        /// </summary>
        public bool ValueIsSet { get; internal set; }

        /// <summary>
        /// Gets the declared type of the field or property (<see cref="Nullable{T}"/> included).
        /// </summary>
        public Type OptionType => this.optionType;

        /// <summary>
        /// Gets the nested options class when <see cref="OptionType"/> is a class with its own options; otherwise, <see langword="null"/>.
        /// </summary>
        public OptionClass? OptionClass { get; }

        /// <summary>
        /// Gets the parser which provides the parser options.
        /// </summary>
        internal ISimpleParser Parser { get; }

        /// <summary>
        /// Gets the current value of the member.
        /// </summary>
        /// <param name="instance">The instance of the options class.</param>
        /// <returns>The value, or <see langword="null"/> if it cannot be read.</returns>
        internal object? GetValue(object? instance)
        {
            if (instance == null)
            {
                return null;
            }
            else if (this.getInvoker is not null)
            {// Get property
                return this.getInvoker.Invoke(instance);
            }
            else if (this.FieldInfo != null)
            {// Get field
                return this.FieldInfo.GetValue(instance);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Clears the state of the previous parse (called at the beginning of <see cref="OptionClass.Parse"/>).
        /// </summary>
        internal void Reset()
        {
            this.ValueIsSet = false;
            this.OptionClass?.optionInstance = default;
        }

        private readonly Type optionType;
        private readonly Type? enumType;
        private readonly Func<ReadOnlySpan<char>, object?>? converter;
        private readonly MethodInvoker? setInvoker;
        private readonly MethodInvoker? getInvoker;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleParser"/> class.<br/>
    /// Unless <see cref="SimpleParserOptions.RequireStrictCommandName"/> is enabled, the first command type
    /// (or the first one whose <see cref="SimpleCommandAttribute.Default"/> is <see langword="true"/>) becomes the default command.
    /// </summary>
    /// <param name="simpleCommands">The command types. Each must have a <see cref="SimpleCommandAttribute"/>.</param>
    /// <param name="parserOptions">The parser options. Use <see langword="null"/> for <see cref="SimpleParserOptions.Standard"/>.</param>
    /// <exception cref="InvalidOperationException">A command type is not valid, or a command name or alias is duplicated.</exception>
    public SimpleParser(IEnumerable<Type> simpleCommands, SimpleParserOptions? parserOptions = null)
    {
        this.ParserOptions = parserOptions ?? SimpleParserOptions.Standard;
        this.consoleService = this.ParserOptions.ServiceProvider?.GetService<IConsoleService>();

        Command? firstOrDefault = null;
        this.NameToCommand = new(StringComparer.OrdinalIgnoreCase);
        this.AliasToCommand = new(StringComparer.OrdinalIgnoreCase);
        this.ErrorMessage = new();
        this.OptionClassUsage = new();
        foreach (var x in simpleCommands)
        {
            // Get SimpleCommandAttribute
            var attribute = x.GetCustomAttribute<SimpleCommandAttribute>(true);
            if (attribute == null)
            {
                throw new InvalidOperationException($"Type '{x.ToString()}' must have SimpleCommandAttribute.");
            }

            // Get Command from Type x
            var name = attribute.CommandName;
            Command? command;
            if (this.NameToCommand.TryGetValue(name, out command))
            {
                if (x != command.CommandType)
                {// Duplicate name.
                    throw new InvalidOperationException($"Command name '{name}' ({x.ToString()}) already exists.");
                }
            }
            else
            {
                command = new(this, x, attribute);
                this.NameToCommand.Add(name, command);

                // Regards the first command as the default command.
                if (firstOrDefault == null)
                {
                    firstOrDefault = command;
                }
                else if (!firstOrDefault.Default && command.Default)
                {
                    firstOrDefault = command;
                }
            }

            // Alias
            if (!string.IsNullOrEmpty(attribute.Alias))
            {
                if (!this.AliasToCommand.TryAdd(attribute.Alias, command))
                {
                    throw new InvalidOperationException($"Alias '{attribute.Alias}' ({x.ToString()}) already exists.");
                }
            }
        }

        // Auto-alias
        if (this.ParserOptions.AutoAlias)
        {
            foreach (var x in this.NameToCommand.Values)
            {
                if (string.IsNullOrEmpty(x.Alias))
                {
                    var alias = SimpleParserHelper.CreateAliasFromCommand(x.CommandName);
                    this.AliasToCommand.TryAdd(alias, x);
                }
            }
        }

        if (firstOrDefault != null)
        {
            firstOrDefault.Default = true;
            this.DefaultCommandName = firstOrDefault.CommandName;
        }

        if (this.ParserOptions.RequireStrictCommandName)
        {// No default command
            this.DefaultCommandName = null;
        }
    }

    /// <summary>
    /// Parses the arguments and executes the specified command asynchronously.<br/>
    /// A help or version message is displayed instead when it is requested or the arguments are invalid.
    /// </summary>
    /// <param name="simpleCommands">The command types. Each must have a <see cref="SimpleCommandAttribute"/>.</param>
    /// <param name="arg">The arguments specifying the command and its options.</param>
    /// <param name="parserOptions">The parser options. Use <see langword="null"/> for <see cref="SimpleParserOptions.Standard"/>.</param>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    public static Task ParseAndExecute(IEnumerable<Type> simpleCommands, string arg, SimpleParserOptions? parserOptions = null, CancellationToken cancellationToken = default)
    {
        var p = new SimpleParser(simpleCommands, parserOptions);
        p.Parse(arg);
        return p.Execute(cancellationToken);
    }

    /// <summary>
    /// Parses the arguments and executes the specified command asynchronously.<br/>
    /// The arguments are joined with a space, so quoting is lost; use the <see cref="string"/> overload to preserve it.
    /// </summary>
    /// <param name="simpleCommands">The command types. Each must have a <see cref="SimpleCommandAttribute"/>.</param>
    /// <param name="args">The arguments specifying the command and its options.</param>
    /// <param name="parserOptions">The parser options. Use <see langword="null"/> for <see cref="SimpleParserOptions.Standard"/>.</param>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    public static Task ParseAndExecute(IEnumerable<Type> simpleCommands, string[] args, SimpleParserOptions? parserOptions = null, CancellationToken cancellationToken = default)
    {
        var p = new SimpleParser(simpleCommands, parserOptions);
        p.Parse(args);
        return p.Execute(cancellationToken);
    }

    /// <summary>
    /// Parses the arguments and stores the result in <see cref="CurrentCommand"/>.<br/>
    /// The arguments are joined with a space, so quoting is lost; use the <see cref="string"/> overload to preserve it.
    /// </summary>
    /// <param name="args">The arguments specifying the command and its options.</param>
    /// <returns><see langword="true"/> if the arguments are successfully parsed.</returns>
    public bool Parse(string[] args) => this.Parse(string.Join(' ', args));

    /// <summary>
    /// Parses the arguments and stores the result in <see cref="CurrentCommand"/>.<br/>
    /// When help or version is requested, <see cref="HelpCommand"/> or <see cref="VersionCommand"/> is set instead.
    /// </summary>
    /// <param name="arg">The arguments specifying the command and its options.</param>
    /// <returns><see langword="true"/> if the arguments are successfully parsed.</returns>
    public bool Parse(string arg)
    {
        var ret = true;
        var arguments = arg.FormatArguments(this.ParserOptions.ArgumentDelimiter);
        this.OriginalArguments = arg;
        this.HelpCommand = null;
        this.VersionCommand = false;
        this.CurrentCommand = null;
        this.ErrorMessage.Clear();

        var commandName = this.DefaultCommandName;
        var commandSpecified = false;
        var start = 0;
        if (arguments.Length >= 1 && !arguments[0].IsOptionString())
        {// Command
            if (this.NameToCommand.ContainsKey(arguments[0]))
            {// CommandName Found
                commandName = arguments[0];
                commandSpecified = true;
                start = 1;
            }
            else if (this.AliasToCommand.TryGetValue(arguments[0], out var cmd))
            {// Alias Found
                commandName = cmd.CommandName;
                commandSpecified = true;
                start = 1;
            }
        }

        // Not found. Try to load the command from environment variables.
        if (start == 0 &&
            this.ParserOptions.ReadCommandFromEnvironment &&
            Environment.GetEnvironmentVariable(SimpleParser.CommandString) is { } env)
        {
            if (this.NameToCommand.ContainsKey(env))
            {// CommandName Found
                commandName = env;
                commandSpecified = true;
            }
            else if (this.AliasToCommand.TryGetValue(env, out var cmd2))
            {// Alias Found
                commandName = cmd2.CommandName;
                commandSpecified = true;
            }
        }

        if (!commandSpecified)
        {
            TryProcessHelpAndVersion(); // "app.exe help", "app.exe version"
        }

        if (this.HelpCommand != null || this.VersionCommand)
        {
            return ret;
        }

        if (commandName == null)
        {
            this.AddErrorMessage("Specify the command name");
            this.HelpCommand = string.Empty;
            return false;
        }

        if (this.NameToCommand.TryGetValue(commandName, out var command))
        {
            if (commandSpecified && !command.IsSubcommand &&
                arguments.Length > start && OptionEquals(arguments[start], HelpString))
            {
                if (arguments[start].IsOptionString() &&
                    (command.OptionClass.LongNameToOption.ContainsKey(HelpString) || command.OptionClass.ShortNameToOption.ContainsKey(HelpString)))
                {// "app.exe command -help"
                }
                else
                {// "app.exe command help"
                    this.HelpCommand = commandName;
                    return true;
                }
            }

            command.OptionClass.ResetOptionInstance();
            if (command.OptionClass.Parse(arguments, start, command.IsSubcommand))
            {// Success
                this.CurrentCommand = command;
            }
            else
            {
                ret = false;
                this.HelpCommand = commandSpecified ? commandName : string.Empty;
            }
        }
        else
        {// Command not found.
            this.AddErrorMessage($"Command '{commandName}' is not found");
            this.HelpCommand = string.Empty;
            ret = false;
        }

        return ret;

        void TryProcessHelpAndVersion()
        {
            if (arguments.Length == 0)
            {
                return;
            }

            if (OptionEquals(arguments[0], HelpString) ||
                (this.ParserOptions.AutoAlias && OptionEquals(arguments[0], HelpAlias)))
            {// Help
                if (arguments.Length >= 2 && !arguments[1].IsOptionString() && this.NameToCommand.ContainsKey(arguments[1]))
                {// help command
                    this.HelpCommand = arguments[1];
                }
                else
                {
                    this.HelpCommand = string.Empty;
                }
            }
            else if (OptionEquals(arguments[0], VersionString))
            {// Version
                this.VersionCommand = true;
            }
        }
    }

    /// <summary>
    /// Executes the command specified by the last <see cref="Parse(string)"/> call asynchronously,
    /// or displays a help or version message when one was requested.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    public Task Execute(CancellationToken cancellationToken = default)
    {
        if (this.HelpCommand != null)
        {
            this.ShowHelp(this.HelpCommand);
        }
        else if (this.VersionCommand)
        {
            this.ShowVersion();
        }
        else
        {
            if (this.CurrentCommand != null)
            {
                return this.CurrentCommand.Execute(cancellationToken);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Parses the arguments and executes the specified command asynchronously.
    /// </summary>
    /// <param name="arg">The arguments specifying the command and its options.</param>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    public Task ParseAndExecute(string arg, CancellationToken cancellationToken = default)
    {
        this.Parse(arg);
        return this.Execute(cancellationToken);
    }

    /// <summary>
    /// Parses the arguments and executes the specified command asynchronously.<br/>
    /// The arguments are joined with a space, so quoting is lost; use the <see cref="string"/> overload to preserve it.
    /// </summary>
    /// <param name="args">The arguments specifying the command and its options.</param>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    public Task ParseAndExecute(string[] args, CancellationToken cancellationToken = default)
    {
        this.Parse(args);
        return this.Execute(cancellationToken);
    }

    /// <summary>
    /// Attempts to retrieve the command associated with the specified command name.
    /// </summary>
    /// <param name="commandName">The name of the command to retrieve (case insensitive).</param>
    /// <param name="command">When this method returns, contains the command associated with the specified name, if it is found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the command is found; otherwise, <see langword="false"/>.</returns>
    public bool TryGetCommand(string commandName, [MaybeNullWhen(false)] out Command command)
        => this.NameToCommand.TryGetValue(commandName, out command);

    /// <summary>
    /// Attempts to retrieve the option associated with the specified command and option name.
    /// </summary>
    /// <param name="commandName">The name of the command to search for (case insensitive).</param>
    /// <param name="optionName">The long name of the option to search for within the command (case sensitive).</param>
    /// <param name="option">
    /// When this method returns, contains the <see cref="Option"/> associated with the specified option name,
    /// if the option is found; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the option is found; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetOption(string commandName, string optionName, [MaybeNullWhen(false)] out Option option)
    {
        if (!this.NameToCommand.TryGetValue(commandName, out var command))
        {
            option = default;
            return false;
        }

        foreach (var x in command.OptionClass.Options)
        {
            if (x.LongName == optionName)
            {
                option = x;
                return true;
            }
        }

        option = default;
        return false;
    }

    /// <summary>
    /// Writes a help message, preceded by the error messages of the last parse if there are any.
    /// </summary>
    /// <param name="command">
    /// The name of the command to describe. Use <see cref="string.Empty"/> to target all commands,
    /// or <see langword="null"/> to use <see cref="HelpCommand"/>.
    /// </param>
    public void ShowHelp(string? command = null)
    {
        var sb = new StringBuilder();
        this.OptionClassUsage.Clear();
        if (this.ErrorMessage.Count > 0)
        {
            sb.Append("Error: ");
            sb.AppendLine(this.OriginalArguments);
            foreach (var x in this.ErrorMessage)
            {
                sb.Append(IndentString);
                sb.AppendLine(x);
            }

            sb.AppendLine();
            if (command == null)
            {
                command = this.HelpCommand;
            }
        }

        if (!this.ParserOptions.DoNotDisplayUsage)
        {
            this.AppendUsage(sb, command);
        }

        if (string.IsNullOrEmpty(command) && this.ParserOptions.DisplayCommandListAsHelp)
        {
            this.AppendList(sb);
            this.WriteLine(sb.ToString());
            return;
        }

        Command? c = null;
        if (command != null)
        {
            this.NameToCommand.TryGetValue(command, out c);
        }

        if (c == null)
        {
            this.AppendCommandList(sb);
            foreach (var x in this.NameToCommand)
            {
                x.Value.AppendCommand(sb);
            }
        }
        else
        {
            // A property may be modified during command instantiation (inside the constructor), so verify that the instance is created beforehand.
            _ = c.CommandInstance;
            c.AppendCommand(sb);
        }

        // AppendOption() may add a nested option class to the list, so the count is evaluated on each iteration.
        for (var i = 0; i < this.OptionClassUsage.Count; i++)
        {
            this.OptionClassUsage[i].AppendOption(sb, true);
        }

        this.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Writes the version of the entry assembly.
    /// </summary>
    /// <param name="prefix">An optional prefix displayed before the version string.</param>
    public void ShowVersion(string? prefix = default)
    {
        var st = VersionHelper.VersionString;
        this.WriteLine(string.IsNullOrEmpty(prefix) ? st : $"{prefix} {st}");
    }

    /// <summary>
    /// Writes the command names in columns sized to the console width.<br/>
    /// Because the width calculation counts characters, only single-width (alphabetic) command names are supported.
    /// </summary>
    /// <param name="maxLength">The maximum width of a column. A longer command name is truncated.</param>
    public void ShowCommandList(int maxLength = 19)
    {
        var array = this.NameToCommand.Keys.ToArray();
        if (array.Length == 0)
        {
            this.WriteLine();
            return;
        }

        Array.Sort(array, StringComparer.OrdinalIgnoreCase);

        int windowWidth;
        try
        {
            windowWidth = Console.WindowWidth;
        }
        catch
        {// The console is redirected or unavailable.
            windowWidth = DefaultWindowWidth;
        }

        windowWidth = Math.Clamp(windowWidth, 1, MaxWindowWidth);
        var max = 0;
        foreach (var x in array)
        {
            if (x.Length > max)
            {
                max = x.Length;
            }
        }

        var columnWidth = Math.Min(Math.Min(max, maxLength), windowWidth);
        if (columnWidth == 0)
        {
            this.WriteLine();
            return;
        }

        Span<char> buffer = stackalloc char[windowWidth];
        var numberOfColumns = Math.Max(windowWidth / (columnWidth + 1), 1);
        var numberOfRows = array.Length / numberOfColumns;
        var r = array.Length % numberOfColumns;
        if (r == 0)
        {
            r = numberOfColumns;
        }
        else
        {
            numberOfRows++;
        }

        for (var row = 0; row < numberOfRows; row++)
        {
            var span = buffer;
            span.Fill(' ');
            var index = row;
            for (var column = 0; column < numberOfColumns; column++)
            {
                if (row >= (numberOfRows - 1) && (column >= r))
                {
                    break;
                }
                else
                {
                    if (array[index].Length > columnWidth)
                    {
                        array[index].AsSpan(0, columnWidth).CopyTo(span);
                    }
                    else
                    {
                        array[index].AsSpan().CopyTo(span);
                    }
                }

                if (column < r)
                {
                    index += numberOfRows;
                }
                else
                {
                    index += numberOfRows - 1;
                }

                if (span.Length <= columnWidth)
                {
                    break;
                }

                span = span.Slice(columnWidth + 1);
            }

            this.WriteLine(((ReadOnlySpan<char>)buffer).TrimEnd());
        }
    }

    /// <summary>
    /// Gets the options of this parser.
    /// </summary>
    public SimpleParserOptions ParserOptions { get; }

    /// <summary>
    /// Gets the arguments passed to the last <see cref="Parse(string)"/> call.
    /// </summary>
    public string OriginalArguments { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the name of the command executed when the command name is not specified,
    /// or <see langword="null"/> if <see cref="SimpleParserOptions.RequireStrictCommandName"/> is enabled.
    /// </summary>
    public string? DefaultCommandName { get; }

    /// <summary>
    /// Gets the command specified by the last successful parse, or <see langword="null"/> if there is none.
    /// </summary>
    public Command? CurrentCommand { get; private set; }

    /// <summary>
    /// Gets the name of the command for which a help message will be displayed
    /// (<see cref="string.Empty"/> targets all commands, <see langword="null"/> means no help was requested).
    /// </summary>
    public string? HelpCommand { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the version command is specified.
    /// </summary>
    public bool VersionCommand { get; private set; }

    /// <summary>
    /// Gets the registered commands keyed by their command name (case insensitive).
    /// </summary>
    public Dictionary<string, Command> NameToCommand { get; private set; }

    /// <summary>
    /// Gets the registered commands keyed by their alias (case insensitive).
    /// </summary>
    public Dictionary<string, Command> AliasToCommand { get; private set; }

    /// <summary>
    /// Adds an error message to be displayed by <see cref="ShowHelp(string?)"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public void AddErrorMessage(string message) => this.ErrorMessage.Add(message);

    /// <summary>
    /// Gets a value indicating whether an unregistered option name results in an error.
    /// </summary>
    public bool RequireStrictOptionName => this.ParserOptions.RequireStrictOptionName;

    /// <summary>
    /// Registers a nested options class so that its options are described once at the end of a help message.<br/>
    /// The options class is ignored if another one of the same type is already registered.
    /// </summary>
    /// <param name="optionClass">The nested options class.</param>
    public void TryAddOptionClassUsage(OptionClass optionClass)
    {
        foreach (var x in CollectionsMarshal.AsSpan(this.OptionClassUsage))
        {
            if (x.OptionType == optionClass.OptionType)
            {
                return;
            }
        }

        this.OptionClassUsage.Add(optionClass);
    }

    private readonly IConsoleService? consoleService;

    private List<string> ErrorMessage { get; }

    private List<OptionClass> OptionClassUsage { get; }

    /// <summary>
    /// Determines whether the argument matches the specified name, ignoring case and any leading or trailing '-'.
    /// </summary>
    /// <param name="arg">The argument.</param>
    /// <param name="command">The name to compare with.</param>
    /// <returns><see langword="true"/> if they match.</returns>
    internal static bool OptionEquals(ReadOnlySpan<char> arg, ReadOnlySpan<char> command)
            => arg.Trim(SimpleParser.OptionPrefix).Equals(command, StringComparison.OrdinalIgnoreCase);

    private void AppendList(StringBuilder sb)
    {
        var array = this.NameToCommand.Keys.ToArray();
        Array.Sort(array);
        foreach (var x in array)
        {
            sb.Append(x);
            sb.Append(' ');
        }
    }

    private void AppendUsage(StringBuilder sb, string? commandName)
    {
        commandName ??= "<Command>";
        sb.AppendLine($"Usage: {GetEntryName()} {commandName} -option value...");
        sb.AppendLine();

        static string GetEntryName()
        {
            // Assembly.Location is empty for a single-file application.
            var location = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(location))
            {
                location = Environment.ProcessPath;
            }

            return string.IsNullOrEmpty(location) ? string.Empty : Path.GetFileNameWithoutExtension(location);
        }
    }

    private void AppendCommandList(StringBuilder sb)
    {
        sb.AppendLine("Commands:");
        foreach (var x in this.NameToCommand)
        {
            sb.Append(IndentString);
            sb.Append(x.Key);
            if (x.Value.Default)
            {
                sb.AppendLine(" (default)");
            }
            else
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteLine(ReadOnlySpan<char> message)
    {
        if (this.ParserOptions.SuppressConsoleOutput)
        {
            return;
        }

        if (this.consoleService is null)
        {
            Console.Out.WriteLine(message);
        }
        else
        {
            this.consoleService.WriteLine(message);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WriteLine(string? message = default)
    {
        if (this.ParserOptions.SuppressConsoleOutput)
        {
            return;
        }

        if (this.consoleService is null)
        {
            Console.Out.WriteLine(message);
        }
        else
        {
            this.consoleService.WriteLine(message);
        }
    }
}

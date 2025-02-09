// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SimpleCommandLine;

/// <summary>
/// A simple command-line parser.
/// </summary>
public class SimpleParser : ISimpleParser
{
    internal const string HelpString = "help";
    internal const string HelpAlias = "h";
    internal const string VersionString = "version";
    internal const string RunMethodString = "Run";
    internal const string RunAsyncMethodString = "RunAsync";
    internal const string IndentString = "  ";
    internal const string IndentString2 = "    ";
    internal const string BackingField = "<{0}>k__BackingField";
    internal const char OpenBracket = '{'; // '['
    internal const char CloseBracket = '}'; // ']'
    internal const char Quote = '\"';
    internal const string TripleQuotes = "\"\"\"";
    internal const char SingleQuote = '\'';
    internal const char OptionPrefix = '-';
    internal const char Separator = '|';
    internal const string SeparatorString = "|";

    static SimpleParser()
    {
        InitializeTypeConverter();
    }

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

    /*public static TOptions ParseOptions<TOptions>(string[] args, TOptions original)
        => ParseOptions(string.Join(' ', args), original);

    public static TOptions ParseOptions<TOptions>(string args, TOptions original)
    {
        var parser = new HollowParser(SimpleParserOptions.Standard);

        var arguments = args.FormatArguments();
        var optionClass = new OptionClass(parser, typeof(TOptions), null);
        optionClass.optionInstance = original;

        optionClass.Parse(arguments, 0, true);

        if (!optionClass.FatalError &&
            optionClass.OptionInstance is TOptions options)
        {
            return options;
        }

        return original;
    }*/

    public static bool TryParseOptions<TOptions>(string[] args, [MaybeNullWhen(false)] out TOptions options, TOptions? original = default)
        => TryParseOptions(string.Join(' ', args), out options, original);

    public static bool TryParseOptions<TOptions>(string args, [MaybeNullWhen(false)] out TOptions options, TOptions? original = default)
    {
        /*bool requireStrictOptionName;
        if (parserOptions != null)
        {
            requireStrictOptionName = parserOptions.RequireStrictOptionName;
        }
        else
        {
            requireStrictOptionName = SimpleParserOptions.Standard.RequireStrictOptionName;
        }*/

        var parser = new HollowParser(SimpleParserOptions.Standard);

        var arguments = args.FormatArguments();
        var optionClass = new OptionClass(parser, typeof(TOptions), null);
        if (original != null)
        {
            optionClass.optionInstance = original;
        }

        optionClass.Parse(arguments, 0, true);
        if (optionClass.FatalError)
        {
            options = default;
            return false;
        }

        options = (TOptions)optionClass.OptionInstance!;
        return options != null;
    }

    private static void InitializeTypeConverter()
    {
        ParserTypeConverter.Add(typeof(bool), static x =>
        {
            var st = x.ToLower();
            if (st == "true")
            {
                return true;
            }
            else if (st == "false")
            {
                return false;
            }
            else
            {
                throw new InvalidOperationException();
            }
        });

        ParserTypeConverter.Add(typeof(string), static x =>
        {
            if (x.Length >= 6 && x.StartsWith(TripleQuotes) && x.EndsWith(TripleQuotes))
            {
                return x.Substring(3, x.Length - 6);
            }
            else if (x.Length >= 2 && x.StartsWith(Quote) && x.EndsWith(Quote))
            {
                return x.Substring(1, x.Length - 2);
            }
            else if (x.Length >= 2 && x.StartsWith(SingleQuote) && x.EndsWith(SingleQuote))
            {
                return x.Substring(1, x.Length - 2);
            }

            return x;
        });

        ParserTypeConverter.Add(typeof(sbyte), static x => Convert.ToSByte(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(byte), static x => Convert.ToByte(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(short), static x => Convert.ToInt16(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(ushort), static x => Convert.ToUInt16(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(int), static x => Convert.ToInt32(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(uint), static x => Convert.ToUInt32(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(long), static x => Convert.ToInt64(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(ulong), static x => Convert.ToUInt64(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(float), static x => Convert.ToSingle(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(double), static x => Convert.ToDouble(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(decimal), static x => Convert.ToDecimal(x, CultureInfo.InvariantCulture));
        ParserTypeConverter.Add(typeof(char), static x => Convert.ToChar(x, CultureInfo.InvariantCulture));
    }

    public class Command
    {
        public Command(SimpleParser parser, Type commandType, SimpleCommandAttribute attribute)
        {
            const string MultipleInterfacesException = "Type {0} can implement only one ISimpleCommand or ISimpleCommandAsync interface.";

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
                else if (y == typeof(ISimpleCommandAsync))
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
                    if (z == typeof(ISimpleCommand<>) || z == typeof(ISimpleCommandAsync<>))
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
                throw new InvalidOperationException($"Type \"{commandType.ToString()}\" must implement ISimpleCommand or ISimpleCommandAsync.");
            }

            if (this.Parser.ParserOptions.ServiceProvider == null && this.CommandType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException($"Default constructor (parameterless constructor) is required for type '{commandType.ToString()}'.");
            }

            var mi = this.FindMethod();
            if (mi == null)
            {// No Run method
                if (this.CommandInterface == typeof(ISimpleCommandAsync) ||
                    this.CommandInterface == typeof(ISimpleCommandAsync<>))
                {// Async
                    throw new InvalidOperationException($"{RunAsyncMethodString}() method is required in Type {this.CommandType.ToString()}.");
                }
                else
                {
                    throw new InvalidOperationException($"{RunMethodString}() method is required in Type {this.CommandType.ToString()}.");
                }
            }
            else
            {
                this.runMethod = mi;
            }

            this.OptionClass = new OptionClass(this.Parser, this.OptionType, null);
        }

        /*public void Run()
        {
            if (this.Instance == null)
            {
                return;
            }

            var methods = this.CommandType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var methodName = RunMethodString;
            foreach (var x in methods)
            {
                if (x.Name != methodName)
                {
                    continue;
                }

                var parameters = x.GetParameters();
                if (parameters.Length == 0)
                {// Command.Run()
                    x.Invoke(this.Instance, null);
                    return;
                }
                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                {// Command.Run(args)
                    x.Invoke(this.Instance, new object[] { this.RemainingArguments ?? Array.Empty<string>() });
                    return;
                }
            }

            // No Run method
            throw new InvalidOperationException($"{methodName}() or {methodName}(string[] args) method is required in Type {this.CommandType.ToString()}.");
        }*/

        public async Task RunAsync()
        {
            var args = this.OptionClass.RemainingArguments ?? Array.Empty<string>();

            if (this.CommandInterface == typeof(ISimpleCommand))
            {// void Run(string[] args);
                this.runMethod.Invoke(this.CommandInstance, new object[] { args });
            }
            else if (this.CommandInterface == typeof(ISimpleCommand<>))
            {// void Run(Options option, string[] args);
                this.runMethod.Invoke(this.CommandInstance, new object[] { this.OptionClass.OptionInstance!, args });
            }
            else if (this.CommandInterface == typeof(ISimpleCommandAsync))
            {// Task RunAsync(string[] args);
                var task = (Task?)this.runMethod.Invoke(this.CommandInstance, new object?[] { args });
                if (task != null)
                {
                    await task;
                }
            }
            else if (this.CommandInterface == typeof(ISimpleCommandAsync<>))
            {// Task RunAsync(Options option, string[] args);
                var task = (Task?)this.runMethod.Invoke(this.CommandInstance, new object?[] { this.OptionClass.OptionInstance, args });
                if (task != null)
                {
                    await task;
                }
            }
        }

        public void Run()
        {
            var args = this.OptionClass.RemainingArguments ?? Array.Empty<string>();

            if (this.CommandInterface == typeof(ISimpleCommand))
            {// void Run(string[] args);
                this.runMethod.Invoke(this.CommandInstance, new object[] { args });
            }
            else if (this.CommandInterface == typeof(ISimpleCommand<>))
            {// void Run(Options option, string[] args);
                this.runMethod.Invoke(this.CommandInstance, new object[] { this.OptionClass.OptionInstance!, args });
            }
            else if (this.CommandInterface == typeof(ISimpleCommandAsync))
            {// Task RunAsync(string[] args);
                var task = (Task?)this.runMethod.Invoke(this.CommandInstance, new object?[] { args });
                task?.Wait();
            }
            else if (this.CommandInterface == typeof(ISimpleCommandAsync<>))
            {// Task RunAsync(Options option, string[] args);
                var task = (Task?)this.runMethod.Invoke(this.CommandInstance, new object?[] { this.OptionClass.OptionInstance, args });
                task?.Wait();
            }
        }

        public SimpleParser Parser { get; }

        public Type CommandType { get; }

        public Type CommandInterface { get; }

        public Type? OptionType { get; }

        public string CommandName { get; }

        public string Alias { get; internal set; }

        public bool Default { get; internal set; }

        public string? Description { get; }

        public bool IsSubcommand { get; }

        public OptionClass OptionClass { get; }

        // public object CommandInstance => this.commandInstance != null ? this.commandInstance : (this.commandInstance = Activator.CreateInstance(this.CommandType)!);
        public object CommandInstance
        {
            get
            {
                if (this.commandInstance == null)
                {
                    if (this.Parser.ParserOptions.ServiceProvider != null)
                    {
                        this.commandInstance = this.Parser.ParserOptions.ServiceProvider.GetService(this.CommandType);
                    }

                    if (this.commandInstance == null)
                    {
                        this.commandInstance = Activator.CreateInstance(this.CommandType)!;
                    }
                }

                return this.commandInstance;
            }
        }

        internal void AppendCommand(StringBuilder sb)
        {
            if (this.CommandName == string.Empty)
            {
                sb.AppendLine($"{this.Description}");
            }
            else
            {
                sb.AppendLine($"{this.CommandName}: {this.Description}");
            }

            this.OptionClass.AppendOption(sb, false);
        }

        private object? commandInstance;
        private MethodInfo runMethod;

        private MethodInfo? FindMethod()
        {
            string methodString = RunMethodString;
            if (this.CommandInterface == typeof(ISimpleCommandAsync) ||
                    this.CommandInterface == typeof(ISimpleCommandAsync<>))
            {// Async
                methodString = RunAsyncMethodString;
            }

            var methods = this.CommandType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(x => x.Name == methodString);

            if (this.CommandInterface == typeof(ISimpleCommand))
            {
                foreach (var x in methods)
                {
                    if (x.ReturnType == typeof(void))
                    {
                        var parameters = x.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                        {// void Run(string[] args);
                            return x;
                        }
                    }
                }
            }
            else if (this.CommandInterface == typeof(ISimpleCommand<>))
            {
                foreach (var x in methods)
                {
                    if (x.ReturnType == typeof(void))
                    {
                        var parameters = x.GetParameters();
                        if (parameters.Length == 2 && parameters[0].ParameterType == this.OptionType && parameters[1].ParameterType == typeof(string[]))
                        {// void Run(Options option, string[] args);
                            return x;
                        }
                    }
                }
            }
            else if (this.CommandInterface == typeof(ISimpleCommandAsync))
            {
                foreach (var x in methods)
                {
                    if (x.ReturnType == typeof(Task))
                    {
                        var parameters = x.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                        {// Task RunAsync(string[] args);
                            return x;
                        }
                    }
                }
            }
            else if (this.CommandInterface == typeof(ISimpleCommandAsync<>))
            {
                foreach (var x in methods)
                {
                    if (x.ReturnType == typeof(Task))
                    {
                        var parameters = x.GetParameters();
                        if (parameters.Length == 2 && parameters[0].ParameterType == this.OptionType && parameters[1].ParameterType == typeof(string[]))
                        {// Task RunAsync(Options option, string[] args);
                            return x;
                        }
                    }
                }
            }

            return null;
        }
    }

    public class OptionClass
    {
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
            this.OptionType = optionType;

            if (this.OptionType != null && this.OptionType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException($"Default constructor (parameterless constructor) is required for type '{this.OptionType.ToString()}'.");
            }

            this.Options = new();
            this.LongNameToOption = new(StringComparer.InvariantCultureIgnoreCase);
            this.ShortNameToOption = new(StringComparer.InvariantCultureIgnoreCase);
            if (this.OptionType != null)
            {
                var typeList = GetBaseTypesAndThis(this.OptionType).Reverse(); // base type -> derived type
                foreach (var y in typeList)
                {
                    foreach (var x in y.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (x.MemberType != MemberTypes.Field && x.MemberType != MemberTypes.Property)
                        {
                            continue;
                        }

                        var optionAttribute = x.GetCustomAttributes<SimpleOptionAttribute>(true).FirstOrDefault();
                        if (optionAttribute == null)
                        {
                            continue;
                        }

                        var option = new Option(this.Parser, this.OptionType, x, optionAttribute, optionStack);
                        this.Options.Add(option);

                        if (!this.LongNameToOption.TryAdd(option.LongName, option))
                        {
                            throw new InvalidOperationException($"Long option name '{option.LongName}' ({this.OptionType.ToString()}) already exists.");
                        }

                        if (option.ShortName != null && !this.LongNameToOption.TryAdd(option.ShortName, option))
                        {
                            throw new InvalidOperationException($"Short option name '{option.ShortName}' ({this.OptionType.ToString()}) already exists.");
                        }
                    }
                }
            }

            if (optionType != null)
            {
                optionStack.Pop();
            }

            static IEnumerable<Type> GetBaseTypesAndThis(Type symbol)
            {
                var current = symbol;
                while (current != null && current != typeof(object))
                {
                    yield return current;
                    current = current.BaseType;
                }
            }
        }

        public bool Parse(string[] args, int start, bool acceptUnknownOptionName)
        {
            var errorFlag = false;
            List<string> remaining = new();

            foreach (var x in this.Options)
            {
                x.ValueIsSet = false;
            }

            for (var n = start; n < args.Length; n++)
            {
                if (args[n].IsOptionString())
                {// -option
                    var name = args[n].Trim(SimpleParser.OptionPrefix);
                    Option? option;
                    if (!this.LongNameToOption.TryGetValue(name, out option))
                    {
                        this.ShortNameToOption.TryGetValue(name, out option);
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
                     // if (!string.Equals(args[n], "inputFormat", StringComparison.OrdinalIgnoreCase) && !string.Equals(args[n], "outputFormat", StringComparison.OrdinalIgnoreCase))
                        remaining.Add(args[n]);

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
                else if (args[n] == SimpleParser.SeparatorString)
                {

                }
                else
                {
                    if (this.Parser.ParserOptions.OmitOptionNamesForRequiredOptions)
                    {
                        if (this.Options.FirstOrDefault(x => x.Required && !x.ValueIsSet) is { } option)
                        {
                            if (option.Parse(args[n], this.OptionInstance, acceptUnknownOptionName))
                            {
                                option.ValueIsSet = true;
                            }
                            else
                            {// Parse error
                                if (n > 0)
                                {
                                    this.Parser.AddErrorMessage($"Could not convert '{args[n]}' to Type '{option.OptionType.Name}' ({args[n - 1]} {args[n]})");
                                    errorFlag = true;
                                }
                            }

                            continue;
                        }
                    }

                    remaining.Add(args[n]);
                }
            }

            this.LoadEnvironmentVariables(acceptUnknownOptionName);

            foreach (var x in this.Options)
            {
                if (x.Required && !x.ValueIsSet)
                {// Value required.
                    this.Parser.AddErrorMessage($"Value is required for option '{x.LongName}' <{this.OptionType?.Name}>");
                    errorFlag = true;
                    this.FatalError = true;
                }

                if (x.OptionClass != null && !x.ValueIsSet)
                {// Set instance.
                    if (this.OptionInstance != null)
                    {
                        if (x.OptionClass.optionInstance == null)
                        {
                            x.OptionClass.optionInstance = x.GetValue(this.OptionInstance);
                        }

                        if (x.OptionClass.optionInstance != null)
                        {
                            x.ValueIsSet = true;
                        }
                        else if (x.OptionClass.OptionInstance != null)
                        {
                            if (x.SetValue(this.OptionInstance, x.OptionClass.OptionInstance))
                            {
                                x.ValueIsSet = true;
                            }
                        }
                    }
                }
            }

            if (errorFlag)
            {
                return !errorFlag;
            }

            this.RemainingArguments = remaining.ToArray();
            return true;
        }

        public Type? OptionType { get; }

        public List<Option> Options { get; }

        public Dictionary<string, Option> LongNameToOption { get; }

        public Dictionary<string, Option> ShortNameToOption { get; }

        public object? OptionInstance
        {// public object? OptionInstance => this.optionInstance != null ? this.optionInstance : (this.optionInstance = this.OptionType == null ? null : Activator.CreateInstance(this.OptionType)!);
            get
            {
                if (this.optionInstance is null && this.OptionType is not null)
                {
                    this.optionInstance = Activator.CreateInstance(this.OptionType);
                }

                return this.optionInstance;
            }
        }

        public object? DefaultInstance => this.defaultInstance ??= this.OptionType is null ? null : Activator.CreateInstance(this.OptionType);

        public string[]? RemainingArguments { get; private set; }

        internal ISimpleParser Parser { get; }

        internal bool FatalError { get; private set; }

        internal void AppendOption(StringBuilder sb, bool addName)
        {
            if (addName)
            {
                sb.AppendLine($"{{{this.OptionType?.Name}}}");
            }

            if (this.Options.Count == 0)
            {
                sb.AppendLine();
                return;
            }

            var maxWidth = this.Options.Max(x => x.OptionText.Length);
            foreach (var x in this.Options)
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
                    if (x.DefaultValueText != null)
                    {
                        sb.Append($" (Required: {x.DefaultValueText})");
                    }
                    else
                    {
                        sb.Append($" (Required)");
                    }
                }
                else if (x.DefaultValueText != null)
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
                        sb.Append($" (Optional)");
                    }
                    else if (x.OptionClass != null)
                    {
                    }
                    else if (value is string st)
                    {
                        sb.Append($" (Default: \"{value.ToString()}\")");
                    }
                    else
                    {
                        sb.Append($" (Default: {value.ToString()})");
                    }
                }

                /*if (x.OptionType.IsEnum)
                {
                    sb.AppendLine();
                    sb.Append(SimpleParser.IndentString);
                    for (var i = 0; i < maxWidth; i++)
                    {
                        sb.Append(' ');
                    }

                    sb.Append(SimpleParser.IndentString2);

                    var names = Enum.GetNames(x.OptionType);
                    for (var i = 0; i < names.Length; i++)
                    {
                        sb.Append(names[i]);
                        if (i != names.Length - 1)
                        {
                            sb.Append(", ");
                        }
                    }
                }*/

                sb.AppendLine();

                if (x.OptionClass != null)
                {
                    this.Parser.TryAddOptionClassUsage(x.OptionClass);
                }
            }

            sb.AppendLine();
        }

        internal void ResetOptionInstance()
        {
            if (this.OptionType != null)
            {
                this.optionInstance = Activator.CreateInstance(this.OptionType);
            }
        }

        private void LoadEnvironmentVariables(bool acceptUnknownOptionName)
        {
            foreach (var x in this.Options.Where(x => !x.ValueIsSet && x.GetEnvironmentVariable))
            {
                string? env = null;

                if (x.ShortName is not null)
                {
                    env ??= Environment.GetEnvironmentVariable(x.ShortName);
                }

                if (x.LongName is not null)
                {
                    env ??= Environment.GetEnvironmentVariable(x.LongName);
                }

                if (env is not null)
                {
                    if (x.Parse(env, this.OptionInstance, acceptUnknownOptionName))
                    {
                        x.ValueIsSet = true;
                    }
                }
            }
        }

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1401
        internal object? optionInstance;
        private object? defaultInstance;
#pragma warning restore SA1401
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
    }

    public class Option
    {
        internal Option(ISimpleParser parser, Type optionType, MemberInfo memberInfo, SimpleOptionAttribute attribute, Stack<Type> optionStack)
        {
            this.Parser = parser;
            this.LongName = attribute.LongName.Trim();
            this.PropertyInfo = memberInfo as PropertyInfo;
            this.FieldInfo = memberInfo as FieldInfo;
            if (this.PropertyInfo != null && this.FieldInfo == null && optionType != null)
            {
                this.FieldInfo = optionType.GetField(string.Format(BackingField, this.PropertyInfo.Name), BindingFlags.Instance | BindingFlags.NonPublic);
                if (!this.PropertyInfo.CanWrite && this.FieldInfo == null)
                {
                    throw new InvalidOperationException($"{optionType?.Name}.{this.PropertyInfo.Name} is a getter-only property and inaccessible.");
                }
            }

            if (this.PropertyInfo == null && this.FieldInfo == null)
            {
                throw new InvalidOperationException();
            }

            if (this.OptionType.IsEnum)
            {// Enum
            }
            else if (!SimpleParser.ParserTypeConverter.ContainsKey(this.OptionType))
            {
                var optionClass = new OptionClass(this.Parser, this.OptionType, optionStack);
                if (optionClass.Options.Count > 0)
                {
                    this.OptionClass = optionClass;
                }
                else
                {
                    throw new InvalidOperationException($"Type: '{this.OptionType.Name}' is not supported for SimpleOption.");
                }
            }

            if (attribute.ShortName != null)
            {
                this.ShortName = attribute.ShortName.Trim();
                if (string.IsNullOrWhiteSpace(this.ShortName))
                {
                    this.ShortName = null;
                }
            }

            this.Description = attribute.Description;
            this.Required = attribute.Required;
            this.GetEnvironmentVariable = attribute.GetEnvironmentVariable;
            this.DefaultValueText = attribute.DefaultValueText;
            var s = "-" + this.LongName + (this.ShortName == null ? string.Empty : ", -" + this.ShortName);
            if (this.OptionClass != null)
            {
                this.OptionText = s + " {" + this.OptionType.Name + "}";
            }
            else
            {
                this.OptionText = s + " <" + this.OptionType.Name + ">";
            }
        }

        public bool Parse(string arg, object? instance, bool acceptUnknownOptionName)
        {
            if (instance == null)
            {
                return false;
            }

            object value;
            if (this.OptionClass != null)
            {
                if (arg.Length >= 2 && arg.StartsWith(SimpleParser.OpenBracket) && arg.EndsWith(SimpleParser.CloseBracket))
                {
                    arg = arg.Substring(1, arg.Length - 2);
                }

                var ret = this.OptionClass.Parse(arg.FormatArguments(), 0, acceptUnknownOptionName);
                if (!ret || this.OptionClass.OptionInstance == null)
                {
                    return false;
                }

                value = this.OptionClass.OptionInstance;
            }
            else if (this.OptionType.IsEnum)
            {// Enum
                if (!Enum.TryParse(this.OptionType, arg, true, out var result))
                {
                    return false;
                }

                if (result == null)
                {
                    return false;
                }
                else
                {
                    value = result;
                }
            }
            else
            {
                try
                {
                    value = SimpleParser.ParserTypeConverter[this.OptionType](arg)!;
                }
                catch
                {
                    return false;
                }
            }

            return this.SetValue(instance, value);
        }

        internal bool SetValue(object instance, object value)
        {
            if (this.PropertyInfo?.GetSetMethod() is { } mi)
            {// Set property
                mi.Invoke(instance, new object[] { value });
            }
            else if (this.FieldInfo != null)
            {// Set field
                this.FieldInfo.SetValue(instance, value);
            }
            else
            {
                return false;
            }

            return true;
        }

        public PropertyInfo? PropertyInfo { get; }

        public FieldInfo? FieldInfo { get; }

        public string LongName { get; }

        public string? ShortName { get; }

        public string? Description { get; }

        public string? DefaultValueText { get; }

        public string OptionText { get; }

        public bool Required { get; }

        public bool GetEnvironmentVariable { get; }

        public bool ValueIsSet { get; internal set; }

        public Type OptionType => this.PropertyInfo != null ? this.PropertyInfo.PropertyType : this.FieldInfo!.FieldType;

        public OptionClass? OptionClass { get; }

        internal ISimpleParser Parser { get; }

        internal object? GetValue(object? instance)
        {
            if (instance == null)
            {
                return null;
            }
            else if (this.PropertyInfo?.GetGetMethod() is { } mi)
            {// Get property
                return mi.Invoke(instance, Array.Empty<object>());
            }
            else if (this.FieldInfo != null)
            {// Set field
                return this.FieldInfo.GetValue(instance);
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleParser"/> class that is a parser class for Simple command.
    /// </summary>
    /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    public SimpleParser(IEnumerable<Type> simpleCommands, SimpleParserOptions? parserOptions = null)
    {
        this.ParserOptions = parserOptions ?? SimpleParserOptions.Standard;

        Command? firstOrDefault = null;
        this.SimpleCommands = new(StringComparer.InvariantCultureIgnoreCase);
        this.AliasToCommand = new(StringComparer.InvariantCultureIgnoreCase);
        this.ErrorMessage = new();
        this.OptionClassUsage = new();
        foreach (var x in simpleCommands)
        {
            // Get SimpleCommandAttribute
            var attribute = x.GetCustomAttributes<SimpleCommandAttribute>(true).FirstOrDefault();
            if (attribute == null)
            {
                throw new InvalidOperationException($"Type '{x.ToString()}' must have SimpleCommandAttribute.");
            }

            // Get Command from Type x
            var name = attribute.CommandName;
            Command? command;
            if (this.SimpleCommands.TryGetValue(name, out command))
            {
                if (x != command.CommandType)
                {// Duplicate name.
                    throw new InvalidOperationException($"Command name '{name}' ({x.ToString()}) already exists.");
                }
            }
            else
            {
                command = new(this, x, attribute);
                this.SimpleCommands.Add(name, command);

                /* // Option 2: The first default command is the default command.
                if (command.Default && this.DefaultCommandName == null)
                {
                    this.DefaultCommandName = command.CommandName;
                }*/

                // Option 1: Regards the first command as the default command.
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
            foreach (var x in this.SimpleCommands.Values)
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
    /// Parse the arguments and executes the specified command asynchronously.
    /// </summary>
    /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
    /// <param name="arg">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    /// <returns>A task that represents the command execution.</returns>
    public static async Task ParseAndRunAsync(IEnumerable<Type> simpleCommands, string arg, SimpleParserOptions? parserOptions = null)
    {
        var p = new SimpleParser(simpleCommands, parserOptions);
        p.Parse(arg);
        await p.RunAsync();
    }

    /// <summary>
    /// Parse the arguments and executes the specified command asynchronously.
    /// </summary>
    /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
    /// <param name="args">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    /// <returns>A task that represents the command execution.</returns>
    public static async Task ParseAndRunAsync(IEnumerable<Type> simpleCommands, string[] args, SimpleParserOptions? parserOptions = null)
    {
        var p = new SimpleParser(simpleCommands, parserOptions);
        p.Parse(args);
        await p.RunAsync();
    }

    /// <summary>
    /// Parse the arguments and executes the specified command.
    /// </summary>
    /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
    /// <param name="arg">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    public static void ParseAndRun(IEnumerable<Type> simpleCommands, string arg, SimpleParserOptions? parserOptions = null)
    {
        var p = new SimpleParser(simpleCommands, parserOptions);
        p.Parse(arg);
        p.Run();
    }

    /// <summary>
    /// Parse the arguments and executes the specified command.
    /// </summary>
    /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
    /// <param name="args">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    public static void ParseAndRun(IEnumerable<Type> simpleCommands, string[] args, SimpleParserOptions? parserOptions = null)
    {
        var p = new SimpleParser(simpleCommands, parserOptions);
        p.Parse(args);
        p.Run();
    }

    /// <summary>
    /// Parse the arguments.
    /// </summary>
    /// <param name="args">The arguments for specifying commands and options.</param>
    /// <returns><see langword="true"/> if the arguments are successfully parsed.</returns>
    public bool Parse(string[] args) => this.Parse(string.Join(' ', args));

    /// <summary>
    /// Parse the arguments.
    /// </summary>
    /// <param name="arg">The arguments for specifying commands and options.</param>
    /// <returns><see langword="true"/> if the arguments are successfully parsed.</returns>
    public bool Parse(string arg)
    {
        var ret = true;
        var arguments = arg.FormatArguments();
        this.OriginalArguments = arg;
        this.HelpCommand = null;
        this.VersionCommand = false;
        this.ErrorMessage.Clear();

        var commandName = this.DefaultCommandName;
        var commandSpecified = false;
        var start = 0;
        if (arguments.Length >= 1)
        {
            if (!arguments[0].IsOptionString())
            {// Command
                if (this.SimpleCommands.ContainsKey(arguments[0]))
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
                else
                {// Not found
                    TryProcessHelpAndVersion(); // "app.exe help", "app.exe version"
                }
            }
            else
            {// Other (option or value)
                TryProcessHelpAndVersion(); // "app.exe -help", "app.exe -version"
            }
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

        if (this.SimpleCommands.TryGetValue(commandName, out var command))
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
                /*if (args.Any(x => x.IsOptionString() && OptionEquals(x, HelpString)))
                {// -help option. Clear error messages.
                    this.ErrorMessage.Clear();
                }*/
            }
        }

        return ret;

        void TryProcessHelpAndVersion()
        {
            if (OptionEquals(arguments[0], HelpString) ||
                (this.ParserOptions.AutoAlias && OptionEquals(arguments[0], HelpAlias)))
            {// Help
                if (arguments.Length >= 2 && !arguments[1].IsOptionString() && this.SimpleCommands.ContainsKey(arguments[1]))
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
    /// Executes the currently specified command asynchronously or help/version command if needed.
    /// </summary>
    /// <returns>A task that represents the command execution.</returns>
    public async Task RunAsync()
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
                await this.CurrentCommand.RunAsync();
            }
        }
    }

    /// <summary>
    /// Executes the currently specified command or help/version command if needed.
    /// </summary>
    public void Run()
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
                this.CurrentCommand.Run();
            }
        }
    }

    /// <summary>
    /// Parse the arguments and executes the specified command asynchronously.
    /// </summary>
    /// <param name="arg">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    /// <returns>A task that represents the command execution.</returns>
    public async Task ParseAndRunAsync(string arg, SimpleParserOptions? parserOptions = null)
    {
        this.Parse(arg);
        await this.RunAsync();
    }

    /// <summary>
    /// Parse the arguments and executes the specified command asynchronously.
    /// </summary>
    /// <param name="args">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    /// <returns>A task that represents the command execution.</returns>
    public async Task ParseAndRunAsync(string[] args, SimpleParserOptions? parserOptions = null)
    {
        this.Parse(args);
        await this.RunAsync();
    }

    /// <summary>
    /// Parse the arguments and executes the specified command.
    /// </summary>
    /// <param name="arg">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    public void ParseAndRun(string arg, SimpleParserOptions? parserOptions = null)
    {
        this.Parse(arg);
        this.Run();
    }

    /// <summary>
    /// Parse the arguments and executes the specified command.
    /// </summary>
    /// <param name="args">The arguments for specifying commands and options.</param>
    /// <param name="parserOptions">The parser options. Use <c>null</c> to use default options.</param>
    public void ParseAndRun(string[] args, SimpleParserOptions? parserOptions = null)
    {
        this.Parse(args);
        this.Run();
    }

    /// <summary>
    /// Shows help messages.
    /// </summary>
    /// <param name="command">The name of the command for which help message will be displayed (string.Empty targets all commands).</param>
    public void ShowHelp(string? command = null)
    {
        var sb = new StringBuilder();
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
            Console.WriteLine(sb.ToString());
            return;
        }

        Command? c = null;
        if (command != null)
        {
            this.SimpleCommands.TryGetValue(command, out c);
        }

        if (c == null)
        {
            this.AppendCommandList(sb);
            foreach (var x in this.SimpleCommands)
            {
                x.Value.AppendCommand(sb);
            }
        }
        else
        {
            c.AppendCommand(sb);
        }

        foreach (var x in this.OptionClassUsage)
        {
            x.AppendOption(sb, true);
        }

        Console.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Show version.
    /// </summary>
    public void ShowVersion()
    {
        var asm = Assembly.GetEntryAssembly();
        var version = "1.0.0";
        var infoVersion = asm!.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (infoVersion != null)
        {
            version = infoVersion.InformationalVersion;
        }
        else
        {
            var asmVersion = asm!.GetCustomAttribute<AssemblyVersionAttribute>();
            if (asmVersion != null)
            {
                version = asmVersion.Version;
            }
        }

        Console.WriteLine(version);
    }

    /// <summary>
    /// Shows a list of commands.
    /// </summary>
    public void ShowList()
    {
        var sb = new StringBuilder();
        this.AppendList(sb);
        Console.WriteLine(sb.ToString());
    }

    /// <summary>
    /// Gets the collection of type converters.
    /// </summary>
    public static Dictionary<Type, Func<string, object?>> ParserTypeConverter { get; private set; } = new();

    public SimpleParserOptions ParserOptions { get; }

    /// <summary>
    /// Gets the original arguments which is passed to Parse() method.
    /// </summary>
    public string OriginalArguments { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the name of the default command.
    /// </summary>
    public string? DefaultCommandName { get; }

    /// <summary>
    /// Gets the currently specified command.
    /// </summary>
    public Command? CurrentCommand { get; private set; }

    /// <summary>
    /// Gets the name of the command for which help message will be displayed (string.Empty targets all commands).
    /// </summary>
    public string? HelpCommand { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the version command is specified.
    /// </summary>
    public bool VersionCommand { get; private set; }

    /// <summary>
    /// Gets the collection of simple commands.
    /// </summary>
    public Dictionary<string, Command> SimpleCommands { get; private set; }

    public Dictionary<string, Command> AliasToCommand { get; private set; }

    public void AddErrorMessage(string message) => this.ErrorMessage.Add(message);

    public bool RequireStrictOptionName => this.ParserOptions.RequireStrictOptionName;

    public void TryAddOptionClassUsage(OptionClass optionClass)
    {
        if (!this.OptionClassUsage.Any(a => a.OptionType == optionClass.OptionType))
        {
            this.OptionClassUsage.Add(optionClass);
        }
    }

    private List<string> ErrorMessage { get; }

    private List<OptionClass> OptionClassUsage { get; }

    internal static bool OptionEquals(string arg, string command) => arg.Trim(SimpleParser.OptionPrefix).Equals(command, StringComparison.OrdinalIgnoreCase);

    private void AppendList(StringBuilder sb)
    {
        foreach (var x in this.SimpleCommands.Keys.OrderBy(a => a))
        {
            sb.Append(x);
            sb.Append(' ');
        }
    }

    private void AppendUsage(StringBuilder sb, string? commandName)
    {
        if (commandName == null)
        {
            commandName = "<Command>";
        }

        var name = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location);
        sb.AppendLine($"Usage: {name} {commandName} -option value...");
        sb.AppendLine();
    }

    private void AppendCommandList(StringBuilder sb)
    {
        sb.AppendLine("Commands:");
        foreach (var x in this.SimpleCommands)
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
}

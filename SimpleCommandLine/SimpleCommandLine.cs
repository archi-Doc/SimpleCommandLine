// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable SA1009 // Closing parenthesis should be spaced correctly

namespace SimpleCommandLine
{
    /// <summary>
    /// A simple command class with options class requires an implementation of <seealso cref="ISimpleCommandAsync{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of options class.</typeparam>
    public interface ISimpleCommandAsync<T>
        where T : new()
    {
        /// <summary>
        /// The method called when the command is executed.
        /// </summary>
        /// <param name="option">The command options class parsed from command-line arguments.</param>
        /// <param name="args">The remaining command-line arguments.</param>
        /// <returns>A task that represents the command execution.</returns>
        Task Run(T option, string[] args);
    }

    /// <summary>
    /// A simple command class with options class requires an implementation of <seealso cref="ISimpleCommand{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of options class.</typeparam>
    public interface ISimpleCommand<T>
        where T : new()
    {
        /// <summary>
        /// The method called when the command is executed.
        /// </summary>
        /// <param name="option">The command-line options class.</param>
        /// <param name="args">The remaining command-line arguments.</param>
        void Run(T option, string[] args);
    }

    /// <summary>
    /// A simple command class without options requires an implementation of <seealso cref="ISimpleCommandAsync"/>.
    /// </summary>
    public interface ISimpleCommandAsync
    {
        /// <summary>
        /// The method called when the command is executed.
        /// </summary>
        /// <param name="args"> The command-line arguments.</param>
        /// <returns>A task that represents the command execution.</returns>
        Task Run(string[] args);
    }

    /// <summary>
    /// A simple command class without options requires an implementation of <seealso cref="ISimpleCommand"/>.
    /// </summary>
    public interface ISimpleCommand
    {
        /// <summary>
        /// The method called when the command is executed.
        /// </summary>
        /// <param name="args"> The command-line arguments.</param>
        void Run(string[] args);
    }

    public static class SimpleParserExtensions
    {
        public static string[] SplitAtSpace(this string text) => text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);

        public static bool IsOptionString(this string text) => text.StartsWith('-');
    }

    /*/// <summary>
    /// A class with no options.
    /// </summary>
    public sealed class WithoutOption
    {
    }*/

    /// <summary>
    /// Specifies the command name and other properties of the simple command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class SimpleCommandAttribute : Attribute
    {
        /// <summary>
        /// Gets the name of the command.
        /// </summary>
        public string CommandName { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this command will be executed if command name is not specified.
        /// </summary>
        public bool Default { get; set; }

        /// <summary>
        /// Gets or sets the description of the command.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleCommandAttribute"/> class.
        /// </summary>
        /// <param name="commandName">The name of the command.</param>
        /// <param name="description">The description of the command.</param>
        public SimpleCommandAttribute(string commandName, string? description = null)
        {
            this.CommandName = commandName.Trim();
            this.Description = description;
        }
    }

    /// <summary>
    /// Specifies the long/short option name and other properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class SimpleOptionAttribute : Attribute
    {
        /// <summary>
        /// Gets the long option name.
        /// </summary>
        public string LongName { get; }

        /// <summary>
        /// Gets the short option name. Null if you don't want use short name.
        /// </summary>
        public string? ShortName { get; }

        /// <summary>
        /// Gets or sets the description of the command-line option.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the default value text of the command-line option.
        /// </summary>
        public string? DefaultValueText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this command-line option is required [the default is false].
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleOptionAttribute"/> class.
        /// </summary>
        /// <param name="longName">The long command-line name.</param>
        /// <param name="shortName">The short command-line name. Null if you don't want use short name.</param>
        /// <param name="description">The description of the command-line option.</param>
        public SimpleOptionAttribute(string longName, string? shortName = null, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(longName))
            {
                throw new ArgumentNullException(nameof(longName));
            }

            this.LongName = longName;
            this.ShortName = shortName;
            this.Description = description;
        }
    }

    /// <summary>
    /// A simple command-line parser.
    /// </summary>
    public class SimpleParser
    {
        internal const string HelpString = "help";
        internal const string VersionString = "version";
        internal const string RunMethodString = "Run";
        internal const string IndentString = "  ";
        internal const string IndentString2 = "    ";

        public class Command
        {
            public Command(SimpleParser parser, Type commandType, string commandName, bool @default)
            {
                const string MultipleInterfacesException = "Type {0} can implement only one ISimpleCommand or ISimpleCommandAsync interface.";

                this.Parser = parser;
                this.CommandType = commandType;
                this.CommandName = commandName;
                this.Default = @default;
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

                if (this.CommandType.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException($"Default constructor (parameterless constructor) is required for type '{commandType.ToString()}'.");
                }

                var mi = this.FindMethod();
                if (mi == null)
                {// No Run method
                    throw new InvalidOperationException($"{RunMethodString}() method is required in Type {this.CommandType.ToString()}.");
                }
                else
                {
                    this.runMethod = mi;
                }

                this.Options = new();
                this.LongNameToOption = new(StringComparer.InvariantCultureIgnoreCase);
                this.ShortNameToOption = new(StringComparer.InvariantCultureIgnoreCase);
                if (this.OptionType != null)
                {
                    foreach (var x in this.OptionType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (x.MemberType != MemberTypes.Field && x.MemberType != MemberTypes.Property)
                        {
                            continue;
                        }

                        var attribute = x.GetCustomAttributes<SimpleOptionAttribute>(true).FirstOrDefault();
                        if (attribute == null)
                        {
                            continue;
                        }

                        var option = new Option(parser, x, attribute);
                        this.Options.Add(option);

                        if (!this.LongNameToOption.TryAdd(option.LongName, option))
                        {
                            throw new InvalidOperationException($"Long option name '{option.LongName}' ({commandType.ToString()}) already exists.");
                        }

                        if (option.ShortName != null && !this.LongNameToOption.TryAdd(option.ShortName, option))
                        {
                            throw new InvalidOperationException($"Short option name '{option.ShortName}' ({commandType.ToString()}) already exists.");
                        }
                    }
                }
            }

            public bool Parse(string[] args, int start)
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
                        var name = args[n].Trim('-');
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
                                    if (option.Parse(args[n], this.OptionInstance))
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
                        {// Not found
                            remaining.Add(args[n]);
                        }
                    }
                    else
                    {
                        remaining.Add(args[n]);
                    }
                }

                foreach (var x in this.Options)
                {
                    if (x.Required && !x.ValueIsSet)
                    {// Value required.
                        this.Parser.AddErrorMessage($"Value is required for option '{x.LongName}'");
                        errorFlag = true;
                    }
                }

                if (errorFlag)
                {
                    return !errorFlag;
                }

                this.RemainingArguments = remaining.ToArray();
                return true;
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
                var args = this.RemainingArguments ?? Array.Empty<string>();

                if (this.CommandInterface == typeof(ISimpleCommand))
                {// void Run(string[] args);
                    this.runMethod.Invoke(this.CommandInstance, new object[] { args });
                }
                else if (this.CommandInterface == typeof(ISimpleCommand<>))
                {// void Run(Options option, string[] args);
                    this.runMethod.Invoke(this.CommandInstance, new object[] { this.OptionInstance!, args });
                }
                else if (this.CommandInterface == typeof(ISimpleCommandAsync))
                {// Task Run(string[] args);
                    var task = (Task)this.runMethod.Invoke(this.CommandInstance, new object[] { args });
                    await task;
                }
                else if (this.CommandInterface == typeof(ISimpleCommandAsync<>))
                {// Task Run(Options option, string[] args);
                    var task = (Task)this.runMethod.Invoke(this.CommandInstance, new object[] { this.OptionInstance!, args });
                    await task;
                }
            }

            public void Run()
            {
                var args = this.RemainingArguments ?? Array.Empty<string>();

                if (this.CommandInterface == typeof(ISimpleCommand))
                {// void Run(string[] args);
                    this.runMethod.Invoke(this.CommandInstance, new object[] { args });
                }
                else if (this.CommandInterface == typeof(ISimpleCommand<>))
                {// void Run(Options option, string[] args);
                    this.runMethod.Invoke(this.CommandInstance, new object[] { this.OptionInstance!, args });
                }
                else if (this.CommandInterface == typeof(ISimpleCommandAsync))
                {// Task Run(string[] args);
                    var task = (Task)this.runMethod.Invoke(this.CommandInstance, new object[] { args });
                    task.Wait();
                }
                else if (this.CommandInterface == typeof(ISimpleCommandAsync<>))
                {// Task Run(Options option, string[] args);
                    var task = (Task)this.runMethod.Invoke(this.CommandInstance, new object[] { this.OptionInstance!, args });
                    task.Wait();
                }
            }

            public SimpleParser Parser { get; }

            public Type CommandType { get; }

            public Type CommandInterface { get; }

            public Type? OptionType { get; }

            public string CommandName { get; }

            public bool Default { get; internal set; }

            public List<Option> Options { get; }

            public Dictionary<string, Option> LongNameToOption { get; }

            public Dictionary<string, Option> ShortNameToOption { get; }

            public object CommandInstance => this.commandInstance != null ? this.commandInstance : (this.commandInstance = Activator.CreateInstance(this.CommandType)!);

            public object? OptionInstance => this.optionInstance != null ? this.optionInstance : (this.optionInstance = this.OptionType == null ? null : Activator.CreateInstance(this.OptionType)!);

            public string[]? RemainingArguments { get; private set; }

            internal void AppendCommand(StringBuilder sb)
            {
                if (this.Default)
                {
                    sb.AppendLine($"{this.CommandName} (default) options:");
                }
                else
                {
                    sb.AppendLine($"{this.CommandName} options:");
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
                        var value = x.GetValue(this.OptionInstance);
                        if (value == null)
                        {
                            sb.Append($" (Optional)");
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

                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            private object? commandInstance;
            private object? optionInstance;
            private MethodInfo runMethod;

            private MethodInfo? FindMethod()
            {
                var methods = this.CommandType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(x => x.Name == RunMethodString);

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
                            {// Task Run(string[] args);
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
                            {// Task Run(Options option, string[] args);
                                return x;
                            }
                        }
                    }
                }

                return null;
            }
        }

        public class Option
        {
            public Option(SimpleParser parser, MemberInfo memberInfo, SimpleOptionAttribute attribute)
            {
                this.Parser = parser;
                this.LongName = attribute.LongName.Trim();
                this.PropertyInfo = memberInfo as PropertyInfo;
                this.FieldInfo = memberInfo as FieldInfo;
                if (this.PropertyInfo == null && this.FieldInfo == null)
                {
                    throw new InvalidOperationException();
                }

                if (!this.Parser.TypeConverter.ContainsKey(this.OptionType))
                {
                    throw new InvalidOperationException($"Type: '{this.OptionType.Name}' is not supported for SimpleOption.");
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
                this.DefaultValueText = attribute.DefaultValueText;
                this.OptionText = "-" + this.LongName + (this.ShortName == null ? string.Empty : ", -" + this.ShortName) + " <" + this.OptionType.Name + ">";
            }

            public bool Parse(string arg, object? instance)
            {
                if (instance == null)
                {
                    return false;
                }

                object value;
                try
                {
                    value = this.Parser.TypeConverter[this.OptionType](arg)!;
                }
                catch
                {
                    return false;
                }

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

            public SimpleParser Parser { get; }

            public PropertyInfo? PropertyInfo { get; }

            public FieldInfo? FieldInfo { get; }

            public string LongName { get; }

            public string? ShortName { get; }

            public string? Description { get; }

            public string? DefaultValueText { get; }

            public string OptionText { get; }

            public bool Required { get; }

            public bool ValueIsSet { get; internal set; }

            public Type OptionType => this.PropertyInfo != null ? this.PropertyInfo.PropertyType : this.FieldInfo!.FieldType;

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
        public SimpleParser(IEnumerable<Type> simpleCommands)
        {
            this.InitializeTypeConverter();

            // Command? firstOrDefault = null;
            this.SimpleCommands = new(StringComparer.InvariantCultureIgnoreCase);
            this.ErrorMessage = new();
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
                    command = new(this, x, name, attribute.Default);
                    this.SimpleCommands.Add(name, command);

                    if (command.Default && this.DefaultCommandName == null)
                    {
                        this.DefaultCommandName = command.CommandName;
                    }

                    /*if (firstOrDefault == null)
                    {
                        firstOrDefault = command;
                    }
                    else if (!firstOrDefault.Default && command.Default)
                    {
                        firstOrDefault = command;
                    }*/
                }
            }

            /*if (firstOrDefault != null)
            {
                firstOrDefault.Default = true;
                this.DefaultCommandName = firstOrDefault.CommandName;
            }*/
        }

        /// <summary>
        /// Parse the arguments and executes the specified command asynchronously.
        /// </summary>
        /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
        /// <param name="arg">The arguments for specifying commands and options.</param>
        /// <returns>A task that represents the command execution.</returns>
        public static Task ParseAndRunAsync(IEnumerable<Type> simpleCommands, string arg) => ParseAndRunAsync(simpleCommands, arg.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Parse the arguments and executes the specified command asynchronously.
        /// </summary>
        /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
        /// <param name="args">The arguments for specifying commands and options.</param>
        /// <returns>A task that represents the command execution.</returns>
        public static async Task ParseAndRunAsync(IEnumerable<Type> simpleCommands, string[] args)
        {
            var p = new SimpleParser(simpleCommands);
            p.Parse(args);
            await p.RunAsync();
        }

        /// <summary>
        /// Parse the arguments and executes the specified command.
        /// </summary>
        /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
        /// <param name="arg">The arguments for specifying commands and options.</param>
        public static void ParseAndRun(IEnumerable<Type> simpleCommands, string arg) => ParseAndRun(simpleCommands, arg.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));

        /// <summary>
        /// Parse the arguments and executes the specified command.
        /// </summary>
        /// <param name="simpleCommands">The <seealso cref="IEnumerable{T}"/> whose simple command types are used to parse arguments and execute the command.</param>
        /// <param name="args">The arguments for specifying commands and options.</param>
        public static void ParseAndRun(IEnumerable<Type> simpleCommands, string[] args)
        {
            var p = new SimpleParser(simpleCommands);
            p.Parse(args);
            p.Run();
        }

        // public static SimpleParser Parse(Type simpleCommand, string[] args) => Parse(new Type[] { simpleCommand }, args);

        // public static SimpleParser Parse(Type simpleCommand, string args) => Parse(new Type[] { simpleCommand }, args.SplitAtSpace());

        /// <summary>
        /// Parse the arguments.
        /// </summary>
        /// <param name="arg">The arguments for specifying commands and options.</param>
        /// <returns>True if the arguments are successfully parsed.</returns>
        public bool Parse(string arg) => this.Parse(arg.SplitAtSpace());

        /// <summary>
        /// Parse the arguments.
        /// </summary>
        /// <param name="args">The arguments for specifying commands and options.</param>
        /// <returns>True if the arguments are successfully parsed.</returns>
        public bool Parse(string[] args)
        {
            var ret = true;
            this.OriginalArguments = string.Join(' ', args);
            this.HelpCommand = null;
            this.VersionCommand = false;

            var commandName = this.DefaultCommandName;
            var commandSpecified = false;
            var start = 0;
            if (args.Length >= 1)
            {
                if (!args[0].IsOptionString())
                {// Command
                    if (this.SimpleCommands.ContainsKey(args[0]))
                    {// Found
                        commandName = args[0];
                        commandSpecified = true;
                        start = 1;
                    }
                    else
                    {// Not found
                        if (OptionEquals(args[0], HelpString))
                        {// Help
                            this.HelpCommand = string.Empty;
                        }
                        else if (OptionEquals(args[0], VersionString))
                        {// Version
                            this.VersionCommand = true;
                        }
                        else
                        {
                        }
                    }
                }
                else
                {// Other
                    if (OptionEquals(args[0], HelpString))
                    {// Help
                        this.HelpCommand = string.Empty;
                    }
                    else if (OptionEquals(args[0], VersionString))
                    {// Version
                        this.VersionCommand = true;
                    }
                }
            }

            if (commandName == null)
            {
                this.AddErrorMessage("Specify the command name");
                ret = false;
                this.HelpCommand = string.Empty;
            }

            if (this.HelpCommand != null || this.VersionCommand)
            {
                return ret;
            }

            if (commandName != null && this.SimpleCommands.TryGetValue(commandName, out var command))
            {
                if (command.Parse(args, start))
                {// Success
                    this.CurrentCommand = command;
                }
                else
                {
                    ret = false;
                    this.HelpCommand = commandSpecified ? commandName : string.Empty;
                    if (args.Any(x => x.IsOptionString() && OptionEquals(x, HelpString)))
                    {// -help option. Clear error messages.
                        this.ErrorMessage.Clear();
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Executes the currently specified command asynchronously or help/version command if needed.
        /// </summary>
        /// <returns>A task that represents the command execution.</returns>
        public async Task RunAsync()
        {
            if (this.HelpCommand != null)
            {
                this.ShowHelp();
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
                this.ShowHelp();
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
        /// Shows help messages.
        /// </summary>
        /// <param name="command">The name of the command for which help message will be displayed (string.Empty targets all commands).</param>
        public void ShowHelp(string? command = null)
        {
            var sb = new StringBuilder();
            if (command == null && this.ErrorMessage.Count > 0)
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

            this.AppendUsage(sb, command);

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

        public void AddErrorMessage(string message) => this.ErrorMessage.Add(message);

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

        /// <summary>
        /// Gets the collection of type converters.
        /// </summary>
        public Dictionary<Type, Func<string, object?>> TypeConverter { get; private set; } = default!;

        private List<string> ErrorMessage { get; }

        internal static bool OptionEquals(string arg, string command) => arg.Trim('-').Equals(command, StringComparison.OrdinalIgnoreCase);

        private void AppendUsage(StringBuilder sb, string? commandName)
        {
            if (commandName == null)
            {
                commandName = "<Command>";
            }

            var name = Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly()!.Location);
            sb.AppendLine($"Usage: {name} {commandName} [options...]");
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

        private void InitializeTypeConverter()
        {
            this.TypeConverter = new();
            this.TypeConverter.Add(typeof(bool), static x =>
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

            this.TypeConverter.Add(typeof(sbyte), static x => Convert.ToSByte(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(byte), static x => Convert.ToByte(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(short), static x => Convert.ToInt16(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(ushort), static x => Convert.ToUInt16(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(int), static x => Convert.ToInt32(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(uint), static x => Convert.ToUInt32(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(long), static x => Convert.ToInt64(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(ulong), static x => Convert.ToUInt64(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(float), static x => Convert.ToSingle(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(double), static x => Convert.ToDouble(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(decimal), static x => Convert.ToDecimal(x, CultureInfo.InvariantCulture));
            this.TypeConverter.Add(typeof(string), static x => x);
            this.TypeConverter.Add(typeof(char), static x => Convert.ToChar(x, CultureInfo.InvariantCulture));
        }
    }
}

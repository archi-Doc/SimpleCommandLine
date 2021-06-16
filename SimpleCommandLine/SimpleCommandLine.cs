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
    public interface ISimpleCommand<T>
        where T : new()
    {
        Task Run(T option, string[] args);
    }

    public interface ISimpleCommand
    {
        Task Run(string[] args);
    }

    public static class SimpleParserExtensions
    {
        public static string[] SplitAtSpace(this string text) => text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);

        public static bool IsOptionString(this string text) => text.StartsWith('-');
    }

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
        /// Gets or sets the description of the commandline option.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the default value text of the commandline option.
        /// </summary>
        public string? DefaultValueText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this commandline option is required [the default is false].
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SimpleOptionAttribute"/> class.
        /// </summary>
        /// <param name="longName">The long commandline name.</param>
        /// <param name="shortName">The short commandline name. Null if you don't want use short name.</param>
        /// <param name="description">The description of the commandline option.</param>
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
                this.Parser = parser;
                this.CommandType = commandType;
                this.CommandName = commandName;
                this.Default = @default;

                if (this.CommandType.GetConstructor(Type.EmptyTypes) == null)
                {
                    throw new InvalidOperationException($"Default constructor (parameterless constructor) is required for type '{commandType.ToString()}'.");
                }

                this.Options = new();
                this.LongNameToOption = new(StringComparer.InvariantCultureIgnoreCase);
                this.ShortNameToOption = new(StringComparer.InvariantCultureIgnoreCase);
                foreach (var x in this.CommandType.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
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
                                    if (option.Parse(args[n], this.Instance))
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
                        this.Parser.AddErrorMessage($"Set a valid value for option '{x.LongName}'");
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
                    if (x.ReturnType == typeof(void))
                    {
                        /*if (parameters.Length == 0)
                        {// Command.Run()
                            x.Invoke(this.Instance, null);
                            return;
                        }
                        else */if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                        {// Command.Run(args)
                            x.Invoke(this.Instance, new object[] { this.RemainingArguments ?? Array.Empty<string>() });
                            return;
                        }
                    }
                    else if (x.ReturnType == typeof(Task))
                    {
                        /*if (parameters.Length == 0)
                        {// Command.Run()
                            var task = (Task)x.Invoke(this.Instance, null);
                            await task;
                            return;
                        }
                        else */if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                        {// Command.Run(args)
                            var task = (Task)x.Invoke(this.Instance, new object[] { this.RemainingArguments ?? Array.Empty<string>() });
                            await task;
                            return;
                        }
                    }
                }

                // No Run method
                throw new InvalidOperationException($"{methodName}() or {methodName}(string[] args) method is required in Type {this.CommandType.ToString()}.");
            }

            public SimpleParser Parser { get; }

            public Type CommandType { get; }

            public string CommandName { get; }

            public bool Default { get; internal set; }

            public List<Option> Options { get; }

            public Dictionary<string, Option> LongNameToOption { get; }

            public Dictionary<string, Option> ShortNameToOption { get; }

            public object Instance => this.instance != null ? this.instance : (this.instance = Activator.CreateInstance(this.CommandType)!);

            public string[]? RemainingArguments { get; private set; }

            internal void Append(StringBuilder sb)
            {
                var instance = Activator.CreateInstance(this.CommandType);
                if (this.instance == null)
                {
                    this.instance = instance;
                }

                if (this.Default)
                {
                    sb.AppendLine($"{this.CommandName} (default) options:");
                }
                else
                {
                    sb.AppendLine($"{this.CommandName} options:");
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
                        var value = x.GetValue(instance);
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

            private object? instance;
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
                if (this.PropertyInfo?.GetGetMethod() is { } mi)
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

        private SimpleParser(IEnumerable<Type> simpleCommands)
        {
            this.InitializeTypeConverter();

            Command? firstOrDefault = null;
            this.SimpleCommands = new(StringComparer.InvariantCultureIgnoreCase);
            this.ErrorMessage = new();
            foreach (var x in simpleCommands)
            {
                /*if (!x.GetInterfaces().Contains(typeof(ISimpleCommand)))
                {
                    throw new InvalidOperationException($"Type \"{x.ToString()}\" must implement ISimpleCommand.");
                }*/

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

                    if (firstOrDefault == null)
                    {
                        firstOrDefault = command;
                    }
                    else if (!firstOrDefault.Default && command.Default)
                    {
                        firstOrDefault = command;
                    }
                }
            }

            if (firstOrDefault != null)
            {
                firstOrDefault.Default = true;
                this.DefaultCommandName = firstOrDefault.CommandName;
            }
        }

        /*public static void ParseAndRun(IEnumerable<Type> simpleCommands, string arg) => ParseAndRun(simpleCommands, arg.SplitAtSpace());

        public static void ParseAndRun(IEnumerable<Type> simpleCommands, string[] args)
        {
            var p = Parse(simpleCommands, args);
            if (p.HelpCommand != null)
            {
                p.ShowHelp();
            }
            else if (p.VersionCommand)
            {
                p.ShowVersion();
            }
            else
            {
                p.Run();
            }
        }*/

        public static Task ParseAndRunAsync(IEnumerable<Type> simpleCommands, string arg) => ParseAndRunAsync(simpleCommands, arg.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));

        public static async Task ParseAndRunAsync(IEnumerable<Type> simpleCommands, string[] args)
        {
            var p = Parse(simpleCommands, args);
            await p.RunAsync();
        }

        public static SimpleParser Parse(Type simpleCommand, string[] args) => Parse(new Type[] { simpleCommand }, args);

        public static SimpleParser Parse(Type simpleCommand, string args) => Parse(new Type[] { simpleCommand }, args.SplitAtSpace());

        public static SimpleParser Parse(IEnumerable<Type> simpleCommands, string arg) => Parse(simpleCommands, arg.SplitAtSpace());

        public static SimpleParser Parse(IEnumerable<Type> simpleCommands, string[] args)
        {
            var p = new SimpleParser(simpleCommands);
            p.CommandLine = string.Join(' ', args);

            var commandName = p.DefaultCommandName;
            var commandSpecified = false;
            var start = 0;
            if (args.Length >= 1)
            {
                if (!args[0].IsOptionString())
                {// Command
                    if (p.SimpleCommands.ContainsKey(args[0]))
                    {// Found
                        commandName = args[0];
                        commandSpecified = true;
                        start = 1;
                    }
                    else
                    {// Not found
                        if (OptionEquals(args[0], HelpString))
                        {// Help
                            p.HelpCommand = string.Empty;
                        }
                        else if (OptionEquals(args[0], VersionString))
                        {// Version
                            p.VersionCommand = true;
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
                        p.HelpCommand = string.Empty;
                    }
                    else if (OptionEquals(args[0], VersionString))
                    {// Version
                        p.VersionCommand = true;
                    }
                }
            }

            if (p.HelpCommand != null || p.VersionCommand)
            {
                return p;
            }

            if (commandName != null && p.SimpleCommands.TryGetValue(commandName, out var command))
            {
                if (command.Parse(args, start))
                {
                    p.CurrentCommand = command;
                }
                else
                {
                    p.HelpCommand = commandSpecified ? commandName : string.Empty;
                    if (args.Any(x => x.IsOptionString() && OptionEquals(x, HelpString)))
                    {// -help option. Clear error messages.
                        p.ErrorMessage.Clear();
                    }
                }
            }

            return p;
        }

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

        public void ShowHelp(string? command = null)
        {
            var sb = new StringBuilder();
            if (command == null && this.ErrorMessage.Count > 0)
            {
                sb.Append("Error: ");
                sb.AppendLine(this.CommandLine);
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
                    x.Value.Append(sb);
                }
            }
            else
            {
                c.Append(sb);
            }

            Console.WriteLine(sb.ToString());
        }

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

        public string CommandLine { get; private set; } = string.Empty;

        public string? DefaultCommandName { get; }

        public Command? CurrentCommand { get; private set; }

        public string? HelpCommand { get; private set; }

        public bool VersionCommand { get; private set; }

        public Dictionary<string, Command> SimpleCommands { get; private set; }

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

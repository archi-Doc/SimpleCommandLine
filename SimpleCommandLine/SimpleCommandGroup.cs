// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Threading.Tasks;
using Arc.Unit;

namespace SimpleCommandLine;

/// <summary>
/// <see cref="SimpleCommandGroup{TCommand}"/> is base class for a group of commands.
/// </summary>
/// <typeparam name="TCommand">The type of command group.</typeparam>
public abstract class SimpleCommandGroup<TCommand> : ISimpleCommandAsync
    where TCommand : SimpleCommandGroup<TCommand>
{
    /// <summary>
    /// Gets a <see cref="CommandGroup"/> to configure commands.
    /// </summary>
    /// <param name="context"><see cref="UnitBuilderContext"/>.</param>
    /// <param name="parentCommand"><see cref="Type"/> of the parent command.<br/>
    /// <see langword="null"/>: No parent.</param>
    /// <returns><see cref="CommandGroup"/>.</returns>
    public static CommandGroup ConfigureGroup(IUnitConfigurationContext context, Type? parentCommand = null)
    {
        var commandType = typeof(TCommand);

        // Add a command type to the parent.
        CommandGroup group;
        if (parentCommand != null)
        {
            group = context.GetCommandGroup(parentCommand);
        }
        else
        {
            group = context.GetSubcommandGroup();
        }

        group.AddCommand(commandType);

        // Get the command group.
        group = context.GetCommandGroup(commandType);
        return group;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCommandGroup{TCommand}"/> class.
    /// </summary>
    /// <param name="context"><see cref="UnitContext"/>.</param>
    /// <param name="defaultArgument">The default argument to be used if the argument is empty.</param>
    /// <param name="parserOptions"><see cref="SimpleParserOptions"/>.</param>
    public SimpleCommandGroup(UnitContext context, string? defaultArgument = null, SimpleParserOptions? parserOptions = null)
    {
        this.commandTypes = context.GetCommandTypes(typeof(TCommand));

        if (parserOptions != null)
        {
            this.SimpleParserOptions = parserOptions with { ServiceProvider = context.ServiceProvider, };
        }
        else
        {
            this.SimpleParserOptions = SimpleParserOptions.Standard with
            {
                ServiceProvider = context.ServiceProvider,
                RequireStrictCommandName = true,
                RequireStrictOptionName = true,
                DoNotDisplayUsage = true,
                DisplayCommandListAsHelp = true,
            };
        }

        this.defaultArgument = defaultArgument;
    }

    /// <summary>
    /// Parse the arguments and executes the specified command.<br/>
    /// The default argument will be used if the argument is empty.
    /// </summary>
    /// <param name="args">The arguments to specify commands and options.</param>
    /// <returns><see cref="Task"/>.</returns>
    public async Task RunAsync(string[] args)
    {
        if (args.Length == 0 && this.defaultArgument != null)
        {// Default argument
            args = [this.defaultArgument,];
        }

        await this.SimpleParser.ParseAndRunAsync(args).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets <see cref="SimpleParserOptions"/>.
    /// </summary>
    public SimpleParserOptions SimpleParserOptions { get; }

    /// <summary>
    /// Gets <see cref="SimpleParser"/> instance.
    /// </summary>
    public SimpleParser SimpleParser
    {
        get
        {
            this.simpleParser ??= new(this.commandTypes, this.SimpleParserOptions);
            return this.simpleParser;
        }
    }

    private Type[] commandTypes;
    private SimpleParser? simpleParser;
    private string? defaultArgument;
}

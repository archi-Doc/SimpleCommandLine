// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace SimpleCommandLine;

/// <summary>
/// Implemented by a command class which takes an options class.<br/>
/// The class must also have a <see cref="SimpleCommandAttribute"/>.
/// </summary>
/// <typeparam name="TOption">The type of the options class.</typeparam>
public interface ISimpleCommand<TOption>
    where TOption : new()
{
    /// <summary>
    /// Called when the command is executed.
    /// </summary>
    /// <param name="option">The options parsed from the command line.</param>
    /// <param name="args">The arguments which were not consumed as an option.</param>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    Task Execute(TOption option, string[] args, CancellationToken cancellationToken);
}

/// <summary>
/// Implemented by a command class which takes no options.<br/>
/// The class must also have a <see cref="SimpleCommandAttribute"/>.
/// </summary>
public interface ISimpleCommand
{
    /// <summary>
    /// Called when the command is executed.
    /// </summary>
    /// <param name="args">The arguments which were not consumed as an option.</param>
    /// <param name="cancellationToken">A token used to cancel the command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    Task Execute(string[] args, CancellationToken cancellationToken);
}

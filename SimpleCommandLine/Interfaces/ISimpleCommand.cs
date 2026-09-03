// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace SimpleCommandLine;

/// <summary>
/// Executes a command with typed options and remaining arguments.
/// </summary>
/// <typeparam name="TOptions">The type of the options class.</typeparam>
/// <remarks>Annotate the implementing class with <see cref="SimpleCommandAttribute"/>.</remarks>
public interface ISimpleCommand<TOptions>
    where TOptions : new()
{
    /// <summary>
    /// Called when the command is executed.
    /// </summary>
    /// <param name="options">The options parsed from the command line.</param>
    /// <param name="args">The arguments which were not consumed as an option.</param>
    /// <param name="cancellationToken">The caller's cancellation token, which the implementation should observe.</param>
    /// <returns>A task that represents the command execution.</returns>
    Task Execute(TOptions options, string[] args, CancellationToken cancellationToken);
}

/// <summary>
/// Executes a command without a typed options class.
/// </summary>
/// <remarks>Annotate the implementing class with <see cref="SimpleCommandAttribute"/>.</remarks>
public interface ISimpleCommand
{
    /// <summary>
    /// Called when the command is executed.
    /// </summary>
    /// <param name="args">The arguments which were not consumed as an option.</param>
    /// <param name="cancellationToken">The caller's cancellation token, which the implementation should observe.</param>
    /// <returns>A task that represents the command execution.</returns>
    Task Execute(string[] args, CancellationToken cancellationToken);
}

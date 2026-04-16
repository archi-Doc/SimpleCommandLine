// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace SimpleCommandLine;

/// <summary>
/// A simple command class with an options class requires an implementation of <seealso cref="ISimpleCommandAsync{T}"/>.
/// </summary>
/// <typeparam name="T">The type of options class.</typeparam>
public interface ISimpleCommandAsync<T>
    where T : new()
{
    /// <summary>
    /// The asynchronous method called when the command is executed.
    /// </summary>
    /// <param name="option">The command-line options class.</param>
    /// <param name="args">The remaining command-line arguments.</param>
    /// <param name="cancellationToken">A token used to cancel command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    Task RunAsync(T option, string[] args, CancellationToken cancellationToken);
}

/*/// <summary>
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
}*/

/// <summary>
/// A simple command class without options requires an implementation of <seealso cref="ISimpleCommandAsync"/>.
/// </summary>
public interface ISimpleCommandAsync
{
    /// <summary>
    /// The asynchronous method called when the command is executed.
    /// </summary>
    /// <param name="args">The remaining command-line arguments.</param>
    /// <param name="cancellationToken">A token used to cancel command execution.</param>
    /// <returns>A task that represents the command execution.</returns>
    Task RunAsync(string[] args, CancellationToken cancellationToken);
}

/*/// <summary>
/// A simple command class without options requires an implementation of <seealso cref="ISimpleCommand"/>.
/// </summary>
public interface ISimpleCommand
{
    /// <summary>
    /// The method called when the command is executed.
    /// </summary>
    /// <param name="args"> The command-line arguments.</param>
    void Run(string[] args);
}*/

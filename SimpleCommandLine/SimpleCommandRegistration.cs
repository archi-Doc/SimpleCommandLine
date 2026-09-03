// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleCommandLine;

internal sealed class SimpleCommandRegistration
{
    public SimpleCommandRegistration(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type commandType,
        Type? optionType,
        Type commandInterface,
        Func<object, object?, string[], CancellationToken, Task> execute)
    {
        this.CommandType = commandType;
        this.OptionType = optionType;
        this.CommandInterface = commandInterface;
        this.Execute = execute;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type CommandType { get; }

    public Type? OptionType { get; }

    public Type CommandInterface { get; }

    public Func<object, object?, string[], CancellationToken, Task> Execute { get; }

    [RequiresUnreferencedCode(PreservedType.ReflectionWarning)]
    internal static SimpleCommandRegistration FromReflection(Type commandType)
    {
        Type? commandInterface = null;
        Type? optionType = null;
        foreach (var candidate in commandType.GetInterfaces())
        {
            if (candidate != typeof(ISimpleCommand) &&
                (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != typeof(ISimpleCommand<>)))
            {
                continue;
            }

            if (commandInterface is not null)
            {
                throw new InvalidOperationException($"Type {commandType} can implement only single ISimpleCommand interface.");
            }

            commandInterface = candidate;
            optionType = candidate.IsGenericType ? candidate.GetGenericArguments()[0] : null;
        }

        if (commandInterface is null)
        {
            throw new InvalidOperationException($"Type \"{commandType}\" must implement ISimpleCommand or ISimpleCommand<TOption>.");
        }

        // Interface mapping also supports explicit and inherited implementations.
        var method = commandType.GetInterfaceMap(commandInterface).TargetMethods[0];
        var invoker = MethodInvoker.Create(method);
        return new SimpleCommandRegistration(
            commandType,
            optionType,
            optionType is null ? typeof(ISimpleCommand) : typeof(ISimpleCommand<>),
            (command, options, args, cancellationToken) => (Task?)(optionType is null
                ? invoker.Invoke(command, args, cancellationToken)
                : invoker.Invoke(command, options, args, cancellationToken)) ?? Task.CompletedTask);
    }
}

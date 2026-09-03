// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;

namespace SimpleCommandLine;

internal sealed class PreservedType
{
    internal const string ReflectionWarning = "Runtime type discovery is not compatible with trimming. Use SimpleParserBuilder and register each command and nested options type.";

    public PreservedType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        this.Type = type;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    public Type Type { get; }

    [RequiresUnreferencedCode(ReflectionWarning)]
    internal static PreservedType FromReflection(Type type) => new(type);
}

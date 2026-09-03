// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Runtime.CompilerServices;
using NativeAotTest;

try
{
    if (Array.IndexOf(args, "--require-aot") >= 0 && RuntimeFeature.IsDynamicCodeSupported)
    {
        throw new InvalidOperationException("The smoke test must run as a NativeAOT executable.");
    }

    var checks = await SmokeScenarios.Run();
    checks += await UnitIntegrationScenarios.Run();
    Console.WriteLine($"NativeAOT smoke tests passed: {checks} checks; dynamic code supported: {RuntimeFeature.IsDynamicCodeSupported}.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

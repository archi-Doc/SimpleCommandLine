// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using Arc.Unit;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCommandLine;

internal sealed class SimpleCommandConfiguration : IUnitCustomContext
{
    public SimpleCommandConfiguration()
    {
    }

    public SimpleParserBuilder Builder => !this.processed ? this.builder
        : throw new InvalidOperationException("SimpleCommandLine registration is complete. Register commands and options during UnitBuilder.Configure, before the service provider is built.");

    public void ProcessContext(IUnitConfigurationContext context)
    {
        context.Services.AddSingleton(this.Builder.CreateRegistry());
        this.processed = true;
    }

    private readonly SimpleParserBuilder builder = new();
    private bool processed;
}

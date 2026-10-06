// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleCommandLine;

var settings = SimpleParserOptions.Standard with
{
    ReadCommandFromEnvironment = false,
    SuppressConsoleOutput = true,
};
var builder = new SimpleParserBuilder().AddCommand<RunCommand, RunOptions>();
var parser = builder.Build(settings);
string[] values = ["run", "-number", "42", "-enabled", "true", "-text", "two words"];
var tail = "run -number 42 | " + string.Join(' ', Enumerable.Repeat("ignored", 200));
var many = string.Join(' ', Enumerable.Range(0, 100));
var deep = new string('{', 100) + "value" + new string('}', 100);
var commands = string.Join(" | ", Enumerable.Repeat("run -number 42 -text 'two words'", 20));
var optionsTail = "-number 42 | " + string.Join(' ', Enumerable.Repeat("ignored", 200));
var reflectedParser = new SimpleParser([typeof(PlainCommand)], settings);
reflectedParser.Parse("plain");
if (!parser.Parse(values) || ((RunOptions)parser.CurrentCommand!.OptionSet.Instance!).Number != 42 ||
    commands.SplitCommandLines().Length != 20 ||
    !builder.TryParseOptions<RunOptions>(optionsTail, out var options) || options.Number != 42)
{
    throw new InvalidOperationException("Benchmark input validation failed.");
}

Measure("Parse array", () => parser.Parse(values));
Measure("Parse raw", () => parser.Parse("run -number '42' -enabled true -text 'two words'"));
Measure("Parse remaining", () => parser.Parse("run one two three four five six"));
Measure("Parse first command", () => parser.Parse(tail));
Measure("Split 100 arguments", () => many.SplitArguments());
Measure("Split nesting 100", () => deep.SplitArguments());
Measure("Split 20 commands", () => commands.SplitCommandLines());
Measure("Standalone first command", () => builder.TryParseOptions<RunOptions>(optionsTail, out _));
Measure("Build reused registry", () => builder.Build(settings));
Measure("Repeat registration", () => builder.AddCommand<RunCommand, RunOptions>());
Measure("Reflection plain execute", () => reflectedParser.Execute());
Measure("Suppressed help", () => parser.ShowHelp());

static void Measure(string name, Action action)
{
    const int Iterations = 100_000;
    for (var i = 0; i < 20_000; i++)
    {
        action();
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var start = Stopwatch.GetTimestamp();
    for (var i = 0; i < Iterations; i++)
    {
        action();
    }

    var elapsed = Stopwatch.GetElapsedTime(start);
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Console.WriteLine($"{name,-24} {elapsed.TotalNanoseconds / Iterations,10:F1} ns/op {allocated / (double)Iterations,10:F1} B/op");
}

public class RunOptions
{
    [SimpleOption("number")]
    public int Number { get; set; }

    [SimpleOption("enabled")]
    public bool Enabled { get; set; }

    [SimpleOption("text")]
    public string Text { get; set; } = string.Empty;
}

[SimpleCommand("run")]
public class RunCommand : ISimpleCommand<RunOptions>
{
    public Task Execute(RunOptions options, string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
}

[SimpleCommand("plain")]
public class PlainCommand : ISimpleCommand
{
    public Task Execute(string[] args, CancellationToken cancellationToken) => Task.CompletedTask;
}

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
var parser = new SimpleParserBuilder().AddCommand<RunCommand, RunOptions>().Build(settings);
string[] values = ["run", "-number", "42", "-enabled", "true", "-text", "two words"];
var tail = "run -number 42 | " + string.Join(' ', Enumerable.Repeat("ignored", 200));
var many = string.Join(' ', Enumerable.Range(0, 100));
var deep = new string('{', 100) + "value" + new string('}', 100);
Measure("Parse array", () => parser.Parse(values));
Measure("Parse raw", () => parser.Parse("run -number '42' -enabled true -text 'two words'"));
Measure("Parse remaining", () => parser.Parse("run one two three four five six"));
Measure("Parse first command", () => parser.Parse(tail));
Measure("Split 100 arguments", () => many.SplitArguments());
Measure("Split nesting 100", () => deep.SplitArguments());
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

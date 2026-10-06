# Parser measurements

The harness measures repeated parsing, command splitting, registration, and dispatch. It has no additional package dependencies. Run from the repository root:

```powershell
dotnet build Benchmarks/Benchmarks.csproj -c Release
$env:DOTNET_TieredCompilation = '0'
dotnet Benchmarks/bin/Release/net10.0/Benchmarks.dll
Remove-Item Env:DOTNET_TieredCompilation
```

Use the same runtime settings for both revisions. Omitting the environment variable measures normal tiered JIT behavior, which can change during a short run.

## Current review comparison

Measured on 2026-10-06 with Windows x64, .NET SDK 10.0.401 and runtime 10.0.12. The baseline is commit `6ff9079`, rebuilt with the same dependencies and measured using the current harness. Each scenario warms up for 20,000 operations and measures 100,000 operations. Times are medians of three separate processes with tiered compilation disabled; allocations are rounded to the nearest byte.

| Scenario | Before (ns/op) | After (ns/op) | Before (B/op) | After (B/op) |
| --- | ---: | ---: | ---: | ---: |
| Pre-split arguments | 169.4 | 171.2 | 56 | 56 |
| Raw text with a quoted integer and string | 823.3 | 425.0 | 432 | 432 |
| Six remaining arguments | 389.3 | 305.6 | 408 | 408 |
| First command followed by 200 ignored tokens | 348.2 | 191.6 | 208 | 208 |
| Split 100 arguments | 1,954.9 | 1,828.7 | 3,944 | 3,944 |
| Split 100 nested braces | 529.5 | 552.1 | 464 | 464 |
| Split 20 commands | 3,967.7 | 2,644.7 | 6,600 | 1,944 |
| Standalone options followed by 200 ignored tokens | 8,886.1 | 1,284.9 | 12,512 | 2,816 |
| Build from unchanged registrations | 2,092.5 | 1,820.7 | 4,128 | 3,888 |
| Repeat an existing typed registration | 35.6 | 12.9 | 48 | 0 |
| Execute a reflection-registered command without options | 20.8 | 9.5 | 24 | 0 |
| Suppressed help | 1.8 | 2.0 | 0 | 0 |

Command splitting allocates about 71% less by constructing final strings directly from token ranges. Standalone options stop tokenization at the first command separator, reducing allocations by about 77% for this input. Registry reuse avoids repeated snapshots, and direct interface dispatch removes cancellation-token boxing for commands without options. Small timing differences, including the nested-brace slowdown, vary between runs; these short measurements are not statistical throughput guarantees.

Parsing scenarios reuse a parser. The build scenario includes parser construction with three options and warmed metadata; registration measures an already registered command. Standalone parsing still constructs option metadata and an options instance on each call. Reflection registration and command construction occur before the dispatch measurement. Environment command lookup and console output are disabled.

## Historical review comparison

Measured on 2026-09-07 with Windows x64, .NET SDK 10.0.400 and runtime 10.0.11. The baseline is commit `b186d77`; the comparison used the same harness against the implementation reviewed on that date. These are historical measurements, not results for the current checkout. Each process warms each scenario for 20,000 operations and measures 100,000 operations. Times below are medians of three separate processes with tiered compilation disabled. Allocation totals were identical across these runs.

| Scenario | Before (ns/op) | After (ns/op) | Before (B/op) | After (B/op) |
| --- | ---: | ---: | ---: | ---: |
| Pre-split arguments | 196.1 | 182.8 | 168 | 168 |
| Raw text with a quoted integer and string | 1,004.5 | 898.3 | 464 | 432 |
| Six remaining arguments | 502.1 | 341.4 | 584 | 408 |
| First command followed by 200 ignored tokens | 6,719.0 | 356.5 | 13,752 | 208 |
| Split 100 arguments | 1,740.9 | 1,477.4 | 5,808 | 3,944 |
| Split 100 nested braces | 770.3 | 534.6 | 984 | 464 |
| Suppressed help | 812.5 | 2.2 | 1,832 | 0 |

The largest parsing improvement comes from stopping tokenization at the command separator: about 18.8 times faster and 98.5% fewer allocated bytes for that input. Ordinary raw parsing has a much smaller improvement; earlier runs were approximately equal in speed. These short measurements are directional, not a statistical throughput guarantee. The suppressed-help result measures an early return.

Fresh options instances, result arrays, raw token strings, and reflection-based scalar assignment still allocate. Array diagnostics allocate only when `OriginalCommandLine` is accessed, which this harness does not measure. Reused scratch buffers and shared pools can retain capacity, so fewer allocated bytes do not imply lower retained memory. Concurrent use, environment lookup, actual console output, and application command work are outside these measurements.

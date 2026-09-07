# Parser measurements

The harness measures repeated operations on a reused parser. It has no additional package dependencies. Run from the repository root:

```powershell
dotnet build Benchmarks/Benchmarks.csproj -c Release
$env:DOTNET_TieredCompilation = '0'
dotnet Benchmarks/bin/Release/net10.0/Benchmarks.dll
Remove-Item Env:DOTNET_TieredCompilation
```

Use the same runtime settings for both revisions. Omitting the environment variable measures normal tiered JIT behavior, which can change during a short run.

## Review comparison

Measured on 2026-09-07 with Windows x64, .NET SDK 10.0.400 and runtime 10.0.11. The baseline is commit `b186d77`; the comparison uses the same harness against the reviewed implementation. Each process warms each scenario for 20,000 operations and measures 100,000 operations. Times below are medians of three separate processes with tiered compilation disabled. Allocation totals were identical across these runs.

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

Fresh options instances, result arrays, raw token strings, array diagnostics, and reflection boxing still allocate. Reused scratch buffers and shared pools can retain capacity, so fewer allocated bytes do not imply lower retained memory. Construction, concurrent use, environment lookup, actual console output, and command work are outside these measurements.

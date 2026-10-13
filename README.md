

![](branding/logo/github_logo.png)

**BiomSharp**, or '**Biom#**', is a set of C# libraries and demo applications that provide easier and more consistent management of person biometric data, as well as an abstraction framework for working with various biometric sensors. Some of the biometric functions are provided as C# class libraries that interoperate with C/C++ native libraries.

## Repository

<a href="https://github.com/BiomSharp/BiomSharp" target="_blank">BiomSharp</a>

## Motivation

Faced with client requirements to provide fingerprint verification and identification of persons, while maintaining a high degree of vendor independence as regards scanning devices and SDK's, we have created a C# framework to abstract the processes and data of biometric capture. The framework is written with the intent to provide multiple biometric modalities, although it is currently developed for fingerprint and hand-palm capture.

Our full framework also provides functionality required for biometric analysis separate from vendor SDK's, including minutiae detection, extraction, and print quality measures, mainly by interfacing to existing open-source libraries for biometric functions, image-format conversion, image-processing and other utilities. The framework provides access to biometric scanners functions via a C# 'wrapper' of the corresponding functions in the SDK, the functionality abstracted and exposed through a set of standard interfaces defined in the framework core library. The ultimate goal in all instances of such wrappers is to use the underlying SDK only for print-capture, while the rest of the process is present in the framework itself. In cases where C# interoperability with underlying C/C++ libraries are required, this framework endeavours to provide such functionality 'in-the-box', or a rewrite in pure C#.

## Why C#?

Most of our clients require deployments on the Microsoft Windows operating system. Also, as most biometric vendors provide mainly C++ and nowadays, C# SDK's, it has become our path of least-resistance. It is our experience that using a modern memory-managed framework/language leads to higher quality solutions and faster delivery times. Add to this the fact that most application developers today are less experience in C/C++, makes this type of framework a more attractive alternative.

Secondly, .NET (and C#) is gaining wider acceptance as a credible cross-platform development environment on desktop, mobile and back-end systems and applications. The wider appeal is being seen as a greater demand for C#/.NET to be used for developing end-to-end biometric-based solutions.

## Status

This Github published framework is currently a (very) incomplete version of the full framework. The idea is to release more functionality on an ongoing basis.

## Cross-platform portability

We endeavour to provide the code as 'platform-neutral' as possible - most of the projects are portable to any of the existing .NET 6.0 target platforms. Also, they do not reference Windows-specific packages such as 'System.Drawing.Common' or the Windows application frameworks, Windows-Forms, WPF/UWP, etc. Where they are explicitly referenced they are so documented.

## Requirements

The framework currently supports .NET 6.0 core. The applications and libraries are provided as a set of Visual Studio solutions (.sln), and C# projects (.csproj) or C++ projects (.vcxproj). The existing builds are for Microsoft Windows and target x64 processor architectures.

We recommend using Visual Studio 2022 (any edition).

## WSQ performance tests

The `Performance` tests use `TestResources.DatasetDirectory` and the ten
`cmp00001.pgm` through `cmp00010.pgm` images. Encoding and decoding each cover the
default (0.75), minimum (0.1875), and maximum (2.25) bitrates: 60 cases in total.
They use a separate, non-parallel collection without the full-process fixture,
NFIQ scoring, or native codec execution.

Run from the repository root in PowerShell, without a debugger and with other
CPU-intensive work stopped:

```powershell
$env:BIOMSHARP_PERFORMANCE_REVISION = git rev-parse HEAD
dotnet test .\UnitTests\BiomSharp.Imaging.Tests\BiomSharp.Imaging.Tests.csproj -c Release --filter "Category=Performance"
```

Each case performs one untimed warm-up and five measured operations on fresh
codec instances. Only `WsqCodec.Encode` or `WsqCodec.Decode` is timed; disk I/O,
PGM loading, codec construction, `FromRaw`, `ToRaw`, output validation, and JSON
writing are excluded. Decoding inputs are encoded in memory during Arrange at
the requested bitrate, including the minimum for which no NIST reference exists.
The other WSQ parameters retain their defaults.

Every measured operation must take **no more than 1,000 ms**. Tests also verify
nonempty, decodable encoding output and decoded dimensions, bit depth, and pixel
count. Pixel equality is deliberately not required because WSQ is lossy. These
are warmed-up performance guardrails, not cold-start benchmarks or certification
tests; wall-clock timings remain sensitive to machine load and hardware.

All new results accumulate in
`UnitTests\TestResults\Performance\history.json` (ignored by Git). Schema version 3
contains a `Runs` array, with separate `ResultsEncoding` and `ResultsDecoding`
arrays inside each run. Existing per-case timing fields are preserved, along with
input hashes, source revision supplied by the command above, codec assembly hash,
build configuration, machine/runtime details, and validation outcomes.

Encoding also records five per-operation allocation/memory samples:

| Measurement | Meaning |
|---|---|
| Allocated bytes | `GC.GetAllocatedBytesForCurrentThread()` difference around synchronous `Encode`, including output allocation. This is managed allocation, not native, retained, or peak memory. Future worker-thread encoding would need different instrumentation. |
| Managed heap before/after | `GC.GetTotalMemory(false)` snapshots; no forced collection. Process-wide, including unrelated live objects and uncollected garbage. |
| Working set / private memory before/after | Process-wide memory snapshots, not memory attributable solely to encoding and not peak memory. |
| Process CPU milliseconds | CPU time across process threads; diagnostic context, with OS-dependent resolution and some measurement overhead. |
| Gen 0/1/2 collection counts | Process-wide collections observed around each operation. |
| Output size / SHA-256 | Encoded byte count and digest; the report flags changed output across compared runs. |

The allocation counter and stopwatch exclude snapshot collection, output hashing,
result construction, assertions, and report writing. Snapshot/counter probes still
perturb the experiment; compare runs made with the same measurement version.
Earlier samples' retained output arrays and process GC activity affect memory
snapshots. No allocation or memory ceiling has been introduced.

After every completed case, `UnitTests\TestResults\Performance\comparison.md` is
regenerated for the most recently updated run. It shows previous/current values,
absolute changes, and percentage changes for each recorded case. Negative changes
mean lower measurements, not statistically proven improvements. A zero previous
value has no percentage change. JSON retains every run and its comparisons, even
though the Markdown report shows only the current run.

Each case searches the entire history for its **latest comparable, validated
result**, not merely the last run. Running a single test therefore preserves all
other cases and still finds its own previous result. Matching includes operation,
filename, bitrate setting, dimensions, input hash, all WSQ parameters, measurement
version, sampling settings, machine, runtime, architecture, GC settings, and build
configuration. Source revision and codec hash are recorded but deliberately need
not match, so code changes can be compared.

Debug/debugger runs and failed validation results are saved but are not comparison
references. A changed environment/input without a matching reference reports that
no comparable previous result exists; missing metrics are not treated as zero. Completed sample
sets are saved even when output validation or the 1,000 ms assertion fails. An
exception during setup/encoding can prevent a case from producing a complete
sample set; a JSON history does not prove that an entire test run completed.

Writes use an exclusive lock with a bounded wait and atomic JSON replacement so
individual/concurrent runners cannot overwrite each other's history. Invalid
history or persistence errors fail explicitly rather than silently starting over.
The JSON is authoritative; the Markdown report is a generated view. History grows
until explicitly archived. When `history.json` is absent, the tests initialize it
from `BiomSharp.Imaging.Tests\Performance\Baselines\initial.json`, then append the
current case and generate its comparison. The baseline is only read, never updated
by test execution. Existing history takes precedence and is not reseeded when
only the report or lock file is missing. A missing or invalid baseline fails
explicitly when initialization is required.

Deleting `TestResults` while no tests are running is supported: the next completed
performance case recreates the directory, seeded history, report, and lock. Only
the baseline measurements are recoverable; any later deleted runs are lost.
Machine/runtime/build compatibility checks still apply to the seeded results.
The TRX file belongs to the test runner, not the baseline; request it with the
logger option below to record the actual current run.

To run only encoding:

```powershell
$env:BIOMSHARP_PERFORMANCE_REVISION = git rev-parse HEAD
dotnet test .\UnitTests\BiomSharp.Imaging.Tests\BiomSharp.Imaging.Tests.csproj -c Release --filter "FullyQualifiedName~BiomSharpEncodingPerformanceTests" --logger "trx;LogFileName=performance.trx" --results-directory .\UnitTests\TestResults\Performance
```

Keep an explicitly reviewed full-run snapshot under
`UnitTests\BiomSharp.Imaging.Tests\Performance\Baselines`. The `initial.json`
snapshot was refreshed once from a complete Release performance run on the
unchanged codec. It uses the same schema version 3 as history: one run with
30 encoding cases (including allocation/memory/CPU/GC/output metrics) and 30
decoding cases (timings). Its comparisons are labelled `Baseline`, without
references to runs outside the snapshot. Normal test runs leave it unchanged and
use it only to seed missing history. Old timestamped reports remain untouched.
Before accepting a snapshot, check the test-runner outcome, all 60 unique cases, five samples per
case, Release configuration, no debugger, and the source revision/working-tree
state. Compare medians and maxima only on comparable hardware/runtime settings;
repeat runs to distinguish improvements from noise. Decoding input bytes may
change if the encoder changes, so investigate such changes when interpreting
decoder timings. The recorded measurements are a baseline, while 1,000 ms is a
separate acceptance ceiling. Tests never overwrite a baseline snapshot, relax
the ceiling, or automatically promote a new run.

## License

[MIT](LICENSE.txt)

## Current features

1. C# implementation of the <a href="Source/BiomSharp/BiomSharp/Imaging/Wsq#readme" target="_blank">WSQ codec</a>. This codec is NIST/FBI certified.
2. C#-wrapper implementation of the <a href="https://www.nist.gov/services-resources/software/nfiq-2" target="_blank">NIST NFIQ 2</a> (version 2.2) fingerprint quality measure.
3. Integration to <a href="Source/BiomSharp/BiomSharp.Windows#readme" target="_blank">Windows-codecs</a> and <a href="Source/BiomSharp/BiomSharp.ImageSharp#readme" target="_blank">ImageSharp-codecs</a>.
4. <a href="Demos/BiomStudio#readme" target="_blank">Windows-forms demo</a> of some of the functionality.

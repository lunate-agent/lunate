# S-9 report — what the single-file release's bytes pay for

- Date: 2026-11-22
- Change: measurement behind issue #654 (no product change; it decides the publish step
  recorded in the distribution runbook and reaffirms [ADR-0008](../../../../adr/0008-release-publish-flags.md))
- Question: which publish flag makes the release asset what it is, and what does each flag cost at
  startup rather than on disk?
- Method: four self-contained `osx-arm64` publishes from one commit, identical but for the flags
  (`PublishReadyToRun` × `EnableCompressionInSingleFile`), each measured three ways — apparent bytes,
  `gzip -9` of the binary, the `tar -czf` archive the release attaches — and benchmarked with the
  gate's own startup measurement (hyperfine, 3 warmup + 20 runs, the floor is the number, ADR-0005)
  on five offline workloads. The payload is then attributed per component by publishing the same tree
  loose (`PublishSingleFile=false`), where every assembly is a file. The same two shapes were
  cross-published for `linux-x64` and `win-x64`, to check that the size delta is not a macOS effect.
  A 100-start probe checks the compressed shapes against the crash in ADR-0008.
- Outcome: **the artifact is ReadyToRun, not the Roslyn ring.** R2R is +318.5 MB of the 462.0 MB
  binary and 2.7× the download (139.0 MB → 51.2 MB `tar.gz`); compression halves the binary but not
  the download (−3.5%) and costs ~720 ms of every start. On this RID R2R also *costs* startup rather
  than buying it, which contradicts the premise ADR-0008 accepted — turning R2R off is a release-shape
  change for a follow-up card with Linux/Windows evidence, not a measurement's call.
- Raw evidence: [`evidence/`](evidence/).

## The four shapes

`osx-arm64`, commit `642b178b`, version 0.5.0, .NET 11.0.100, macOS 26.3.1, Apple M4:

| Publish shape | Binary | `gzip -9` | `tar.gz` (the asset) | Startup floor, `lunate --version` |
| --- | --- | --- | --- | --- |
| Single file + ReadyToRun (shipped today) | 462,020,919 (440.6 MiB) | 138,116,061 | 139,042,371 | 139–177 ms |
| Single file + ReadyToRun + compression | 143,916,263 (137.2 MiB) | 134,227,141 | 134,234,176 | 860–910 ms |
| Single file, no ReadyToRun | 143,522,759 (136.9 MiB) | 50,962,898 | 51,157,999 | 38–66 ms |
| Single file + compression, no ReadyToRun | 57,609,543 (54.9 MiB) | 51,083,408 | 51,090,384 | 92–334 ms |

The other two RIDs, cross-published from the same commit (sizes only — nothing on them can run here;
`win-x64` ships a `.zip`):

| RID | Publish shape | Binary | `gzip -9` | Archive |
| --- | --- | --- | --- | --- |
| `linux-x64` | Single file + ReadyToRun | 401,239,053 | 149,245,907 | 149,713,701 |
| `linux-x64` | Single file, no ReadyToRun | 136,631,653 | 53,260,713 | 53,438,670 |
| `win-x64` | Single file + ReadyToRun | 400,143,390 | 148,213,451 | 148,539,230 |
| `win-x64` | Single file, no ReadyToRun | 139,224,566 | 53,834,641 | 53,918,260 |

Three numbers decide the question:

1. **R2R is the size.** Turning it off takes the `osx-arm64` binary from 440.6 MiB to 136.9 MiB and
   the download from 139.0 MB to 51.2 MB — and the same 2.8× on the other two RIDs (`linux-x64`
   149.7 → 53.4 MB, `win-x64` 148.5 → 53.9 MB). Nothing else in the table moves the download by more
   than 4%.
2. **Single-file compression barely touches the download.** It halves the binary (440.6 → 137.2 MiB)
   and the archive falls 139.0 → 134.2 MB, because the archive is `gzip` either way: compressing inside
   the binary and then again at the distribution layer buys 3.5%. Without R2R it buys 0.1%
   (51.2 → 51.1 MB).
3. **Compression costs a factor of five at startup.** The floor goes 139–177 ms → 860–910 ms with R2R,
   and 38–66 ms → 92–334 ms without: the larger the bundled payload, the more there is to decompress
   before `Main` can print.

## Per component

The loose `osx-arm64` publish (`PublishSingleFile=false`) is 292 files, 150,009,373 bytes; the same
publish with R2R is 236,754,205 bytes. Grouped by assembly name:

| Group | IL bytes | R2R bytes | R2R delta | Files |
| --- | --- | --- | --- | --- |
| Roslyn (`Microsoft.CodeAnalysis.*`, `Microsoft.Build.*`) | 24,042,170 | 55,691,786 | +31,649,616 | 84 |
| NuGet (`NuGet.*`) | 2,348,368 | 6,321,664 | +3,973,296 | 6 |
| Model providers (`Anthropic`, `OpenAI`, `Google.*`) | 17,300,944 | 47,139,840 | +29,838,896 | 6 |
| Runtime and framework (`System.*`, `lib*`) | 90,433,968 | 95,724,552 | +5,290,584 | 198 |
| Lunate assemblies (`Lunate.*`, `lunate.dll`) | 5,528,084 | 12,001,812 | +6,473,728 | 29 |
| `Microsoft.Extensions.*` and MCP | 3,885,336 | 8,422,192 | +4,536,856 | 16 |
| everything else | 6,470,503 | 11,452,359 | +4,981,856 | 24 |
| **total** | **150,009,373** | **236,754,205** | **+86,744,832** | 292 |

So the Roslyn ring is the largest single component of the *payload* (24.0 MB, 16% of the IL bytes) and
the most expensive to pre-compile (+31.6 MB, the largest delta), but it is not what makes the asset
378 MiB. That is ReadyToRun itself: the single-file R2R bundle is 318.5 MB larger than the same bundle
without R2R, while the whole loose R2R delta is 86.7 MB (the bundle is 462.0 MB against a loose
payload of 236.8 MB), so the bundle carries pre-compiled code the loose tree does not account for.
Attribute the size by the flag A/B and the components by the loose tree, not the other way round.

## Startup across workloads

Five offline workloads, floor in ms (`--version` is the gate's measurement; the rest reach further
into the process, `--acp` composes the agent, tools and providers and exits at stdin EOF):

| Workload | SF + R2R | SF + R2R + compression | SF, no R2R | SF + compression, no R2R |
| --- | --- | --- | --- | --- |
| `--version` | 139–177 | 860–910 | 38–66 | 92–334 |
| `--help` | 139 | 823 | 51 | 100 |
| `extensions --json` | 228 | 1351 | 224 | 319 |
| `packages --json` | 288 | 904 | 48 | 112 |
| `--acp` | 163–178 | 854–931 | 52–100 | 119 |

R2R does not pay for itself anywhere measured: the uncompressed no-R2R binary starts as fast or
faster on every workload, and the compressed no-R2R binary — 51.1 MB of download — is within ~60 ms
of it. This is the one result the report does not recommend acting on from one machine: R2R buys JIT
time on code paths these probes only touch, and the RIDs that matter for that (Windows especially)
cannot be measured here.

## The compressed-start probe

100 starts per shape, `lunate --version`, exit status counted: **0 failures in every shape**,
including both compressed ones. ADR-0008 kept compression off because compressed single-file R2R
builds died with `AccessViolationException` on macOS 26 arm64 under .NET 10.0.103 (11/100 starts,
[`../S-5/evidence/startup-crashes.txt`](../S-5/evidence/startup-crashes.txt)). The crash does not
reproduce on .NET 11.0.100 here; 100 starts bound the rate at roughly 3%, so that is "not observed",
not "fixed". Compression stays off for the startup cost above, which needs no crash to be decisive.

## What this decided

- **`EnableCompressionInSingleFile` stays off.** 3.5% of the download for ~720 ms on every launch is
  not a trade a terminal agent makes; the archive already carries the compression a download needs.
- **The release shape stays single file + ReadyToRun**, as ADR-0008 decided, with the numbers now
  attached ([the runbook's section](../../distribution.md#single-file-release-shape-size-vs-startup)).
- **R2R is the lever for the size**, and the measurement says a follow-up should test dropping it:
  −87.9 MB of download on `osx-arm64`, and (unexpected) a lower startup floor. That flips an accepted
  ADR and needs the other two RIDs' startup floors from CI, so it is a card of its own, not a
  measurement's decision.

## Limits

- One RID was measured for time (`osx-arm64`); `linux-x64` and `win-x64` were cross-published for size
  only, because a binary for another OS cannot run here. A startup floor from `ci.yml`'s verify matrix
  on each runner is the evidence the follow-up needs.
- The interactive first frame is not measured. It needs a terminal and a configured model, so it is
  not observable on a published binary from a script; `--acp` is the deepest offline path that is.
- The floor is a floor: in two of four measurements the shipped shape's `--version` floor sat above
  the gate's local 150 ms budget (168 and 177 ms), which is the noise ADR-0005's addendum describes,
  and the reason each cell reports both floor and median.

## Reproducing it

```sh
# the four shapes (add -p:EnableCompressionInSingleFile=true to a second pair)
for r2r in true false; do
  dotnet publish src/Lunate.Coding/Lunate.Coding.csproj -c Release -r osx-arm64 --self-contained true \
    -p:PublishSingleFile=true -p:PublishReadyToRun=$r2r -o "artifacts/shape/sf-$r2r"
done
stat -f %z artifacts/shape/sf-true/lunate            # apparent bytes
gzip -9 -c artifacts/shape/sf-true/lunate | wc -c    # gzip -9
tar -czf artifacts/shape/sf-true.tar.gz -C artifacts/shape/sf-true lunate
stat -f %z artifacts/shape/sf-true.tar.gz            # the asset, tar's own gzip level

# startup, the gate's own measurement (BUDGET_MS only has to be out of the way)
BUDGET_MS=100000 PERF_RUNS=20 PERF_WARMUP=3 scripts/perf.sh artifacts/shape/sf-true/lunate

# per component: the same publish loose, then compare the two trees file by file
dotnet publish src/Lunate.Coding/Lunate.Coding.csproj -c Release -r osx-arm64 --self-contained true \
  -p:PublishSingleFile=false -p:PublishReadyToRun=true -o artifacts/shape/loose-true
```

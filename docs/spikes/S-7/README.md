# S-7 spike — execution environments (where do Lunate's tools run?)

Time-boxed spike for issue #625: can `bash` and the file tools run inside a Docker/Podman container
with the workspace bind-mounted, and what does that cost? The answers are posted on the follow-on
cards — [#626](https://github.com/wdaniel1993/lunate/issues/626) (abstraction),
[#627](https://github.com/wdaniel1993/lunate/issues/627) (container runner),
[#628](https://github.com/wdaniel1993/lunate/issues/628) (docs) — and recorded here with the raw
measurements in [`evidence/`](evidence/).

- [`report.md`](report.md) — the findings, the decision and the scope each follow-on card carries.
- `S7.Harness/` — a throwaway console app that drives the **real** `WriteTool`, `ReadTool` and
  `BashTool` against a container. Modes: `env`, `file-tool`, `bash-tool`, `startup`, `cleanup`.
- `scripts/run-evidence.sh` — starts the container, runs every mode, writes `evidence/*.txt`, and
  removes the container and the workspace probes it left.
- `evidence/` — the raw output of one run on 2026-11-08 (macOS 26.3.1 arm64, Podman 6.1.0, Apple
  `podman machine`). Each file starts with the run's own facts; a `FAIL` line in one is a check the
  measurement did not satisfy, and the run exits non-zero if any exists.

## How the container seam is faked without changing the product

The spike does not touch `src/`. `BashTool` takes a `ShellResolver`, whose public override path
(`ShellResolver(string overridePath)`) is the settings seam; the harness points it at a host shim
(`$TMPDIR/s7-container-shell.sh`) that `exec`s `podman exec -i -w <workspace> <container> /bin/bash
"$@"`. The tool therefore runs its whole real path — guard wrap, output cap, timeout, tree kill,
change scan — with only the last hop moved into the container. That seam *is* the thing #626 has to
make first class, which is why measuring through it is honest evidence rather than a reimplementation.

The file tools are not shimmed at all: they run on the host, and the evidence shows the container
seeing their exact bytes through the mount.

## Running it

```bash
# macOS: the machine must be running (podman machine start)
./scripts/run-evidence.sh
# another engine, image or sample count
S7_ENGINE=docker S7_IMAGE=docker.io/library/debian:stable-slim S7_RUNS=25 ./scripts/run-evidence.sh
```

Requires the .NET SDK `global.json` pins (10.0.403) on `PATH`; set `DOTNET=/path/to/dotnet` if the
SDK lives outside it. Nothing here is in `lunate.slnx`, in `scripts/verify.sh` or in the CI gates:
the project is throwaway, and `bin/`/`obj/` are ignored the way `docs/spikes/README.md` requires.

# Extension authoring

An in-process extension is a .NET assembly plus an `extension.json` manifest, or a single `.cs` file
whose header declares what it provides ([single-file.md](single-file.md)). It registers
contributions through `IExtensionContext` — tools, hook handlers, services, file-change
subscriptions, MCP servers, model providers, middleware, virtual models, skill sources and terminal
UI pieces — and compiles against `Lunate.Contracts`, `Lunate.Extensibility.Abstractions` and
`Microsoft.Extensions.AI.Abstractions`, never against `Lunate.Agent` internals. An out-of-process
extension replaces the assembly with a `runtime` child process; see
[remote extensions](../extensions-remote.md).

The tool namespace remains `Lunate.Agent` for source compatibility; using that namespace does not
require a project reference to the `Lunate.Agent` assembly.

This section is the authoring guide. The normative contract is
[`docs/dev/spec/extensibility.md`](../dev/spec/extensibility.md); when this guide and the spec disagree,
the spec wins.

| Page | Contents |
| --- | --- |
| [manifest.md](manifest.md) | Every `extension.json` field, what is enforced, and the refusal messages |
| [single-file.md](single-file.md) | One `.cs` file as an extension: the header grammar, the directive table, trust and the `/extensions` states |
| [packages.md](packages.md) | Installing a package by id: the bundle, dependencies and the lock, the record, both scopes, upgrade, uninstall and trust |
| [lifecycle-and-trust.md](lifecycle-and-trust.md) | Global and project scope, loading, content-hash trust, unload, `apiVersion` |
| [contributions/](contributions/README.md) | One page per surface: API, constraints, a real example |
| [testing.md](testing.md) | The test kit, fixtures, and the missing-primitive rule |
| [troubleshooting.md](troubleshooting.md) | `/extensions` states and the failures you can hit |
| [Remote extensions](../extensions-remote.md) | Out-of-process manifest, wire and conformance runner |
| [Contract and hook catalogue](../dev/spec/extensibility.md) | The normative behaviour |
| [Extension template](../../templates/extension/README.md) | A working starting project |
| [Reference extensions](../../samples/extensions/) | The fitness suite that runs in CI |
| [Supported extensions](../../extensions/) | The first-party tier users install and rely on |

## Contract version notes

What an extension compiles against is versioned under `ExtensionApi`, and a contract change that an
author can feel gets a note here. The notes are additive: the version a note names is the one an
extension declares in its manifest's `apiVersion`.

### 1.16.0 — the event and hook-result families are unions

The contract's closed sets are C# 15 `union` types ([ADR-0043](../../adr/0043-contract-unions.md)):
`AgentEvent` and `ExtensionEvent`, and the seven hook-result families `ProjectTrustResult`,
`InputReceivedResult`, `RunStartingResult`, `MessageCompletedResult`, `ToolCallingResult`,
`TurnEndedResult` and `CompactingResult`. Each union is declared over its cases, so the set of kinds
and outcomes is a compile-time fact. ADR-0043 supersedes
[ADR-0042](../../adr/0042-contract-closed-sets.md), which had chosen `closed` hierarchies.

- **What stays:** every type name, case name and constructor, and the `IHookResult` / `IHookPayload`
  markers. `new ToolCallingResult.Block("held")` converts to the family, `result is
  ToolCallingResult.Block block` and `result switch { ToolCallingResult.Block block => …, … }` match,
  and the hook wire and the `--json` event stream are unchanged.
- **What you gain:** a `switch` over the union is checked for exhaustiveness. Cover every declared
  case, or keep a `_` arm for the ones you ignore — an arm you forget is a build error in this
  repository and a compiler warning in yours, never a silent fall-through. `@event.Value` is the
  concrete event record when you need its type; `events.OfType<ToolCallResult>()` keeps working.
- **What you lose:** a union has no base to derive from, so you cannot declare your own subclass of
  `AgentEvent`, `ExtensionEvent` or a hook-result family, and `AgentEvent` is now a struct: compare
  events by value (`==`), and use `Kind` where you used `GetType()`.
- **If a case is added:** it is a breaking contract change, it moves the API version and it gets a
  note here. Your exhaustive `switch` is why that is announced rather than shipped quietly.
- **Added in 1.16.0:** `TodoUpdated` joins the event unions, carrying the complete todo list after
  each successful `todo` tool call. Handle it or leave it to your `_` arm.

## Quickstart

### 1. Create the project

The fastest start is to copy [`templates/extension/`](../../templates/extension/README.md) and
rename its projects and namespaces. The references that matter are the template's own —
`templates/extension/TemplateExtension/TemplateExtension.csproj`, generated into this page so it
cannot drift from the file:

<!-- snippet: templates/extension/TemplateExtension/TemplateExtension.csproj -->
```xml
<!--
    The host owns Lunate.Contracts, Lunate.Decisions.Model and
    Lunate.Extensibility.Abstractions and loads them once; every extension binds to those
    copies so their types keep one identity. They are NuGet packages pinned to the stable
    contract version (1.15.0, the number the manifest's apiVersion is judged against), and
    each reference is compile-only: PrivateAssets="all" keeps them from flowing to consumers
    of a packaged extension, ExcludeAssets="runtime" keeps them out of the deps file and the
    output directory — a copy there is ignored at load, with one warning saying so. The
    model needs its own reference because the Abstractions dependency would otherwise bring
    a copy into the extension's output.
-->
<PackageReference
    Condition="'$(LunateUseNuGetContracts)' == 'true'"
    Include="Lunate.Contracts"
    Version="$(LunateContractsVersion)"
    PrivateAssets="all"
    ExcludeAssets="runtime"
/>
<PackageReference
    Condition="'$(LunateUseNuGetContracts)' == 'true'"
    Include="Lunate.Decisions.Model"
    Version="$(LunateContractsVersion)"
    PrivateAssets="all"
    ExcludeAssets="runtime"
/>
<PackageReference
    Condition="'$(LunateUseNuGetContracts)' == 'true'"
    Include="Lunate.Extensibility.Abstractions"
    Version="$(LunateContractsVersion)"
    PrivateAssets="all"
    ExcludeAssets="runtime"
/>
```

The contract packages are pinned to `ExtensionApi.Current`; their READMEs carry the versioning and
deprecation policy. Inside this repository the same template binds the
contract as project references instead — the packages a feed serves may not exist before the
release that publishes them, so the template switches on its own: a copy outside this
repository gets the packages, a copy inside gets the projects, and `LunateUseNuGetContracts=true`
forces the package shape (the verify gate does, against a feed of freshly packed packages). The
template declares its target framework, nullability and implicit usings itself, so a project of
your own declares those too. Both its projects target `net11.0`, like the host and the contract
packages: the repository does not multi-target ([ADR-0041](../../adr/0041-net11-target-frameworks.md)),
so an extension for this host is `net11.0` as well.

The host owns `Lunate.Contracts`, `Lunate.Extensibility.Abstractions`, `Lunate.Decisions.Model` and
`Lunate.Agent`, which ships as a preview package, and loads them once, so every extension binds to
those copies. The model needs a reference of its own — the Abstractions dependency would otherwise
bring a copy into your output, where the host ignores it and warns.
`ExcludeAssets="runtime"` keeps the contract assemblies out of your output directory and deps
file, while they still compile against. A copy in the output is ignored at load, with one line
saying which copy and who shipped it — so keep these references as they are. `PrivateAssets="all"`
prevents the contract references from flowing to consumers of a packaged extension.

### 2. Write the extension

One public `IExtensionFactory` with a public parameterless constructor is the entry point. Its
`Create` method registers contributions and returns the extension instance. This example registers
one read-only tool; the model-facing name is `hello__greet`.

```csharp
using System.Text.Json;
using Lunate.Agent;
using Lunate.Extensibility.Abstractions;

namespace HelloExtension;

public sealed class HelloExtension : IExtension { }

public sealed class HelloExtensionFactory : IExtensionFactory
{
    public IExtension Create(IExtensionContext context)
    {
        context.RegisterTool(new ExtensionToolRegistration
        {
            Name = "greet",
            Description = "Returns a fixed greeting.",
            ParametersSchema = JsonSerializer.SerializeToElement(new { type = "object" }),
            Annotations = new ToolAnnotations(ReadOnly: true),
            Handler = new GreetHandler(),
        });
        return new HelloExtension();
    }
}

internal sealed class GreetHandler : IExtensionToolHandler
{
    public Task<ToolResult> InvokeAsync(
        JsonElement arguments,
        ToolContext context,
        CancellationToken ct
    ) => Task.FromResult(new ToolResult("hello from HelloExtension", IsError: false));
}
```

Registration is registration only: `Create` must not start processes, sockets or timers. Long-lived
resources belong in a registered background service or start on first use — see
[background-services.md](contributions/background-services.md).

### 3. Write `extension.json`

```json
{
    "id": "hello",
    "version": "0.1.0",
    "apiVersion": "^1.10.0",
    "entryAssembly": "HelloExtension.dll",
    "tools": ["greet"]
}
```

`tools` must list every tool the code registers by its local name. The full field reference is
[manifest.md](manifest.md).

### 4. Build and install

```bash
dotnet build -c Release
mkdir -p ~/.lunate/extensions/hello
cp bin/Release/net11.0/HelloExtension.dll \
   bin/Release/net11.0/extension.json \
   ~/.lunate/extensions/hello/
```

Copy any further dependencies next to the assembly. `Lunate.Contracts`,
`Lunate.Extensibility.Abstractions`, `Lunate.Decisions.Model`, `Lunate.Agent`,
`Microsoft.Extensions.AI.Abstractions` and `System.Text.Json` are shared from the host and are never
loaded from the extension folder: a copy shipped there anyway is ignored, and `/extensions` and the
log say so once, naming what was dropped.

There are two installation scopes:

| Scope | Directory | Trust |
| --- | --- | --- |
| Global | `~/.lunate/extensions/<directory>/` | Loaded as-is; no prompt |
| Project | `<worktree>/.lunate/extensions/<directory>/` | Repository code; one trust decision per repository, re-asked when the files change |

The directory name is not the extension identity: `id` in the manifest is. Every directory under
either scope needs its own `extension.json`.

Per-extension settings go in `~/.lunate/extensions-settings/<id>.json` and secrets in
`~/.lunate/extensions-secrets/<id>.json` — see
[settings-and-secrets.md](contributions/settings-and-secrets.md).

### 5. Confirm it loaded

Start `lunate` in a session and run `/extensions`. A loaded global extension reads:

```text
extensions: hello 0.1.0 (global) loaded in 4 ms, tools 1, commands 0, hooks 0, services 0
```

The same data is available without a session as `lunate extensions --json`. Ask the model to call
`hello__greet`; a read-only tool runs without the write approval.

If the extension is missing from the list, it did not survive discovery; the reason is in the
session log (the manifest is read before any code loads). If it reads `trust-denied` or `failed`,
see [troubleshooting.md](troubleshooting.md).

## Related

- The contribution you want to add: [contributions/](contributions/README.md).
- When it loads, and how project trust behaves: [lifecycle-and-trust.md](lifecycle-and-trust.md).
- How to test it without live APIs: [testing.md](testing.md).
- The normative semantics, hook catalogue and fitness suite: [`docs/dev/spec/extensibility.md`](../dev/spec/extensibility.md).

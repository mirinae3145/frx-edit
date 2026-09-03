# Contributing to FrxEdit

This guide is for humans and AI agents changing FrxEdit itself. If the task is to edit JSON, VBA, or assets exported by FrxEdit, use the [packaged AI editing guide](ai-plugin/skills/frxedit-builder.md) instead.

FrxEdit modifies a binary format with several representations of the same form. Treat parser, model, Writer, schema, documentation, and regression behavior as one contract.

## Development setup

Install the .NET 8 SDK and PowerShell 7 or another PowerShell capable of running the repository scripts. The codebase has no required third-party NuGet packages beyond the SDK-provided framework.

```powershell
dotnet restore FrxEdit.sln
dotnet build FrxEdit.sln -c Release
dotnet run --project src/FrxEdit.Cli/FrxEdit.Cli.csproj -c Release -- --help
```

The CLI project targets `net8.0`. Build and test on the platforms affected by a change when practical. Native Office checks are separate because the automated suites do not drive the VBE or an Office application.

## Repository map

| Path | Purpose |
| --- | --- |
| `src/FrxEdit.Cli` | CLI, `.frm` handling, CFB/MSForms parsing, reconstruction, validation, and provenance |
| `tests/FrxEdit.Tests` | Focused executable regressions for binary boundaries and contract behavior |
| `test_data` | Repository-owned form pair and generated-container template |
| `scripts` | Canonical comparison and end-to-end reconstruction suites |
| `docs` | Architecture, supported Writer surface, and patch schema |
| `ai-plugin` | Instructions packaged with published builds for editing generated artifacts |
| `.github/workflows` | Automated build, test, packaging, and release jobs |

The AI package is copied into CLI publish output by `FrxEdit.Cli.csproj`. Changes to it must remain useful outside the source tree; links and instructions should not assume that an agent can edit FrxEdit source code.

## Fork engineering baseline

Changes in this fork build on these invariants and repaired behaviors:

- FormControl parsing consumes either legal `GuidAndFont` representation exactly before FormSiteData begins.
- Strict parsing rejects missing, unexpected, malformed, or trailing FormDesignExData according to `FORM_FLAG_DESINKPERSISTED`; tolerant parsing reports recoveries without presenting them as exact.
- The `f`, `o`, and `x` streams are planned together. Per-storage site order and object-payload order must remain coordinated.
- Frames, MultiPages, and Pages own storages and can contain nested controls. Parent dependencies are resolved from the completed graph rather than incoming JSON order.
- Explicit Pages control MultiPage recreation; compatibility fallback Pages are used only when the template does not provide them.
- Generated UserForms, MultiPages, Pages, and Frames use the appropriate persisted DesignExtender data when their flags require it.
- Existing site records and object payloads are copied when unchanged. Rebuilt records start from parsed source state so omitted values and unknown packed bits are not replaced wholesale by generator defaults.
- Structural SITE_FLAG values (`streamed` and `promoteControls`) are derived from topology. Editable named projections overlay the raw `siteBitFlags` word.
- Root `.frm` text, root binary FormControl data, control sites, and object payloads are separate mutation domains.
- Relative `file://` assets resolve from the containing JSON document for all patch entry points.
- Exported patches/templates have a flattened compatibility representation, while normalization maps them to the canonical top-level graph, layout, and property operations.
- Reader and Writer provenance record where supported values were observed, transformed, preserved, emitted, and reread.
- Canonical comparison gates supported semantics and reports native structural normalization separately instead of requiring byte-identical FRX output.

Do not weaken these behaviors to make a new fixture pass. If a format assumption changes, document the evidence, update the relevant parser/Writer boundary, and add a regression that distinguishes exact parsing from heuristic recovery.

## Format and preservation rules

### Coordinated files

A UserForm is at least a paired `.frm` and `.frx`. The `.frm` contains the root form declaration, attributes, and VBA; child control structure is held in the `.frx`. A `.scopes.json` sidecar may preserve control scope information that cannot be recovered reliably from the abbreviated `.frm` declaration.

Never update only the `OleObjectBlob` filename or only one half of an output pair. Build/create code must write a consistent pair and reread it before reporting success.

### Binary streams

- Preserve unknown bytes and bits unless a documented reconstruction path owns them.
- Keep FormSiteData order and corresponding `o` payload order synchronized per storage.
- Do not infer tab order or global z-order from flattened Reader order.
- Treat MultiPage Page order as explicit tab/Page state, not dictionary ordering.
- Keep persistence flags and FormDesignExData presence consistent.
- Prefer semantic assertions over compound-file byte equality.

### Text and encodings

FrxEdit preserves UTF-8 BOM input as UTF-8 BOM and otherwise reads/writes `.frm` using Windows-1252. New generated forms use Windows-1252. JSON outputs and scope files use UTF-8.

Do not normalize line endings, re-encode source forms, or rewrite unrelated VBA as a side effect. Test non-ASCII captions, names, and code whenever encoding logic changes.

### JSON contract

The canonical sections are `properties`, `layout`, `renames`, `move`, `remove`, `add`, and `code`. Exported files may also place compatibility metadata such as `$action`, `$newName`, `type`, `parent`, and point geometry inside `properties`; `PatchDocument.Normalize` translates it before validation.

The published schema is a shared value-shape envelope. Existing-control patches do not need to repeat a control type, so `PatchValidator` and `RebuildPatchApplier` remain the final type-aware authorities. Keep all three layers aligned:

1. Deserialization and normalization must preserve accepted compatibility input.
1. The schema must describe every public value representation without claiming a property applies to every control.
1. The CLI must reject unknown properties, invalid target types, graph conflicts, and unsupported mutation domains with actionable errors.

Compatibility aliases may be retained when they have a Writer consumer. Exporters should use one canonical spelling.

## Implementing changes

### Parser work

Record exact offsets, lengths, masks, and validation state needed by the Writer or provenance system. Strict mode must have deterministic boundaries. Any tolerant search or recovery must be bounded and explicitly reported.

### Writer and graph work

Build reconstruction intent before serialization. Structural changes must account for descendants, owned storages, object-bearing site order, IDs, names, parent scopes, and companion `x` streams. New controls should use type-specific factory defaults; changes to existing controls should begin from parsed state.

When adding an editable property:

1. Parse and expose it with sufficient native evidence.
1. Add it to the correct root, site, object, container, or tab Writer set.
1. Validate its JSON shape and control-type compatibility.
1. Preserve omitted and unknown packed state.
1. Export the canonical name and retain only necessary compatibility aliases.
1. Update the schema, supported-controls reference, AI editing guide if relevant, and tests.

### CLI work

Defaults and option interactions are public behavior. Keep `--help`, `README.md`, and errors synchronized. Prefer output to a separate pair for one-shot commands. In-place or continuously rewriting operations must use temporary files and recoverable backups.

### Documentation work

Use current code and passing tests as the source of truth. Distinguish these claims explicitly:

- parsed or observed;
- accepted by the Writer;
- generated for new forms;
- preserved only when unchanged;
- covered by automated fixtures; and
- verified separately in a native host.

Avoid performance promises, claims of universal Office compatibility, or wording that implies successful serialization proves VBA compilation or native behavior.

## Testing

Run the focused tests and both end-to-end suites for changes that affect parsing, normalization, validation, reconstruction, exported JSON, assets, or form text:

```powershell
dotnet run --project tests/FrxEdit.Tests/FrxEdit.Tests.csproj -c Release
./scripts/test-canonical-roundtrip.ps1 -Configuration Release
./scripts/test-generated-container-pipeline.ps1 -Configuration Release
```

The focused executable should contain small deterministic regressions for byte boundaries and model contracts. The canonical suite covers no-op builds, positional and named patches, watch regeneration, template recreation, strict reread, and semantic comparison. The generated-container suite covers graph ordering, nested containers, explicit/fallback Pages, and generated stream boundaries.

Suites retain diagnostics under `.build` by default. Use `-ArtifactsRoot` to isolate a run. `-SkipWatch` is diagnostic only; a normal acceptance run includes watch behavior.

When a change affects creation, container topology, examples, or the public editing instructions, an additional clean-room smoke test can expose assumptions hidden by an existing source form. Create a form from zero with a nested Frame and a MultiPage containing explicit Pages and child controls, then run strict validation and both human and raw inspection. Prefer converting a recurring failure into a deterministic automated regression instead of retaining a one-off prompt as the test definition.

Also run focused commands for the behavior being documented. Useful examples include:

```powershell
frxedit inspect test_data/forms/original/userformallcontrol.frm --mode strict --as-patch
frxedit validate test_data/forms/original/userformallcontrol.frm --mode strict
```

If a change affects Office-facing behavior, record which application/version was tested and what was checked: import, compilation, layout, focus, events, interaction, and save/reopen. Do not turn the absence of a native environment into an automated-pass claim.

If strict validation succeeds but a native host rejects the generated form, preserve the failing pair and diagnose the representation boundary before changing generator defaults:

- Confirm that the output `.frm` `OleObjectBlob` line names the generated `.frx`.
- Generate strict human and raw inspection output and review the rebuild report; require `semanticMatch: true` wherever the report defines an expected supported-semantic result.
- Check FormSiteData parent, depth, type, and site-order relationships against the reconstructed graph.
- Check owned storage paths and the coordinated `f`, `o`, and `x` stream sizes and object-payload ordering.
- For MultiPage/Page failures, inspect the internal TabStrip state and each Page's companion `x` stream metadata.

Use `dump-storage` and `dump-stream-records` to collect structural evidence. Native rejection is not, by itself, evidence that an unrelated parser or Writer invariant should be weakened.

## Review checklist

- The source form and unrelated working-tree changes remain untouched.
- New behavior has a regression at the narrowest useful level.
- Strict and tolerant behavior remain distinguishable.
- Existing no-op preservation and nested-container scenarios still pass.
- Exported JSON contains only fields with a valid Writer path.
- Schema, CLI help, README, supported-controls documentation, and AI instructions agree.
- Error messages identify the control/property or binary boundary involved.
- Generated outputs and `.build` diagnostics are not accidentally committed.
- Any native-host evidence states its environment and scope.

## Guidance for AI agents

AI agents contributing source changes must follow this file, inspect the implementation before editing docs, and verify observable behavior rather than extrapolating from names. Preserve user changes already present in the worktree and avoid bulk rewrites unrelated to the task.

Do not edit `.frx` fixtures manually. Do not regenerate the repository fixture unless the requested behavior requires it and the resulting binary change has been inspected. Prefer small source patches and focused regressions. When a command produces retained artifacts, review only the relevant reports and leave cleanup decisions to the task's scope.

Do not silently broaden a compatibility surface. If code accepts a legacy representation, either preserve and document it or make a deliberate migration with explicit approval and tests. If documentation and code disagree, correct the smallest implementation defect needed for a truthful contract or report the larger mismatch before redesigning it.

The packaged AI guide is product-facing documentation, not contributor policy. Keep it focused on safely editing FrxEdit-generated JSON, `.vba`, and assets; source-development guidance belongs here.

## Commits and change descriptions

Keep commits coherent and explain non-obvious preservation or compatibility decisions in the message body. Summaries should describe user-visible behavior or the repaired invariant. Include test commands and native validation scope in the contribution description so reviewers can reproduce the evidence.

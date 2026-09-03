# FrxEdit

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

FrxEdit is a .NET command-line tool for inspecting, exporting, patching, and reconstructing VBA UserForms represented by paired `.frm` and `.frx` files. It exposes the supported MSForms state as JSON, keeps VBA in an editable sidecar, and can extract embedded picture data into ordinary files.

FrxEdit is intended for controlled source-based workflows. It understands a documented subset of MS-OFORMS and preserves source-derived state that is outside an explicit patch where the current reconstruction path supports doing so. It does not replace testing in the VBA editor or an Office host.

## What this fork adds

This fork extends the original tool around semantic round trips and diagnosable reconstruction:

- strict, tolerant, and legacy inspection modes;
- exact parsing checks at the FormStreamData, `GuidAndFont`, FormSiteData, and FormDesignExData boundaries;
- coordinated reconstruction of `f`, `o`, and `x` streams;
- graph-aware add, remove, rename, and move operations;
- nested Frame, MultiPage, and Page storage generation;
- preservation of unchanged site records, object payloads, unknown packed bits, root text, and VBA;
- template recreation with persisted DesignExtender data when required by the form flags;
- patch-relative `file://` asset resolution and automatic asset extraction;
- Reader and Writer provenance reports;
- semantic comparison that separates user-visible values from native structural diagnostics; and
- repository-owned regression suites for existing-form and generated-container pipelines.

These capabilities are limited to the controls and properties in [Supported controls and properties](docs/supported-controls.md). FRX byte identity is not an acceptance criterion because a rebuilt compound file can encode the same supported state differently.

## Supported controls

The current Writer recognizes these 15 control types:

| Category | Controls |
| --- | --- |
| Text and choices | CommandButton, Label, TextBox, CheckBox, OptionButton, ToggleButton |
| Lists | ComboBox, ListBox |
| Numeric selectors | ScrollBar, SpinButton |
| Pictures | Image |
| Tabs | TabStrip |
| Containers | Frame, MultiPage, Page |

The root UserForm has its own narrower contract. Properties are not universal across the controls above: for example, Frame is not treated as a general picture-bearing control, TabStrip does not share the MultiPage container fields, and ComboBox/ListBox use `displayStyle` rather than the tab `style` property. Consult the full property matrix before adding a field to a patch.

## Requirements and platforms

- Building from source requires the .NET 8 SDK.
- Framework-dependent published binaries require a compatible .NET 8 runtime.
- Self-contained packages, when published, include their runtime.
- Inspection and reconstruction run on Windows, Linux, and macOS under .NET. Native Office/VBE validation requires a compatible Windows Office environment and is not part of the cross-platform automated tests.

## Build from source

```powershell
dotnet restore FrxEdit.sln
dotnet build FrxEdit.sln -c Release
dotnet run --project src/FrxEdit.Cli/FrxEdit.Cli.csproj -c Release -- --help
```

To publish a local framework-dependent executable:

```powershell
dotnet publish src/FrxEdit.Cli/FrxEdit.Cli.csproj `
  -c Release --self-contained false -o .build/frxedit
```

The workflow also produces platform-specific artifacts, and version tags can publish release archives. Availability depends on completed workflow and release runs in this repository.

## Quick start

Keep the source pair together: an input `.frm` must reference its accompanying `.frx` through `OleObjectBlob`.

### Inspect a form

```powershell
# Human-oriented view; tolerant mode is the inspect default.
frxedit inspect UserForm1.frm --out UserForm1.inspect.json

# Strict raw inspection for diagnostics.
frxedit inspect UserForm1.frm --mode strict `
  --out UserForm1.inspect.json --raw-out UserForm1.raw.json
```

Tolerant mode retains reachable information and reports recoveries. Strict mode rejects malformed boundaries, inconsistent persistence flags, unexpected trailing structures, and other conditions that make a supported round trip unsafe. Legacy mode retains the earlier heuristic inspection path for compatibility and diagnosis.

### Export an editable patch

```powershell
frxedit inspect UserForm1.frm --mode strict --as-patch `
  --out UserForm1.patch.json
```

Writing a patch or template with `--out` also writes a matching `.vba` file, such as `UserForm1.patch.vba`. If the form contains supported pictures or mouse icons, they are extracted into a `UserForm1` subdirectory beside the JSON and represented by relative `file://` references. `--extract-images` remains accepted for compatibility, but extraction is already automatic for file-based patch/template exports. An export written to standard output keeps assets inline as `base64:` values.

### Rebuild to a separate pair

```powershell
frxedit build UserForm1.frm UserForm1.patch.json `
  --out rebuilt/UserForm1.frm

# Equivalent named patch form.
frxedit build UserForm1.frm --patch UserForm1.patch.json `
  --out rebuilt/UserForm1.frm
```

`build` defaults to strict parsing and `full-patch` reconstruction. It writes both `rebuilt/UserForm1.frm` and `rebuilt/UserForm1.frx`, then rereads the result. Using a separate output directory is recommended until the rebuilt pair has passed the checks appropriate to the project.

A build without a patch exercises a no-op semantic reconstruction:

```powershell
frxedit build UserForm1.frm --out rebuilt/UserForm1.frm
```

### Export and recreate a template

```powershell
frxedit inspect UserForm1.frm --mode strict --as-template `
  --out UserForm1.template.json

frxedit create recreated/UserForm1.frm --name UserForm1 `
  --patch UserForm1.template.json
```

A template includes the `add` list and generated-only state needed to recreate the supported control graph. It is not a general conversion of every native field.

## Patch model

The canonical top-level sections are:

| Section | Purpose |
| --- | --- |
| `properties` | Root, site, control, container, and tab properties keyed by form/control name |
| `layout` | Geometry changes keyed by control name |
| `renames` | Existing control name to new control name |
| `move` | Control name to new parent name, or `null`/empty root ownership |
| `remove` | Existing control names to remove |
| `add` | New control declarations and optional inline properties |
| `code.tabStripPanels` | Optional generated VBA for switching Frame panels from a TabStrip |

For example:

```json
{
  "properties": {
    "UserForm1": {
      "caption": "Order editor"
    },
    "SaveButton": {
      "caption": "Save",
      "fontBold": true
    },
    "StatusLabel": {
      "caption": "Ready"
    }
  },
  "layout": {
    "SaveButton": {
      "leftPt": 18,
      "topPt": 144,
      "widthPt": 72,
      "heightPt": 24
    }
  },
  "renames": {
    "CommandButton1": "SaveButton"
  },
  "move": {
    "DetailsTextBox": "FooterFrame"
  },
  "remove": [
    "ObsoleteLabel"
  ],
  "add": [
    {
      "type": "Label",
      "name": "StatusLabel",
      "parent": "FooterFrame",
      "leftPt": 8,
      "topPt": 8,
      "widthPt": 100,
      "heightPt": 18
    }
  ]
}
```

Names must remain valid VBA identifiers. A patch cannot remove a control and also explicitly rename, move, lay out, or patch it. Parent dependencies are resolved as a graph, so `add` entries do not have to be ordered parent-first.

### Generated compatibility form

Patch/template exports currently place geometry, `type`, `parent`, `$action`, and `$newName` in each `properties` entry. That representation is accepted and normalized:

```json
{
  "properties": {
    "CommandButton1": {
      "$action": "edit",
      "$newName": "",
      "type": "CommandButton",
      "leftPt": 18,
      "topPt": 144,
      "widthPt": 72,
      "heightPt": 24,
      "caption": "Save"
    }
  }
}
```

When editing a generated file, it is safe to update those existing values in place. For structural edits, either use the canonical top-level sections and remove conflicting flattened fields, or use the compatibility actions:

- `"$action": "remove"` converts the entry to a canonical removal and discards the entry's other payload.
- `"$action": "rename"` requires a non-empty `$newName`.
- `"$action": "add"` requires `type`; `parent` and point geometry describe placement.

## VBA sidecars

Patch/template inspection with `--out path/name.json` writes `path/name.vba`. During `build`, `create`, and `watch`, FrxEdit first looks for the `.vba` file sharing the patch's stem. If it does not exist, it can use `<source>.frm.vba` as a fallback.

The sidecar contains the editable VBA portion, not a complete form export. Keep procedure names and control references synchronized with control renames/removals. A successful binary rebuild does not prove that the resulting VBA compiles or that event procedures have the intended signature.

`code.tabStripPanels` can generate a marked block containing `UserForm_Initialize`, the relevant `<TabStrip>_Change` handlers, and Frame visibility logic. FrxEdit rejects generation if conflicting procedures already exist outside its marked block.

## Assets and paths

Supported `picture` and `mouseIcon` values use either:

```json
{
  "picture": "file://UserForm1/Image1_picture.png",
  "mouseIcon": "base64:..."
}
```

Relative file URIs are resolved from the patch/template document's directory for positional patches, `--patch`, `build`, `create`, and `watch`. They do not depend on the process working directory. Keep the JSON and its form-named asset directory together when moving an exported edit set.

## Watch mode

```powershell
frxedit watch UserForm1.frm UserForm1.patch.json `
  --out preview/UserForm1.frm --mode strict
```

Watch observes the patch, its VBA sidecar, and supported image files. It always uses full-patch reconstruction. If `--out` is omitted, it replaces the source pair in place and creates `.bak` files when prior outputs exist. With a separate `--out`, it performs an initial build and then rebuilds after changes.

`--wysiwyg` rewrites the patch from the effective reconstructed layout after a successful build and creates a backup of the previous patch. Do not enable it when hand-authored formatting or comments in the JSON must be retained.

Stop watch mode with Ctrl+C.

## Diagnostics

```powershell
frxedit inspect UserForm1.frm --mode strict --as-template `
  --out UserForm1.template.json `
  --raw-out UserForm1.raw.json `
  --reader-audit-out UserForm1.reader-audit.json

frxedit build UserForm1.frm UserForm1.patch.json `
  --out rebuilt/UserForm1.frm `
  --writer-audit-out UserForm1.writer-audit.json `
  --report-out UserForm1.rebuild-report.json
```

Reader audits trace parser observations through the raw model to exported JSON. Writer audits trace JSON through normalization, reconstruction planning, emitted binary evidence, and strict reread. Large binary values are represented by lengths and hashes rather than repeated in full.

Additional inspection commands are available through `frxedit --help`, including `validate`, `dump-records`, `dump-storage`, `dump-stream-records`, and `check-internal`.

## Safety and validation

Treat `.frm`, `.frx`, JSON, VBA, and extracted assets as one coordinated edit set.

1. Work from version-controlled files or a recoverable copy.
1. Prefer strict inspection before editing.
1. Build to a separate output pair.
1. Strictly validate or inspect the rebuilt pair.
1. Review semantic/provenance reports when preservation matters.
1. Import into the applicable Office/VBE host, compile the VBA project, exercise controls and events, and save/reopen before replacing a production form.

Automated checks cover the implemented codec and tested fixtures. They do not establish native import, compilation, rendering, focus behavior, event execution, runtime interaction, or save/reopen compatibility for every host and form.

## Repository verification

```powershell
dotnet run --project tests/FrxEdit.Tests/FrxEdit.Tests.csproj -c Release
./scripts/test-canonical-roundtrip.ps1 -Configuration Release
./scripts/test-generated-container-pipeline.ps1 -Configuration Release
```

The focused executable covers binary boundaries, persistence-flag behavior, and patch normalization/conflicts. The canonical suite exercises no-op reconstruction, both patch input forms, watch regeneration, template recreation, strict reread, and semantic comparison against the local comprehensive fixture. The generated-container suite checks nested storage planning, MultiPage/Page relationships, DesignExtender boundaries, and container regeneration. Test artifacts are retained under `.build` unless another `-ArtifactsRoot` is supplied.

Forms obtained from the external IguanaTex project have also been used to verify FrxEdit operation. IguanaTex is not a dependency or part of the standard build, test, or contribution workflow.

## Documentation

- [Supported controls and properties](docs/supported-controls.md)
- [Patch JSON Schema](docs/frxedit-patch.schema.json)
- [Architecture and binary format](docs/architecture.md)
- [Contributor guide](CONTRIBUTING.md)
- [Packaged AI editing guide](skills/edit-frx/SKILL.md)

## AI use disclosure

Parts of FrxEdit and its documentation have been developed with AI-assisted tools. Maintainers remain responsible for reviewing changes and deciding what is released. JSON or VBA generated by an AI system is untrusted input and requires the same schema, CLI, code-review, and native-host validation as manually authored content.

## License

FrxEdit is licensed under the [MIT License](LICENSE).

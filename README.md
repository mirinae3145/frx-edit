# FrxEdit: MSForms JSON round-trip CLI

[![Build and Release](https://github.com/viktormax3/vba-macro-project/actions/workflows/build.yml/badge.svg)](https://github.com/viktormax3/vba-macro-project/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

FrxEdit is a cross-platform command-line tool for inspecting, extracting, patching, and reconstructing Microsoft Forms (`.frm` and `.frx`). It exposes supported MSForms state as JSON so forms can be reviewed in version control or changed without driving the VBA editor.

FrxEdit targets semantic fidelity for the controls and properties documented in this repository. It does not use FRX byte equality as its correctness criterion, and automated codec tests do not replace validation in PowerPoint, the VBE, or another native host.

## Features

- Strict and tolerant inspection of MS-OFORMS `f`, `o`, and `x` streams.
- JSON patch export for updating an existing form.
- JSON template export for recreating a supported form graph.
- Reconstruction of nested Frame, MultiPage, and Page storage graphs.
- Extraction and reapplication of embedded picture and mouse-icon assets.
- Machine-readable Reader and Writer provenance reports for fidelity diagnosis.
- Copy-based round-trip tests that leave source forms unchanged.

See [Supported Controls & Properties](docs/supported-controls.md) for the current JSON contract and [Architecture & Binary Format](docs/architecture.md) for implementation details.

## Installation

Download a framework-dependent or self-contained build from the project’s GitHub Actions artifacts or Releases page. Framework-dependent builds require .NET 8; self-contained builds do not require a separate .NET installation.

## Usage

### Inspect and export

```bash
# Human-readable inspection
frxedit inspect UserForm1.frm --mode strict --out UserForm1.inspect.json

# Patch for reapplying the exposed state to an existing form
frxedit inspect UserForm1.frm --mode strict --as-patch \
  --extract-images --out UserForm1.patch.json

# Template for recreating the supported control graph
frxedit inspect UserForm1.frm --mode strict --as-template \
  --extract-images --out UserForm1.template.json
```

When `--extract-images` is used, relative `file://` references are resolved from the directory containing the patch or template JSON. This rule is the same for positional patches, `--patch`, `create`, `build`, and `watch`; it does not depend on the process working directory.

### Build an existing form

```bash
# No-op reconstruction
frxedit build UserForm1.frm --out UserForm1.rebuilt.frm \
  --mode strict --stream-mode full-patch

# Apply a patch; the positional and --patch forms have the same path semantics
frxedit build UserForm1.frm UserForm1.patch.json \
  --out UserForm1.rebuilt.frm --mode strict --stream-mode full-patch

frxedit build UserForm1.frm --patch UserForm1.patch.json \
  --out UserForm1.rebuilt.frm --mode strict --stream-mode full-patch
```

Unchanged existing sites and object payloads are preserved. Changed fields are reconstructed from the parsed native state plus the requested delta so omitted and unknown native state is not replaced by generator defaults.

The CLI validates properties against the target control type and rejects known type-incompatible fields instead of silently ignoring them. The root UserForm has a narrower Writer contract than ordinary controls: existing root picture, mouse-icon, and font bytes are retained when unchanged, but those payloads are not currently editable or generated from patch/template JSON.

### Create a form from a template

```bash
frxedit create UserForm1.frm --name UserForm1 \
  --patch UserForm1.template.json
```

### Watch a patch

```bash
frxedit watch UserForm1.frm UserForm1.patch.json \
  --out UserForm1.rebuilt.frm --stream-mode full-patch
```

### Provenance diagnostics

```bash
frxedit inspect UserForm1.frm --mode strict --as-template \
  --out UserForm1.template.json --raw-out UserForm1.raw.json \
  --reader-audit-out UserForm1.reader-audit.json

frxedit build UserForm1.frm UserForm1.patch.json \
  --out UserForm1.rebuilt.frm --mode strict --stream-mode full-patch \
  --writer-audit-out UserForm1.writer-audit.json
```

Reader audits trace parser observations through the raw model and exported JSON. Writer audits trace the requested JSON through normalization, reconstruction, CFB output, and strict reread. Large payloads are represented by length and SHA-256 metadata.

## Fidelity testing

The repository provides two copy-based regression suites:

```powershell
./scripts/test-canonical-roundtrip.ps1 -Configuration Release
./scripts/test-generated-container-pipeline.ps1 -Configuration Release
```

The canonical test always exercises `test_data/forms/original/userformallcontrol`. To run the external nine-form IguanaTex corpus as well:

```powershell
./scripts/test-canonical-roundtrip.ps1 -Configuration Release `
  -IguanaTexRepo ../IguanaTex `
  -ArtifactsRoot ../IguanaTex/.build/frxedit-canonical
```

The canonical suite includes bounded `watch` regeneration from a working directory unrelated to the patch file, followed by strict reread and semantic comparison. It retains every run under `.build` by default. The comparator treats identity, type, parentage, geometry, explicit tab order, Page order, exposed values, root `.frm` properties, root `formGroupCount`, and picture content as hard semantics. Documented default-vs-omitted representations are normalized. Native per-storage Sites order, corresponding object-payload order, and `formShapeCookie` are reported separately, and flattened Reader array order is not treated as form semantics.

These tests establish automated codec behavior only. Native-host import, VBE compilation, visual layout, event execution, interaction, and save/reopen behavior require separate Office validation.

## AI use disclosure

Parts of FrxEdit and its documentation have been developed with AI-assisted tools. Maintainers remain responsible for reviewing changes, validating behavior, and deciding what is released. JSON generated by an AI system is untrusted input and is subject to the same schema, CLI, and native-host validation as manually authored patches.

## License

FrxEdit is licensed under the [MIT License](LICENSE).

---
name: edit-frx
description: Guidance for AI agents editing JSON, VBA sidecars, and assets exported by the FrxEdit CLI.
---

# Editing FrxEdit-generated UserForm artifacts

Use this guide when the user asks you to edit JSON, a `.vba` sidecar, or an asset set exported by FrxEdit. It does not authorize changes to FrxEdit source code, tests, release configuration, or binary fixtures. Repository development follows `CONTRIBUTING.md` when that file is available.

FrxEdit operates on paired VBA UserForm `.frm` and `.frx` files. JSON is a supported edit interface for a documented subset of MSForms; it is not a complete replacement for the native VBA editor.

## Safety boundary

- Do not modify `.frx` bytes directly.
- Keep the input `.frm` beside the `.frx` named by its `OleObjectBlob` line.
- Preserve the original pair or ensure it is recoverable before rebuilding.
- Prefer strict inspection and a separate output path.
- Treat generated JSON, VBA, and assets as untrusted input until the CLI accepts them and the rebuilt form is checked in its intended Office host.
- Do not claim that a successful rebuild proves VBA compilation, visual fidelity, event behavior, or save/reopen compatibility.

When strict inspection rejects the source, report the error. Use tolerant mode for diagnosis only unless the user explicitly accepts a tolerant reconstruction risk.

## Artifact set

Given this command:

```powershell
frxedit inspect UserForm1.frm --mode strict --as-patch `
  --out edits/UserForm1.patch.json
```

FrxEdit may produce:

- `edits/UserForm1.patch.json`: editable layout and supported properties;
- `edits/UserForm1.patch.vba`: editable VBA body; and
- `edits/UserForm1/`: extracted picture and mouse-icon files when the form has supported embedded assets.

`--as-template` additionally exports the `add` list and generated-only data used to recreate the supported graph. File-based patch/template export extracts embedded assets automatically. `--extract-images` is only a compatibility flag. Output sent to standard output retains `base64:` assets inline.

Keep the JSON, its `.vba` sidecar, and its form-named asset directory together.

## Editing workflow

1. Inspect the form in strict mode and export a patch or template if the user has not supplied one.
1. Read the entire JSON and identify the root form name, control names, types, parents, and existing generated fields.
1. Make the smallest requested change. Preserve fields you do not understand rather than replacing an entry from memory.
1. Update the `.vba` sidecar when renamed, removed, or added controls affect procedures or references.
1. Rebuild to a separate pair.
1. Confirm that the rebuilt `.frm` references the generated `.frx` through its `OleObjectBlob` line.
1. Strictly inspect or validate the rebuilt pair. For a failed or questionable rebuild, generate both human and raw inspection output and review the rebuild report. Require `semanticMatch: true` when the report defines an expected supported-semantic result.
1. Tell the user that native Office/VBE checks remain necessary for compilation and runtime behavior.

Recommended build:

```powershell
frxedit build UserForm1.frm edits/UserForm1.patch.json `
  --out preview/UserForm1.frm --mode strict

frxedit validate preview/UserForm1.frm --mode strict
```

`build` already defaults to strict mode and full-patch reconstruction; the explicit mode above makes the acceptance condition visible.

## Generated JSON representation

An exported patch normally resembles this:

```json
{
  "properties": {
    "UserForm1": {
      "caption": "Example"
    },
    "CommandButton1": {
      "$action": "edit",
      "$newName": "",
      "type": "CommandButton",
      "leftPt": 18,
      "topPt": 120,
      "widthPt": 72,
      "heightPt": 24,
      "caption": "OK"
    }
  }
}
```

For an ordinary property or geometry request, edit the existing exported value in place. Do not add a second conflicting value in a top-level section.

The canonical top-level sections accepted by FrxEdit are:

| Section | Purpose |
| --- | --- |
| `properties` | Root and control values keyed by name |
| `layout` | Geometry keyed by control name |
| `renames` | Old name to new name |
| `move` | Control name to new parent; `null` or an empty parent means the root |
| `remove` | Names of existing controls to remove |
| `add` | New controls, parents, geometry, and optional properties |
| `code.tabStripPanels` | Optional generated VBA for TabStrip-controlled Frame panels |

The `$action` fields in exported entries are a supported compatibility interface:

- `edit` keeps the entry as an existing-control edit.
- `remove` converts the control to `remove` and discards other values in that entry.
- `rename` requires a non-empty `$newName`.
- `add` requires a valid `type`; include `parent` and point geometry when applicable.

For a top-level `remove`, delete the same name from `properties` and `layout`; a control cannot be removed and explicitly edited in the same patch. A top-level rename may continue to use the old key for source-targeted properties or the new name where the validator permits a renamed target. Avoid combining compatibility actions with duplicate canonical operations.

## Canonical edit examples

Move and resize an existing control:

```json
{
  "layout": {
    "CommandButton1": {
      "leftPt": 18,
      "topPt": 144,
      "widthPt": 84,
      "heightPt": 24
    }
  },
  "properties": {
    "CommandButton1": {
      "caption": "Save",
      "fontBold": true
    }
  }
}
```

Add a nested Label:

```json
{
  "add": [
    {
      "type": "Label",
      "name": "StatusLabel",
      "parent": "FooterFrame",
      "leftPt": 8,
      "topPt": 8,
      "widthPt": 100,
      "heightPt": 18,
      "properties": {
        "caption": "Ready"
      }
    }
  ]
}
```

Rename and remove controls:

```json
{
  "renames": {
    "CommandButton1": "SaveButton"
  },
  "remove": [
    "ObsoleteLabel"
  ]
}
```

Names must be valid VBA identifiers and unique after all operations. Update VBA event procedures and references when a control is renamed or removed.

When adding a control, choose a stable, descriptive VBA-compatible name suitable for event procedures and code references. Parent controls according to the supported graph:

- An omitted or `null` parent means the root UserForm.
- Ordinary controls may be parented to the root, a `Frame`, or a `Page`.
- A `Page` may only be parented to a `MultiPage`.
- Do not parent an ordinary control directly to a `MultiPage`; parent it to one of that MultiPage's Pages.

## Controls and property boundaries

Supported control types are CommandButton, Label, TextBox, ComboBox, ListBox, CheckBox, OptionButton, ToggleButton, Image, ScrollBar, SpinButton, TabStrip, Frame, MultiPage, and Page.

Properties are type-specific. Never infer that a field emitted for one type is universal. When repository documentation is present, consult `docs/supported-controls.md` and `docs/frxedit-patch.schema.json`. Otherwise, retain the exported fields for each control and make only the requested, well-understood change.

Important boundaries:

- Frame accepts container values plus `caption` and `specialEffect`; an exported template may carry generated-only Frame font data.
- MultiPage accepts font, container, tab-array, selected-value, and tab-style fields.
- Page accepts container values and `caption`.
- TabStrip accepts font and tab-array fields plus `caption`, selected `value`, `style`, and `mouseIcon`.
- ComboBox and ListBox use `displayStyle`; do not substitute a tab `style` field.
- `pictureSizeMode` and `pictureAlignment` are Image control properties. The root has similarly named FormControl settings, but they do not create a root picture payload.
- `largeChange` and `proportionalThumb` are ScrollBar-only; SpinButton does not accept them.
- Root picture, mouse-icon, and font payloads are preserved when unchanged where supported, but are not editable through the current root Writer.
- `formDesignExData` is opaque generated/template data. Do not hand-edit it or use it as an in-place root/container property.
- `streamed`, `promoteControls`, and `formShapeCookie` are diagnostic/structural evidence, not editable properties.

## Value rules

- Point geometry uses JSON numbers in `leftPt`, `topPt`, `widthPt`, and `heightPt`.
- Raw layout fields and textual root `.frm` measurements are not point aliases. Preserve them unless the requested change specifically targets that domain.
- `fontSize` is in points, greater than zero, and no greater than 72.
- `dragBehavior` is integer `0` or `1`, not a JSON boolean.
- `pictureSizeMode` accepts `0` (clip), `1` (stretch), or `3` (zoom).
- `specialEffect` uses `0`, `1`, `2`, `3`, or `6`.
- `mousePointer` uses `0`, `1`, `2`, `3`, `6`–`15`, or `99`; do not use undefined values `4` or `5`.
- `textAlign` accepts `"left"`, `"center"`, `"right"`, or their integer values `1`, `2`, and `3`.
- `keepScrollBarsVisible` is a boolean packed-bit projection. It is not the numeric `scrollBars` enum.
- Preserve exported `picturePosition` integers. The current representation is not limited to the simple `0`–`12` constants.
- Packed words such as `siteBitFlags`, `fontEffects`, and `formBooleanProperties` accept unsigned numbers, decimal strings, or `0x`-prefixed strings. Prefer changing a named projection so unknown bits remain intact.
- Colors accept `#RRGGBB`, VBA `&H...&`, supported `system...` names, unsigned JSON integers, or unsigned decimal strings.

Do not erase a property merely because it looks like a default. Absence is normalized only for specific documented file defaults, and a present native value can carry preservation meaning.

## VBA sidecar

The `.vba` sidecar contains the editable code body. FrxEdit combines it with the source-derived form declaration and attributes during reconstruction.

- Preserve `Option` statements, declarations, and unrelated procedures.
- Rename event procedures and `Me.ControlName` or `Controls("ControlName")` references when their control changes.
- Remove an event procedure only when the user's request makes it obsolete.
- Keep source encoding and line endings when the editing environment exposes them.
- Do not place the binary form declaration or `OleObjectBlob` line in the sidecar.

If `code.tabStripPanels` is used, FrxEdit owns the code between its generated markers. Do not duplicate `UserForm_Initialize` or the relevant TabStrip change procedure outside that block.

## Assets

Use only these forms:

```json
{
  "picture": "file://UserForm1/Image1_picture.png",
  "mouseIcon": "base64:..."
}
```

Relative `file://` paths resolve from the JSON file's directory, not from the shell's working directory. Do not replace them with bare paths. Confirm that renamed/moved asset files still match the JSON reference.

Picture editing is supported for CommandButton, Label, TextBox, ComboBox, ListBox, CheckBox, OptionButton, ToggleButton, and Image. Mouse-icon editing additionally includes ScrollBar, SpinButton, and TabStrip. It does not include Frame, MultiPage, Page, or the root.

## Watch mode

Use watch only when the user asks for continuous regeneration:

```powershell
frxedit watch UserForm1.frm edits/UserForm1.patch.json `
  --out preview/UserForm1.frm --mode strict
```

Without `--out`, watch replaces the source pair and creates `.bak` files when outputs already exist. `--wysiwyg` also rewrites the patch and backs it up, so use it only when that normalization is wanted. Watch does not accept or need a `--stream-mode`; it always performs full-patch reconstruction.

## Completion checklist

- The input pair is unchanged or recoverable.
- JSON remains valid and uses only supported names, types, and values.
- Structural operations do not conflict.
- The `.vba` sidecar matches renamed, removed, and added controls.
- Every `file://` asset exists relative to the JSON.
- The rebuilt `.frm` names the generated `.frx` in its `OleObjectBlob` line.
- Build output uses a separate `.frm`/`.frx` pair unless in-place output was explicitly requested.
- The rebuilt form passes strict inspection/validation.
- Any applicable rebuild report has `semanticMatch: true`.
- The user is told what native Office/VBE validation remains.

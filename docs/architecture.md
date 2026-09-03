# FrxEdit Architecture and Binary Format

This document describes how FrxEdit inspects and reconstructs its supported subset of Microsoft Forms. The implementation follows the structures documented by MS-OFORMS while retaining unmodified native bytes whenever no semantic change is requested.

## The dual-file form model

A VBA form is represented by two coordinated files:

1. **`.frm` text** contains the root UserForm declaration, textual root properties, `OleObjectBlob` reference, module attributes, and VBA code. Child control declarations are not listed in the abbreviated export used by this project.
1. **`.frx` binary storage** is an OLE Compound File Binary Format container holding FormControl data, FormSiteData, object payloads, pictures, fonts, and nested container storages.

The textual root properties and binary FormControl properties are related but are not interchangeable. In particular, `.frm` `ClientWidth` and `ClientHeight` are preserved independently from the binary displayed and logical dimensions. A no-op binary reconstruction must not rewrite the textual client dimensions or caption.

Binary root `formGroupCount` is a Writer-backed semantic value; its documented file default is zero. `formShapeCookie` is retained and exposed as native structural evidence, but is not an editable or hard canonical property. The automated Reader can observe its persisted integer but cannot validate the corresponding Office/VBE compiled-type state, so cookie changes remain separately reported until native-host validation is performed.

## MSForms stream graph

Controls are represented by sites in the root storage or in a storage owned by a container:

- **`f` (form data)** contains a FormControl or container record followed immediately by the ordered FormSiteData records for its immediate children and, when `FORM_FLAG_DESINKPERSISTED` is set, one FormDesignExData structure. `FormStreamData` has no trailing alignment field: its `MouseIcon`, `GuidAndFont`, and `Picture` values determine the exact SiteData boundary.
- **`o` (object data)** contains the concatenated payloads of object-bearing immediate children. Its payload order must agree with the corresponding Sites order.
- **`x` (extended data)** records relationships such as MultiPage Page IDs.

The ordered Sites sequence is native structural state. It is kept internally consistent with `o`, but it is not inferred to be tab order or global z-order. `tabIndex` is compared separately, and Page order comes from explicit MultiPage Page/TabStrip semantics. The flattened order in an inspection JSON array is not a semantic ordering contract.

Each site's optional `BitFlags` word has the effective file default `0x00000033`. FrxEdit exposes the behavioral bits as `siteAutoSize`, `preserveHeight`, `fitToParent`, and `selectChild`, and preserves the raw `siteBitFlags` word while overlaying named edits. The `streamed` bit describes object-stream versus owned-storage persistence, while `promoteControls` is required for Frame, MultiPage, and Page; both are derived from the planned graph and are not independently editable.

## Reconstruction pipeline

FrxEdit uses the following stages:

1. **Parse and validate.** The Reader discovers the CFB graph and parses known `f`, `o`, and `x` records in strict or tolerant mode. It computes the exact end of both legal `GuidAndFont` variants (`StdFont` and `TextProps`). Strict mode requires FormSiteData at that exact boundary and requires FormDesignExData presence to agree with `FORM_FLAG_DESINKPERSISTED`, with no malformed or trailing bytes. Tolerant mode retains reachable controls while reporting the exact boundary, missing, unexpected, malformed, or trailing-data condition.
1. **Normalize patch intent.** Patch and template JSON are validated and normalized. The reconstruction plan records object-property, site-property, geometry, structure, binary-root, and textual FRM-root changes separately.
1. **Preserve or patch existing state.** Unchanged site records and object-stream slices are copied from the source. Modified objects are rebuilt from their parsed native state plus the requested delta, preserving omitted values, unsigned bitfields, and unknown bits rather than replacing them with generated defaults.
1. **Plan the control graph.** Adds, removes, moves, renames, and container ownership are resolved before bytes are emitted. Parent dependencies do not depend on the incoming JSON `add` order, while source sibling order within a parent is retained.
1. **Generate new controls and containers.** New controls use type-specific schemas. Frames, Pages, and MultiPages receive owned storage, and their child `f`/`o` streams participate in the same graph plan as existing containers. Generated Frame and MultiPage fonts use `GuidAndTextProps` when the template carries TextProps semantics and the exact 33-byte `GuidAndStdFont` representation otherwise; neither encoding adds padding before SiteData. If the persistence flag is set, generation uses the template's exact FormDesignExData or the type-specific default captured by the regression fixtures. Explicit Pages suppress the two-Page fallback.
1. **Serialize coordinated streams.** FormSiteData, object payload order, container streams, MultiPage TabStrip data, and Page-ID `x` streams are emitted from the same completed plan. Picture lengths include their property masks and native picture envelopes.
1. **Build a new CFB container.** FrxEdit writes a fresh compound container from the planned streams. FRX byte equality is intentionally not required; parsed semantics and native structural invariants are the acceptance criteria.
1. **Update `.frm` text.** Control declarations and explicitly requested textual properties are synchronized. Unchanged root text and VBA code remain source-derived.
1. **Strictly reread.** The rebuilt pair is inspected again so reports and tests compare observable output rather than assuming that successful serialization proves fidelity.

## JSON and asset boundaries

Patch/template JSON is the public edit contract. Existing property names remain backward compatible; newly round-trippable values such as `fontEffects`, `paragraphAlign`, `controlTipText`, Image `borderStyle`, FRM client dimensions, and MultiPage tab arrays use their existing names. Recreation templates may also carry opaque `formDesignExData` as `base64:` data. It is a lossless generation input rather than an in-place mutation interface.

The canonical model separates `properties`, `layout`, `renames`, `move`, `remove`, `add`, and `code`. Exported files retain flattened `$action`, type, parent, and point-geometry fields for editing compatibility. Normalization converts those fields before validation. A legacy `$action: "remove"` entry becomes a canonical removal and its other flattened payload is discarded; independently supplied rename, move, or layout conflicts remain validation errors.

The property object in the published JSON Schema is a shared value-shape envelope because an existing-control patch does not have to repeat the target control's type. The CLI resolves that type from the source form and is the final contract validator: unknown properties and the implemented type-incompatible combinations are rejected with an error. Examples include Image-only `pictureSizeMode` and `pictureAlignment`, ScrollBar-only `largeChange` and `proportionalThumb`, and tab-array fields limited to MultiPage and TabStrip.

Picture and mouse-icon strings may be embedded `base64:` values or `file://` references. A relative file URI is resolved against the directory of the JSON document that contains it for positional patches, `--patch`, `create`, `build`, and `watch`. The current working directory is not part of this contract.

Root FormControl parsing is broader than root generation. Existing root picture, mouse-icon, and font payloads remain source-derived when unchanged, but the current root patch/template Writer does not accept `formPicture`, `formMouseIcon`, or root font fields. Picture and mouse-icon editing is available only for the control types listed in the supported-controls document.

Property absence is meaningful. The Writer only treats absence as equivalent to an explicit value where MS-OFORMS establishes that file default. Other presence differences are preserved or reported rather than broadly normalized.

## Fidelity diagnostics

Reader provenance audits trace parser observations (`P`) through the raw inspection model (`R`) to exported JSON (`J`). Writer audits trace JSON (`J`) through normalization and target planning to emitted binary evidence and strict reread.

`scripts/compare-canonical-form.ps1` compares two strict raw inspections. Its hard comparison includes:

- control identity, type, parent, and point geometry;
- editable control and root properties;
- explicit `tabIndex` and MultiPage Page/tab order;
- decoded picture and mouse-icon payload hashes;
- strict parser warning, error, and heuristic counts.

Documented default-vs-omitted values are listed separately as normalizations. Native structure is also non-gating and reports the per-storage ordered Sites sequence, object-bearing controls ordered by their object-payload offsets, and `formShapeCookie`. This exposes whether each `o` sequence still agrees with its Sites plan without treating either sequence as tab order, while keeping cookie normalization visible. Global inspection-array position is ignored.

## Regression suites

- `scripts/test-canonical-roundtrip.ps1` performs no-op rebuild, both exported-patch command forms, bounded watch regeneration, and template recreation on copies of the comprehensive local fixture. Watch runs from a directory unrelated to the exported patch, then requires strict reread and canonical comparison of the regenerated output. `-SkipWatch` is available only for focused diagnosis; watch is part of the default acceptance suite.
- `tests/FrxEdit.Tests` directly checks both `GuidAndFont` variants, exact generated font bytes, strict FormStreamData/FormSiteData/FormDesignExData boundary rejection, flag/payload consistency, explicitly reported tolerant recovery, and legacy action normalization/conflicts.
- `scripts/test-generated-container-pipeline.ps1` validates parent-first and child-first graph planning, nested Page/Frame reachability, exact generated FormStreamData/SiteData/FormDesignExData boundaries, exact MultiPage streams, Page cloning and replacement, and fallback Pages.

By default both suites retain each run in a fresh GUID-scoped directory under `.build`; `-ArtifactsRoot` selects another retained-artifact root. The legacy `-KeepArtifacts` switch remains accepted for invocation compatibility but is no longer required. The canonical suite retains command logs, generated pairs, raw inspections, comparison reports, watch state, and source-hash manifests on success or failure.

## MS-OFORMS references

The reconstruction and comparison policies above are based on Microsoft's definitions of:

- [FormSiteData and its ordered Sites sequence](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/f65e0b17-6383-4570-b030-7b868f2c07d5);
- [object-stream control ordering](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/15778df8-8a8e-45dc-933b-f914f4e011cf);
- [OleSiteConcreteControl optional/default persistence](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/21354226-e08d-44d2-a06f-c9e751b56188), [SitePropMask](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/896d3774-dd6e-46b5-bfa7-6651aba111a8), and [SITE_FLAG](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/ed58f23c-ec1f-43f8-a593-df2626191d27);
- [FormDataBlock](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/096870e8-5263-44ed-885b-379d23471c4f) and the zero-default [GroupCount](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/f6e7d082-f4b8-4727-8f02-4a43dad6771e);
- [FormControl field ordering](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/219bef34-8932-4287-853d-f8e1dd73edb1), [FormStreamData ordering](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/0f5520cd-6a6c-4bf4-9bd0-5c322b6a288f), and the two legal [FormFont](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/53f28a1b-e029-4592-a8e2-f95e80994a76) encodings;
- [FormFlags](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/f91415fe-acdd-44ae-a522-34bd8118b011) and persisted [FormDesignExData](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/a3c2c801-a2c7-41b8-ada7-a2f8cbc3a676);
- [Image `cbImage`](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/aa5531bb-1ab3-430e-8091-f03df8d22891); and
- [VariousPropertyBits defaults](https://learn.microsoft.com/en-us/openspecs/office_file_formats/ms-oforms/7a72ac4a-39d9-4e2b-829e-19e3e9a1f60d).

## Native Office boundary

Codec success demonstrates automated semantic reconstruction for the tested corpus. It does not establish PowerPoint or VBE import, compilation, visual rendering, focus behavior, event execution, runtime interaction, or native save/reopen compatibility. Those checks require a separate Windows Office validation session.

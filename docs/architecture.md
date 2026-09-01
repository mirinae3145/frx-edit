# FrxEdit Architecture & Binary Format

This document provides a technical overview of how FrxEdit inspects and reconstructs supported Microsoft Forms (MS-OFORMS) binary streams and text layouts.

## The Dual-File Structure

A standard VBA form consists of two synchronized files:

1.  **`.frm` (Text Layout)**: A legacy VB6-style plain text file. It stores the macro code, high-level control declarations (e.g., `Begin MSForms.CommandButton`), and absolute positioning in **Points** (pt).
1.  **`.frx` (Binary Storage)**: An **OLE Compound File Binary Format (CFB)**. It acts as a miniature file system inside a single file, storing rich streams of binary data that plain text cannot represent natively, such as:
    *   Embedded Images (`.frx` offsets).
    *   OLE control metadata (`f`, `o`, `x` streams).
    *   Complex Unicode strings that cannot be saved in the `.frm` ANSI encoding.
    *   Typographic arrays (StdFont variants).

## OLE Streams (`f`, `o`, `x`)

Within the `.frx` CFB file, controls are represented by sites in the root or an owned container storage. The following stream kinds encode the graph; not every control owns every stream:

*   **`f` (Form Data)**: A form or container stream containing its intrinsic properties and the FormSiteData records for its immediate children.
*   **`o` (Object Data)**: A form or container stream containing the concatenated payloads of object-bearing immediate children, such as BackColor, Caption, Enabled, and MousePointer data.
*   **`x` (Extended Data)**: An optional stream used by structures such as MultiPage to record Page relationships and IDs.

## The FrxEdit Rebuild Pipeline

FrxEdit reconstructs supported form semantics through a strict pipeline:

1.  **JSON Patch DOM (`PatchDocument`)**: When a user or an AI Agent runs `frxedit build`, FrxEdit reads the target `patch.json`. This JSON document represents the state of the UI (modifications, creations, deletions).
1.  **Schema Validation**: The patch is strictly validated against `MsFormsControlSchemaCatalog`. This prevents the injection of illegal types (e.g., placing a string in a boolean property), which would cause a fatal crash in the host VBA environment (Excel/Corel).
1.  **Round-Trip Parsing**:
    *   For an existing form, FrxEdit opens the original `.frx` and reads the CFB streams using its own **custom, zero-dependency CFB parser** (`CompoundStorageInspector`). A create operation starts from a generated root form instead.
    *   Existing `f`, `o`, and `x` streams are parsed into in-memory .NET objects (`LocatedValue`).
1.  **Graph Planning and Morphing**:
    *   Structural additions are resolved as a complete parent graph before generated container bytes become final. Parent dependencies, Page ownership, tab order, IDs, and storage paths therefore do not depend on JSON `add` ordering.
    *   Explicit Page children define a generated MultiPage's effective Page set. The two-Page fallback is used only when the completed graph requests no explicit Pages.
    *   Property changes morph parsed objects in memory, while structural additions use generated site, object, and container records.
    *   The pipeline updates twips, hex colors, font flags, and byte alignments as required by the supported schemas.
1.  **Stream Planning**:
    *   Every generated Frame, Page, and MultiPage storage is materialized into the working storage plan before object and FormSiteData rewriting.
    *   Children of generated containers participate in the same `f` and `o` rewrite pass as children of pre-existing containers. MultiPage `f`, internal TabStrip `o`, and Page-ID `x` data are derived from the same completed Page graph.
1.  **CFB Serialization**:
    *   The morphed objects are serialized back into byte streams.
    *   A completely new `.frx` CFB container is generated to avoid OLE fragmentation (a common issue when manipulating `.frx` files directly).
1.  **Text Layout Generation**:
    *   Finally, FrxEdit generates the new `.frm` text layout and recalculates `pt` (points) dimensions from the `twips` stored in the binary.

The generated-container invariant is covered by `scripts/test-generated-container-pipeline.ps1`. The test recreates an explicit three-Page graph in parent-first and child-first order, validates nested Page/Frame reachability and exact stream consumption, exercises Page clones and a count-neutral Page replacement in an existing MultiPage, and checks selection-aware fallback Pages.

## AI Design Contracts

By abstracting the complex CFB binary logic into a clean JSON interface, FrxEdit allows LLMs to design UIs. The AI does not need to know about `twips`, OLE headers, or `Site` allocations; it only outputs JSON according to our schema, and FrxEdit handles the binary compilation.

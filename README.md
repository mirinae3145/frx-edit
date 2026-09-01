# FrxEdit: MS-Forms JSON-to-Binary CLI

[![Build and Release](https://github.com/viktormax3/vba-macro-project/actions/workflows/build.yml/badge.svg)](https://github.com/viktormax3/vba-macro-project/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**FrxEdit** is a modern, cross-platform Command Line Interface (CLI) tool designed to inspect, extract, patch, and rebuild Microsoft Forms (`.frm` and `.frx`) used natively in VBA (Microsoft Office, CorelDraw, AutoCAD, etc.).

The primary goal of this tool is to enable manipulation, version control, and dynamic UI generation of VBA forms from a modern environment using **JSON** as the intermediate exchange format. FrxEdit achieves **perfect binary and structural parity** against the proprietary `[MS-OFORMS]` specification.

---

## 🚀 Key Features

*   **Perfect Extraction**: Extracts the entire layout from a `.frm`/`.frx` file and converts it into readable, editable JSON.
*   **Binary Reconstruction**: Rebuilds compatible `.frx` binaries and `.frm` text blocks from a JSON patch, ready to be imported back into VBA without corruption.
*   **Native Parity**: Byte-level handling of OLE streams (`f`, `o`, `x`), ANSI/Unicode strings, typographic alignments, twip-to-point conversions, and hex colors.
*   **VBA Editor Independence**: Design, modify, and manage forms completely autonomously without ever opening the legacy VBA IDE.
*   **AI-Ready Engine**: Operates exclusively via JSON DOM structures, making it the perfect bridge for LLMs and AI Agents to design UI/UX for legacy systems autonomously.

---

## 🛠️ Quick Installation

This tool is distributed as a self-contained executable for your platform (Windows, macOS, Linux). 
Download the latest binaries from the [GitHub Actions / Releases tab](https://github.com/viktormax3/vba-macro-project/releases) and run it directly in your terminal. No .NET SDK installation is required!

---

## 📖 Usage Guide

FrxEdit operates through three main commands: `inspect`, `build`, and `watch`.

### 1. Inspect (Extract to JSON)
Read an existing `.frm`/`.frx` file and extract its properties into a JSON format.

```bash
# Extract full layout as a JSON patch tree
frxedit inspect UserForm1.frm --as-patch --out layout.json

# (Optional) Extract embedded images as base64 strings or files
frxedit inspect UserForm1.frm --as-patch --extract-images --out layout.json

# (Diagnostic) Record parser-to-raw-to-template provenance without changing normal outputs
frxedit inspect UserForm1.frm --mode strict --as-template --out layout.json \
  --raw-out layout.raw.json --reader-audit-out layout.audit.json
```

`--reader-audit-out` is opt-in and writes a machine-readable P/R/J audit: parser observations
(P), the raw inspection model (R), and the generated patch/template representation (J).
Large binary or text values are represented by length and SHA-256 metadata in the audit.

### 2. Build (Patch and Rebuild Binary)
Apply a JSON patch over an existing layout to modify colors, sizes, behavior, or even inject new controls, compiling everything back into a perfect `.frx` binary.

```bash
# Rebuild the form using the JSON patch (creates UserForm1_new.frm and .frx)
frxedit build UserForm1.frm layout.json --out UserForm1_new.frm

# Full-Patch mode (allows deep structural edits, renaming, adding, deleting controls)
frxedit build UserForm1.frm layout.json --out UserForm1_new.frm --stream-mode full-patch
```

### 3. Watch (Hot-Reloading)
Launch a daemon that monitors a JSON patch file for changes and automatically rebuilds the `.frm`/`.frx` every time you save. Excellent for real-time AI design iteration.

```bash
# Watch the layout.json file for changes and rebuild automatically
frxedit watch UserForm1.frm layout.json --out UserForm1_rebuilt.frm --stream-mode full-patch
```

### Reconstruction provenance diagnostics

`build` and `create --patch` can emit a separate, machine-readable reconstruction audit without changing the normal output files or JSON schemas:

```bash
frxedit create UserForm1_new.frm --name UserForm1 --patch UserForm1.json --writer-audit-out UserForm1.writer-audit.json
```

The audit traces the deserialized template (`J`), normalized patch (`N`), requested target layout (`T`), Writer/CFB evidence (`B`), and strict re-read result (`C`). Binary payloads are represented by byte length and SHA-256 rather than embedded blobs. The option is intended for defect isolation; it does not by itself establish native Office compatibility.

---

## 🤖 AI Integration & Autonomous Generation

FrxEdit was explicitly engineered to be driven by AI. By using a strict JSON schema, any Large Language Model can generate rich user interfaces for legacy applications.

To use this project with an AI Agent:
1. Provide the AI with the **[Supported Controls & Schema Documentation](docs/supported-controls.md)**.
2. Ask the AI to generate a `patch.json` with the desired UI layout.
3. Run `frxedit build ...` to inject the AI's design directly into a VBA `.frx` file.

The CLI is heavily stress-tested to fail gracefully on structural hallucinations, acting as a secure compiler for AI-generated layouts.

---

## 📚 Technical Documentation

For deep-dives into how the reverse-engineering of the `[MS-OFORMS]` specification works, please refer to our internal documentation:
* [Architecture & Binary Format](docs/architecture.md)
* [Supported Controls & Valid Properties](docs/supported-controls.md)

---

## 📄 License

This project is licensed under the **MIT License**. See the [LICENSE](LICENSE) file for details.

# Supported controls and properties

FrxEdit recognizes and reconstructs the 15 MSForms control types listed here. This is the public patch/template Writer contract. Raw inspection output contains additional parser evidence and native fields that are not necessarily editable.

The JSON Schema describes shared value shapes, while the CLI performs the final control-type check. A property appearing in the schema does not make it valid for every control.

## Patch layers

- `layout` changes an existing or newly added control's geometry.
- `properties` changes root, site, object, container, or tab data.
- `renames`, `move`, `remove`, and `add` change the control graph.
- `code.tabStripPanels` generates supported TabStrip panel procedures.

Generated patch/template files also put `leftPt`, `topPt`, `widthPt`, `heightPt`, `type`, `parent`, `$action`, and `$newName` inside each `properties` entry. FrxEdit normalizes that exported representation into the same internal model. For newly authored patches, the top-level sections are easier to review.

## Geometry and common site properties

Use point-valued `leftPt`, `topPt`, `widthPt`, and `heightPt` for normal layout work. Low-level `left`, `top`, `rawWidth`, and `rawHeight` values expose native metrics and primarily exist for compatibility and diagnostics. Textual root `.frm` measurements such as `clientWidth` are a separate domain and are not point aliases.

Every supported control site accepts these Writer fields:

| Category | Properties |
| --- | --- |
| Identity and ownership | `name`, `type`, and `parent` in `add`; `renames` and `move` for existing controls |
| Geometry | `leftPt`, `topPt`, `widthPt`, `heightPt`; low-level layout aliases in `layout` |
| Site values | `tabIndex`, `tabStop`, `visible`, `default`, `cancel`, `helpContextId`, `groupId` |
| Site strings | `tag`, `controlTipText`, `controlSource`, `rowSource` |
| SITE_FLAG values | `siteBitFlags`, `siteAutoSize`, `preserveHeight`, `fitToParent`, `selectChild` |

`siteBitFlags` accepts an unsigned 32-bit number, decimal string, or `0x`-prefixed hexadecimal string. Named SITE_FLAG edits overlay the raw word. The structural `streamed` and `promoteControls` projections are read-only and must agree with the planned storage graph.

For an existing control, a site string can be changed only when its corresponding native span already exists. Adds and recreation templates can emit supported site strings directly.

## Reusable property groups

The table below mirrors the Writer's internal property groups.

| Group | Properties |
| --- | --- |
| Font | `fontName`, `fontSize`, `fontWeight`, `fontEffects`, `fontBold`, `fontItalic`, `fontUnderline`, `fontStrikethrough`, `fontCharSet`, `fontPitchAndFamily`, `textAlign`, `paragraphAlign` |
| Morph | `enabled`, `locked`, `backStyle`, `alignment`, `wordWrap`, `autoSize`, `autoTab`, `autoWordSelect`, `hideSelection`, `integralHeight`, `multiLine`, `selectionMargin`, `enterKeyBehavior`, `tabKeyBehavior`, `enterFieldBehavior`, `dragBehavior`, `imeMode`, `columnHeads`, `matchRequired`, `editable` |
| Container | `enabled`, `pictureTiling`, `keepScrollBarsVisible`, `rightToLeft`, `logicalWidth`, `logicalHeight`, `scrollLeft`, `scrollTop`, `logicalWidthPt`, `logicalHeightPt`, `scrollLeftPt`, `scrollTopPt`, `formBooleanProperties`, `formDrawBuffer`, `drawBuffer` |
| Tabs | `tabCaptions`, `tabTooltips`, `tabNames`, `tabTags`, `tabAccelerators`, `tabFlags`; compatibility aliases `pageNames` and `pageCaptions` |

`fontSize` is in points and must be greater than zero and no greater than 72. `fontEffects` is the lossless packed word; the named font booleans overlay their corresponding bits. `fontBold` and `fontWeight` are separate accepted projections.

`keepScrollBarsVisible` is a boolean projection of a bit in `formBooleanProperties`. It is not the four-valued `fmScrollBars` enumeration used by `scrollBars`.

## Control property matrix

All rows also include the common site properties above.

| Control | Groups | Additional Writer properties |
| --- | --- | --- |
| `CommandButton` | Font | `caption`, `backColor`, `foreColor`, `enabled`, `locked`, `backStyle`, `wordWrap`, `autoSize`, `imeMode`, `picturePosition`, `mousePointer`, `accelerator`, `takeFocusOnClick`, `picture`, `mouseIcon` |
| `Label` | Font | `caption`, `backColor`, `foreColor`, `borderColor`, `enabled`, `backStyle`, `wordWrap`, `autoSize`, `imeMode`, `picturePosition`, `mousePointer`, `accelerator`, `borderStyle`, `specialEffect`, `picture`, `mouseIcon` |
| `TextBox` | Font, Morph | `value`, `backColor`, `foreColor`, `borderColor`, `maxLength`, `passwordChar`, `scrollBars`, `mousePointer`, `borderStyle`, `specialEffect`, `picture`, `mouseIcon` |
| `ComboBox` | Font, Morph | `value`, `backColor`, `foreColor`, `borderColor`, `borderStyle`, `scrollBars`, `displayStyle`, `mousePointer`, `listWidth`, `boundColumn`, `textColumn`, `columnCount`, `listRows`, `matchEntry`, `listStyle`, `showDropButtonWhen`, `dropButtonStyle`, `maxLength`, `specialEffect`, `picture`, `mouseIcon` |
| `ListBox` | Font, Morph | `value`, `backColor`, `foreColor`, `borderColor`, `borderStyle`, `scrollBars`, `displayStyle`, `mousePointer`, `listWidth`, `boundColumn`, `textColumn`, `columnCount`, `matchEntry`, `listStyle`, `multiSelect`, `specialEffect`, `picture`, `mouseIcon` |
| `CheckBox` | Font | `value`, `caption`, `groupName`, `backColor`, `foreColor`, `enabled`, `locked`, `backStyle`, `alignment`, `wordWrap`, `autoSize`, `imeMode`, `mousePointer`, `multiSelect`, `picturePosition`, `specialEffect`, `accelerator`, `picture`, `mouseIcon` |
| `OptionButton` | Font | Same Writer set as `CheckBox` |
| `ToggleButton` | Font | Same Writer set as `CheckBox` |
| `Image` | — | `backColor`, `borderColor`, `enabled`, `locked`, `imeMode`, `autoSize`, `borderStyle`, `mousePointer`, `pictureSizeMode`, `specialEffect`, `picture`, `pictureAlignment`, `pictureTiling`, `mouseIcon` |
| `ScrollBar` | Morph | `value`, `backColor`, `foreColor`, `mousePointer`, `min`, `max`, `position`, `smallChange`, `largeChange`, `orientation`, `delay`, `proportionalThumb`, `mouseIcon` |
| `SpinButton` | Morph | `value`, `backColor`, `foreColor`, `mousePointer`, `min`, `max`, `position`, `smallChange`, `orientation`, `delay`, `mouseIcon` |
| `TabStrip` | Font, Tabs | `caption`, `value`, `style`, `mouseIcon`; compatibility aliases `listIndex` and `tabStyle` |
| `Frame` | Container | `caption`, `specialEffect`; compatibility alias `formSpecialEffect` |
| `MultiPage` | Font, Container, Tabs | `value`, `style`; compatibility aliases `listIndex` and `tabStyle` |
| `Page` | Container | `caption` |

Frame fonts may appear in recreation templates so newly generated Frames can reproduce the observed font encoding. Existing Frame font mutation is not part of the patch Writer contract.

`value` is the exported name for the active tab/page index on `TabStrip` and `MultiPage`; `listIndex` remains accepted. `style` is exported for their tab style; `tabStyle` remains accepted. `tabFlags` retains each raw word together with its `visible` and `enabled` projections.

## Root UserForm

The root is addressed by its form name or by the aliases `UserForm`, `Form`, or `root`.

| Domain | Writer properties |
| --- | --- |
| Textual `.frm` | `caption`, `clientLeft`, `clientTop`, `clientWidth`, `clientHeight`, `left`, `top`, `width`, `height`, `startUpPosition`, `showModal`, `tag`, `drawBuffer`, `whatsThisButton`, `whatsThisHelp` |
| Binary FormControl | `backColor`, `foreColor`, `borderColor`, `formCaption`, `borderStyle`, `mousePointer`, `scrollBars`, `cycle`, `specialEffect`, `pictureAlignment`, `pictureSizeMode`, `zoom`, `nextAvailableId`, `formGroupCount`, displayed/logical dimensions, scroll positions, `formDrawBuffer`, and `formBooleanProperties` |
| Binary boolean projections | `enabled`, `pictureTiling`, `keepScrollBarsVisible`, `rightToLeft` |
| Accepted prefixed aliases | `formBackColor`, `formForeColor`, `formBorderColor`, `formBorderStyle`, `formMousePointer`, `formScrollBars`, `formCycle`, `formSpecialEffect`, `formPictureAlignment`, `formPictureSizeMode`, `formZoom` |

At the root, textual `caption` is distinct from binary `formCaption`, and textual signed `drawBuffer` is distinct from unsigned binary `formDrawBuffer`. The CLI applies the domain-specific numeric range.

Recreation templates may carry `formDesignExData` for a UserForm, Frame, MultiPage, or Page as opaque `base64:` data. It is not accepted as an in-place root/container edit. Its presence must agree with `FORM_FLAG_DESINKPERSISTED` in `formBooleanProperties`.

The Reader can observe root picture, mouse-icon, font data, and `formShapeCookie`; unchanged builds preserve source bytes where supported. The root Writer does not accept `formPicture`, `formMouseIcon`, root font edits, or `formShapeCookie`. Root `pictureAlignment`, `pictureSizeMode`, and `pictureTiling` change settings only and do not create a picture payload.

## Assets

`picture` is Writer-backed for CommandButton, Label, TextBox, ComboBox, ListBox, CheckBox, OptionButton, ToggleButton, and Image. `mouseIcon` is Writer-backed for those controls plus ScrollBar, SpinButton, and TabStrip. Frame, MultiPage, Page, and the root do not accept picture or mouse-icon payload edits.

An asset value must be either `base64:<data>` or `file://<path>`. Relative `file://` paths are resolved from the directory containing the patch/template JSON, not from the process working directory. When an inspect patch/template is written with `--out`, embedded assets are extracted automatically into a directory named after the form.

## Values and enums

The following values are the documented MSForms projections used by FrxEdit:

| Property | Values |
| --- | --- |
| `pictureAlignment` | `0` top-left, `1` top-right, `2` center, `3` bottom-left, `4` bottom-right |
| `pictureSizeMode` | `0` clip, `1` stretch, `3` zoom |
| `scrollBars` | `0` none, `1` horizontal, `2` vertical, `3` both |
| `textAlign` | `1`/`"left"`, `2`/`"center"`, `3`/`"right"` |
| `specialEffect` | `0` flat, `1` raised, `2` sunken, `3` etched, `6` bump |
| `borderStyle` | `0` none, `1` single |
| `alignment` | `0` left, `1` right |
| `matchEntry` | `0` first letter, `1` complete, `2` none |
| `multiSelect` | `0` single, `1` multi, `2` extended |
| `orientation` | `-1` automatic, `0` vertical, `1` horizontal |
| `style` / `tabStyle` | `0`, `1`, or `2` |
| `dragBehavior` | integer `0` or `1` |
| `mousePointer` | `0`, `1`, `2`, `3`, `6`–`15`, or `99`; values `4` and `5` are not defined in this projection |

`picturePosition` is stored as a signed native integer by the current Writer. Do not assume it is limited to the simple `0`–`12` enumeration; preserve an exported value unless a tested change requires otherwise.

Colors accept `#RRGGBB`, VBA `&H...&`, a supported `system...` name, an unsigned JSON integer, or an unsigned decimal string. Hex RGB is converted to OLE `0x00BBGGRR`; raw numeric and VBA forms preserve the corresponding OLE color word.

## Preservation and validation boundaries

Property absence is meaningful. FrxEdit normalizes absence only for established file defaults such as the 8-point default font, root `formGroupCount` zero, and Site `BitFlags` default `0x00000033`. Packed values preserve unknown bits while named edits overlay supported projections.

Unknown properties and known type-incompatible combinations fail CLI validation rather than being treated as edits. Successful automated reconstruction establishes parser/Writer behavior for the tested data; native Office import, rendering, event execution, and save/reopen behavior require separate validation in an applicable Office host.

# NHI format notes

This document records the format currently implemented by ACNH NHI Builder.

## Slot size

Each inventory slot is exactly **8 bytes**.

| Offset | Size | Field |
|---|---:|---|
| `0x00` | 2 | Item ID (`ushort`, little-endian) |
| `0x02` | 1 | System parameter |
| `0x03` | 1 | Additional parameter |
| `0x04` | 2 | Count / low part of free parameter |
| `0x06` | 2 | Use count / high part of free parameter |

The last four bytes are context-sensitive in ACNH and can represent stack counts, customization data, flower genes, extensions, etc. The first implementation exposes the common stackable representation while preserving raw values when importing.

## Empty slots

An empty slot uses item ID:

```text
0xFFFE
```

## Stackable quantities

For ordinary stackable items, the stored count is zero-based:

```text
stored Count = visible quantity - 1
```

Example: a visible stack of 30 uses `Count = 29`.

## Inventory length

An `.nhi` is a concatenation of 8-byte slots. The format itself can represent different slot counts. A 9,000-slot storage dump is:

```text
9000 × 8 = 72000 bytes
```

The application keeps the capacity explicit and validates the output length before export.

## Safety

Do not assume every combination of item ID and free parameters is valid. Item kinds such as flowers, DIY recipes, wrapped items and customizable furniture may require specialized parameter handling.

The project therefore aims to obtain current item metadata from a compatible NHSE installation and defaults to excluding invalid/internal entries.

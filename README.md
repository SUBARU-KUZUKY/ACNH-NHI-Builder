# ACNH NHI Builder

A visual, open-source Windows application for building, cleaning, analyzing and exporting **Animal Crossing: New Horizons** `.nhi` inventory files.

> Status: **v0.2.0-alpha**. The visual generator now loads metadata from the user's own NHSE installation at runtime.

## Download — Windows

Download the latest prerelease from GitHub Releases:

**[ACNH NHI Builder releases](https://github.com/SUBARU-KUZUKY/ACNH-NHI-Builder/releases)**

For Windows x64, download the latest `ACNH-NHI-Builder-...-win-x64.zip`, extract the ZIP, then launch `ACNH-NHI-Builder.exe`.

The build is **self-contained**, so users do not need to install the .NET runtime separately.

## What works in v0.2.0-alpha

- Open, inspect, clean and save existing `.nhi` files.
- Create a new **9,000-slot** `.nhi` inventory.
- Select the folder of a compatible NHSE installation.
- Remember the NHSE folder between launches.
- Load **French item names** directly from `NHSE.Core.dll`.
- Classify items into furniture, clothing, materials, special collections, tools/consumables and more.
- Clothing subcategories include tops, bottoms, dresses/outfits, hats, accessories, socks, shoes and bags.
- Search by item name or ID.
- Hide internal/debug and invalid entries by default.
- Show NHSE sprites when available, loaded from the user's local NHSE data rather than redistributed by this project.
- Select individual items or all currently visible items.
- Apply presets such as all furniture, all clothing, materials, photos, gyroids and authentic art.
- Use NHSE's maximum stack size for stackable items.
- Add multiple stacks/copies in one operation.
- Optionally skip IDs already present in the current `.nhi`.
- Enforce the available slot capacity before generating.
- Export a CSV manifest with localized names when NHSE is loaded.
- Dark interface with violet/cyan accents and a dark Windows title bar where supported.

### Current limitation

Advanced body/customization variants that share a single base item ID are not fully modeled yet. Color/style variants represented by separate NHSE item IDs are already selectable individually. Variant grouping and manual body-variant selection are planned next.

## Typical workflow

1. Extract and launch `ACNH-NHI-Builder.exe`.
2. Click **Choisir / recharger NHSE…** and select the folder containing `NHSE.Core.dll` (and preferably `NHSE.Sprites.dll`).
3. Search or browse categories, or choose a preset.
4. Select items and choose stack options.
5. Click **Ajouter la sélection au .nhi**.
6. Review the **Inventaire .nhi** tab and the 9,000-slot counter.
7. Click **Générer / enregistrer .nhi**.

## Goals

- Build `.nhi` files up to **9,000 inventory slots**.
- Select entire collections, categories, subcategories, individual items or variants.
- Furniture and clothing categories with useful subcategories such as shoes, socks, hats, tops, bottoms, dresses and bags.
- Choose **one variant**, **all variants**, or **custom variants** as variant support matures.
- Configure stackable materials with maximum stack sizes and a custom number of stacks.
- Import an existing `.nhi`, analyze it, remove duplicates and complete only missing collections.
- Keep a live **used/free slot counter** and refuse exports above the selected capacity.
- Exclude internal/debug/invalid or normally unobtainable items by default.
- Export a `.nhi` plus a human-readable CSV manifest.
- Load item metadata and sprites from a user-selected NHSE installation instead of redistributing game assets.
- French-first UI, with localization support planned.

## NHI format

The project currently treats an NHI slot as **8 bytes**:

- `ushort ItemId`
- `byte SystemParam`
- `byte AdditionalParam`
- `ushort Count`
- `ushort UseCount`

The empty item sentinel is `0xFFFE`. For ordinary stackable items, the in-file `Count` is represented as `quantity - 1`.

See [`docs/NHI_FORMAT.md`](docs/NHI_FORMAT.md).

## NHSE compatibility

This project is inspired by and interoperates with [kwsch/NHSE](https://github.com/kwsch/NHSE). NHSE is GPL-3.0 licensed.

The project does **not** ship NHSE binaries or Nintendo image assets. Users select their own NHSE installation, and ACNH NHI Builder reads compatible metadata/sprites from it at runtime.

## Build from source

Requirements:

- Windows 10/11
- .NET 10 SDK
- Visual Studio with Windows Desktop support, or the `dotnet` CLI

```powershell
dotnet restore
dotnet build ACNH-NHI-Builder.sln
dotnet test ACNH-NHI-Builder.sln
```

## Contributing

Contributions are welcome. See [`CONTRIBUTING.md`](CONTRIBUTING.md).

Useful areas for contributors:

- variant grouping and customization parameters
- richer collection presets
- sprite loading and caching
- item categorization and subcategories
- localization
- accessibility and UI polish
- NHI validation and tests

## License

GPL-3.0. See [`LICENSE`](LICENSE).

## Disclaimer

This is an unofficial community tool. Animal Crossing and Nintendo Switch are trademarks of Nintendo. No Nintendo game assets are included in this repository.

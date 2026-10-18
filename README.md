# ACNH NHI Builder

A visual, open-source Windows application for building, cleaning, analyzing and exporting **Animal Crossing: New Horizons** `.nhi` inventory files.

> Status: **v0.1.0-alpha**. A runnable Windows x64 prerelease is available. The NHSE metadata/sprite adapter and full visual collection generator are the next milestones.

## Download — Windows

Download the latest prerelease from GitHub Releases:

**[ACNH NHI Builder v0.1.0-alpha](https://github.com/SUBARU-KUZUKY/ACNH-NHI-Builder/releases/tag/v0.1.0-alpha)**

For Windows x64, download `ACNH-NHI-Builder-v0.1.0-alpha-win-x64.zip`, extract the ZIP, then launch `ACNH-NHI-Builder.exe`.

The build is **self-contained**, so users do not need to install the .NET runtime separately.

### What works in v0.1.0-alpha

- Open an existing `.nhi` file.
- Validate the basic NHI file structure.
- Display used inventory slots and their raw item IDs/parameters.
- Show capacity, used/free slots, strict duplicate count and unique item IDs.
- Remove strictly identical duplicate slots and compact the inventory.
- Save/export the resulting `.nhi`.
- Export a CSV manifest.

### Coming next

- NHSE metadata adapter.
- French item names.
- Item images/sprites loaded without redistributing Nintendo assets.
- Categories and clothing subcategories.
- One/all/custom variant modes.
- Material stack presets.
- Full collection presets and visual `.nhi` generation up to 9,000 slots.

## Goals

- Build `.nhi` files up to **9,000 inventory slots**.
- Select entire collections, categories, subcategories, individual items or variants.
- Furniture and clothing categories with useful subcategories such as shoes, socks, hats, tops, bottoms, dresses and bags.
- Choose **one variant**, **all variants**, or **custom variants**.
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

The project does **not** redistribute Nintendo image assets. The intended design is to let the user select their own compatible NHSE installation and load metadata/sprites at runtime where technically and legally appropriate.

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

- NHSE data adapter
- sprite loading
- item categorization and subcategories
- variant handling
- localization
- accessibility and UI polish
- preset collections
- NHI validation and tests

## License

GPL-3.0. See [`LICENSE`](LICENSE).

## Disclaimer

This is an unofficial community tool. Animal Crossing and Nintendo Switch are trademarks of Nintendo. No Nintendo game assets are included in this repository.

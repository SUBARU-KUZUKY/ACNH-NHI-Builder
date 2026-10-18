# Contributing

Thanks for helping improve ACNH NHI Builder.

## Ground rules

- Keep the project GPL-3.0 compatible.
- Do not commit Nintendo game assets, copyrighted sprite packs, save files, credentials or personal data.
- Prefer loading metadata/sprites from a user's own compatible installation rather than bundling extracted assets.
- Add tests for NHI parsing/writing changes.
- Document new item categories, filters and presets.
- Keep safety filters enabled by default for internal/debug/invalid items.

## Development

1. Fork the repository.
2. Create a feature branch.
3. Make focused changes.
4. Run tests.
5. Open a pull request describing the user-visible behavior.

## Good first issues

- Wire Open/Save dialogs to `NhiInventory`.
- Bind the used/free slot counter to the UI.
- Implement a basic category tree model.
- Add French/English localization resources.
- Add more round-trip and malformed-file tests.

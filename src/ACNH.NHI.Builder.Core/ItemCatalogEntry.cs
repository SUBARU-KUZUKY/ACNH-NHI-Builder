namespace ACNH.NHI.Builder.Core;

public sealed record ItemCatalogEntry(
    ushort ItemId,
    string Name,
    string Category,
    string? Subcategory = null,
    int MaxStack = 1,
    bool IsOrderable = false,
    bool IsInternal = false,
    bool IsInvalid = false,
    IReadOnlyList<ItemVariant>? Variants = null,
    string? SpriteKey = null);

public sealed record ItemVariant(int BodyType, string Name, string? SpriteKey = null);

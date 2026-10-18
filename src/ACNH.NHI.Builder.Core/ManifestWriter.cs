using System.Text;

namespace ACNH.NHI.Builder.Core;

public static class ManifestWriter
{
    public static void WriteCsv(string path, NhiInventory inventory, IReadOnlyDictionary<ushort, ItemCatalogEntry> catalog)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Slot;ItemID;Name;Category;Subcategory;Quantity;SystemParam;AdditionalParam;UseCount");
        for (var i = 0; i < inventory.Capacity; i++)
        {
            var item = inventory[i];
            if (item.IsEmpty)
                continue;
            catalog.TryGetValue(item.ItemId, out var entry);
            writer.WriteLine(string.Join(';',
                i + 1,
                item.ItemId,
                Escape(entry?.Name ?? "Unknown"),
                Escape(entry?.Category ?? "Unknown"),
                Escape(entry?.Subcategory ?? string.Empty),
                item.Quantity,
                item.SystemParam,
                item.AdditionalParam,
                item.UseCount));
        }
    }

    private static string Escape(string value) => value.Contains(';') || value.Contains('"')
        ? $"\"{value.Replace("\"", "\"\"")}\""
        : value;
}

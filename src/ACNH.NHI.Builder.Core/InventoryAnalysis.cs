namespace ACNH.NHI.Builder.Core;

public sealed record InventoryAnalysis(
    int Capacity,
    int Used,
    int Free,
    int DuplicateSlots,
    int UniqueItemIds);

public static class InventoryAnalyzer
{
    public static InventoryAnalysis Analyze(NhiInventory inventory)
    {
        var used = inventory.Slots.Where(z => !z.IsEmpty).ToArray();
        var duplicateSlots = used.Length - used.Distinct().Count();
        return new InventoryAnalysis(
            inventory.Capacity,
            used.Length,
            inventory.FreeSlots,
            duplicateSlots,
            used.Select(z => z.ItemId).Distinct().Count());
    }
}

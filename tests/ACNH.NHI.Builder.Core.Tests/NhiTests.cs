using ACNH.NHI.Builder.Core;

namespace ACNH.NHI.Builder.Core.Tests;

public class NhiTests
{
    [Fact]
    public void EmptyItemUsesExpectedSentinel()
    {
        Assert.Equal(0xFFFE, NhiItem.Empty.ItemId);
        Assert.True(NhiItem.Empty.IsEmpty);
    }

    [Fact]
    public void RoundTripPreservesSlot()
    {
        var input = NhiItem.Create(0x09F0, quantity: 30, systemParam: 2, additionalParam: 1, useCount: 7);
        Span<byte> data = stackalloc byte[8];
        input.Write(data);
        var output = NhiItem.Read(data);
        Assert.Equal(input, output);
        Assert.Equal(30, output.Quantity);
    }

    [Fact]
    public void InventoryRoundTripKeeps9000Slots()
    {
        var inv = new NhiInventory();
        inv.Add(NhiItem.Create(0x09F0));
        inv.Add(NhiItem.Create(0x1742));
        var data = inv.ToBytes();
        Assert.Equal(72_000, data.Length);
        var parsed = NhiInventory.FromBytes(data);
        Assert.Equal(9000, parsed.Capacity);
        Assert.Equal(2, parsed.UsedSlots);
    }
}

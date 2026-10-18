using System.Buffers.Binary;

namespace ACNH.NHI.Builder.Core;

/// <summary>
/// One New Horizons Inventory slot. The on-disk representation is exactly 8 bytes.
/// </summary>
public readonly record struct NhiItem(
    ushort ItemId,
    byte SystemParam,
    byte AdditionalParam,
    ushort Count,
    ushort UseCount)
{
    public const int Size = 8;
    public const ushort None = 0xFFFE;

    public static NhiItem Empty => new(None, 0, 0, 0, 0);
    public bool IsEmpty => ItemId == None;
    public int Quantity => IsEmpty ? 0 : Count + 1;

    public static NhiItem Create(ushort itemId, int quantity = 1, byte systemParam = 0, byte additionalParam = 0, ushort useCount = 0)
    {
        if (quantity is < 1 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be between 1 and 65536.");

        return new NhiItem(itemId, systemParam, additionalParam, checked((ushort)(quantity - 1)), useCount);
    }

    public static NhiItem Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
            throw new ArgumentException("An NHI item requires 8 bytes.", nameof(data));

        return new NhiItem(
            BinaryPrimitives.ReadUInt16LittleEndian(data[0..2]),
            data[2],
            data[3],
            BinaryPrimitives.ReadUInt16LittleEndian(data[4..6]),
            BinaryPrimitives.ReadUInt16LittleEndian(data[6..8]));
    }

    public void Write(Span<byte> destination)
    {
        if (destination.Length < Size)
            throw new ArgumentException("An NHI item requires 8 bytes.", nameof(destination));

        BinaryPrimitives.WriteUInt16LittleEndian(destination[0..2], ItemId);
        destination[2] = SystemParam;
        destination[3] = AdditionalParam;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..6], Count);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..8], UseCount);
    }
}

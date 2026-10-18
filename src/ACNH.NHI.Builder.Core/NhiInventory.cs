namespace ACNH.NHI.Builder.Core;

public sealed class NhiInventory
{
    public const int DefaultCapacity = 9000;
    private readonly List<NhiItem> _slots;

    public NhiInventory(int capacity = DefaultCapacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _slots = Enumerable.Repeat(NhiItem.Empty, capacity).ToList();
    }

    private NhiInventory(IEnumerable<NhiItem> slots) => _slots = slots.ToList();

    public int Capacity => _slots.Count;
    public int UsedSlots => _slots.Count(z => !z.IsEmpty);
    public int FreeSlots => Capacity - UsedSlots;
    public IReadOnlyList<NhiItem> Slots => _slots;

    public NhiItem this[int index]
    {
        get => _slots[index];
        set => _slots[index] = value;
    }

    public int Add(NhiItem item)
    {
        var index = _slots.FindIndex(z => z.IsEmpty);
        if (index < 0)
            throw new InvalidOperationException("The inventory is full.");
        _slots[index] = item;
        return index;
    }

    public int RemoveDuplicates(bool keepFirst = true)
    {
        var seen = new HashSet<ulong>();
        var removed = 0;
        var indexes = keepFirst ? Enumerable.Range(0, Capacity) : Enumerable.Range(0, Capacity).Reverse();
        foreach (var i in indexes)
        {
            var item = _slots[i];
            if (item.IsEmpty)
                continue;
            var key = ToRawValue(item);
            if (!seen.Add(key))
            {
                _slots[i] = NhiItem.Empty;
                removed++;
            }
        }
        Compact();
        return removed;
    }

    public void Compact()
    {
        var used = _slots.Where(z => !z.IsEmpty).ToArray();
        for (var i = 0; i < Capacity; i++)
            _slots[i] = i < used.Length ? used[i] : NhiItem.Empty;
    }

    public byte[] ToBytes()
    {
        var data = new byte[Capacity * NhiItem.Size];
        for (var i = 0; i < Capacity; i++)
            _slots[i].Write(data.AsSpan(i * NhiItem.Size, NhiItem.Size));
        return data;
    }

    public static NhiInventory FromBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0 || data.Length % NhiItem.Size != 0)
            throw new InvalidDataException("NHI file size must be a non-zero multiple of 8 bytes.");

        var result = new NhiItem[data.Length / NhiItem.Size];
        for (var i = 0; i < result.Length; i++)
            result[i] = NhiItem.Read(data.Slice(i * NhiItem.Size, NhiItem.Size));
        return new NhiInventory(result);
    }

    public static NhiInventory Load(string path) => FromBytes(File.ReadAllBytes(path));
    public void Save(string path) => File.WriteAllBytes(path, ToBytes());

    private static ulong ToRawValue(NhiItem item)
    {
        Span<byte> data = stackalloc byte[NhiItem.Size];
        item.Write(data);
        return BitConverter.ToUInt64(data);
    }
}

namespace ACNH.NHI.Builder.Core;

public enum VariantMode
{
    One,
    All,
    Custom
}

public sealed class SelectionModel
{
    public VariantMode VariantMode { get; set; } = VariantMode.One;
    public bool ExcludeInternalItems { get; set; } = true;
    public bool ExcludeInvalidVariants { get; set; } = true;
    public bool ExcludeNormallyUnobtainableItems { get; set; } = true;
    public int MaterialStackCopies { get; set; } = 1;
    public bool UseMaximumStackSize { get; set; } = true;
}

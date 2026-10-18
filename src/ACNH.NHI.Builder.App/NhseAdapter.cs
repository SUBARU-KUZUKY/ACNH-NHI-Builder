using ACNH.NHI.Builder.Core;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ACNH.NHI.Builder.App;

/// <summary>
/// Loads the user's own NHSE installation at runtime. This project does not redistribute
/// NHSE binaries or Nintendo assets; it reads metadata/sprites from the selected folder.
/// </summary>
public sealed class NhseAdapter : IDisposable
{
    private readonly NhseLoadContext _context;
    private readonly MethodInfo? _getItemSprite;
    private readonly Dictionary<ushort, ImageSource?> _spriteCache = new();

    private NhseAdapter(
        string folder,
        NhseLoadContext context,
        MethodInfo? getItemSprite,
        IReadOnlyList<ItemCatalogEntry> catalog)
    {
        Folder = folder;
        _context = context;
        _getItemSprite = getItemSprite;
        Catalog = catalog;
        CatalogById = catalog.ToDictionary(z => z.ItemId);
    }

    public string Folder { get; }
    public IReadOnlyList<ItemCatalogEntry> Catalog { get; }
    public IReadOnlyDictionary<ushort, ItemCatalogEntry> CatalogById { get; }

    public static NhseAdapter Load(string folder, string language = "fr")
    {
        folder = Path.GetFullPath(folder);
        var corePath = Path.Combine(folder, "NHSE.Core.dll");
        if (!File.Exists(corePath))
            throw new FileNotFoundException("NHSE.Core.dll est introuvable dans le dossier sélectionné.", corePath);

        var context = new NhseLoadContext(folder);
        try
        {
            var core = context.LoadFromAssemblyPath(corePath);
            var gameInfoType = core.GetType("NHSE.Core.GameInfo", throwOnError: true)!;
            var itemInfoType = core.GetType("NHSE.Core.ItemInfo", throwOnError: true)!;

            var getStrings = gameInfoType.GetMethod(
                "GetStrings",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(string)],
                modifiers: null)
                ?? throw new MissingMethodException("NHSE.Core.GameInfo.GetStrings(string) est introuvable.");

            var strings = getStrings.Invoke(null, [language])
                ?? throw new InvalidOperationException("NHSE n'a pas retourné les chaînes de langue demandées.");

            var stringsType = strings.GetType();
            var itemListField = stringsType.GetField("itemlistdisplay", BindingFlags.Public | BindingFlags.Instance)
                ?? throw new MissingFieldException("NHSE.Core.GameStrings.itemlistdisplay est introuvable.");
            var names = itemListField.GetValue(strings) as string[]
                ?? throw new InvalidOperationException("Impossible de lire la liste des objets NHSE.");

            var getItemKind = itemInfoType.GetMethod(
                "GetItemKind",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(ushort)],
                modifiers: null)
                ?? throw new MissingMethodException("NHSE.Core.ItemInfo.GetItemKind(ushort) est introuvable.");

            var tryGetMaxStack = itemInfoType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "TryGetMaxStackCount")
                        return false;
                    var p = m.GetParameters();
                    return p.Length == 2 && p[0].ParameterType == typeof(ushort) && p[1].IsOut;
                });

            var catalog = BuildCatalog(names, getItemKind, tryGetMaxStack);

            MethodInfo? getItemSprite = null;
            var spritesPath = Path.Combine(folder, "NHSE.Sprites.dll");
            if (File.Exists(spritesPath))
            {
                try
                {
                    var sprites = context.LoadFromAssemblyPath(spritesPath);
                    var spriteType = sprites.GetType("NHSE.Sprites.ItemSprite", throwOnError: false);
                    var initializeSprites = spriteType?.GetMethod(
                        "Initialize",
                        BindingFlags.Public | BindingFlags.Static,
                        binder: null,
                        types: [typeof(string[])],
                        modifiers: null);
                    getItemSprite = spriteType?.GetMethod(
                        "GetItemSprite",
                        BindingFlags.Public | BindingFlags.Static,
                        binder: null,
                        types: [typeof(ushort), typeof(ushort)],
                        modifiers: null);
                    initializeSprites?.Invoke(null, [names]);
                }
                catch
                {
                    getItemSprite = null;
                }
            }

            return new NhseAdapter(folder, context, getItemSprite, catalog);
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    private static IReadOnlyList<ItemCatalogEntry> BuildCatalog(
        string[] names,
        MethodInfo getItemKind,
        MethodInfo? tryGetMaxStack)
    {
        var result = new List<ItemCatalogEntry>(names.Length);
        var upper = Math.Min(names.Length, ushort.MaxValue + 1);

        for (var i = 1; i < upper; i++)
        {
            var name = names[i]?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var id = (ushort)i;
            string kind;
            try
            {
                kind = getItemKind.Invoke(null, [id])?.ToString() ?? "Unknown";
            }
            catch
            {
                kind = "Unknown";
            }

            var (category, subcategory) = Classify(kind);
            var isInternal = IsInternal(kind, name);
            var isInvalid = kind == "Unknown" || name.StartsWith("(Item #", StringComparison.OrdinalIgnoreCase) || name == "???";
            var maxStack = GetMaxStack(id, tryGetMaxStack);

            result.Add(new ItemCatalogEntry(
                id,
                name,
                category,
                subcategory,
                maxStack,
                IsOrderable: false,
                IsInternal: isInternal,
                IsInvalid: isInvalid,
                Variants: null,
                SpriteKey: $"{id:00000}_0"));
        }

        return result
            .OrderBy(z => z.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(z => z.Subcategory, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(z => z.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static int GetMaxStack(ushort id, MethodInfo? method)
    {
        if (method is null)
            return 1;

        try
        {
            object?[] args = [id, (ushort)0];
            var ok = method.Invoke(null, args) is true;
            if (ok && args[1] is ushort value && value > 0)
                return value;
        }
        catch
        {
        }
        return 1;
    }

    private static bool IsInternal(string kind, string name) =>
        kind.Contains("Dummy", StringComparison.OrdinalIgnoreCase) ||
        kind is "Kind_SequenceOnly" or "Kind_GardenEditList" or "Kind_PhotoStudioList" or "Kind_NnpcRoomMarker" ||
        name.Contains("dummy", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("debug", StringComparison.OrdinalIgnoreCase);

    private static (string Category, string? Subcategory) Classify(string kind)
    {
        if (kind.StartsWith("Top_", StringComparison.Ordinal)) return ("Vêtements", "Hauts");
        if (kind.StartsWith("Bottoms_", StringComparison.Ordinal)) return ("Vêtements", "Pantalons / jupes");
        if (kind.StartsWith("Onepiece_", StringComparison.Ordinal)) return ("Vêtements", "Robes / tenues");
        if (kind.StartsWith("Shoes_", StringComparison.Ordinal) || kind == "Kind_Shoes") return ("Vêtements", "Chaussures");
        if (kind == "Kind_Socks") return ("Vêtements", "Chaussettes");
        if (kind is "Kind_Bag" or "Kind_HandBag") return ("Vêtements", "Sacs");
        if (kind is "Kind_Cap" or "Kind_Helmet") return ("Vêtements", "Chapeaux");
        if (kind == "Kind_Accessory") return ("Vêtements", "Accessoires");
        if (kind is "Kind_NpcOutfit" or "Kind_PlayerDemoOutfit") return ("Vêtements", "Tenues spéciales");
        if (kind == "Kind_Umbrella") return ("Vêtements", "Parapluies");

        if (kind.StartsWith("Ftr_", StringComparison.Ordinal) || kind is "Kind_Ftr" or "Kind_EventObjFtr") return ("Meubles", "Mobilier");
        if (kind is "Kind_FtrWall" or "Kind_DoorDeco") return ("Meubles", "Objets muraux");
        if (kind == "Kind_RoomWall") return ("Meubles", "Papiers peints");
        if (kind == "Kind_RoomFloor") return ("Meubles", "Sols");
        if (kind is "Kind_Rug" or "Kind_RugMyDesign" or "Kind_CommonFabricRug") return ("Meubles", "Tapis");
        if (kind is "Kind_Pillar" or "Kind_Counter") return ("Meubles", "Piliers / comptoirs");

        if (kind is "Kind_CraftMaterial" or "Kind_CookingMaterial" or "Kind_Ore" or "Kind_WoodenStickTool" or
            "Kind_AutumnLeaf" or "Kind_EasterEgg" or "Kind_Feather" or "Kind_Honeycomb" or "Kind_LoveCrystal" or
            "Kind_Mushroom" or "Kind_RainbowFeather" or "Kind_Sakurapetal" or "Kind_SnowCrystal" or "Kind_StarPiece" or
            "Kind_Vine" or "Kind_XmasDeco" or "Kind_ShellDrift") return ("Matériaux", "Matériaux / saisonniers");

        if (kind is "Kind_Fruit" or "Kind_Vegetable" or "Kind_VegeSeedling" or "Kind_VegeTree" or "Kind_TreeSeedling" or "Kind_BushSeedling" or "Kind_FlowerSeed")
            return ("Matériaux", "Fruits / légumes / graines");

        if (kind == "Kind_Bromide") return ("Collections spéciales", "Photos");
        if (kind is "Kind_Gyroid" or "Kind_GyroidScrap") return ("Collections spéciales", "Gyroïdes");
        if (kind is "Kind_Picture" or "Kind_Sculpture") return ("Collections spéciales", "Art authentique");
        if (kind is "Kind_PictureFake" or "Kind_SculptureFake") return ("Collections spéciales", "Art contrefait");
        if (kind is "Kind_Music" or "Kind_MusicMiss") return ("Collections spéciales", "Musiques");
        if (kind == "Kind_Poster") return ("Collections spéciales", "Posters");
        if (kind is "Kind_FishToy" or "Kind_InsectToy") return ("Collections spéciales", "Modèles poissons / insectes");
        if (kind is "Kind_Fish" or "Kind_Insect" or "Kind_DiveFish" or "Kind_ShellFish" or "Kind_Fossil" or "Kind_FossilUnknown")
            return ("Collections spéciales", "Musée / créatures");

        if (kind is "Kind_Axe" or "Kind_FishingRod" or "Kind_Ladder" or "Kind_Net" or "Kind_Shovel" or "Kind_Slingshot" or "Kind_Watering" or "Kind_CliffMaker" or "Kind_GroundMaker" or "Kind_RiverMaker")
            return ("Outils / consommables", "Outils");

        if (kind is "Kind_Medicine" or "Kind_FishBait" or "Kind_WrappingPaper" or "Kind_PartyPopper" or "Kind_MilePlaneTicket" or "Kind_TailorTicket" or "Kind_RollanTicket")
            return ("Outils / consommables", "Consommables / tickets");

        if (kind is "Kind_DIYRecipe" or "Kind_MessageBottle") return ("Recettes", "Plans / recettes");
        if (kind is "Kind_Flower" or "Kind_FlowerBud" or "Kind_Bush" or "Kind_Tree" or "Kind_VegeTree") return ("Nature", "Plantes");

        return ("Autres", kind.Replace("Kind_", string.Empty));
    }

    public ImageSource? GetSprite(ushort id)
    {
        if (_spriteCache.TryGetValue(id, out var cached))
            return cached;

        var directPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NHSE", "img", $"{id:00000}_0.png");

        if (File.Exists(directPath))
        {
            var direct = LoadBitmap(directPath);
            _spriteCache[id] = direct;
            return direct;
        }

        if (_getItemSprite is null)
        {
            _spriteCache[id] = null;
            return null;
        }

        try
        {
            var raw = _getItemSprite.Invoke(null, [id, (ushort)0]);
            if (raw is not Image image)
            {
                _spriteCache[id] = null;
                return null;
            }

            using (image)
            using (var stream = new MemoryStream())
            {
                image.Save(stream, ImageFormat.Png);
                stream.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                _spriteCache[id] = bitmap;
                return bitmap;
            }
        }
        catch
        {
            _spriteCache[id] = null;
            return null;
        }
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _spriteCache.Clear();
        _context.Unload();
    }

    private sealed class NhseLoadContext(string folder) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name?.StartsWith("System.", StringComparison.Ordinal) == true)
                return null;

            var candidate = Path.Combine(folder, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
                return LoadFromAssemblyPath(candidate);
            return null;
        }
    }
}

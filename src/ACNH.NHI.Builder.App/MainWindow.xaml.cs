using ACNH.NHI.Builder.Core;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;

namespace ACNH.NHI.Builder.App;

public partial class MainWindow : Window
{
    private NhiInventory _inventory = new();
    private string? _currentPath;
    private NhseAdapter? _nhse;
    private ObservableCollection<CatalogRow> _catalogRows = [];
    private ICollectionView? _catalogView;
    private string _categoryFilter = "*";
    private readonly AppSettings _settings = AppSettings.Load();

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closed += (_, _) => _nhse?.Dispose();
        RefreshView();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_settings.NhsePath) && LooksLikeNhseFolder(_settings.NhsePath))
            await LoadNhseAsync(_settings.NhsePath, showErrorDialog: false);
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var enabled = 1;
            const int DwmwaUseImmersiveDarkMode = 20;
            const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
        }
        catch
        {
            // Cosmetic only. The WPF surface stays dark even when the OS title bar API is unavailable.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _inventory = new NhiInventory();
        _currentPath = null;
        RefreshView();
        SetStatus("Nouvel inventaire de 9 000 cases prêt.");
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Ouvrir un inventaire NHI",
            Filter = "Fichiers NHI (*.nhi)|*.nhi|Tous les fichiers (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _inventory = NhiInventory.Load(dialog.FileName);
            _currentPath = dialog.FileName;
            RefreshView();
            MainTabs.SelectedIndex = 1;
            SetStatus($"{Path.GetFileName(dialog.FileName)} chargé : {_inventory.UsedSlots:N0} case(s) utilisée(s).");
        }
        catch (Exception ex)
        {
            ShowError("Fichier NHI invalide", $"Impossible d'ouvrir ce fichier .nhi.\n\n{ex.Message}");
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var suggested = _currentPath is null ? "ACNH_collection_9000.nhi" : Path.GetFileName(_currentPath);
        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer l'inventaire NHI",
            Filter = "Fichiers NHI (*.nhi)|*.nhi|Tous les fichiers (*.*)|*.*",
            FileName = suggested,
            AddExtension = true,
            DefaultExt = ".nhi"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            _inventory.Save(dialog.FileName);
            _currentPath = dialog.FileName;
            RefreshView();
            SetStatus($"Enregistré : {dialog.FileName}", success: true);
        }
        catch (Exception ex)
        {
            ShowError("Erreur d'enregistrement", ex.Message);
        }
    }

    private void CleanDuplicates_Click(object sender, RoutedEventArgs e)
    {
        var analysis = InventoryAnalyzer.Analyze(_inventory);
        if (analysis.DuplicateSlots == 0)
        {
            SetStatus("Aucun doublon strict détecté.");
            return;
        }

        var answer = MessageBox.Show(this,
            $"Supprimer {analysis.DuplicateSlots} doublon(s) strictement identique(s) et compacter l'inventaire ?\n\nUne copie de l'original est recommandée.",
            "Confirmer le nettoyage",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        var removed = _inventory.RemoveDuplicates();
        RefreshView();
        SetStatus($"{removed} doublon(s) supprimé(s).", success: true);
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exporter le manifeste CSV",
            Filter = "Fichier CSV (*.csv)|*.csv",
            FileName = _currentPath is null
                ? "ACNH_collection_manifest.csv"
                : $"{Path.GetFileNameWithoutExtension(_currentPath)}_manifest.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            IReadOnlyDictionary<ushort, ItemCatalogEntry> names = _nhse?.CatalogById
                ?? new Dictionary<ushort, ItemCatalogEntry>();
            ManifestWriter.WriteCsv(dialog.FileName, _inventory, names);
            SetStatus($"Manifeste CSV exporté : {dialog.FileName}", success: true);
        }
        catch (Exception ex)
        {
            ShowError("Erreur d'export", ex.Message);
        }
    }

    private async void Nhse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Sélectionner le dossier contenant NHSE"
        };

        if (dialog.ShowDialog() != true)
            return;

        var detected = FindNhseFolder(dialog.FolderName);
        if (detected is null)
        {
            ShowError("NHSE non détecté",
                "Aucune installation NHSE compatible n'a été trouvée.\n\nSélectionne le dossier contenant au minimum NHSE.Core.dll. Le programme vérifie aussi les sous-dossiers directs.");
            return;
        }

        await LoadNhseAsync(detected, showErrorDialog: true);
    }

    private async Task LoadNhseAsync(string folder, bool showErrorDialog)
    {
        SearchBox.IsEnabled = false;
        CategoryTree.IsEnabled = false;
        AdapterStatusText.Text = "Chargement des noms français, catégories et sprites NHSE…";
        AdapterStatusText.Foreground = FindBrush("CyanBrush");
        NhseStatusText.Text = folder;

        try
        {
            var adapter = await Task.Run(() => NhseAdapter.Load(folder, "fr"));

            CatalogGrid.ItemsSource = null;
            _catalogView = null;
            _catalogRows.Clear();
            _nhse?.Dispose();
            _nhse = adapter;

            var rows = adapter.Catalog
                .Select(entry => new CatalogRow(entry, adapter))
                .ToList();
            foreach (var row in rows)
                row.PropertyChanged += CatalogRow_PropertyChanged;

            _catalogRows = new ObservableCollection<CatalogRow>(rows);
            _catalogView = CollectionViewSource.GetDefaultView(_catalogRows);
            _catalogView.Filter = CatalogFilter;
            CatalogGrid.ItemsSource = _catalogView;

            SearchBox.IsEnabled = true;
            CategoryTree.IsEnabled = true;
            _settings.NhsePath = folder;
            _settings.Save();

            NhseStatusText.Text = $"NHSE : {folder}";
            NhseStatusText.ToolTip = folder;
            AdapterStatusText.Text = $"✓ {rows.Count:N0} entrées chargées en français. Le catalogue et les presets sont actifs.";
            AdapterStatusText.Foreground = FindBrush("SuccessBrush");

            RefreshCatalogFilter();
            RefreshView();
            SetStatus("NHSE chargé : le générateur visuel est prêt.", success: true);
        }
        catch (Exception ex)
        {
            AdapterStatusText.Text = "Échec du chargement NHSE. Le lecteur .nhi reste utilisable.";
            AdapterStatusText.Foreground = FindBrush("DangerBrush");
            SearchBox.IsEnabled = _nhse is not null;
            CategoryTree.IsEnabled = _nhse is not null;

            if (showErrorDialog)
                ShowError("Impossible de charger NHSE", $"Le dossier a bien été détecté, mais ses données n'ont pas pu être chargées.\n\n{ex.Message}");
        }
    }

    private static string? FindNhseFolder(string selectedFolder)
    {
        if (LooksLikeNhseFolder(selectedFolder))
            return Path.GetFullPath(selectedFolder);

        try
        {
            foreach (var child in Directory.EnumerateDirectories(selectedFolder))
            {
                if (LooksLikeNhseFolder(child))
                    return Path.GetFullPath(child);
            }
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (IOException)
        {
        }

        return null;
    }

    private static bool LooksLikeNhseFolder(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) &&
        Directory.Exists(folder) &&
        File.Exists(Path.Combine(folder, "NHSE.Core.dll"));

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshCatalogFilter();

    private void CatalogFilter_Changed(object sender, RoutedEventArgs e) => RefreshCatalogFilter();

    private void CategoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item)
            _categoryFilter = item.Tag?.ToString() ?? "*";
        RefreshCatalogFilter();
    }

    private bool CatalogFilter(object obj)
    {
        if (obj is not CatalogRow row)
            return false;

        if (!IsAllowedBySafety(row))
            return false;

        if (!MatchesCategory(row, _categoryFilter))
            return false;

        var query = SearchBox.Text.Trim();
        if (query.Length == 0)
            return true;

        return row.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               row.IdText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               row.ItemId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAllowedBySafety(CatalogRow row)
    {
        if (HideInternalCheckBox.IsChecked == true && row.Entry.IsInternal)
            return false;
        if (HideInvalidCheckBox.IsChecked == true && row.Entry.IsInvalid)
            return false;
        return true;
    }

    private static bool MatchesCategory(CatalogRow row, string filter)
    {
        if (filter == "*" || string.IsNullOrWhiteSpace(filter))
            return true;

        var split = filter.Split('|', 2);
        if (!string.Equals(row.Category, split[0], StringComparison.CurrentCultureIgnoreCase))
            return false;
        if (split.Length == 1)
            return true;
        return string.Equals(row.Subcategory, split[1], StringComparison.CurrentCultureIgnoreCase);
    }

    private void RefreshCatalogFilter()
    {
        _catalogView?.Refresh();
        RefreshCatalogSummary();
    }

    private void RefreshCatalogSummary()
    {
        if (_catalogView is null)
        {
            CatalogCountText.Text = "Charge NHSE pour afficher les objets.";
            SelectedCountText.Text = "0 objet sélectionné";
            return;
        }

        var visible = _catalogView.Cast<CatalogRow>().Count();
        var selected = _catalogRows.Count(z => z.Selected);
        CatalogCountText.Text = $"{visible:N0} visible(s) sur {_catalogRows.Count:N0} entrée(s) NHSE";
        SelectedCountText.Text = $"{selected:N0} objet(s) sélectionné(s)";
    }

    private void CatalogRow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CatalogRow.Selected))
            RefreshCatalogSummary();
    }

    private void SelectVisible_Click(object sender, RoutedEventArgs e)
    {
        if (_catalogView is null)
            return;
        foreach (var row in _catalogView.Cast<CatalogRow>().ToArray())
            row.Selected = true;
        RefreshCatalogSummary();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _catalogRows)
            row.Selected = false;
        RefreshCatalogSummary();
    }

    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_nhse is null)
        {
            SetStatus("Charge d'abord le dossier NHSE.");
            return;
        }

        if (PresetComboBox.SelectedItem is not ComboBoxItem preset)
            return;

        var filter = preset.Tag?.ToString() ?? "*";
        var count = 0;
        foreach (var row in _catalogRows)
        {
            var select = IsAllowedBySafety(row) && MatchesCategory(row, filter);
            row.Selected = select;
            if (select)
                count++;
        }

        RefreshCatalogSummary();
        SetStatus($"Preset « {preset.Content} » : {count:N0} objet(s) sélectionné(s).");
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_nhse is null)
        {
            SetStatus("Charge d'abord NHSE pour sélectionner des objets.");
            return;
        }

        var selected = _catalogRows.Where(z => z.Selected && IsAllowedBySafety(z)).ToArray();
        if (selected.Length == 0)
        {
            SetStatus("Aucun objet sélectionné.");
            return;
        }

        if (!int.TryParse(StackCopiesBox.Text, out var copies) || copies is < 1 or > 100)
        {
            ShowError("Nombre de piles invalide", "Indique un nombre de piles/exemplaires entre 1 et 100.");
            return;
        }

        var existingIds = SkipExistingCheckBox.IsChecked == true
            ? _inventory.Slots.Where(z => !z.IsEmpty).Select(z => z.ItemId).ToHashSet()
            : [];

        var candidates = selected
            .Where(row => SkipExistingCheckBox.IsChecked != true || !existingIds.Contains(row.ItemId))
            .ToArray();

        var slotsNeeded = candidates.Length * copies;
        if (slotsNeeded == 0)
        {
            SetStatus("Tous les objets sélectionnés sont déjà présents dans le .nhi.");
            return;
        }

        if (slotsNeeded > _inventory.FreeSlots)
        {
            ShowError("Stockage insuffisant",
                $"La sélection demande {slotsNeeded:N0} case(s), mais il n'en reste que {_inventory.FreeSlots:N0}.\n\nRéduis la sélection ou le nombre de piles.");
            return;
        }

        var added = 0;
        foreach (var row in candidates)
        {
            var quantity = UseMaxStackCheckBox.IsChecked == true ? Math.Max(1, row.MaxStack) : 1;
            for (var copy = 0; copy < copies; copy++)
            {
                _inventory.Add(NhiItem.Create(row.ItemId, quantity));
                added++;
            }
        }

        foreach (var row in selected)
            row.Selected = false;

        RefreshView();
        RefreshCatalogSummary();
        MainTabs.SelectedIndex = 1;
        SetStatus($"{added:N0} case(s) ajoutée(s) au .nhi.", success: true);
    }

    private void RefreshView()
    {
        var analysis = InventoryAnalyzer.Analyze(_inventory);
        var rows = _inventory.Slots
            .Select((item, index) =>
            {
                _nhse?.CatalogById.TryGetValue(item.ItemId, out var entry);
                return new InventoryRow(
                    index + 1,
                    entry?.Name ?? "Inconnu",
                    $"0x{item.ItemId:X4}",
                    item.Quantity,
                    entry?.Category ?? "—",
                    item.SystemParam,
                    item.AdditionalParam,
                    item.UseCount,
                    item.IsEmpty);
            })
            .Where(row => !row.IsEmpty)
            .ToArray();

        ItemsGrid.ItemsSource = rows;
        CurrentFileText.Text = _currentPath ?? "Nouvel inventaire non enregistré";
        CapacityText.Text = $"Capacité : {analysis.Capacity:N0}";
        UsedText.Text = $"Utilisées : {analysis.Used:N0}";
        FreeText.Text = $"Libres : {analysis.Free:N0}";
        DuplicateText.Text = $"Doublons stricts : {analysis.DuplicateSlots:N0}";
        UniqueText.Text = $"IDs uniques : {analysis.UniqueItemIds:N0}";
        CleanButton.IsEnabled = analysis.DuplicateSlots > 0;
        SlotProgress.Maximum = analysis.Capacity;
        SlotProgress.Value = analysis.Used;
        StatusText.Text = $"{analysis.Used:N0} / {analysis.Capacity:N0} cases utilisées — {analysis.Free:N0} libres";
    }

    private void SetStatus(string message, bool success = false)
    {
        AdapterStatusText.Text = message;
        AdapterStatusText.Foreground = FindBrush(success ? "SuccessBrush" : "MutedTextBrush");
    }

    private SolidColorBrush FindBrush(string key) =>
        (SolidColorBrush)(Application.Current.TryFindResource(key) ?? Brushes.White);

    private void ShowError(string title, string message)
    {
        AdapterStatusText.Text = message.Replace("\n", " ");
        AdapterStatusText.Foreground = FindBrush("DangerBrush");
        MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private sealed class CatalogRow : INotifyPropertyChanged
    {
        private readonly NhseAdapter _adapter;
        private bool _selected;
        private ImageSource? _sprite;
        private bool _spriteLoaded;

        public CatalogRow(ItemCatalogEntry entry, NhseAdapter adapter)
        {
            Entry = entry;
            _adapter = adapter;
        }

        public ItemCatalogEntry Entry { get; }
        public ushort ItemId => Entry.ItemId;
        public string IdText => $"0x{ItemId:X4}";
        public string Name => Entry.Name;
        public string Category => Entry.Category;
        public string Subcategory => Entry.Subcategory ?? "—";
        public int MaxStack => Entry.MaxStack;

        public ImageSource? Sprite
        {
            get
            {
                if (_spriteLoaded)
                    return _sprite;
                _spriteLoaded = true;
                _sprite = _adapter.GetSprite(ItemId);
                return _sprite;
            }
        }

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected == value)
                    return;
                _selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed record InventoryRow(
        int Slot,
        string Name,
        string ItemId,
        int Quantity,
        string Category,
        byte SystemParam,
        byte AdditionalParam,
        ushort UseCount,
        bool IsEmpty);
}

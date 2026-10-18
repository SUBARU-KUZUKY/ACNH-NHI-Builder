using ACNH.NHI.Builder.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace ACNH.NHI.Builder.App;

public partial class MainWindow : Window
{
    private NhiInventory _inventory = new();
    private string? _currentPath;
    private string? _nhsePath;

    public MainWindow()
    {
        InitializeComponent();
        RefreshView();
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _inventory = new NhiInventory();
        _currentPath = null;
        RefreshView();
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
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                $"Impossible d'ouvrir ce fichier .nhi.\n\n{ex.Message}",
                "Fichier NHI invalide",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var suggested = _currentPath is null ? "inventory.nhi" : Path.GetFileName(_currentPath);
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
            MessageBox.Show(this,
                "Le fichier .nhi a été enregistré.",
                "Enregistrement terminé",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur d'enregistrement", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CleanDuplicates_Click(object sender, RoutedEventArgs e)
    {
        var analysis = InventoryAnalyzer.Analyze(_inventory);
        if (analysis.DuplicateSlots == 0)
        {
            MessageBox.Show(this, "Aucun doublon strict n'a été trouvé.", "Nettoyage", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(this,
            $"Supprimer {analysis.DuplicateSlots} doublon(s) strictement identique(s) et compacter l'inventaire ?\n\nFais une copie de ton .nhi original si tu veux pouvoir revenir en arrière.",
            "Confirmer le nettoyage",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
            return;

        var removed = _inventory.RemoveDuplicates();
        RefreshView();
        MessageBox.Show(this, $"{removed} doublon(s) supprimé(s).", "Nettoyage terminé", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exporter le manifeste CSV",
            Filter = "Fichier CSV (*.csv)|*.csv",
            FileName = _currentPath is null
                ? "inventory_manifest.csv"
                : $"{Path.GetFileNameWithoutExtension(_currentPath)}_manifest.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            ManifestWriter.WriteCsv(dialog.FileName, _inventory, new Dictionary<ushort, ItemCatalogEntry>());
            MessageBox.Show(this,
                "Le manifeste CSV a été exporté. Les noms restent 'Unknown' jusqu'à l'intégration des données NHSE dans la v0.2.",
                "Export terminé",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur d'export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Nhse_Click(object sender, RoutedEventArgs e)
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
            MessageBox.Show(this,
                "Je n'ai pas trouvé d'installation NHSE dans ce dossier.\n\nSélectionne le dossier qui contient NHSE.exe / NHSE.WinForms.exe ou NHSE.Core.dll. Le programme vérifie aussi les sous-dossiers directs.",
                "NHSE non détecté",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _nhsePath = detected;
        NhseStatusText.Text = $"NHSE détecté : {_nhsePath}";
        NhseStatusText.ToolTip = _nhsePath;

        MessageBox.Show(this,
            "Dossier NHSE détecté et mémorisé pour cette session.\n\nLa prochaine étape (v0.2) utilisera ce dossier pour charger les noms, catégories, variantes et sprites.",
            "NHSE détecté",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static string? FindNhseFolder(string selectedFolder)
    {
        if (LooksLikeNhseFolder(selectedFolder))
            return selectedFolder;

        try
        {
            foreach (var child in Directory.EnumerateDirectories(selectedFolder))
            {
                if (LooksLikeNhseFolder(child))
                    return child;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // The selected root itself was readable, but one of its children was not.
        }
        catch (IOException)
        {
            // Ignore inaccessible child folders and report a normal detection failure.
        }

        return null;
    }

    private static bool LooksLikeNhseFolder(string folder)
    {
        var markers = new[]
        {
            "NHSE.exe",
            "NHSE.WinForms.exe",
            "NHSE.Core.dll",
            "NHSE.Sprites.dll"
        };

        return markers.Any(file => File.Exists(Path.Combine(folder, file)));
    }

    private void Presets_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this,
            "Les presets (tout le catalogue, vêtements, matériaux, collections, variantes, etc.) seront activés quand l'adaptateur NHSE chargera réellement les données dans la v0.2.",
            "Presets — v0.2",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void RefreshView()
    {
        var analysis = InventoryAnalyzer.Analyze(_inventory);
        var rows = _inventory.Slots
            .Select((item, index) => new InventoryRow(
                index + 1,
                $"0x{item.ItemId:X4}",
                item.Quantity,
                item.SystemParam,
                item.AdditionalParam,
                item.UseCount,
                item.IsEmpty))
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

    private sealed record InventoryRow(
        int Slot,
        string ItemId,
        int Quantity,
        byte SystemParam,
        byte AdditionalParam,
        ushort UseCount,
        bool IsEmpty);
}

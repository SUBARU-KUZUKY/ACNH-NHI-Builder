using ACNH.NHI.Builder.Core;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace ACNH.NHI.Builder.App;

public partial class MainWindow : Window
{
    private NhiInventory _inventory = new();
    private string? _currentPath;

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
                "Le manifeste CSV a été exporté. Les noms restent 'Unknown' dans cette alpha jusqu'à l'intégration des données NHSE.",
                "Export terminé",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur d'export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Nhse_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this,
            "La détection des données et sprites NHSE est la prochaine grosse étape. Cette première release permet déjà de charger, analyser, nettoyer et réenregistrer les .nhi.",
            "Adaptateur NHSE — bientôt",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void Presets_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(this,
            "Les presets (tout le catalogue, vêtements, matériaux, collections, variantes, etc.) arriveront avec l'adaptateur NHSE.",
            "Presets — bientôt",
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

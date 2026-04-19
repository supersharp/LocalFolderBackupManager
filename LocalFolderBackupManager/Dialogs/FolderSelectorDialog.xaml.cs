using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LocalFolderBackupManager.Dialogs;

public class FolderItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _suppressNotifications;

    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public ObservableCollection<FolderItem> Children { get; set; } = new();
    public FolderItem? Parent { get; set; } // Reference to parent folder

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;

                if (!_suppressNotifications)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));

                    // Cascade to children
                    foreach (var child in Children)
                    {
                        child.IsSelected = value;
                    }

                    // Update parent checkbox state based on children
                    Parent?.UpdateFromChildren();
                }
            }
        }
    }

    private void UpdateFromChildren()
    {
        if (Children.Count == 0)
            return;

        // Check if all children are selected
        bool allSelected = Children.All(c => c.IsSelected);
        bool noneSelected = Children.All(c => !c.IsSelected);

        _suppressNotifications = true;
        if (allSelected)
        {
            _isSelected = true;
        }
        else if (noneSelected)
        {
            _isSelected = false;
        }
        else
        {
            // Some children selected, some not - uncheck parent (partial state)
            _isSelected = false;
        }
        _suppressNotifications = false;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));

        // Propagate update to parent's parent
        Parent?.UpdateFromChildren();
    }

    public void SetSelectedSilent(bool value)
    {
        _suppressNotifications = true;
        IsSelected = value;

        // Cascade to children silently
        foreach (var child in Children)
        {
            child.SetSelectedSilent(value);
        }

        _suppressNotifications = false;
    }

    internal void SetSelectedWithoutCascade(bool value)
    {
        _suppressNotifications = true;
        _isSelected = value;
        _suppressNotifications = false;
    }

    internal void RaisePropertyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class FolderSelectorDialog : FluentWindow
{
    private readonly ObservableCollection<FolderItem> _folders = new();
    private bool _isBatchUpdating = false;
    private int _maxDepth = int.MaxValue; // Default: no limit
    private string _sourcePath = string.Empty;
    private Models.FilterMode _filterMode;
    public List<string> SelectedFolderNames { get; private set; } = new();
    public int LastUsedDepth { get; private set; } = 1;

    public FolderSelectorDialog()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();

        // Populate depth limit combo box - Top Level first, All Levels last
        DepthLimitComboBox.Items.Add(new ComboBoxItem { Content = "1 Level (Top Only)", Tag = 1 });
        DepthLimitComboBox.Items.Add(new ComboBoxItem { Content = "2 Levels", Tag = 2 });
        DepthLimitComboBox.Items.Add(new ComboBoxItem { Content = "3 Levels", Tag = 3 });
        DepthLimitComboBox.Items.Add(new ComboBoxItem { Content = "4 Levels", Tag = 4 });
        DepthLimitComboBox.Items.Add(new ComboBoxItem { Content = "5 Levels", Tag = 5 });
        DepthLimitComboBox.Items.Add(new ComboBoxItem { Content = "All Levels (No Limit)", Tag = int.MaxValue });
        DepthLimitComboBox.SelectedIndex = 0; // Default: Top Level (1 level)
        _maxDepth = 1; // Set initial max depth to match default selection
    }

    public static FolderSelectorDialog Create(string sourcePath, Models.FilterMode filterMode, List<string>? preSelectedFolders = null, int preferredDepth = 1)
    {
        var dialog = new FolderSelectorDialog();

        // Set the preferred depth before loading folders
        if (preferredDepth >= 1 && preferredDepth <= 5)
        {
            dialog.DepthLimitComboBox.SelectedIndex = preferredDepth - 1;
            dialog._maxDepth = preferredDepth; // Manually update _maxDepth since SelectionChanged won't fire
        }
        else if (preferredDepth == int.MaxValue)
        {
            dialog.DepthLimitComboBox.SelectedIndex = 5; // "All Levels"
            dialog._maxDepth = int.MaxValue; // Manually update _maxDepth
        }
        else
        {
            // Fallback to default depth if invalid
            dialog.DepthLimitComboBox.SelectedIndex = 0;
            dialog._maxDepth = 1;
        }

        dialog.LoadFolders(sourcePath, filterMode, preSelectedFolders);
        return dialog;
    }

    private void LoadFolders(string sourcePath, Models.FilterMode filterMode, List<string>? preSelectedFolders = null)
    {
        _sourcePath = sourcePath;
        _filterMode = filterMode;

        SourcePathText.Text = sourcePath;

        // Update the descriptive text based on filter mode
        var descriptionText = filterMode switch
        {
            Models.FilterMode.Blacklist => "Select folders to exclude from the backup:",
            Models.FilterMode.Whitelist => "Select folders to include in the backup:",
            _ => "Select folders to add to filter:"
        };

        // Find the description TextBlock and update it (we'll add this to XAML)
        var descBlock = FindName("FilterDescriptionText") as System.Windows.Controls.TextBlock;
        if (descBlock != null)
        {
            descBlock.Text = descriptionText;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !Directory.Exists(sourcePath))
        {
            NoFoldersMessage.Visibility = Visibility.Visible;
            NoFoldersMessage.Text = "Source path is invalid or does not exist.";
            UpdateSelectionInfo();
            return;
        }

        try
        {
            var directories = Directory.GetDirectories(sourcePath);

            if (directories.Length == 0)
            {
                NoFoldersMessage.Visibility = Visibility.Visible;
                UpdateSelectionInfo();
                return;
            }

            foreach (var dir in directories.OrderBy(d => Path.GetFileName(d)))
            {
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName)) continue;

                var folderItem = new FolderItem
                {
                    Name = folderName,
                    FullPath = dir,
                    IsSelected = false
                };

                // Load child folders recursively
                LoadChildFolders(folderItem, dir, 1);

                folderItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(FolderItem.IsSelected) && !_isBatchUpdating)
                        UpdateSelectionInfo();
                };

                _folders.Add(folderItem);
            }

            FoldersTreeView.ItemsSource = _folders;

            // Pre-select folders that are already in the filter list
            if (preSelectedFolders != null && preSelectedFolders.Any())
            {
                PreSelectFolders(preSelectedFolders);
            }

            UpdateSelectionInfo();
        }
        catch (Exception ex)
        {
            NoFoldersMessage.Visibility = Visibility.Visible;
            NoFoldersMessage.Text = $"Error loading folders: {ex.Message}";
            UpdateSelectionInfo();
        }
    }

    private void PreSelectFolders(List<string> foldersToSelect)
    {
        _isBatchUpdating = true;

        foreach (var folderPath in foldersToSelect)
        {
            SelectFolderByPath(_folders, folderPath);
        }

        _isBatchUpdating = false;
        RefreshPropertyChangedForTree(_folders);
        UpdateSelectionInfo();
    }

    private bool SelectFolderByPath(ObservableCollection<FolderItem> items, string folderPath)
    {
        var pathParts = folderPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var item in items)
        {
            // Check if this item matches the first part of the path
            if (item.Name.Equals(pathParts[0], StringComparison.OrdinalIgnoreCase))
            {
                if (pathParts.Length == 1)
                {
                    // Found the target folder
                    item.SetSelectedWithoutCascade(true);
                    return true;
                }
                else
                {
                    // Continue searching in children
                    var remainingPath = string.Join("\\", pathParts.Skip(1));
                    return SelectFolderByPath(item.Children, remainingPath);
                }
            }
        }

        return false;
    }

    private void LoadChildFolders(FolderItem parent, string path, int currentDepth = 1)
    {
        // Stop if we've reached the maximum depth
        if (currentDepth >= _maxDepth)
            return;

        try
        {
            var childDirectories = Directory.GetDirectories(path);

            foreach (var dir in childDirectories.OrderBy(d => Path.GetFileName(d)))
            {
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName)) continue;

                var childItem = new FolderItem
                {
                    Name = folderName,
                    FullPath = dir,
                    IsSelected = false,
                    Parent = parent
                };

                // Recursively load grandchildren with incremented depth
                LoadChildFolders(childItem, dir, currentDepth + 1);

                childItem.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(FolderItem.IsSelected) && !_isBatchUpdating)
                        UpdateSelectionInfo();
                };

                parent.Children.Add(childItem);
            }
        }
        catch
        {
            // Skip folders we can't access (permissions, etc.)
        }
    }

    private void UpdateSelectionInfo()
    {
        var selectedCount = CountSelected(_folders);
        var totalCount = CountTotal(_folders);

        if (totalCount == 0)
        {
            SelectionInfoText.Text = "No folders available to select.";
        }
        else
        {
            SelectionInfoText.Text = $"{selectedCount} of {totalCount} folders selected";
        }
    }

    private int CountSelected(ObservableCollection<FolderItem> items)
    {
        int count = 0;
        foreach (var item in items)
        {
            if (item.IsSelected) count++;
            count += CountSelected(item.Children);
        }
        return count;
    }

    private int CountTotal(ObservableCollection<FolderItem> items)
    {
        int count = items.Count;
        foreach (var item in items)
        {
            count += CountTotal(item.Children);
        }
        return count;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as System.Windows.Controls.Button;
        if (button != null)
        {
            button.IsEnabled = false;
            button.Content = "Selecting...";
        }

        try
        {
            // Enable batch mode to suppress PropertyChanged updates
            _isBatchUpdating = true;

            // Select folders up to the current depth limit
            SetAllSelectedBatchWithDepth(_folders, true, 1, _maxDepth);

            // Disable batch mode
            _isBatchUpdating = false;

            // Manually trigger PropertyChanged on all affected items to update UI bindings
            RefreshPropertyChangedForTree(_folders);

            // Update selection count
            UpdateSelectionInfo();
        }
        finally
        {
            _isBatchUpdating = false;
            if (button != null)
            {
                button.Content = "Select All";
                button.IsEnabled = true;
            }
        }
    }

    private void SetAllSelectedBatchWithDepth(ObservableCollection<FolderItem> items, bool isSelected, int currentDepth, int maxDepth)
    {
        foreach (var item in items)
        {
            item.SetSelectedWithoutCascade(isSelected);

            // Only recurse into children if we haven't reached the max depth
            if (currentDepth < maxDepth)
            {
                SetAllSelectedBatchWithDepth(item.Children, isSelected, currentDepth + 1, maxDepth);
            }
        }
    }

    private void RefreshPropertyChangedForTree(ObservableCollection<FolderItem> items)
    {
        foreach (var item in items)
        {
            // Manually trigger PropertyChanged to update UI binding
            item.RaisePropertyChanged();

            // Recurse into children
            RefreshPropertyChangedForTree(item.Children);
        }
    }

    private void SetAllSelectedBatch(ObservableCollection<FolderItem> items, bool isSelected)
    {
        foreach (var item in items)
        {
            item.SetSelectedSilent(isSelected);
            SetAllSelectedBatch(item.Children, isSelected);
        }
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedFolderNames = GetSelectedFolderPaths(_folders, "");
        LastUsedDepth = _maxDepth; // Save the current depth setting

        DialogResult = true;
        Close();
    }

    private List<string> GetSelectedFolderPaths(ObservableCollection<FolderItem> items, string parentPath)
    {
        var selected = new List<string>();

        foreach (var item in items)
        {
            var currentPath = string.IsNullOrEmpty(parentPath) 
                ? item.Name 
                : $"{parentPath}\\{item.Name}";

            if (item.IsSelected)
            {
                selected.Add(currentPath);
            }

            // Also get selected children with their full relative paths
            selected.AddRange(GetSelectedFolderPaths(item.Children, currentPath));
        }

        return selected;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void DepthLimitComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DepthLimitComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
        {
            if (selectedItem.Tag is int depth)
            {
                _maxDepth = depth;

                // Reload folders with new depth limit if already loaded
                if (!string.IsNullOrEmpty(_sourcePath))
                {
                    ReloadFolders();
                }
            }
        }
    }

    private void ReloadFolders()
    {
        // Clear existing folders
        _folders.Clear();

        // Reload with current depth limit
        LoadFolders(_sourcePath, _filterMode);
    }
}

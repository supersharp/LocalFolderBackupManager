using LocalFolderBackupManager.Models;
using LocalFolderBackupManager.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace LocalFolderBackupManager.ViewModels;

public class FilterEntryViewModel : ViewModelBase
{
    private readonly FolderMappingViewModel _parent;
    private static int _lastUsedDepth = 1; // Remember depth preference across dialog instances

    private string _pattern;
    public string Pattern
    {
        get => _pattern;
        set => SetProperty(ref _pattern, value);
    }

    private string _description;
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private FilterMode _mode;
    public FilterMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
                OnPropertyChanged(nameof(SymbolText));
        }
    }

    public string SymbolText => Mode == FilterMode.Whitelist ? "➕" : "➖";

    public ICommand RemoveCommand { get; }
    public ICommand BrowseFoldersCommand { get; }

    public FilterEntryViewModel(FolderMappingViewModel parent)
    {
        _parent = parent;
        _pattern     = string.Empty;
        _description = string.Empty;
        RemoveCommand = new RelayCommand(_ => parent.Filters.Remove(this));
        BrowseFoldersCommand = new RelayCommand(_ => BrowseFolders());
    }

    private void BrowseFolders()
    {
        var sourcePath = _parent.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            DialogService.ShowWarning("Please select a source folder first before adding folder filters.", "Source Path Required");
            return;
        }

        // Determine the mode to use - use the filter's Mode if it's not None, otherwise use Blacklist
        var filterMode = Mode != FilterMode.None ? Mode : FilterMode.Blacklist;

        // Auto-set parent FilterMode if this is the first filter and it's currently None
        if (_parent.Filters.Count == 1 && _parent.FilterMode == FilterMode.None)
        {
            _parent.FilterMode = filterMode;
        }

        // Get existing filter patterns to pre-select in dialog
        var existingPatterns = _parent.Filters
            .Where(f => f.Mode == filterMode)
            .Select(f => f.Pattern)
            .ToList();

        var dialog = Dialogs.FolderSelectorDialog.Create(sourcePath, filterMode, existingPatterns, _lastUsedDepth);
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true && dialog.SelectedFolderNames.Any())
        {
            // Remember the depth setting for next time
            _lastUsedDepth = dialog.LastUsedDepth;

            // Remove the current empty filter entry
            _parent.Filters.Remove(this);

            // Get existing filter patterns to check for duplicates
            var existingFilterSet = new HashSet<string>(
                _parent.Filters.Select(f => f.Pattern),
                StringComparer.OrdinalIgnoreCase);

            var addedCount = 0;
            var duplicateCount = 0;

            // Create a separate filter entry for each selected folder (no optimization)
            // Insert at index 0 (top of list) instead of adding to end
            int insertIndex = 0;
            foreach (var folderName in dialog.SelectedFolderNames)
            {
                // Skip if this filter already exists
                if (existingFilterSet.Contains(folderName))
                {
                    duplicateCount++;
                    continue;
                }

                var newFilter = new FilterEntryViewModel(_parent)
                {
                    Pattern = folderName,
                    Mode = filterMode,
                    Description = $"Folder: {folderName}"
                };
                _parent.Filters.Insert(insertIndex, newFilter);
                insertIndex++; // Increment so multiple new filters maintain their relative order
                existingFilterSet.Add(folderName);
                addedCount++;
            }

            // Show summary
            if (addedCount > 0 || duplicateCount > 0)
            {
                var message = addedCount > 0 
                    ? $"Added {addedCount} folder filter(s)." 
                    : "No new filters added.";

                if (duplicateCount > 0)
                {
                    message += $"\n\nSkipped {duplicateCount} duplicate filter(s).";
                }

                DialogService.ShowSuccess(message, "Folders Added");
            }
        }
    }
}

public class FolderMappingViewModel : ViewModelBase
{
    private readonly FoldersViewModel _parent;
    private static int _lastUsedDepth = 1; // Remember last depth setting across dialog openings

    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _sourcePath;
    public string SourcePath
    {
        get => _sourcePath;
        set => SetProperty(ref _sourcePath, value);
    }

    private string _destinationPath;
    public string DestinationPath
    {
        get => _destinationPath;
        set => SetProperty(ref _destinationPath, value);
    }

    private FilterMode _filterMode;
    public FilterMode FilterMode
    {
        get => _filterMode;
        set
        {
            if (SetProperty(ref _filterMode, value))
                FilteredFiltersView?.Refresh();
        }
    }

    public ObservableCollection<FilterEntryViewModel> Filters { get; } = new();
    public ObservableCollection<string> AssignedSchedules { get; } = new();
    public System.ComponentModel.ICollectionView FilteredFiltersView { get; }

    public ICommand BrowseSourceCommand     { get; }
    public ICommand BrowseDestinationCommand { get; }
    public ICommand RemoveCommand           { get; }
    public ICommand AddFilterCommand        { get; }
    public ICommand ClearAllFiltersCommand  { get; }
    public ICommand ManageSchedulesCommand  { get; }

    public IEnumerable<FilterMode> AvailableFilterModes =>
        Enum.GetValues(typeof(FilterMode)).Cast<FilterMode>();

    public string SchedulesSummary => AssignedSchedules.Count == 0 
        ? "No schedules assigned" 
        : $"{AssignedSchedules.Count} schedule(s) assigned";

    public FolderMappingViewModel(FoldersViewModel parent)
    {
        _parent          = parent;
        _name            = string.Empty;
        _sourcePath      = string.Empty;
        _destinationPath = string.Empty;

        BrowseSourceCommand = new RelayCommand(_ =>
        {
            var dialog = new OpenFolderDialog { Title = "Select Source Folder" };
            if (!string.IsNullOrEmpty(SourcePath)) dialog.InitialDirectory = SourcePath;
            if (dialog.ShowDialog() == true) SourcePath = dialog.FolderName;
        });

        BrowseDestinationCommand = new RelayCommand(_ =>
        {
            var dialog = new OpenFolderDialog { Title = "Select Destination Folder" };
            if (!string.IsNullOrEmpty(DestinationPath)) dialog.InitialDirectory = DestinationPath;
            if (dialog.ShowDialog() == true) DestinationPath = dialog.FolderName;
        });

        RemoveCommand    = new RelayCommand(_ => parent.CustomFolders.Remove(this));
        AddFilterCommand = new RelayCommand(_ =>
        {
            // Auto-set FilterMode to Blacklist if this is the first filter and FilterMode is None
            if (Filters.Count == 0 && FilterMode == FilterMode.None)
            {
                FilterMode = FilterMode.Blacklist;
            }

            // Insert new filter at the top (index 0) instead of adding to the end
            Filters.Insert(0, new FilterEntryViewModel(this)
            {
                Pattern     = "*",
                Description = "New filter",
                Mode        = FilterMode != FilterMode.None ? FilterMode : FilterMode.Blacklist
            });
            FilteredFiltersView?.Refresh();
        });

        ClearAllFiltersCommand = new RelayCommand(_ =>
        {
            if (Filters.Count == 0)
                return;

            bool confirmed = DialogService.Confirm(
                $"Are you sure you want to remove all {Filters.Count} filter(s)?",
                "Clear All Filters",
                Dialogs.FluentDialogIcon.Warning,
                yesLabel: "Clear All",
                noLabel: "Cancel");

            if (confirmed)
            {
                Filters.Clear();
                FilteredFiltersView?.Refresh();
                DialogService.ShowSuccess($"All filters have been cleared.", "Filters Cleared");
            }
        });

        ManageSchedulesCommand = new RelayCommand(_ => ManageSchedules());

        FilteredFiltersView = System.Windows.Data.CollectionViewSource.GetDefaultView(Filters);
        FilteredFiltersView.Filter = item => ((FilterEntryViewModel)item).Mode == FilterMode;

        // Wire schedule collection changes
        AssignedSchedules.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SchedulesSummary));
    }

    private void ManageSchedules()
    {
        var dialog = Dialogs.ScheduleAssignmentDialog.Create(
            Name,
            _parent.Config,
            _parent.ConfigService,
            _parent.TaskScheduler,
            AssignedSchedules.ToList()
        );
        dialog.Owner = System.Windows.Application.Current.MainWindow;

        if (dialog.ShowDialog() == true)
        {
            // Update the assigned schedules
            AssignedSchedules.Clear();
            foreach (var scheduleName in dialog.SelectedScheduleNames)
            {
                AssignedSchedules.Add(scheduleName);
            }

            // Mark as dirty since schedules changed
            _parent.MarkDirtyPublic();
        }
    }
}

public class FoldersViewModel : ViewModelBase
{
    private readonly BackupConfig _config;
    private readonly ConfigurationService _configService;
    private readonly TaskSchedulerService _taskScheduler;

    // ── Dirty tracking ────────────────────────────────────────────
    private bool _isDirty;
    private bool _suppressDirty = true; // suppress during initial load

    public BackupConfig Config => _config;
    public ConfigurationService ConfigService => _configService;
    public TaskSchedulerService TaskScheduler => _taskScheduler;

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            SetProperty(ref _isDirty, value);
            OnPropertyChanged(nameof(DirtyIndicator));
        }
    }

    /// <summary>Small label shown in the view header when there are unsaved changes.</summary>
    public string DirtyIndicator => IsDirty ? "● Unsaved changes" : string.Empty;

    public ObservableCollection<FolderMappingViewModel> CustomFolders { get; } = new();

    public ICommand AddFolderCommand { get; }
    public ICommand SaveCommand      { get; }

    public FoldersViewModel(BackupConfig config, ConfigurationService configService)
    {
        _config        = config;
        _configService = configService;
        _taskScheduler = new TaskSchedulerService(config.TaskName);

        if (_config.FolderMappings != null)
        {
            foreach (var mapping in _config.FolderMappings)
            {
                var vm = new FolderMappingViewModel(this)
                {
                    Name            = mapping.Name,
                    SourcePath      = mapping.SourcePath,
                    DestinationPath = mapping.DestinationPath,
                    FilterMode      = mapping.FilterMode
                };
                foreach (var filter in mapping.Filters ?? Enumerable.Empty<FilterEntry>())
                {
                    vm.Filters.Add(new FilterEntryViewModel(vm)
                    {
                        Pattern     = filter.Pattern,
                        Description = filter.Description,
                        Mode        = filter.Mode
                    });
                }
                foreach (var schedule in mapping.AssignedSchedules ?? Enumerable.Empty<string>())
                {
                    vm.AssignedSchedules.Add(schedule);
                }
                CustomFolders.Add(vm);
            }
        }

        AddFolderCommand = new RelayCommand(_ =>
        {
            CustomFolders.Add(new FolderMappingViewModel(this));
            WireFolder(CustomFolders[^1]);
            MarkDirty();
        });

        SaveCommand = new RelayCommand(_ => SaveAndMarkClean(showFeedback: true));

        // Wire dirty tracking for existing folders
        foreach (var folder in CustomFolders)
            WireFolder(folder);

        // Monitor the top-level collection for add/remove
        CustomFolders.CollectionChanged += OnCollectionChanged;

        // Enable dirty tracking now that load is done
        _suppressDirty = false;
    }

    // ── Public API used by MainWindow ─────────────────────────────

    /// <summary>
    /// Persists the current state. Optionally shows a success toast.
    /// Returns false if the save threw an exception.
    /// </summary>
    public bool SaveAndMarkClean(bool showFeedback = false)
    {
        _config.FolderMappings = CustomFolders.Select(f => new FolderMapping
        {
            Name            = f.Name,
            SourcePath      = f.SourcePath,
            DestinationPath = f.DestinationPath,
            FilterMode      = f.FilterMode,
            AssignedSchedules = f.AssignedSchedules.ToList(),
            Filters = f.Filters
                .Select(filter => new FilterEntry
                {
                    Pattern     = filter.Pattern,
                    Description = filter.Description,
                    Mode        = filter.Mode
                })
                .ToList()
        }).ToList();

        try
        {
            _configService.SaveConfiguration(_config);
            IsDirty = false;

            if (showFeedback)
                DialogService.ShowSuccess("Configuration saved successfully!", "Saved");

            return true;
        }
        catch (Exception ex)
        {
            DialogService.ShowError($"Failed to save configuration: {ex.Message}", "Save Failed");
            return false;
        }
    }

    // ── Dirty tracking helpers ─────────────────────────────────────

    private void MarkDirty()
    {
        if (!_suppressDirty)
            IsDirty = true;
    }

    public void MarkDirtyPublic() => MarkDirty();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (FolderMappingViewModel vm in e.NewItems)
                WireFolder(vm);

        if (e.OldItems != null)
            foreach (FolderMappingViewModel vm in e.OldItems)
                UnwireFolder(vm);

        MarkDirty();
    }

    private void WireFolder(FolderMappingViewModel vm)
    {
        vm.PropertyChanged += OnFolderPropertyChanged;
        vm.Filters.CollectionChanged += OnFiltersCollectionChanged;
        foreach (var f in vm.Filters)
            f.PropertyChanged += OnFilterPropertyChanged;
    }

    private void UnwireFolder(FolderMappingViewModel vm)
    {
        vm.PropertyChanged -= OnFolderPropertyChanged;
        vm.Filters.CollectionChanged -= OnFiltersCollectionChanged;
        foreach (var f in vm.Filters)
            f.PropertyChanged -= OnFilterPropertyChanged;
    }

    private void OnFolderPropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();

    private void OnFiltersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (FilterEntryViewModel f in e.NewItems)
                f.PropertyChanged += OnFilterPropertyChanged;
        if (e.OldItems != null)
            foreach (FilterEntryViewModel f in e.OldItems)
                f.PropertyChanged -= OnFilterPropertyChanged;
        MarkDirty();
    }

    private void OnFilterPropertyChanged(object? sender, PropertyChangedEventArgs e) => MarkDirty();
}

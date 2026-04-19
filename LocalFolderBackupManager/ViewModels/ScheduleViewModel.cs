using LocalFolderBackupManager.Dialogs;
using LocalFolderBackupManager.Models;
using LocalFolderBackupManager.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace LocalFolderBackupManager.ViewModels;

public class ScheduleEntryViewModel : ViewModelBase
{
    private readonly ScheduleEntry _entry;
    private readonly BackupConfig _config;
    private string _liveStatus = "Checking...";
    private bool   _isStale;
    private string _exePath = string.Empty;

    public ScheduleEntryViewModel(ScheduleEntry entry, BackupConfig config)
    {
        _entry = entry;
        _config = config;
    }

    public ScheduleEntry Entry => _entry;

    public string TaskName        => _entry.TaskName;
    public string Description     => _entry.Description;
    public string TriggerSummary  => _entry.TriggerSummary;
    public bool   IsEnabled       => _entry.IsEnabled;

    public string LiveStatus
    {
        get => _liveStatus;
        set
        {
            SetProperty(ref _liveStatus, value);
            OnPropertyChanged(nameof(StatusColour));
        }
    }

    /// <summary>True when the task's exe path no longer exists at the recorded location.</summary>
    public bool IsStale
    {
        get => _isStale;
        set => SetProperty(ref _isStale, value);
    }

    /// <summary>The exe path the task currently points at in Task Scheduler.</summary>
    public string ExePath
    {
        get => _exePath;
        set => SetProperty(ref _exePath, value);
    }

    // Icon based on trigger type
    public string TriggerIcon => _entry.TriggerType switch
    {
        ScheduleTriggerType.AtLogon => "🔓",
        ScheduleTriggerType.Daily   => "📅",
        ScheduleTriggerType.Weekly  => "🗓️",
        ScheduleTriggerType.Hourly  => "⏱️",
        _ => "📋"
    };

    public bool HasDescription => !string.IsNullOrWhiteSpace(_entry.Description);

    // Badge colour for status chip
    public string StatusColour => LiveStatus switch
    {
        "Ready"    => "#2ECC71",
        "Running"  => "#3498DB",
        "Disabled" => "#E67E22",
        _ => "#95A5A6"
    };

    // Assigned folders
    public List<string> AssignedFolderNames
    {
        get
        {
            return _config.FolderMappings
                .Where(f => f.AssignedSchedules.Contains(_entry.TaskName, StringComparer.OrdinalIgnoreCase))
                .Select(f => string.IsNullOrWhiteSpace(f.Name) ? f.SourcePath : f.Name)
                .ToList();
        }
    }

    public bool HasAssignedFolders => AssignedFolderNames.Count > 0;

    public void RefreshAssignedFolders()
    {
        OnPropertyChanged(nameof(AssignedFolderNames));
        OnPropertyChanged(nameof(HasAssignedFolders));
    }
}

public class ScheduleViewModel : ViewModelBase
{
    private readonly BackupConfig _config;
    private readonly ConfigurationService _configService;
    private readonly TaskSchedulerService _taskScheduler;

    // ── Creation form ─────────────────────────────────────────────
    private string _newTaskName      = string.Empty;
    private string _newDescription   = string.Empty;
    private ScheduleTriggerType _newTriggerType = ScheduleTriggerType.AtLogon;
    private string _newTimeOfDay     = "08:00";
    private DayOfWeek _newDayOfWeek  = DayOfWeek.Monday;
    private int    _newIntervalHours = 1;

    private bool   _isBusy;
    private string _busyMessage  = string.Empty;
    private string _folderPath   = string.Empty;
    private bool   _hasStale;

    public ScheduleViewModel(BackupConfig config, ConfigurationService configService, TaskSchedulerService taskScheduler)
    {
        _config        = config;
        _configService = configService;
        _taskScheduler = taskScheduler;

        // Seed from saved config
        Schedules = new ObservableCollection<ScheduleEntryViewModel>(
            _config.ScheduledTasks.Select(e => new ScheduleEntryViewModel(e, _config)));

        Schedules.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoSchedules));
            OnPropertyChanged(nameof(HasSchedules));
        };

        AddScheduleCommand        = new RelayCommand(_ => AddSchedule(),            _ => CanAdd());
        DeleteScheduleCommand     = new RelayCommand(o => DeleteSchedule(o as ScheduleEntryViewModel));
        EditScheduleCommand       = new RelayCommand(o => EditSchedule(o as ScheduleEntryViewModel));
        RefreshCommand            = new RelayCommand(_ => SyncAndRefresh());
        UpdatePathCommand         = new RelayCommand(o => UpdatePath(o as ScheduleEntryViewModel));

        FolderPath = $@"Task Scheduler\Task Scheduler Library\{TaskSchedulerService.AppFolderName}";

        // Sync against the live Task Scheduler folder
        SyncAndRefresh();
    }

    // ── Collections ───────────────────────────────────────────────

    public ObservableCollection<ScheduleEntryViewModel> Schedules { get; }

    public bool HasNoSchedules => Schedules.Count == 0;
    public bool HasSchedules   => Schedules.Count > 0;

    // Trigger type options for ComboBox
    public IEnumerable<ScheduleTriggerType> TriggerTypes   => Enum.GetValues<ScheduleTriggerType>();
    public IEnumerable<DayOfWeek>           DaysOfWeek     => Enum.GetValues<DayOfWeek>();
    public IEnumerable<int>                 HourIntervals  => Enumerable.Range(1, 12);

    // ── Commands ──────────────────────────────────────────────────

    public ICommand AddScheduleCommand    { get; }
    public ICommand DeleteScheduleCommand { get; }
    public ICommand EditScheduleCommand   { get; }
    public ICommand RefreshCommand        { get; }
    public ICommand UpdatePathCommand     { get; }

    // ── Status bar ────────────────────────────────────────────────

    /// <summary>Friendly path shown in the UI so users know where to look in Task Scheduler.</summary>
    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    /// <summary>True when at least one task has a stale (missing) exe path.</summary>
    public bool HasStale
    {
        get => _hasStale;
        set => SetProperty(ref _hasStale, value);
    }

    // ── Form properties ───────────────────────────────────────────

    public string NewTaskName
    {
        get => _newTaskName;
        set { SetProperty(ref _newTaskName, value); OnPropertyChanged(nameof(IsFormValid)); }
    }

    public string NewDescription
    {
        get => _newDescription;
        set => SetProperty(ref _newDescription, value);
    }

    public ScheduleTriggerType NewTriggerType
    {
        get => _newTriggerType;
        set
        {
            SetProperty(ref _newTriggerType, value);
            OnPropertyChanged(nameof(ShowTimePicker));
            OnPropertyChanged(nameof(ShowDayPicker));
            OnPropertyChanged(nameof(ShowIntervalPicker));
        }
    }

    public string NewTimeOfDay
    {
        get => _newTimeOfDay;
        set => SetProperty(ref _newTimeOfDay, value);
    }

    public DayOfWeek NewDayOfWeek
    {
        get => _newDayOfWeek;
        set => SetProperty(ref _newDayOfWeek, value);
    }

    public int NewIntervalHours
    {
        get => _newIntervalHours;
        set => SetProperty(ref _newIntervalHours, value);
    }

    // Conditional form-section visibility
    public bool ShowTimePicker     => NewTriggerType is ScheduleTriggerType.Daily or ScheduleTriggerType.Weekly;
    public bool ShowDayPicker      => NewTriggerType == ScheduleTriggerType.Weekly;
    public bool ShowIntervalPicker => NewTriggerType == ScheduleTriggerType.Hourly;

    public bool IsFormValid =>
        !string.IsNullOrWhiteSpace(NewTaskName) &&
        !_config.ScheduledTasks.Any(t => t.TaskName.Equals(NewTaskName.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string BusyMessage
    {
        get => _busyMessage;
        set => SetProperty(ref _busyMessage, value);
    }

    // ── Logic ─────────────────────────────────────────────────────

    private bool CanAdd() => IsFormValid;

    private void AddSchedule()
    {
        var entry = new ScheduleEntry
        {
            TaskName      = NewTaskName.Trim(),
            Description   = NewDescription.Trim(),
            TriggerType   = NewTriggerType,
            TimeOfDay     = NewTimeOfDay,
            DayOfWeek     = NewDayOfWeek,
            IntervalHours = NewIntervalHours,
            IsEnabled     = true
        };

        try
        {
            IsBusy      = true;
            BusyMessage = $"Creating task '{entry.TaskName}'…";

            _taskScheduler.CreateOrUpdateScheduledTask(entry);

            _config.ScheduledTasks.Add(entry);
            _configService.SaveConfiguration(_config);

            var vm = new ScheduleEntryViewModel(entry, _config);
            RefreshVm(vm);
            Schedules.Add(vm);

            // Reset form
            NewTaskName      = string.Empty;
            NewDescription   = string.Empty;
            NewTriggerType   = ScheduleTriggerType.AtLogon;
            NewTimeOfDay     = "08:00";
            NewDayOfWeek     = DayOfWeek.Monday;
            NewIntervalHours = 1;

            DialogService.ShowSuccess(
                $"Task '{entry.TaskName}' created successfully in:\n{FolderPath}\n\nTrigger: {entry.TriggerSummary}",
                "Schedule Created");
        }
        catch (Exception ex)
        {
            DialogService.ShowError(
                $"Failed to create task:\n{ex.Message}\n\nMake sure the application is running as administrator.",
                "Create Failed");
        }
        finally
        {
            IsBusy   = false;
            HasStale = Schedules.Any(s => s.IsStale);
        }
    }

    private void DeleteSchedule(ScheduleEntryViewModel? vm)
    {
        if (vm == null) return;

        bool confirmed = DialogService.Confirm(
            $"Delete scheduled task '{vm.TaskName}'?\n\nThis will also remove it from Windows Task Scheduler.",
            "Confirm Delete",
            FluentDialogIcon.Warning,
            yesLabel: "Delete",
            noLabel: "Cancel");

        if (!confirmed) return;

        try
        {
            _taskScheduler.DeleteScheduledTask(vm.TaskName);

            var entry = _config.ScheduledTasks.FirstOrDefault(e => e.TaskName == vm.TaskName);
            if (entry != null) _config.ScheduledTasks.Remove(entry);

            _configService.SaveConfiguration(_config);
            Schedules.Remove(vm);
            HasStale = Schedules.Any(s => s.IsStale);
        }
        catch (Exception ex)
        {
            DialogService.ShowError($"Failed to delete task:\n{ex.Message}", "Delete Failed");
        }
    }

    private void EditSchedule(ScheduleEntryViewModel? vm)
    {
        if (vm == null) return;

        var dialog = ScheduleEditDialog.Create(vm.Entry, _config, _configService, _taskScheduler);
        bool? result = dialog.ShowDialog();

        if (result == true)
        {
            // Refresh the schedule list to reflect changes
            SyncAndRefresh();

            // Notify that assigned folders might have changed
            foreach (var schedule in Schedules)
            {
                schedule.RefreshAssignedFolders();
            }
        }
    }

    /// <summary>
    /// Scans the Task Scheduler app folder and merges any tasks found there
    /// that are not yet in the config, then refreshes live status for all.
    /// </summary>
    private void SyncAndRefresh()
    {
        // Discover tasks currently in the Task Scheduler folder
        var discovered = _taskScheduler.GetAllAppTasks();

        foreach (var dt in discovered)
        {
            // If not already in the config list, add a placeholder entry
            if (!_config.ScheduledTasks.Any(e => e.TaskName.Equals(dt.TaskName, StringComparison.OrdinalIgnoreCase)))
            {
                var recovered = new ScheduleEntry
                {
                    TaskName    = dt.TaskName,
                    Description = "(Discovered from Task Scheduler)",
                    TriggerType = ScheduleTriggerType.AtLogon, // best guess
                    IsEnabled   = dt.State != "Disabled"
                };
                _config.ScheduledTasks.Add(recovered);
                _configService.SaveConfiguration(_config);

                var vm = new ScheduleEntryViewModel(recovered, _config);
                RefreshVm(vm, dt);
                Schedules.Add(vm);
            }
            else
            {
                // Refresh the existing VM with live data
                var vm = Schedules.FirstOrDefault(s => s.TaskName.Equals(dt.TaskName, StringComparison.OrdinalIgnoreCase));
                if (vm != null) RefreshVm(vm, dt);
            }
        }

        // Refresh state for all VMs (covers entries in config that may not be in folder yet)
        foreach (var vm in Schedules.Where(s => s.LiveStatus == "Checking..." || discovered.All(d => d.TaskName != s.TaskName)))
            vm.LiveStatus = _taskScheduler.GetScheduledTaskState(vm.TaskName);

        HasStale = Schedules.Any(s => s.IsStale);
    }

    /// <summary>Re-registers the task with the current exe path so the action points to the right place.</summary>
    private void UpdatePath(ScheduleEntryViewModel? vm)
    {
        if (vm == null) return;

        try
        {
            IsBusy      = true;
            BusyMessage = $"Updating path for '{vm.TaskName}'…";

            _taskScheduler.UpdateTaskExePath(vm.TaskName);
            RefreshVm(vm);

            DialogService.ShowSuccess(
                $"Task '{vm.TaskName}' updated to point to the current application location.",
                "Path Updated");
        }
        catch (Exception ex)
        {
            DialogService.ShowError($"Failed to update path:\n{ex.Message}", "Update Failed");
        }
        finally
        {
            IsBusy   = false;
            HasStale = Schedules.Any(s => s.IsStale);
        }
    }

    // ── Private refresh helpers ────────────────────────────────────

    private void RefreshVm(ScheduleEntryViewModel vm, DiscoveredTask? dt = null)
    {
        vm.LiveStatus = dt?.State ?? _taskScheduler.GetScheduledTaskState(vm.TaskName);
        vm.IsStale    = dt?.IsStale ?? false;
        vm.ExePath    = dt?.ExePath ?? string.Empty;
    }
}

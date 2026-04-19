using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LocalFolderBackupManager.Models;
using LocalFolderBackupManager.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LocalFolderBackupManager.Dialogs;

public class ScheduleSelectionItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public string TaskName { get; set; } = string.Empty;
    public string TriggerSummary { get; set; } = string.Empty;
    public string Icon { get; set; } = "📋";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public partial class ScheduleAssignmentDialog : FluentWindow
{
    private readonly ObservableCollection<ScheduleSelectionItem> _schedules = new();
    private BackupConfig _config;
    private ConfigurationService _configService;
    private TaskSchedulerService _taskScheduler;

    public List<string> SelectedScheduleNames { get; private set; } = new();
    public bool ShouldOpenSchedulePage { get; private set; } = false;

    public ScheduleAssignmentDialog()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();

        _config = new BackupConfig();
        _configService = new ConfigurationService();
        _taskScheduler = new TaskSchedulerService("Save_Game_Backup");
    }

    public static ScheduleAssignmentDialog Create(
        string folderName, 
        BackupConfig config,
        ConfigurationService configService,
        TaskSchedulerService taskScheduler,
        List<string> currentlyAssigned)
    {
        var dialog = new ScheduleAssignmentDialog
        {
            _config = config,
            _configService = configService,
            _taskScheduler = taskScheduler
        };
        dialog.LoadSchedules(folderName, config, currentlyAssigned);
        dialog.InitializeForm();
        return dialog;
    }

    private void InitializeForm()
    {
        // Populate Trigger Type combo
        TriggerTypeCombo.ItemsSource = Enum.GetValues<ScheduleTriggerType>();
        TriggerTypeCombo.SelectedIndex = 0;

        // Populate Day of Week combo
        DayOfWeekCombo.ItemsSource = Enum.GetValues<DayOfWeek>();
        DayOfWeekCombo.SelectedIndex = 0;

        // Populate Hour Intervals combo (1-12 hours)
        IntervalCombo.ItemsSource = Enumerable.Range(1, 12);
        IntervalCombo.SelectedIndex = 0;
    }

    private void LoadSchedules(string folderName, BackupConfig config, List<string> currentlyAssigned)
    {
        FolderNameText.Text = string.IsNullOrWhiteSpace(folderName) ? "Unnamed Backup Rule" : folderName;

        RefreshSchedulesList(currentlyAssigned);
    }

    private void RefreshSchedulesList(List<string> currentlyAssigned)
    {
        _schedules.Clear();

        if (_config.ScheduledTasks == null || _config.ScheduledTasks.Count == 0)
        {
            NoSchedulesMessage.Visibility = Visibility.Visible;
            UpdateSelectionInfo();
            return;
        }

        NoSchedulesMessage.Visibility = Visibility.Collapsed;

        foreach (var schedule in _config.ScheduledTasks)
        {
            var icon = schedule.TriggerType switch
            {
                ScheduleTriggerType.AtLogon => "🔓",
                ScheduleTriggerType.Daily => "📅",
                ScheduleTriggerType.Weekly => "🗓️",
                ScheduleTriggerType.Hourly => "⏱️",
                _ => "📋"
            };

            var item = new ScheduleSelectionItem
            {
                TaskName = schedule.TaskName,
                TriggerSummary = schedule.TriggerSummary,
                Icon = icon,
                IsSelected = currentlyAssigned.Contains(schedule.TaskName)
            };

            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScheduleSelectionItem.IsSelected))
                    UpdateSelectionInfo();
            };

            _schedules.Add(item);
        }

        SchedulesListBox.ItemsSource = _schedules;
        UpdateSelectionInfo();
    }

    private void UpdateSelectionInfo()
    {
        var selectedCount = _schedules.Count(s => s.IsSelected);
        var totalCount = _schedules.Count;

        if (totalCount == 0)
        {
            SelectionInfoText.Text = "No schedules available.";
        }
        else
        {
            SelectionInfoText.Text = $"{selectedCount} of {totalCount} schedules selected";
        }
    }

    private void TriggerTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TriggerTypeCombo.SelectedItem is not ScheduleTriggerType triggerType)
            return;

        // Show/hide conditional fields based on trigger type
        TimePicker.Visibility = triggerType is ScheduleTriggerType.Daily or ScheduleTriggerType.Weekly
            ? Visibility.Visible
            : Visibility.Collapsed;

        DayPicker.Visibility = triggerType == ScheduleTriggerType.Weekly
            ? Visibility.Visible
            : Visibility.Collapsed;

        IntervalPicker.Visibility = triggerType == ScheduleTriggerType.Hourly
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void CreateNewScheduleButton_Click(object sender, RoutedEventArgs e)
    {
        var taskName = NewTaskNameBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(taskName))
        {
            Services.DialogService.ShowWarning("Please enter a task name.", "Validation Error");
            return;
        }

        // Ensure ScheduledTasks list is initialized
        if (_config.ScheduledTasks == null)
        {
            _config.ScheduledTasks = new System.Collections.Generic.List<ScheduleEntry>();
        }

        // Check for duplicate names
        if (_config.ScheduledTasks.Any(t => t.TaskName.Equals(taskName, StringComparison.OrdinalIgnoreCase)))
        {
            Services.DialogService.ShowWarning("A schedule with this name already exists.", "Duplicate Name");
            return;
        }

        var entry = new ScheduleEntry
        {
            TaskName = taskName,
            Description = DescriptionBox.Text?.Trim() ?? string.Empty,
            TriggerType = (ScheduleTriggerType)TriggerTypeCombo.SelectedItem,
            TimeOfDay = TimeOfDayBox.Text?.Trim() ?? "08:00",
            DayOfWeek = (DayOfWeek)(DayOfWeekCombo.SelectedItem ?? DayOfWeek.Monday),
            IntervalHours = (int)(IntervalCombo.SelectedItem ?? 1),
            IsEnabled = true
        };

        try
        {
            // Validate the task scheduler service
            if (_taskScheduler == null)
            {
                Services.DialogService.ShowError("Task scheduler service is not initialized.", "Error");
                return;
            }

            // Create the scheduled task
            _taskScheduler.CreateOrUpdateScheduledTask(entry);

            // Add to config
            _config.ScheduledTasks.Add(entry);
            _configService.SaveConfiguration(_config);

            // Refresh the list
            var currentlySelected = _schedules.Where(s => s.IsSelected).Select(s => s.TaskName).ToList();
            currentlySelected.Add(taskName); // Auto-select the newly created schedule
            RefreshSchedulesList(currentlySelected);

            // Clear the form
            NewTaskNameBox.Text = string.Empty;
            DescriptionBox.Text = string.Empty;
            TimeOfDayBox.Text = "08:00";
            TriggerTypeCombo.SelectedIndex = 0;
            CreateScheduleExpander.IsExpanded = false;

            Services.DialogService.ShowSuccess($"Schedule '{taskName}' created successfully!", "Success");
        }
        catch (UnauthorizedAccessException)
        {
            Services.DialogService.ShowError("Access denied. Please ensure the application is running with administrator privileges.", "Permission Error");
        }
        catch (Exception ex)
        {
            Services.DialogService.ShowError($"Failed to create schedule: {ex.Message}\n\nDetails: {ex.ToString()}", "Error");
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedScheduleNames = _schedules
            .Where(s => s.IsSelected)
            .Select(s => s.TaskName)
            .ToList();

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

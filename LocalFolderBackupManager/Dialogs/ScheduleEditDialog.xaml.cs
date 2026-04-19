using System.Windows;
using System.Windows.Controls;
using LocalFolderBackupManager.Models;
using LocalFolderBackupManager.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LocalFolderBackupManager.Dialogs;

public partial class ScheduleEditDialog : FluentWindow
{
    private ScheduleEntry _entry;
    private BackupConfig _config;
    private ConfigurationService _configService;
    private TaskSchedulerService _taskScheduler;

    public ScheduleEditDialog()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();

        _entry = new ScheduleEntry();
        _config = new BackupConfig();
        _configService = new ConfigurationService();
        _taskScheduler = new TaskSchedulerService("Save_Game_Backup");
    }

    public static ScheduleEditDialog Create(
        ScheduleEntry entry,
        BackupConfig config,
        ConfigurationService configService,
        TaskSchedulerService taskScheduler)
    {
        var dialog = new ScheduleEditDialog
        {
            _entry = entry,
            _config = config,
            _configService = configService,
            _taskScheduler = taskScheduler
        };

        dialog.LoadSchedule();
        return dialog;
    }

    private void LoadSchedule()
    {
        HeaderText.Text = $"Edit Schedule: {_entry.TaskName}";
        TaskNameBox.Text = _entry.TaskName;
        DescriptionBox.Text = _entry.Description;

        // Populate combo boxes
        TriggerTypeCombo.ItemsSource = Enum.GetValues<ScheduleTriggerType>();
        TriggerTypeCombo.SelectedItem = _entry.TriggerType;

        DayOfWeekCombo.ItemsSource = Enum.GetValues<DayOfWeek>();
        DayOfWeekCombo.SelectedItem = _entry.DayOfWeek;

        IntervalCombo.ItemsSource = Enumerable.Range(1, 12);
        IntervalCombo.SelectedItem = _entry.IntervalHours;

        TimeOfDayBox.Text = _entry.TimeOfDay;

        // Show/hide fields based on trigger type
        UpdateFieldVisibility();
    }

    private void TriggerTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFieldVisibility();
    }

    private void UpdateFieldVisibility()
    {
        if (TriggerTypeCombo.SelectedItem is not ScheduleTriggerType triggerType)
            return;

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

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Update the entry
            _entry.Description = DescriptionBox.Text?.Trim() ?? string.Empty;
            _entry.TriggerType = (ScheduleTriggerType)TriggerTypeCombo.SelectedItem;
            _entry.TimeOfDay = TimeOfDayBox.Text?.Trim() ?? "08:00";
            _entry.DayOfWeek = (DayOfWeek)(DayOfWeekCombo.SelectedItem ?? DayOfWeek.Monday);
            _entry.IntervalHours = (int)(IntervalCombo.SelectedItem ?? 1);

            // Update in Task Scheduler
            _taskScheduler.CreateOrUpdateScheduledTask(_entry);

            // Save configuration
            _configService.SaveConfiguration(_config);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            DialogService.ShowError($"Failed to update schedule: {ex.Message}", "Error");
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

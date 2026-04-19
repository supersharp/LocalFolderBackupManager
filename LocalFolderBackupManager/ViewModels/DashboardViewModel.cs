using LocalFolderBackupManager.Models;
using LocalFolderBackupManager.Services;
using System.Windows;
using System.Windows.Input;

namespace LocalFolderBackupManager.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly BackupConfig _config;
    private readonly BackupService _backupService;
    private readonly TaskSchedulerService _taskScheduler;
    private readonly ConfigurationService _configService;

    private string _lastBackupDate = "Never";
    private string _backupSize = "Calculating...";
    private string _taskStatus = "Checking...";
    private bool _isBackingUp;
    private int _backupProgress;
    private string _backupStatusMessage = "Ready";

    public DashboardViewModel(BackupConfig config, BackupService backupService,
        TaskSchedulerService taskScheduler, ConfigurationService configService)
    {
        _config = config;
        _backupService = backupService;
        _taskScheduler = taskScheduler;
        _configService = configService;

        BackupNowCommand = new RelayCommand(_ => BackupNow(), _ => !IsBackingUp);
        RestoreCommand   = new RelayCommand(_ => Restore(),   _ => !IsBackingUp);
        RefreshCommand   = new RelayCommand(_ => RefreshStats());

        RefreshStats();
    }

    public string LastBackupDate
    {
        get => _lastBackupDate;
        set => SetProperty(ref _lastBackupDate, value);
    }

    public string BackupSize
    {
        get => _backupSize;
        set => SetProperty(ref _backupSize, value);
    }

    public string TaskStatus
    {
        get => _taskStatus;
        set => SetProperty(ref _taskStatus, value);
    }

    public bool IsBackingUp
    {
        get => _isBackingUp;
        set => SetProperty(ref _isBackingUp, value);
    }

    public int BackupProgress
    {
        get => _backupProgress;
        set => SetProperty(ref _backupProgress, value);
    }

    public string BackupStatusMessage
    {
        get => _backupStatusMessage;
        set => SetProperty(ref _backupStatusMessage, value);
    }

    public ICommand BackupNowCommand { get; }
    public ICommand RestoreCommand   { get; }
    public ICommand RefreshCommand   { get; }

    private async void BackupNow()
    {
        IsBackingUp = true;
        BackupProgress = 0;
        BackupStatusMessage = "Starting backup...";

        var progress = new Progress<BackupProgress>(p =>
        {
            BackupProgress = p.Percentage;
            BackupStatusMessage = p.Message;
        });

        var result = await _backupService.PerformBackupAsync(progress);

        if (result.Success)
        {
            BackupStatusMessage = $"Backup completed successfully in {result.Duration.TotalSeconds:F1} seconds!";

            var message = $"Folders backed up: {result.FoldersBackedUp}\nDuration: {result.Duration.TotalSeconds:F1} seconds";

            if (result.Warnings.Any())
            {
                message += $"\n\n⚠️ Warnings ({result.Warnings.Count}):\n" + string.Join("\n", result.Warnings);
                DialogService.ShowWarning(message, "Backup Complete with Warnings");
            }
            else
            {
                DialogService.ShowSuccess(message, "Backup Complete");
            }
        }
        else
        {
            BackupStatusMessage = "Backup failed!";
            DialogService.ShowError(result.ErrorMessage ?? "An unknown error occurred.", "Backup Failed");
        }

        IsBackingUp = false;
        RefreshStats();
    }

    private async void Restore()
    {
        bool confirmed = DialogService.Confirm(
            "WARNING: This will restore files from the backup and OVERWRITE any existing files that are different.\n\nAre you sure you want to proceed?",
            "Confirm Restore",
            Dialogs.FluentDialogIcon.Warning,
            yesLabel: "Yes, Restore",
            noLabel: "Cancel");

        if (!confirmed) return;

        IsBackingUp = true;
        BackupProgress = 0;
        BackupStatusMessage = "Starting restore...";

        var progress = new Progress<BackupProgress>(p =>
        {
            BackupProgress = p.Percentage;
            BackupStatusMessage = p.Message;
        });

        var restoreResult = await _backupService.PerformRestoreAsync(progress);

        if (restoreResult.Success)
        {
            BackupStatusMessage = "Restore completed successfully!";
            DialogService.ShowSuccess(
                $"Duration: {restoreResult.Duration.TotalSeconds:F1} seconds",
                "Restore Complete");
        }
        else
        {
            BackupStatusMessage = "Restore failed!";
            DialogService.ShowError(restoreResult.ErrorMessage ?? "An unknown error occurred.", "Restore Failed");
        }

        IsBackingUp = false;
    }

    public void RefreshStats()
    {
        var lastBackup = _backupService.GetLastBackupDate();
        LastBackupDate = lastBackup?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never";

        Task.Run(() =>
        {
            var size = _backupService.GetBackupSize();
            Application.Current.Dispatcher.Invoke(() =>
            {
                BackupSize = FormatSize(size);
            });
        });

        TaskStatus = _taskScheduler.GetTaskStatus();
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}

using LocalFolderBackupManager.Models;
using Microsoft.Win32.TaskScheduler;
using System.Diagnostics;
using System.IO;

namespace LocalFolderBackupManager.Services;

public class TaskSchedulerService
{
    // All app tasks live under this subfolder in Task Scheduler.
    public const string AppFolderName = "LocalFolderBackupManager";

    // Full Task Scheduler path for a given task name: \LocalFolderBackupManager\<name>
    private static string TaskPath(string taskName) => $@"\{AppFolderName}\{taskName}";

    private readonly string _taskName;

    // Exe path is resolved fresh each call so it always reflects the current launch location.
    private static string CurrentExePath =>
        Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;

    public TaskSchedulerService(string taskName)
    {
        _taskName = taskName;
    }

    // ──────────────────────────────────────────────────────────────
    // App folder helpers
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the <see cref="AppFolderName"/> subfolder exists under the Task Scheduler root.
    /// Returns the folder (creating it if needed).
    /// </summary>
    private static TaskFolder EnsureAppFolder(TaskService ts)
    {
        try
        {
            return ts.GetFolder($@"\{AppFolderName}");
        }
        catch
        {
            // Folder does not exist – create it.
            try
            {
                return ts.RootFolder.CreateFolder(AppFolderName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to create Task Scheduler folder '{AppFolderName}'. " +
                    $"Ensure the application is running with administrator privileges. " +
                    $"Error: {ex.Message}", ex);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Discovery: enumerate all tasks in the app folder
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns lightweight descriptors for every task that currently exists
    /// with the LocalFolderBackupManager prefix in the Task Scheduler.
    /// </summary>
    public IReadOnlyList<DiscoveredTask> GetAllAppTasks()
    {
        var results = new List<DiscoveredTask>();

        try
        {
            using var ts = new TaskService();
            var prefix = "LocalFolderBackupManager_";

            foreach (var task in ts.RootFolder.Tasks)
            {
                // Only include tasks with our prefix
                if (!task.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var action = task.Definition.Actions
                    .OfType<ExecAction>()
                    .FirstOrDefault();

                // Remove the prefix for display
                var displayName = task.Name.Substring(prefix.Length);

                results.Add(new DiscoveredTask
                {
                    TaskName   = displayName,
                    State      = StateLabel(task.State),
                    ExePath    = action?.Path ?? string.Empty,
                    IsStale    = !string.IsNullOrEmpty(action?.Path) &&
                                 !File.Exists(action.Path)
                });
            }
        }
        catch { /* Task Scheduler not available – return empty */ }

        return results;
    }

    // ──────────────────────────────────────────────────────────────
    // Legacy single-task helpers (kept for Dashboard compatibility)
    // ──────────────────────────────────────────────────────────────

    public bool IsTaskCreated()
    {
        var fullTaskName = $"LocalFolderBackupManager_{_taskName}";
        using var ts = new TaskService();
        return ts.GetTask(fullTaskName) != null;
    }

    public string GetTaskStatus()
    {
        var fullTaskName = $"LocalFolderBackupManager_{_taskName}";
        using var ts = new TaskService();
        var task = ts.GetTask(fullTaskName);
        return task == null ? "Not Created" : StateLabel(task.State);
    }

    public void CreateOrUpdateTask()
    {
        var entry = new ScheduleEntry
        {
            TaskName    = _taskName,
            Description = "Automatically backs up save games at user logon",
            TriggerType = ScheduleTriggerType.AtLogon,
            IsEnabled   = true
        };
        CreateOrUpdateScheduledTask(entry);
    }

    public void DeleteTask()
    {
        var fullTaskName = $"LocalFolderBackupManager_{_taskName}";
        using var ts = new TaskService();
        try { ts.RootFolder.DeleteTask(fullTaskName); } catch { /* already gone */ }
    }

    public void RunTaskNow()
    {
        var fullTaskName = $"LocalFolderBackupManager_{_taskName}";
        using var ts = new TaskService();
        var task = ts.GetTask(fullTaskName);
        task?.Run();
    }

    // ──────────────────────────────────────────────────────────────
    // Multi-schedule API
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates or replaces a scheduled task from a <see cref="ScheduleEntry"/>.
    /// The task is registered under <c>\LocalFolderBackupManager\</c> and the
    /// action exe path is always taken from the current process location.
    /// </summary>
    public void CreateOrUpdateScheduledTask(ScheduleEntry entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        if (string.IsNullOrWhiteSpace(entry.TaskName))
            throw new ArgumentException("Task name cannot be empty", nameof(entry));

        var exePath = CurrentExePath;
        if (string.IsNullOrWhiteSpace(exePath))
            throw new InvalidOperationException("Could not determine current executable path");

        using var ts = new TaskService();

        // Use root folder instead of creating a subfolder to avoid permission issues
        var folder = ts.RootFolder;

        // Prefix the task name to avoid conflicts
        var fullTaskName = $"LocalFolderBackupManager_{entry.TaskName}";

        // Remove if exists (ignore errors – may not exist)
        try { folder.DeleteTask(fullTaskName); } catch { }

        var td = ts.NewTask();
        if (td?.RegistrationInfo == null || td.Principal == null || td.Settings == null || td.Triggers == null || td.Actions == null)
            throw new InvalidOperationException("Failed to create task definition - one or more components are null");

        td.RegistrationInfo.Description = string.IsNullOrWhiteSpace(entry.Description)
            ? "Local Folder Backup Manager scheduled backup"
            : entry.Description;
        td.Principal.RunLevel  = TaskRunLevel.Highest;
        td.Principal.LogonType = TaskLogonType.InteractiveToken;

        // Build trigger
        var userId = $"{Environment.UserDomainName ?? Environment.MachineName}\\{Environment.UserName ?? "User"}";
        Trigger trigger = entry.TriggerType switch
        {
            ScheduleTriggerType.AtLogon => new LogonTrigger
            {
                UserId = userId
            },
            ScheduleTriggerType.Daily  => BuildDailyTrigger(entry),
            ScheduleTriggerType.Weekly => BuildWeeklyTrigger(entry),
            ScheduleTriggerType.Hourly => BuildHourlyTrigger(entry),
            _ => new LogonTrigger { UserId = userId }
        };

        td.Triggers.Add(trigger);

        // Action always uses the current exe location
        // Pass schedule name so we only backup folders assigned to this schedule
        var arguments = $"--automated --schedule \"{entry.TaskName}\"";
        td.Actions.Add(new ExecAction(exePath, arguments, Path.GetDirectoryName(exePath) ?? string.Empty));

        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries     = false;
        td.Settings.ExecutionTimeLimit         = TimeSpan.FromHours(2);
        td.Settings.StartWhenAvailable         = true;
        td.Settings.Enabled                    = entry.IsEnabled;

        folder.RegisterTaskDefinition(fullTaskName, td);
    }

    /// <summary>Updates the action exe path of an existing task to match the current exe location.</summary>
    public void UpdateTaskExePath(string taskName)
    {
        var exePath = CurrentExePath;
        var fullTaskName = $"LocalFolderBackupManager_{taskName}";

        using var ts = new TaskService();
        var task = ts.GetTask(fullTaskName);
        if (task == null) return;

        var td = task.Definition;
        var action = td.Actions.OfType<ExecAction>().FirstOrDefault();
        if (action == null || action.Path == exePath) return;

        action.Path             = exePath;
        action.WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;

        ts.RootFolder.RegisterTaskDefinition(fullTaskName, td);
    }

    /// <summary>Deletes a named task from the app folder in Task Scheduler.</summary>
    public void DeleteScheduledTask(string taskName)
    {
        var fullTaskName = $"LocalFolderBackupManager_{taskName}";
        using var ts = new TaskService();
        try { ts.RootFolder.DeleteTask(fullTaskName); } catch { }
    }

    /// <summary>Returns the live state label of a named task in the app folder.</summary>
    public string GetScheduledTaskState(string taskName)
    {
        var fullTaskName = $"LocalFolderBackupManager_{taskName}";
        using var ts = new TaskService();
        var task = ts.GetTask(fullTaskName);
        return task == null ? "Not Created" : StateLabel(task.State);
    }

    /// <summary>Returns true if the named task exists in the app folder.</summary>
    public bool IsScheduledTaskCreated(string taskName)
    {
        var fullTaskName = $"LocalFolderBackupManager_{taskName}";
        using var ts = new TaskService();
        return ts.GetTask(fullTaskName) != null;
    }

    // ──────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────

    private static TaskFolder? GetAppFolderOrNull(TaskService ts)
    {
        try { return ts.GetFolder($@"\{AppFolderName}"); }
        catch { return null; }
    }

    private static string StateLabel(TaskState state) => state switch
    {
        TaskState.Ready    => "Ready",
        TaskState.Running  => "Running",
        TaskState.Disabled => "Disabled",
        TaskState.Queued   => "Queued",
        _ => state.ToString()
    };

    private static DailyTrigger BuildDailyTrigger(ScheduleEntry entry)
    {
        var (h, m) = ParseTime(entry.TimeOfDay);
        return new DailyTrigger
        {
            DaysInterval  = 1,
            StartBoundary = DateTime.Today.AddHours(h).AddMinutes(m)
        };
    }

    private static WeeklyTrigger BuildWeeklyTrigger(ScheduleEntry entry)
    {
        var (h, m) = ParseTime(entry.TimeOfDay);
        var days = entry.DayOfWeek switch
        {
            DayOfWeek.Monday    => DaysOfTheWeek.Monday,
            DayOfWeek.Tuesday   => DaysOfTheWeek.Tuesday,
            DayOfWeek.Wednesday => DaysOfTheWeek.Wednesday,
            DayOfWeek.Thursday  => DaysOfTheWeek.Thursday,
            DayOfWeek.Friday    => DaysOfTheWeek.Friday,
            DayOfWeek.Saturday  => DaysOfTheWeek.Saturday,
            DayOfWeek.Sunday    => DaysOfTheWeek.Sunday,
            _ => DaysOfTheWeek.Monday
        };
        return new WeeklyTrigger
        {
            DaysOfWeek    = days,
            WeeksInterval = 1,
            StartBoundary = DateTime.Today.AddHours(h).AddMinutes(m)
        };
    }

    private static TimeTrigger BuildHourlyTrigger(ScheduleEntry entry)
    {
        return new TimeTrigger
        {
            StartBoundary = DateTime.Now,
            Repetition    = new RepetitionPattern(TimeSpan.FromHours(entry.IntervalHours), TimeSpan.Zero)
        };
    }

    private static (int hour, int minute) ParseTime(string timeOfDay)
    {
        if (TimeSpan.TryParse(timeOfDay, out var ts))
            return ((int)ts.TotalHours, ts.Minutes);
        return (8, 0);
    }
}

/// <summary>Lightweight task info returned by <see cref="TaskSchedulerService.GetAllAppTasks"/>.</summary>
public class DiscoveredTask
{
    public string TaskName { get; init; } = string.Empty;
    public string State    { get; init; } = string.Empty;
    public string ExePath  { get; init; } = string.Empty;

    /// <summary>True when the task's action exe path no longer exists on disk.</summary>
    public bool IsStale    { get; init; }
}

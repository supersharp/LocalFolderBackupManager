using Newtonsoft.Json;

namespace LocalFolderBackupManager.Models;

public enum FilterMode
{
    None,
    Blacklist,
    Whitelist
}

public enum ScheduleTriggerType
{
    AtLogon,
    Daily,
    Weekly,
    Hourly
}

public class ScheduleEntry
{
    [JsonProperty("taskName")]
    public string TaskName { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("triggerType")]
    public ScheduleTriggerType TriggerType { get; set; } = ScheduleTriggerType.AtLogon;

    /// <summary>Time of day for Daily/Weekly triggers (HH:mm).</summary>
    [JsonProperty("timeOfDay")]
    public string TimeOfDay { get; set; } = "08:00";

    /// <summary>Day of week for Weekly trigger.</summary>
    [JsonProperty("dayOfWeek")]
    public DayOfWeek DayOfWeek { get; set; } = DayOfWeek.Monday;

    /// <summary>Interval in hours for Hourly trigger.</summary>
    [JsonProperty("intervalHours")]
    public int IntervalHours { get; set; } = 1;

    [JsonProperty("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonIgnore]
    public string TriggerSummary => TriggerType switch
    {
        ScheduleTriggerType.AtLogon => "At user logon",
        ScheduleTriggerType.Daily   => $"Daily at {TimeOfDay}",
        ScheduleTriggerType.Weekly  => $"Every {DayOfWeek} at {TimeOfDay}",
        ScheduleTriggerType.Hourly  => $"Every {IntervalHours}h",
        _ => TriggerType.ToString()
    };
}

public class FilterEntry
{
    [JsonProperty("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("mode")]
    public FilterMode Mode { get; set; } = FilterMode.Blacklist;
}

public class FolderMapping
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    [JsonProperty("destinationPath")]
    public string DestinationPath { get; set; } = string.Empty;

    [JsonProperty("filterMode")]
    public FilterMode FilterMode { get; set; } = FilterMode.None;

    [JsonProperty("filters")]
    public List<FilterEntry> Filters { get; set; } = new();

    [JsonProperty("assignedSchedules")]
    public List<string> AssignedSchedules { get; set; } = new();
}

public class BackupConfig
{
    [JsonProperty("folderMappings")]
    public List<FolderMapping> FolderMappings { get; set; } = new();

    [JsonProperty("logDirectory")]
    public string LogDirectory { get; set; } = @"C:\LFBM";

    [JsonProperty("taskName")]
    public string TaskName { get; set; } = "Save_Game_Backup";

    [JsonProperty("scheduledTasks")]
    public List<ScheduleEntry> ScheduledTasks { get; set; } = new();
}

using LocalFolderBackupManager.Services;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace LocalFolderBackupManager;

internal static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        // Check for automated mode FIRST (before admin check)
        bool isAutomated = args.Length > 0 && args[0].Equals("--automated", StringComparison.OrdinalIgnoreCase);

        // Verify administrator privileges
        if (!IsRunningAsAdministrator())
        {
            if (isAutomated)
            {
                // In automated mode, log the error instead of showing MessageBox
                try
                {
                    var logDir = @"C:\Save_Game_Backup_Logs";
                    Directory.CreateDirectory(logDir);
                    var errorLog = Path.Combine(logDir, $"AdminError_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.WriteAllText(errorLog, 
                        "Automated backup failed: Application is not running with administrator privileges.\n\n" +
                        "The scheduled task must be configured to run with highest privileges.\n" +
                        "Please check the task properties in Task Scheduler.");
                }
                catch { }
                Environment.Exit(1);
                return;
            }
            else
            {
                // In GUI mode, show the message box
                MessageBox.Show(
                    "This application requires administrator privileges to run.\n\n" +
                    "Please right-click the application and select 'Run as administrator'.",
                    "Administrator Rights Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Environment.Exit(1);
                return;
            }
        }

        // Check for automated mode (headless backup)
        if (isAutomated)
        {
            // Parse optional --schedule argument
            string? scheduleName = null;
            if (args.Length >= 3 && args[1].Equals("--schedule", StringComparison.OrdinalIgnoreCase))
            {
                scheduleName = args[2];
            }

            RunAutomatedBackup(scheduleName);
            return;
        }

        // Normal GUI mode
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private static void RunAutomatedBackup(string? scheduleName = null)
    {
        var logDir = @"C:\Save_Game_Backup_Logs";
        string? debugLog = null;

        try
        {
            // Create log directory first
            Directory.CreateDirectory(logDir);
            debugLog = Path.Combine(logDir, $"AutomatedBackup_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            void Log(string message)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    File.AppendAllText(debugLog!, $"[{timestamp}] {message}\n");
                }
                catch { }
            }

            Log("=== Automated Backup Started ===");
            Log($"Schedule Name: {scheduleName ?? "(none - backup all)"}");
            Log($"Running as: {Environment.UserName}");
            Log($"Is Admin: {IsRunningAsAdministrator()}");

            // Load configuration
            var configService = new ConfigurationService();
            var config = configService.LoadConfiguration();

            Log($"Config loaded. Total folders: {config.FolderMappings.Count}");

            // Filter folders if a specific schedule was provided
            if (!string.IsNullOrWhiteSpace(scheduleName))
            {
                Log($"Filtering folders for schedule: {scheduleName}");
                var filteredMappings = config.FolderMappings
                    .Where(f => f.AssignedSchedules.Contains(scheduleName, StringComparer.OrdinalIgnoreCase))
                    .ToList();

                Log($"Folders matching schedule: {filteredMappings.Count}");

                if (filteredMappings.Count == 0)
                {
                    Log("No folders assigned to this schedule - exiting successfully");
                    // No folders assigned to this schedule - exit successfully
                    Environment.Exit(0);
                    return;
                }

                foreach (var mapping in filteredMappings)
                {
                    Log($"  - {mapping.Name ?? mapping.SourcePath}");
                }

                // Create a temporary config with only the filtered folders
                config = new Models.BackupConfig
                {
                    FolderMappings = filteredMappings,
                    LogDirectory = config.LogDirectory,
                    TaskName = config.TaskName,
                    ScheduledTasks = config.ScheduledTasks
                };
            }
            else
            {
                Log("No schedule filter - backing up all folders");
                foreach (var mapping in config.FolderMappings)
                {
                    Log($"  - {mapping.Name ?? mapping.SourcePath}");
                }
            }

            // Create backup service
            Log("Creating backup service...");
            var backupService = new BackupService(config);

            // Perform backup synchronously in headless mode
            Log("Starting backup operation...");
            var task = backupService.PerformBackupAsync();
            task.Wait();

            var result = task.Result;

            Log($"Backup completed. Success: {result.Success}");
            if (!result.Success && !string.IsNullOrEmpty(result.ErrorMessage))
            {
                Log($"Error: {result.ErrorMessage}");
            }

            Log("=== Automated Backup Finished ===");

            // Exit with appropriate code
            Environment.Exit(result.Success ? 0 : 1);
        }
        catch (Exception ex)
        {
            // Log error to a file
            try
            {
                if (debugLog != null)
                {
                    File.AppendAllText(debugLog, $"\n=== EXCEPTION ===\n{ex.ToString()}\n");
                }
                else
                {
                    Directory.CreateDirectory(logDir);
                    var errorLog = Path.Combine(logDir, $"Error_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.WriteAllText(errorLog, $"Automated backup failed: {ex.Message}\n\n{ex.StackTrace}");
                }
            }
            catch { }

            Environment.Exit(1);
        }
    }
}

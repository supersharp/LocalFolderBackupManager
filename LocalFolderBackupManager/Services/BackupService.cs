using LocalFolderBackupManager.Models;
using System.Diagnostics;
using System.IO;

namespace LocalFolderBackupManager.Services;

public class BackupService
{
    private readonly BackupConfig _config;

    public BackupService(BackupConfig config)
    {
        _config = config;
    }

    public async Task<BackupResult> PerformBackupAsync(IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new BackupResult { StartTime = DateTime.Now };

        try
        {
            // Ensure directories exist

            Directory.CreateDirectory(_config.LogDirectory);

            // Clean old logs
            CleanOldLogFiles(7);

            var logFile = Path.Combine(_config.LogDirectory, $"Save_Game_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            // Backup Custom Folders
            int customCount = _config.FolderMappings?.Count ?? 0;
            int c = 0;
            foreach (var mapping in _config.FolderMappings ?? Enumerable.Empty<FolderMapping>())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                c++;
                var percentage = (int)((c / (double)(customCount > 0 ? customCount : 1)) * 10);
                progress?.Report(new BackupProgress { Message = $"Backing up {mapping.SourcePath}...", Percentage = percentage });

                if (Directory.Exists(mapping.SourcePath))
                {
                    Directory.CreateDirectory(mapping.DestinationPath);
                    var warning = await BackupFolderAsync(mapping, logFile, cancellationToken);
                    result.FoldersBackedUp++;

                    // Collect any warnings from this folder backup
                    if (!string.IsNullOrEmpty(warning))
                    {
                        result.Warnings.Add($"{mapping.Name}: {warning}");
                    }
                }
            }

            // Backup logic is now completely generic through FolderMappings

            progress?.Report(new BackupProgress { Message = "Backup completed!", Percentage = 100 });
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.Now;
        }

        return result;
    }

    public async Task<RestoreResult> PerformRestoreAsync(IProgress<BackupProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = new RestoreResult { StartTime = DateTime.Now };

        try
        {
            // The backup directory existence check is handled per folder mapping now.

            var logFile = Path.Combine(_config.LogDirectory, $"Restore_{DateTime.Now:yyyyMMdd_HHmmss}.log");

            // Restore Custom Folders
            int customCount = _config.FolderMappings?.Count ?? 0;
            int c = 0;
            foreach (var mapping in _config.FolderMappings ?? Enumerable.Empty<FolderMapping>())
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                c++;
                var percentage = (int)((c / (double)(customCount > 0 ? customCount : 1)) * 50);
                progress?.Report(new BackupProgress { Message = $"Restoring {mapping.SourcePath}...", Percentage = percentage });

                if (Directory.Exists(mapping.DestinationPath))
                {
                    // For restore, we swap source and destination, but we apply the same filters?
                    // Actually, restore should probably just restore everything in the backup folder, 
                    // or apply the same whitelist/blacklist. Let's apply the same filter logic.
                    var restoreMapping = new FolderMapping
                    {
                        SourcePath = mapping.DestinationPath,
                        DestinationPath = mapping.SourcePath,
                        FilterMode = mapping.FilterMode,
                        Filters = mapping.Filters
                    };
                    await BackupFolderAsync(restoreMapping, logFile, cancellationToken);
                }
            }

            progress?.Report(new BackupProgress { Message = "Restore completed!", Percentage = 100 });
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.EndTime = DateTime.Now;
        }

        return result;
    }

    private async Task<string?> BackupFolderAsync(FolderMapping mapping, string logFile, CancellationToken cancellationToken)
    {
        string? excludeFile = null;
        string? warningMessage = null;

        try
        {
            var arguments = $"\"{mapping.SourcePath}\" \"{mapping.DestinationPath}\" /E /MIR /ZB /XJ /R:1 /W:1 /LOG+:\"{logFile}\" /NFL /NDL /NJH /NJS";

            if (mapping.FilterMode == FilterMode.Whitelist)
            {
                // For whitelist mode, only copy matching items
                var whitelist = mapping.Filters
                    .Where(f => f.Mode == FilterMode.Whitelist && !string.IsNullOrWhiteSpace(f.Pattern))
                    .Select(f => f.Pattern.TrimEnd('\\', '*'))  // Remove \* suffix for robocopy compatibility
                    .Distinct()
                    .ToList();

                if (whitelist.Any())
                {
                    // Add whitelist items directly after source/dest paths
                    var whitelistArgs = string.Join(" ", whitelist.Select(w => $"\"{w}\""));
                    arguments = $"\"{mapping.SourcePath}\" \"{mapping.DestinationPath}\" {whitelistArgs} /E /MIR /ZB /XJ /R:1 /W:1 /LOG+:\"{logFile}\" /NFL /NDL /NJH /NJS";
                }
            }
            else if (mapping.FilterMode == FilterMode.Blacklist)
            {
                // For blacklist mode, exclude matching items
                var blacklist = mapping.Filters
                    .Where(f => f.Mode == FilterMode.Blacklist && !string.IsNullOrWhiteSpace(f.Pattern))
                    .Select(f => f.Pattern)
                    .Distinct()
                    .ToList();

                if (blacklist.Any())
                {
                    // Remove the \* suffix from patterns (used internally to indicate "folder and subdirs")
                    // Robocopy doesn't accept wildcards in /XD, just the folder name/path
                    var cleanedPatterns = blacklist.Select(p => p.TrimEnd('\\', '*')).ToList();

                    // Remove parent folders if we have more specific child folder exclusions
                    // e.g., if we have both "Local" and "Local\AMD", remove "Local" so we can backup Local but exclude AMD
                    var filteredPatterns = cleanedPatterns.Where(pattern =>
                    {
                        // Keep this pattern only if there's no more specific child pattern
                        var hasChildPattern = cleanedPatterns.Any(other =>
                            other != pattern &&
                            other.StartsWith(pattern + "\\", StringComparison.OrdinalIgnoreCase));
                        return !hasChildPattern;
                    }).ToList();

                    // Split into likely directories (no extension) and files (with extension or wildcards)
                    var directories = filteredPatterns.Where(b => !b.Contains('.') || b.Contains("\\")).ToList();
                    var files = filteredPatterns.Where(b => b.Contains('.') && !b.Contains("\\")).ToList();

                    // Convert relative paths to absolute paths by combining with source path
                    // Robocopy requires absolute paths for /XD and /XF to work correctly
                    var absoluteDirectories = directories.Select(d => Path.Combine(mapping.SourcePath, d)).ToList();
                    var absoluteFiles = files.Select(f => Path.Combine(mapping.SourcePath, f)).ToList();

                    var excludeArgs = "";
                    if (absoluteDirectories.Any())
                    {
                        excludeArgs += " /XD " + string.Join(" ", absoluteDirectories.Select(d => $"\"{d}\""));
                    }

                    if (absoluteFiles.Any())
                    {
                        excludeArgs += " /XF " + string.Join(" ", absoluteFiles.Select(f => $"\"{f}\""));
                    }

                    // Check if command line would be too long (Windows limit is ~32k, use 30k to be safe)
                    if ((arguments + excludeArgs).Length > 30000)
                    {
                        // Use an exclusion file instead
                        excludeFile = Path.Combine(Path.GetTempPath(), $"robocopy_exclude_{Guid.NewGuid()}.txt");

                        var excludeLines = new List<string>();
                        if (absoluteDirectories.Any())
                        {
                            excludeLines.AddRange(absoluteDirectories);
                        }
                        if (absoluteFiles.Any())
                        {
                            excludeLines.AddRange(absoluteFiles);
                        }

                        File.WriteAllLines(excludeFile, excludeLines);
                        arguments += $" /XF \"{excludeFile}\"";
                    }
                    else
                    {
                        arguments += excludeArgs;
                    }
                }
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "robocopy",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Log the full robocopy command for debugging
            File.AppendAllText(logFile, $"\n\n=== ROBOCOPY COMMAND ===\nrobocopy {arguments}\n\n");

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            await process.WaitForExitAsync(cancellationToken);

            // Robocopy exit codes:
            // 0-7: Success (various combinations of files copied/extras/mismatches)
            // 8: Some files/directories could not be copied (recoverable errors)
            // 9: Files in use, retry limit exceeded (expected during live system backup)
            // 16+: Serious error, no files copied
            var exitCode = process.ExitCode;
            File.AppendAllText(logFile, $"Exit Code: {exitCode}\n");

            if (exitCode >= 16)
            {
                throw new InvalidOperationException($"Robocopy failed with exit code {exitCode}: Serious error, no files copied.");
            }
            else if (exitCode >= 8)
            {
                // Warning: partial success (some files couldn't be copied due to locks, permissions, etc.)
                warningMessage = exitCode == 9 
                    ? "Some files were in use and could not be copied (retry limit exceeded)."
                    : "Some files or directories could not be copied.";
                File.AppendAllText(logFile, $"WARNING: {warningMessage}\n");
                // Don't throw - let backup continue, we'll track warnings in result
            }

            return warningMessage;
        }
        finally
        {
            // Clean up temporary exclude file
            if (excludeFile != null && File.Exists(excludeFile))
            {
                try { File.Delete(excludeFile); } catch { }
            }
        }
    }



    private void CleanOldLogFiles(int daysToKeep)
    {
        if (!Directory.Exists(_config.LogDirectory))
            return;

        var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
        var oldLogs = Directory.GetFiles(_config.LogDirectory, "*.log")
            .Where(f => File.GetLastWriteTime(f) < cutoffDate);

        foreach (var log in oldLogs)
        {
            try
            {
                File.Delete(log);
            }
            catch { }
        }
    }

    public DateTime? GetLastBackupDate()
    {
        if (!Directory.Exists(_config.LogDirectory))
            return null;

        var latestLog = Directory.GetFiles(_config.LogDirectory, "Save_Game_Backup_*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();

        return latestLog?.LastWriteTime;
    }

    public long GetBackupSize()
    {
        long totalSize = 0;

        if (_config.FolderMappings != null)
        {
            foreach (var mapping in _config.FolderMappings)
            {
                if (Directory.Exists(mapping.DestinationPath))
                {
                    totalSize += GetDirectorySize(mapping.DestinationPath);
                }
            }
        }



        return totalSize;
    }

    private long GetDirectorySize(string path)
    {
        try
        {
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
        }
        catch
        {
            return 0;
        }
    }
}

public class BackupResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int FoldersBackedUp { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public TimeSpan Duration => EndTime - StartTime;
}

public class RestoreResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
}

public class BackupProgress
{
    public string Message { get; set; } = string.Empty;
    public int Percentage { get; set; }
}

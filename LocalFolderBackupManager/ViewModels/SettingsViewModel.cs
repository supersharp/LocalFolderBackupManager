using LocalFolderBackupManager.Models;
using LocalFolderBackupManager.Services;

namespace LocalFolderBackupManager.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly BackupConfig _config;
    private readonly ConfigurationService _configService;

    public SettingsViewModel(BackupConfig config, ConfigurationService configService, TaskSchedulerService taskScheduler)
    {
        _config        = config;
        _configService = configService;
        // taskScheduler kept as parameter for future extensibility
    }
}

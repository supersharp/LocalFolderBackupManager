using LocalFolderBackupManager.Dialogs;
using LocalFolderBackupManager.Models;
using LocalFolderBackupManager.Services;
using LocalFolderBackupManager.ViewModels;
using LocalFolderBackupManager.Views;
using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LocalFolderBackupManager;

public partial class MainWindow : FluentWindow
{
    private readonly BackupConfig _config;
    private readonly ConfigurationService _configService;
    private readonly BackupService _backupService;
    private readonly TaskSchedulerService _taskScheduler;

    // Keep a reference to the active FoldersViewModel so we can check IsDirty
    private FoldersViewModel? _activeFoldersVM;

    public MainWindow()
    {
        SystemThemeWatcher.Watch(this);

        InitializeComponent();

        _configService = new ConfigurationService();
        _config        = _configService.LoadConfiguration();
        _backupService = new BackupService(_config);
        _taskScheduler = new TaskSchedulerService(_config.TaskName);

        // Guard window close
        Closing += MainWindow_Closing;

        NavigateTo("Dashboard");
    }

    // ──────────────────────────────────────────────────────────────
    // Navigation
    // ──────────────────────────────────────────────────────────────

    private void NavItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationViewItem item) return;

        var tag = item.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;

        // Guard: prompt for unsaved folder changes before leaving
        if (!ConfirmLeaveIfDirty()) return;

        // Reset active states
        foreach (var menuItem in NavView.MenuItems.OfType<NavigationViewItem>())
            menuItem.IsActive = false;
        foreach (var footerItem in NavView.FooterMenuItems.OfType<NavigationViewItem>())
            footerItem.IsActive = false;

        item.IsActive = true;
        NavigateTo(tag);
    }

    private void NavigateTo(string page)
    {
        // Clear the active Folders VM reference whenever we navigate away
        _activeFoldersVM = null;

        // Reload configuration from disk to ensure fresh data
        var currentConfig = _configService.LoadConfiguration();

        switch (page)
        {
            case "Dashboard":
                var dashboardVM = new DashboardViewModel(currentConfig, _backupService, _taskScheduler, _configService);
                ContentFrame.Content = new DashboardView { DataContext = dashboardVM };
                break;

            case "Folders":
                var foldersVM = new FoldersViewModel(currentConfig, _configService);
                _activeFoldersVM = foldersVM;
                ContentFrame.Content = new FoldersView { DataContext = foldersVM };
                break;

            case "Schedule":
                var scheduleVM = new ScheduleViewModel(currentConfig, _configService, _taskScheduler);
                ContentFrame.Content = new ScheduleView { DataContext = scheduleVM };
                break;

            case "Settings":
                var settingsVM = new SettingsViewModel(currentConfig, _configService, _taskScheduler);
                ContentFrame.Content = new SettingsView { DataContext = settingsVM };
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Unsaved-changes guards
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if it's safe to leave the current view.
    /// Prompts the user if the Folders view has unsaved changes.
    /// </summary>
    private bool ConfirmLeaveIfDirty()
    {
        if (_activeFoldersVM == null || !_activeFoldersVM.IsDirty)
            return true;

        var result = DialogService.PromptUnsavedChanges("Folder Configuration");

        return result switch
        {
            UnsavedChangesResult.Save    => _activeFoldersVM.SaveAndMarkClean(),
            UnsavedChangesResult.Discard => true,
            _                           => false // Cancel — stay on the page
        };
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Discard any unsaved changes on application close
        // No prompt shown - changes are automatically discarded
    }
}

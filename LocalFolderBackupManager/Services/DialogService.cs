using LocalFolderBackupManager.Dialogs;
using System.Windows;

namespace LocalFolderBackupManager.Services;

/// <summary>
/// Drop-in replacement for <see cref="System.Windows.MessageBox"/> that shows
/// themed Fluent dialogs matching the application's dark Mica style.
/// </summary>
public static class DialogService
{
    // ──────────────────────────────────────────────────────────────
    // Informational / status alerts (single OK button)
    // ──────────────────────────────────────────────────────────────

    public static void ShowInfo(string message, string title = "Information")
        => Show(FluentDialog.CreateAlert(title, message, FluentDialogIcon.Information));

    public static void ShowSuccess(string message, string title = "Success")
        => Show(FluentDialog.CreateAlert(title, message, FluentDialogIcon.Success));

    public static void ShowWarning(string message, string title = "Warning")
        => Show(FluentDialog.CreateAlert(title, message, FluentDialogIcon.Warning));

    public static void ShowError(string message, string title = "Error")
        => Show(FluentDialog.CreateAlert(title, message, FluentDialogIcon.Error));

    // ──────────────────────────────────────────────────────────────
    // Confirmations (Yes / No)
    // ──────────────────────────────────────────────────────────────

    /// <summary>Returns true if the user confirms (presses Yes).</summary>
    public static bool Confirm(string message, string title = "Confirm",
        FluentDialogIcon icon = FluentDialogIcon.Question,
        string yesLabel = "Yes", string noLabel = "No")
    {
        var dlg = FluentDialog.CreateConfirm(title, message, icon, yesLabel, noLabel);
        Show(dlg);
        return dlg.PrimaryResult;
    }

    /// <summary>
    /// Shows a Save / Discard / Cancel dialog for unsaved changes.
    /// </summary>
    public static UnsavedChangesResult PromptUnsavedChanges(string context = "this view")
    {
        var dlg = FluentDialog.CreateUnsavedChanges(context);
        Show(dlg);
        return dlg.UnsavedResult;
    }

    // ──────────────────────────────────────────────────────────────
    // Private
    // ──────────────────────────────────────────────────────────────

    private static void Show(FluentDialog dialog)
    {
        // Attach to the main window so it centres correctly and inherits the theme context.
        var owner = Application.Current?.MainWindow;
        if (owner != null)
        {
            dialog.Owner = owner;
        }
        dialog.ShowDialog();
    }
}

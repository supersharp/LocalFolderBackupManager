using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LocalFolderBackupManager.Dialogs;

/// <summary>Which icon/accent to use on the dialog.</summary>
public enum FluentDialogIcon
{
    Information,
    Success,
    Warning,
    Error,
    Question
}

/// <summary>
/// Result from a three-button dialog (Save / Discard / Cancel).
/// </summary>
public enum UnsavedChangesResult
{
    Save,
    Discard,
    Cancel
}

/// <summary>
/// A compact themed message dialog that matches the application's Mica / Fluent Design aesthetic.
/// </summary>
public partial class FluentDialog : FluentWindow
{
    private bool _primaryClicked;

    // Used by the three-button unsaved-changes variant
    private UnsavedChangesResult _unsavedResult = UnsavedChangesResult.Cancel;

    public FluentDialog()
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
    }

    // ──────────────────────────────────────────────────────────────
    // Factory / configuration
    // ──────────────────────────────────────────────────────────────

    /// <summary>Alert with a single OK button.</summary>
    public static FluentDialog CreateAlert(
        string title,
        string message,
        FluentDialogIcon icon = FluentDialogIcon.Information)
    {
        var dlg = new FluentDialog();
        dlg.Configure(title, message, icon, primaryLabel: "OK", showSecondary: false);
        return dlg;
    }

    /// <summary>Confirm dialog with Yes / No buttons.</summary>
    public static FluentDialog CreateConfirm(
        string title,
        string message,
        FluentDialogIcon icon = FluentDialogIcon.Question,
        string yesLabel = "Yes",
        string noLabel = "No")
    {
        var dlg = new FluentDialog();
        dlg.Configure(title, message, icon, primaryLabel: yesLabel, showSecondary: true, secondaryLabel: noLabel);
        return dlg;
    }

    /// <summary>
    /// Three-button unsaved-changes dialog: Save / Discard / Cancel.
    /// Read <see cref="UnsavedResult"/> after <c>ShowDialog()</c>.
    /// </summary>
    public static FluentDialog CreateUnsavedChanges(string context = "this view")
    {
        var dlg = new FluentDialog();
        dlg.ConfigureUnsavedChanges(context);
        return dlg;
    }

    /// <summary>True when the user clicked the primary (OK / Yes / Save) button.</summary>
    public bool PrimaryResult => _primaryClicked;

    /// <summary>Result of a three-button unsaved-changes dialog.</summary>
    public UnsavedChangesResult UnsavedResult => _unsavedResult;

    // ──────────────────────────────────────────────────────────────
    // Configuration helpers
    // ──────────────────────────────────────────────────────────────

    private void Configure(
        string title,
        string message,
        FluentDialogIcon icon,
        string primaryLabel,
        bool showSecondary,
        string secondaryLabel = "No")
    {
        Title            = title;
        TitleText.Text   = title;
        MessageText.Text = message;
        PrimaryButton.Content   = primaryLabel;
        SecondaryButton.Content = secondaryLabel;
        SecondaryButton.Visibility = showSecondary ? Visibility.Visible : Visibility.Collapsed;

        ApplyIcon(icon);
        ApplyPrimaryAppearance(icon, showSecondary);
    }

    private void ConfigureUnsavedChanges(string context)
    {
        Title            = "Unsaved Changes";
        TitleText.Text   = "Unsaved Changes";
        MessageText.Text = $"You have unsaved changes in {context}.\n\nWould you like to save them before leaving?";

        PrimaryButton.Content    = "Save";
        PrimaryButton.Appearance = ControlAppearance.Success;

        SecondaryButton.Content    = "Discard";
        SecondaryButton.Appearance = ControlAppearance.Danger;
        SecondaryButton.Visibility = Visibility.Visible;

        // Add a tertiary Cancel button
        var cancelBtn = new Button
        {
            Content = "Cancel",
            Height  = 32,
            Padding = new Thickness(18, 0, 18, 0),
            Margin  = new Thickness(0, 0, 8, 0)
        };
        cancelBtn.Click += (_, _) =>
        {
            _unsavedResult   = UnsavedChangesResult.Cancel;
            _primaryClicked  = false;
            Close();
        };

        // Re-order: Cancel | Discard | Save
        // Insert Cancel before SecondaryButton in the panel
        ButtonPanel.Children.Insert(0, cancelBtn);

        // Re-wire secondary (Discard) for this scenario
        SecondaryButton.Click -= SecondaryButton_Click;
        SecondaryButton.Click += (_, _) =>
        {
            _unsavedResult   = UnsavedChangesResult.Discard;
            _primaryClicked  = false;
            Close();
        };

        // Primary = Save
        PrimaryButton.Click -= PrimaryButton_Click;
        PrimaryButton.Click += (_, _) =>
        {
            _unsavedResult  = UnsavedChangesResult.Save;
            _primaryClicked = true;
            Close();
        };

        ApplyIcon(FluentDialogIcon.Warning);
    }

    // ──────────────────────────────────────────────────────────────

    private void ApplyIcon(FluentDialogIcon icon)
    {
        var (emoji, bg) = icon switch
        {
            FluentDialogIcon.Success     => ("✅", "#2A2E2A"),
            FluentDialogIcon.Warning     => ("⚠️", "#2E2A1A"),
            FluentDialogIcon.Error       => ("❌", "#2E1A1A"),
            FluentDialogIcon.Question    => ("❓", "#1A1E2E"),
            FluentDialogIcon.Information => ("ℹ️", "#1A1E2E"),
            _                           => ("ℹ️", "#1A1E2E"),
        };

        IconText.Text = emoji;

        try
        {
            IconBorder.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(bg));
        }
        catch { /* keep default */ }
    }

    private void ApplyPrimaryAppearance(FluentDialogIcon icon, bool isConfirm)
    {
        PrimaryButton.Appearance = icon switch
        {
            FluentDialogIcon.Error    => ControlAppearance.Danger,
            FluentDialogIcon.Success  => ControlAppearance.Success,
            FluentDialogIcon.Question when isConfirm => ControlAppearance.Primary,
            FluentDialogIcon.Warning  when isConfirm => ControlAppearance.Caution,
            _ => ControlAppearance.Primary
        };
    }

    // ──────────────────────────────────────────────────────────────
    // Button handlers (standard two-button)
    // ──────────────────────────────────────────────────────────────

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        _primaryClicked = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        _primaryClicked = false;
        Close();
    }
}

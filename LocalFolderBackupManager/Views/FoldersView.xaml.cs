using System.Windows.Controls;
using System.Windows.Input;
using LocalFolderBackupManager.ViewModels;

namespace LocalFolderBackupManager.Views;

public partial class FoldersView : Page
{
    public FoldersView()
    {
        InitializeComponent();
    }

    private void SourceTextBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element && 
            element.DataContext is FolderMappingViewModel vm)
        {
            if (vm.BrowseSourceCommand?.CanExecute(null) == true)
            {
                vm.BrowseSourceCommand.Execute(null);
            }
        }
    }

    private void DestinationTextBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element && 
            element.DataContext is FolderMappingViewModel vm)
        {
            if (vm.BrowseDestinationCommand?.CanExecute(null) == true)
            {
                vm.BrowseDestinationCommand.Execute(null);
            }
        }
    }
}

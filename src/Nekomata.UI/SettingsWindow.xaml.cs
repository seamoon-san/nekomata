using System.Windows;
using Nekomata.UI.ViewModels;
using Wpf.Ui.Controls;

namespace Nekomata.UI;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}

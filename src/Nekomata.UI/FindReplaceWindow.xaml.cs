using System.Windows;
using Nekomata.UI.ViewModels;
using Wpf.Ui.Controls;

namespace Nekomata.UI;

public partial class FindReplaceWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public FindReplaceWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void FindNext_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.FindNext(FindTextBox.Text);
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ReplaceOne(FindTextBox.Text, ReplaceTextBox.Text);
    }

    private async void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ReplaceAll(FindTextBox.Text, ReplaceTextBox.Text);
    }
}

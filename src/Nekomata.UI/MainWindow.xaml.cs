using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Nekomata.UI.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Controls = System.Windows.Controls;

using Nekomata.Models;
using System.Windows.Data;

namespace Nekomata.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }
    private readonly ISnackbarService _snackbarService;

    public MainWindow(MainViewModel viewModel, ISnackbarService snackbarService)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        
        _snackbarService = snackbarService;
        snackbarService.SetSnackbarPresenter(this.SnackbarPresenter);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                // Assuming the first item is the game folder or exe
                var path = files[0];
                // Check if file or directory
                if (System.IO.File.Exists(path))
                {
                    path = System.IO.Path.GetDirectoryName(path);
                }
                
                if (ViewModel.CreateProjectCommand.CanExecute(path))
                {
                    ViewModel.CreateProjectCommand.Execute(path);
                }
            }
        }
    }

    private void DataGridCell_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cell = sender as Controls.DataGridCell;
        if (cell != null && !cell.IsEditing && !cell.IsReadOnly)
        {
            if (!cell.IsFocused)
            {
                cell.Focus();
            }
            var dataGrid = FindVisualParent<Controls.DataGrid>(cell);
            if (dataGrid != null)
            {
                var row = FindVisualParent<Controls.DataGridRow>(cell);
                if (row != null)
                {
                    // Fix: Clear previous selection if no modifier keys are pressed to avoid unwanted multi-selection
                    if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0)
                    {
                        if (dataGrid.SelectionUnit == Controls.DataGridSelectionUnit.FullRow)
                        {
                            // Setting SelectedItem in Extended mode clears other selections
                            dataGrid.SelectedItem = row.Item;
                        }
                        else
                        {
                            if (!cell.IsSelected)
                            {
                                dataGrid.SelectedCells.Clear();
                                cell.IsSelected = true;
                            }
                        }
                    }
                    else
                    {
                        // Preserve existing behavior for modifier keys (add to selection)
                        if (dataGrid.SelectionUnit != Controls.DataGridSelectionUnit.FullRow)
                        {
                            if (!cell.IsSelected) cell.IsSelected = true;
                        }
                        else
                        {
                            if (!row.IsSelected) row.IsSelected = true;
                        }
                    }
                }

                dataGrid.BeginEdit();
                // e.Handled = true; // Do not handle, let the click process (e.g. caret placement)
            }
        }
    }

    private void TranslationTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Shift+Enter to move to next row
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            e.Handled = true;
            var textBox = sender as Controls.TextBox;
            var dataGrid = FindVisualParent<Controls.DataGrid>(textBox);
            if (dataGrid != null)
            {
                dataGrid.CommitEdit(Controls.DataGridEditingUnit.Row, true);

                var cellContent = FindVisualParent<Controls.ContentPresenter>(textBox);
                var cell = FindVisualParent<Controls.DataGridCell>(cellContent);
                
                if (cell != null)
                {
                    var row = FindVisualParent<Controls.DataGridRow>(cell);
                    if (row != null)
                    {
                        var index = dataGrid.ItemContainerGenerator.IndexFromContainer(row);
                        if (index < dataGrid.Items.Count - 1)
                        {
                            dataGrid.SelectedIndex = index + 1;
                            dataGrid.ScrollIntoView(dataGrid.SelectedItem);
                            
                            var nextItem = dataGrid.Items[index + 1];
                            dataGrid.CurrentCell = new Controls.DataGridCellInfo(nextItem, cell.Column);
                            dataGrid.BeginEdit();
                        }
                    }
                }
            }
        }
    }

    private void TranslationTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.TextBox textBox)
        {
            textBox.Focus();
            textBox.CaretIndex = textBox.Text.Length;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void DataGridCell_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Controls.DataGridCell cell)
        {
            if (cell.Column is Controls.DataGridTextColumn textColumn &&
                textColumn.Binding is Binding binding &&
                binding.Path.Path == nameof(TranslationUnit.OriginalText))
            {
                CopyOriginalText(cell.DataContext as TranslationUnit);
            }
        }
    }

    private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (sender is Controls.DataGrid dataGrid && dataGrid.SelectedItem is TranslationUnit unit)
            {
                CopyOriginalText(unit);
                e.Handled = true;
            }
        }
    }

    private void CopyOriginalText(TranslationUnit? unit)
    {
        if (unit == null || string.IsNullOrEmpty(unit.OriginalText)) return;

        try
        {
            Clipboard.SetText(unit.OriginalText);
            _snackbarService.Show("提示", "复制成功", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(2));
        }
        catch
        {
            _snackbarService.Show("错误", "复制失败", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(2));
        }
    }
}

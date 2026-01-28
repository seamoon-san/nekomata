using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Nekomata.UI.ViewModels;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Controls = System.Windows.Controls;

using Nekomata.Models;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System;
using System.ComponentModel;

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

        TranslationGrid.Loaded += TranslationGrid_Loaded;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "CurrentScrollOffset")
        {
            var scrollViewer = FindVisualChild<Controls.ScrollViewer>(TranslationGrid);
            if (scrollViewer != null && Math.Abs(scrollViewer.VerticalOffset - ViewModel.CurrentScrollOffset) > 1.0)
            {
                scrollViewer.ScrollToVerticalOffset(ViewModel.CurrentScrollOffset);
            }
        }
    }

    private void TranslationGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var scrollViewer = FindVisualChild<Controls.ScrollViewer>(TranslationGrid);
        if (scrollViewer != null)
        {
            scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
            scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        }
    }

    private void ScrollViewer_ScrollChanged(object sender, Controls.ScrollChangedEventArgs e)
    {
        if (Math.Abs(ViewModel.CurrentScrollOffset - e.VerticalOffset) > 1.0)
        {
            ViewModel.CurrentScrollOffset = e.VerticalOffset;
        }
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

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;

            var result = FindVisualChild<T>(child);
            if (result != null) return result;
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

    private void DataGrid_SelectionChanged(object sender, Controls.SelectionChangedEventArgs e)
    {
        if (sender is Controls.DataGrid dataGrid && dataGrid.SelectedItem != null)
        {
            dataGrid.ScrollIntoView(dataGrid.SelectedItem);
        }
    }

    private void Expander_Expanded(object sender, RoutedEventArgs e)
    {
        UpdateGroupState(sender, false);
    }

    private void Expander_Collapsed(object sender, RoutedEventArgs e)
    {
        UpdateGroupState(sender, true);
    }

    private void Expander_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.Expander expander)
        {
            // Attach DataContextChanged listener to handle virtualization recycling
            expander.DataContextChanged -= Expander_DataContextChanged;
            expander.DataContextChanged += Expander_DataContextChanged;

            RestoreExpansionState(expander);
        }
    }

    private void Expander_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is Controls.Expander expander)
        {
            expander.DataContextChanged -= Expander_DataContextChanged;
        }
    }

    private void Expander_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is Controls.Expander expander)
        {
            RestoreExpansionState(expander);
        }
    }

    private void RestoreExpansionState(Controls.Expander expander)
    {
        if (expander.DataContext is CollectionViewGroup group && DataContext is MainViewModel vm)
        {
            var name = group.Name?.ToString() ?? string.Empty;
            
            // Temporarily detach events to avoid triggering updates during state restore
            expander.Expanded -= Expander_Expanded;
            expander.Collapsed -= Expander_Collapsed;

            if (vm.CollapsedGroupNames.Contains(name))
            {
                expander.IsExpanded = false;
            }
            else
            {
                expander.IsExpanded = true;
            }

            expander.Expanded += Expander_Expanded;
            expander.Collapsed += Expander_Collapsed;
        }
    }

    private void UpdateGroupState(object sender, bool isCollapsed)
    {
        if (sender is Controls.Expander expander &&
            expander.DataContext is CollectionViewGroup group &&
            DataContext is MainViewModel vm)
        {
            var name = group.Name?.ToString() ?? string.Empty;
            if (isCollapsed)
                vm.CollapsedGroupNames.Add(name);
            else
                vm.CollapsedGroupNames.Remove(name);
        }
    }

    #region Win32 Clipboard API
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint GMEM_ZEROINIT = 0x0040;
    private const uint GHND = GMEM_MOVEABLE | GMEM_ZEROINIT;
    #endregion

    private async void CopyOriginalText(TranslationUnit? unit)
    {
        if (unit == null || string.IsNullOrEmpty(unit.OriginalText)) return;

        try
        {
            var text = unit.OriginalText.Replace("\0", string.Empty);

            const int MaxRetries = 10;
            const int DelayMs = 100;

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    // Attempt to release any potential lock held by this thread
                    // This is a "best effort" to fix the "CLIPBRD_E_CANT_OPEN" if it was caused by a previous leak
                    try { CloseClipboard(); } catch { }

                    await Task.Run(() =>
                    {
                        // Use native API for more control
                        if (!OpenClipboard(IntPtr.Zero))
                        {
                            throw new Exception("OpenClipboard failed");
                        }

                        try
                        {
                            if (!EmptyClipboard())
                            {
                                throw new Exception("EmptyClipboard failed");
                            }

                            // Allocate global memory
                            // +2 for double null terminator (Unicode)
                            var bytes = (uint)(text.Length + 1) * 2;
                            var hGlobal = GlobalAlloc(GHND, (UIntPtr)bytes);
                            
                            if (hGlobal == IntPtr.Zero)
                            {
                                throw new Exception("GlobalAlloc failed");
                            }

                            try
                            {
                                var target = GlobalLock(hGlobal);
                                if (target == IntPtr.Zero)
                                {
                                    throw new Exception("GlobalLock failed");
                                }

                                try
                                {
                                    var data = System.Text.Encoding.Unicode.GetBytes(text);
                                    Marshal.Copy(data, 0, target, data.Length);
                                }
                                finally
                                {
                                    GlobalUnlock(hGlobal);
                                }

                                if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
                                {
                                    throw new Exception("SetClipboardData failed");
                                }
                                
                                // System owns the memory now, do not free hGlobal
                                hGlobal = IntPtr.Zero;
                            }
                            finally
                            {
                                if (hGlobal != IntPtr.Zero)
                                {
                                    GlobalFree(hGlobal);
                                }
                            }
                        }
                        finally
                        {
                            CloseClipboard();
                        }
                    });

                    _snackbarService.Show("提示", "复制成功", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(2));
                    return;
                }
                catch (Exception)
                {
                    if (i == MaxRetries - 1) throw;
                    await Task.Delay(DelayMs);
                }
            }
        }
        catch (Exception ex)
        {
            _snackbarService.Show("错误", $"复制失败: {ex.Message}", ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(2));
        }
    }
}

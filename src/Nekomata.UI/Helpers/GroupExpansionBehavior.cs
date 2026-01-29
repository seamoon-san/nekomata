using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Nekomata.UI.Helpers
{
    public static class GroupExpansionBehavior
    {
        public static readonly DependencyProperty BindExpandedStateProperty =
            DependencyProperty.RegisterAttached("BindExpandedState", typeof(bool), typeof(GroupExpansionBehavior), new PropertyMetadata(false, OnBindExpandedStateChanged));

        public static bool GetBindExpandedState(DependencyObject obj) => (bool)obj.GetValue(BindExpandedStateProperty);
        public static void SetBindExpandedState(DependencyObject obj, bool value) => obj.SetValue(BindExpandedStateProperty, value);

        private static readonly DependencyProperty ExpandedStatesProperty =
            DependencyProperty.RegisterAttached("ExpandedStates", typeof(Dictionary<object, bool>), typeof(GroupExpansionBehavior), new PropertyMetadata(null));

        private static Dictionary<object, bool> GetExpandedStates(DependencyObject obj) => (Dictionary<object, bool>)obj.GetValue(ExpandedStatesProperty);
        private static void SetExpandedStates(DependencyObject obj, Dictionary<object, bool> value) => obj.SetValue(ExpandedStatesProperty, value);

        private static void OnBindExpandedStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Expander expander)
            {
                if ((bool)e.NewValue)
                {
                    expander.Loaded += Expander_Loaded;
                    expander.Expanded += Expander_Expanded;
                    expander.Collapsed += Expander_Collapsed;
                }
                else
                {
                    expander.Loaded -= Expander_Loaded;
                    expander.Expanded -= Expander_Expanded;
                    expander.Collapsed -= Expander_Collapsed;
                }
            }
        }

        private static void Expander_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Expander expander && expander.DataContext is CollectionViewGroup group)
            {
                var dataGrid = FindParentDataGrid(expander);
                if (dataGrid != null)
                {
                    var states = GetExpandedStates(dataGrid);
                    if (states == null)
                    {
                        states = new Dictionary<object, bool>();
                        SetExpandedStates(dataGrid, states);
                    }

                    var key = group.Name ?? string.Empty;

                    if (states.TryGetValue(key, out bool isExpanded))
                    {
                        expander.IsExpanded = isExpanded;
                    }
                }
            }
        }

        private static void Expander_Expanded(object sender, RoutedEventArgs e)
        {
            UpdateState(sender, true);
        }

        private static void Expander_Collapsed(object sender, RoutedEventArgs e)
        {
            UpdateState(sender, false);
        }

        private static void UpdateState(object sender, bool isExpanded)
        {
            if (sender is Expander expander && expander.DataContext is CollectionViewGroup group)
            {
                var dataGrid = FindParentDataGrid(expander);
                if (dataGrid != null)
                {
                    var states = GetExpandedStates(dataGrid);
                    if (states == null)
                    {
                        states = new Dictionary<object, bool>();
                        SetExpandedStates(dataGrid, states);
                    }
                    var key = group.Name ?? string.Empty;
                    states[key] = isExpanded;
                }
            }
        }

        private static DataGrid? FindParentDataGrid(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is DataGrid))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as DataGrid;
        }
    }
}

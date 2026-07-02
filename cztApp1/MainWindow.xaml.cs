using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using cztApp1.Models;
using cztApp1.Views;

namespace cztApp1
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<string> _undoStack = new();
        private readonly ObservableCollection<string> _redoStack = new();
        private const int MaxHistory = 20;

        private const string SpatialDataPath = @"D:\geomatics_task\地理信息工程及应用\2322050202于景赫-12组-长株潭地质灾害（土壤植被）\数据\空间数据";
        private const string AttributeDataPath = @"D:\geomatics_task\地理信息工程及应用\2322050202于景赫-12组-长株潭地质灾害（土壤植被）\数据\属性数据";
        private string _currentRootPath = SpatialDataPath;

        public MainWindow()
        {
            InitializeComponent();
            Resources["SysFolderIcon"] = SystemIconProvider.FolderIcon;
            Resources["SysFileIcon"] = SystemIconProvider.FileIcon;
            UpdateUndoRedoState();
            LoadDirectoryTree(SpatialDataPath);

            StateChanged += (s, e) =>
            {
                MaxBtn.Content = WindowState == WindowState.Maximized ? "" : "";
                MaxBtn.ToolTip = WindowState == WindowState.Maximized ? "还原" : "最大化";
            };
        }

        private void UpdateUndoRedoState()
        {
            UndoBtn.IsEnabled = _undoStack.Count > 0;
            UndoDropBtn.IsEnabled = _undoStack.Count > 0;
            RedoBtn.IsEnabled = _redoStack.Count > 0;
            RedoDropBtn.IsEnabled = _redoStack.Count > 0;
        }

        public void RecordOperation(string name)
        {
            _undoStack.Insert(0, name);
            if (_undoStack.Count > MaxHistory)
                _undoStack.RemoveAt(_undoStack.Count - 1);
            _redoStack.Clear();
            UpdateUndoRedoState();
        }

        #region 标题栏事件

        private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { Maximize_Click(sender, e); return; }
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e)
            => Close();

        #endregion

        #region 撤销 / 重做

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack.Count == 0) return;
            var op = _undoStack[0];
            _undoStack.RemoveAt(0);
            _redoStack.Insert(0, op);
            UpdateUndoRedoState();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (_redoStack.Count == 0) return;
            var op = _redoStack[0];
            _redoStack.RemoveAt(0);
            _undoStack.Insert(0, op);
            UpdateUndoRedoState();
        }

        private void UndoHistory_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            if (_undoStack.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "（无可撤销操作）", IsEnabled = false });
            }
            else
            {
                foreach (var op in _undoStack)
                {
                    var item = new MenuItem { Header = $"↩ {op}" };
                    item.Click += (s, _) =>
                    {
                        _undoStack.Remove(op);
                        _redoStack.Insert(0, op);
                        UpdateUndoRedoState();
                    };
                    menu.Items.Add(item);
                }
            }
            menu.IsOpen = true;
        }

        private void RedoHistory_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            if (_redoStack.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "（无可重做操作）", IsEnabled = false });
            }
            else
            {
                foreach (var op in _redoStack)
                {
                    var item = new MenuItem { Header = $"↪ {op}" };
                    item.Click += (s, _) =>
                    {
                        _redoStack.Remove(op);
                        _undoStack.Insert(0, op);
                        UpdateUndoRedoState();
                    };
                    menu.Items.Add(item);
                }
            }
            menu.IsOpen = true;
        }

        #endregion

        #region 数据面板

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要导入的数据文件",
                Filter = "Shapefile|*.shp|GeoJSON|*.geojson|CSV|*.csv|所有文件|*.*",
                Multiselect = true
            };
            dlg.ShowDialog();
        }

        private void PanelOptions_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();
            var autoHide = new MenuItem { Header = "自动隐藏" };
            autoHide.Click += (s, _) => AutoHide_Click(s, e);
            var close = new MenuItem { Header = "关闭" };
            close.Click += (s, _) => PanelClose_Click(s, e);
            menu.Items.Add(autoHide);
            menu.Items.Add(close);
            menu.IsOpen = true;
        }

        private void AutoHide_Click(object sender, RoutedEventArgs e)
        {
            AutoHideBtn.Tag = AutoHideBtn.Tag is null ? "pinned" : null;
        }

        private void PanelClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe)
            {
                var panel = fe.Parent as UIElement;
                while (panel is not null && panel is not Border)
                    panel = (panel as FrameworkElement)?.Parent as UIElement;
                if (panel is Border b)
                    b.Visibility = b.Visibility == Visibility.Visible
                        ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        private static readonly SolidColorBrush SkyBlue = new(Color.FromRgb(0x4F, 0xC3, 0xF7));
        private static readonly SolidColorBrush GrayBorder = new(Color.FromRgb(0xD0, 0xD0, 0xD0));
        private bool _searchFocused;

        private void SearchBorder_MouseEnter(object sender, MouseEventArgs e)
        { if (!_searchFocused) SearchBorder.BorderBrush = SkyBlue; }
        private void SearchBorder_MouseLeave(object sender, MouseEventArgs e)
        { if (!_searchFocused) SearchBorder.BorderBrush = GrayBorder; }
        private void SearchBorder_MouseDown(object sender, MouseButtonEventArgs e)
            => SearchBox.Focus();

        private void SearchBorder_GotFocus(object sender, RoutedEventArgs e)
        { _searchFocused = true; SearchBorder.BorderBrush = SkyBlue; }
        private void SearchBorder_LostFocus(object sender, RoutedEventArgs e)
        { _searchFocused = false; SearchBorder.BorderBrush = GrayBorder; }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SwitchToSpatial(object sender, RoutedEventArgs e)
        {
            BtnSpatial.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
            BtnSpatial.Foreground = Brushes.White;
            BtnAttribute.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            BtnAttribute.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            LoadDirectoryTree(SpatialDataPath);
        }

        private void SwitchToAttribute(object sender, RoutedEventArgs e)
        {
            BtnAttribute.Background = new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0));
            BtnAttribute.Foreground = Brushes.White;
            BtnSpatial.Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            BtnSpatial.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            LoadDirectoryTree(AttributeDataPath);
        }

        #region 目录树

        private void LoadDirectoryTree(string rootPath)
        {
            _currentRootPath = rootPath;
            DataTree.Items.Clear();
            if (!Directory.Exists(rootPath)) return;
            var root = CreateTreeItem(rootPath, true);
            DataTree.Items.Add(root);
        }

        private TreeViewItem CreateTreeItem(string path, bool isRoot = false)
        {
            var name = isRoot ? Path.GetFileName(path) : Path.GetFileName(path);
            // Build header with icon + text
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var img = new Image
            {
                Source = SystemIconProvider.GetIcon(path),
                Width = 16, Height = 16,
                Margin = new Thickness(0, 0, 4, 0),
                Stretch = System.Windows.Media.Stretch.Uniform
            };
            sp.Children.Add(img);
            sp.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
            var item = new TreeViewItem
            {
                Header = sp,
                Tag = path,        // full path
                FontWeight = isRoot ? FontWeights.SemiBold : FontWeights.Normal
            };

            // Lazy load: add dummy child so expand arrow appears
            if (Directory.Exists(path))
            {
                try
                {
                    bool hasContent = Directory.GetDirectories(path).Length > 0
                                   || Directory.GetFiles(path).Length > 0;
                    if (hasContent)
                    {
                        item.Items.Add(new TreeViewItem { Header = "loading...", Tag = "__dummy__" });
                        item.Expanded += OnDirExpanded;
                    }
                }
                catch { /* skip inaccessible folders */ }
            }

            return item;
        }

        private void OnDirExpanded(object sender, RoutedEventArgs e)
        {
            var item = (TreeViewItem)sender;
            // Only load once: check if first child is the dummy
            if (item.Items.Count == 1 && item.Items[0] is TreeViewItem dummy && "__dummy__".Equals(dummy.Tag))
            {
                item.Items.Clear();
                var path = item.Tag as string;
                if (string.IsNullOrEmpty(path)) return;

                try
                {
                    // Sort: folders first, then files
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        try { item.Items.Add(CreateTreeItem(dir)); } catch { }
                    }
                    foreach (var file in Directory.GetFiles(path))
                    {
                        try { item.Items.Add(CreateTreeItem(file)); } catch { }
                    }
                }
                catch { /* skip inaccessible folders */ }
            }
        }

        #endregion

        private void DataPanel_GotFocus(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
                border.Focus();
        }

        #endregion

        #region 分析面板

        private void ShowAnalysis(ModuleInfo module)
        {
            if (AnalysisHost.Children.Count == 0)
            {
                var panel = new AnalysisPanel();
                panel.Closed += () => { AnalysisHost.Children.Clear(); MapPlaceholder.Visibility = Visibility.Visible; };
                AnalysisHost.Children.Add(panel);
            }
            else if (AnalysisHost.Children[0] is AnalysisPanel existing)
            {
                existing.LoadModule(module);
            }
            MapPlaceholder.Visibility = Visibility.Collapsed;
        }

        private void Geo_Btn_Click(object sender, RoutedEventArgs e) => ShowAnalysis(ModuleRegistry.Geology);
        private void Topo_Btn_Click(object sender, RoutedEventArgs e) => ShowAnalysis(ModuleRegistry.Topography);
        private void Veg_Btn_Click(object sender, RoutedEventArgs e) => ShowAnalysis(ModuleRegistry.Vegetation);

        #endregion
    }
}

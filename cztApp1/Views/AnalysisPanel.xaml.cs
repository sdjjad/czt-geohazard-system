using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using cztApp1.Models;
using cztApp1.Services;
using Microsoft.Win32;

namespace cztApp1.Views
{
    public partial class AnalysisPanel : UserControl
    {
        private readonly GeoAnalysisService _service = new();
        private ModuleInfo _module = ModuleRegistry.SoilTypeAnalysis;
        private List<GeoParameter> _params = new();
        private List<StatResult>? _lastResults;
        public event Action? Closed;

        public AnalysisPanel()
        {
            InitializeComponent();
        }

        public void LoadModule(ModuleInfo module)
        {
            _module = module;
            TitleText.Text = $"{module.Name} — 分析参数设置";
            ModelMethod.Items.Clear();
            foreach (var m in module.Methods)
                ModelMethod.Items.Add(new ComboBoxItem { Content = m, IsSelected = m == "CF值法" });

            _params = module.Parameters.Select(p => new GeoParameter { Name = p }).ToList();
            ParameterList.ItemsSource = _params;
            ResultGrid.ItemsSource = null;
        }

        private void RunAnalysis_Click(object sender, RoutedEventArgs e)
        {
            var config = new AnalysisConfig
            {
                ModuleName = _module.Name,
                DataSource = (DataSource.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "",
                DataRange = (DataRange.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "",
                DataTime = (DataTime.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "",
                ModelMethod = (ModelMethod.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "CF值法",
                OutputFolder = OutputFolder.Text,
                Parameters = _params.Where(p => p.Classes.Count == 0 || true).ToList()
            };

            _lastResults = _service.RunAnalysis(config, msg =>
            {
                Dispatcher.Invoke(() => ResultGrid.ItemsSource = new ObservableCollection<StatResult>());
            });

            ResultGrid.ItemsSource = new ObservableCollection<StatResult>(_lastResults);
            MessageBox.Show($"{_module.Name} 分析完成，共 {_lastResults.Count} 条结果", "完成",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveResults_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResults == null) return;
            _service.SaveResults(OutputFolder.Text, _module.Name, _lastResults);
            MessageBox.Show($"结果已保存至:\n{OutputFolder.Text}", "保存成功",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog { Title = "选择输出目录" };
            if (dlg.ShowDialog() == true)
                OutputFolder.Text = dlg.FolderName;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
            => Closed?.Invoke();
    }
}

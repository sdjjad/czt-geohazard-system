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
            MessageBox.Show("请使用地理处理面板进行分析。\n\n点击功能区 → 土壤植被 → 任一指标按钮，\n在地理处理面板中选择已加载的图层后运行分析。",
                "功能已迁移", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveResults_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("请使用地理处理面板保存结果。",
                "功能已迁移", MessageBoxButton.OK, MessageBoxImage.Information);
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

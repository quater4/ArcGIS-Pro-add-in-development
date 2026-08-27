using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XYL_Tools.Models;
using XYL_Tools.Services;

namespace XYL_Tools
{
    internal class DataCheckDockPaneViewModel : DockPane
    {
        private static DataCheckDockPaneViewModel _instance;
        public static DataCheckDockPaneViewModel Instance => _instance;

        // 界面绑定的数据
        public ObservableCollection<LayerCheckResult> LayerResults { get; set; } = [];
        public ObservableCollection<FeatureIssue> AllIssues { get; set; } = [];

        // 当前选中的错误项
        private FeatureIssue _selectedIssue;
        public FeatureIssue SelectedIssue
        {
            get => _selectedIssue;
            set
            {
                _selectedIssue = value;
                NotifyPropertyChanged();
                (LocateIssueCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 检查状态
        private bool _isChecking;
        public bool IsChecking
        {
            get => _isChecking;
            set
            {
                _isChecking = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsNotChecking));
            }
        }
        public bool IsNotChecking => !IsChecking;

        // 命令
        public ICommand CheckCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand LocateIssueCommand { get; }

        public DataCheckDockPaneViewModel()
        {
            CheckCommand = new RelayCommand(async () => await RunCheckAsync(), () => IsNotChecking);
            ExportCommand = new RelayCommand(ExportToCsv, () => LayerResults.Count > 0);
            LocateIssueCommand = new RelayCommand(async () => await LocateIssueAsync(), () => SelectedIssue != null && IsNotChecking);
        }

        protected override void OnShow(bool isFirstTime)
        {
            base.OnShow(isFirstTime);
            _instance = this;
        }

        public CheckOptions CheckOptions { get; set; } = new();

        /// <summary>
        /// 执行检查：只调用 Service，不写具体检查逻辑
        /// </summary>
        private async Task RunCheckAsync()
        {
            var mapView = MapView.Active;
            var map = MapView.Active?.Map;
            if (map == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No active map found.");
                return;
            }

            // 获取要检查的图层列表
            IEnumerable<FeatureLayer> targetLayers;
            if (CheckOptions.CheckSelectedLayers)
            {
                targetLayers = mapView.GetSelectedLayers().OfType<FeatureLayer>();
                // 选中图层模式下，空选择直接提示返回，不进入后续检查
                if (!targetLayers.Any())
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "请在内容面板至少选中一个要素图层",
                        "提示");
                    return;
                }
            }
            else
            {
                targetLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
            }

            IsChecking = true;
            try
            {
                List<LayerCheckResult> results = null;
                List<FeatureIssue> issues = null;

                await QueuedTask.Run(() =>
                {
                    // 所有业务逻辑都交给 Service
                    var checker = new DataQualityChecker();
                    (results, issues) = checker.CheckLayers(targetLayers, CheckOptions);
                });

                // 更新 UI 数据
                LayerResults.Clear();
                foreach (var r in results) LayerResults.Add(r);
                AllIssues.Clear();
                foreach (var i in issues) AllIssues.Add(i);
            }
            finally
            {
                IsChecking = false;
            }
        }

        /// <summary>
        /// 定位到错误要素：UI 交互逻辑，保留在 ViewModel
        /// </summary>
        private async Task LocateIssueAsync()
        {
            var issue = SelectedIssue;
            if (issue == null) return;

            var mapView = MapView.Active;
            if (mapView == null || string.IsNullOrEmpty(issue.LayerName)) return;

            await QueuedTask.Run(() =>
            {
                if (mapView.Map.FindLayers(issue.LayerName).FirstOrDefault() is not FeatureLayer layer) return;

                var queryFilter = new QueryFilter { ObjectIDs = [issue.ObjectID] };
                layer.Select(queryFilter, SelectionCombinationMethod.New);

                using var featureClass = layer.GetFeatureClass();
                using var cursor = featureClass.Search(queryFilter, false);
                if (cursor.MoveNext())
                {
                    using var feature = cursor.Current as ArcGIS.Core.Data.Feature;
                    var geometry = feature?.GetShape();
                    if (geometry != null && !geometry.IsEmpty)
                    {
                        var extent = geometry.Extent;
                        extent.Expand(1.5, 1.5, true);
                        mapView.ZoomTo(extent);
                    }
                }
            });
        }

        /// <summary>
        /// 导出 CSV：保留在 ViewModel（属于 UI 导出功能）
        /// </summary>
        private void ExportToCsv()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"数据质量检查报告_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = ".csv"
            };
            if (dialog.ShowDialog() != true) return;

            var lines = new List<string>
            {
                "图层,类型,要素数,空间参考,字段数,NULL数,几何错误,重复数,状态"
            }.Concat(LayerResults.Select(r =>
            {
                string status = r.Status switch
                {
                    CheckStatus.Normal => "正常",
                    CheckStatus.Warning => "警告",
                    CheckStatus.Error => "错误",
                    _ => ""
                };
                string layerName = EscapeCsv(r.LayerName);
                string sr = EscapeCsv(r.SpatialReference);
                return $"{layerName},{r.ShapeType},{r.FeatureCount},{sr},{r.FieldCount},{r.NullCount},{r.GeometryErrorCount},{r.DuplicateCount},{status}";
            })).ToList();
            File.WriteAllLines(dialog.FileName, lines, new System.Text.UTF8Encoding(true));
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}

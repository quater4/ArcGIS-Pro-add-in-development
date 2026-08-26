using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO;

namespace XYL_Tools
{
    internal class DataCheckDockPaneViewModel : DockPane
    {
        private static DataCheckDockPaneViewModel _instance;
        public static DataCheckDockPaneViewModel Instance => _instance;

        public ObservableCollection<LayerCheckResult> LayerResults { get; set; }
            = new ObservableCollection<LayerCheckResult>();

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

        public ICommand ExportCommand { get; }

        public ICommand CheckCommand { get; }

        public DataCheckDockPaneViewModel()
        {
            ExportCommand = new RelayCommand(ExportToCsv, () => LayerResults.Count > 0);
            CheckCommand = new RelayCommand(async () => await RunCheckAsync(), () => IsNotChecking);
        }

        protected override void OnShow(bool isFirstTime)
        {
            base.OnShow(isFirstTime);
            _instance = this;
        }

        private void ExportToCsv()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv",
                FileName = $"数据质量检查报告_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var lines = new List<string>
            {
                "图层,类型,要素数,空间参考,字段数,NULL数,几何错误,重复数,状态"
            };

            foreach (var r in LayerResults)
            {
                string status = r.Status switch
                {
                    CheckStatus.Normal => "正常",
                    CheckStatus.Warning => "警告",
                    CheckStatus.Error => "错误",
                    _ => ""
                };

                // 字段值含逗号或引号时用双引号包裹
                string layerName = EscapeCsv(r.LayerName);
                string sr = EscapeCsv(r.SpatialReference);

                lines.Add($"{layerName},{r.ShapeType},{r.FeatureCount},{sr},{r.FieldCount},{r.NullCount},{r.GeometryErrorCount},{r.DuplicateCount},{status}");
            }

            // UTF-8 with BOM，Excel 打开中文不乱码
            File.WriteAllLines(dialog.FileName, lines, new System.Text.UTF8Encoding(true));
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        private async Task RunCheckAsync()
        {
            var map = MapView.Active?.Map;
            if (map == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("No active map found.");
                return;
            }

            IsChecking = true;
            var results = new List<LayerCheckResult>();

            try
            {
                await QueuedTask.Run(() =>
                {
                    var layers = map.GetLayersAsFlattenedList();
                    foreach (var layer in layers)
                    {
                        if (layer is FeatureLayer featureLayer)
                        {
                            using var featureClass = featureLayer.GetFeatureClass();
                            if (featureClass == null) continue;

                            using var definition = featureClass.GetDefinition();
                            var shapeType = definition.GetShapeType();

                            var spatialReference = definition.GetSpatialReference();
                            bool spatialRefOk = spatialReference != null && !spatialReference.IsUnknown;

                            var count = featureClass.GetCount();
                            var fields = definition.GetFields();
                            int fieldCount = fields.Count;

                            var checkFieldIndices = new List<int>();
                            for (int i = 0; i < fields.Count; i++)
                            {
                                if (fields[i].FieldType != FieldType.OID &&
                                    fields[i].FieldType != FieldType.Geometry)
                                    checkFieldIndices.Add(i);
                            }

                            int nullCount = 0;
                            int geometryErrorCount = 0;
                            var valueCounts = new Dictionary<string, int>();

                            using var cursor = featureClass.Search(new QueryFilter(), false);
                            while (cursor.MoveNext())
                            {
                                using var row = cursor.Current;

                                foreach (int i in checkFieldIndices)
                                {
                                    var value = row[i];
                                    if (value == null || value == DBNull.Value)
                                        nullCount++;
                                }

                                var feature = row as ArcGIS.Core.Data.Feature;
                                var geometry = feature?.GetShape();
                                if (geometry == null || geometry.IsEmpty)
                                    geometryErrorCount++;
                                else if (!GeometryEngine.Instance.IsSimpleAsFeature(geometry, true))
                                    geometryErrorCount++;

                                var keyParts = new List<string>();
                                foreach (int i in checkFieldIndices)
                                {
                                    var value = row[i];
                                    keyParts.Add(value == null || value == DBNull.Value ? "[NULL]" : value.ToString());
                                }
                                string key = string.Join("|", keyParts);
                                if (valueCounts.ContainsKey(key))
                                    valueCounts[key]++;
                                else
                                    valueCounts[key] = 1;
                            }

                            int duplicateCount = valueCounts.Values.Sum(c => c > 1 ? c - 1 : 0);

                            results.Add(new LayerCheckResult(
                                layer.Name,
                                shapeType.ToString(),
                                count,
                                spatialReference?.Name ?? string.Empty,
                                fieldCount,
                                spatialRefOk,
                                nullCount,
                                geometryErrorCount,
                                duplicateCount
                            ));
                        }
                    }
                });
            }
            finally
            {
                IsChecking = false;
            }

            LayerResults.Clear();
            foreach (var r in results)
                LayerResults.Add(r);
        }
    }
}

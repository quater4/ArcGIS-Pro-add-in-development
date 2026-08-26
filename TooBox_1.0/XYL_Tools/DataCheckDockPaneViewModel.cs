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
using System.Security.Cryptography.X509Certificates;
using System.Security.Policy;

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

        public ObservableCollection<FeatureIssue> AllIssues { get; set; } = new ObservableCollection<FeatureIssue>();

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
                var allIssues = new List<FeatureIssue>();
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

                            string oidFieldName = definition.GetObjectIDField();
                            int oidIndex = definition.FindField(oidFieldName);


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

                            var keyToOids = new Dictionary<string, List<long>>();

                            using var cursor = featureClass.Search(new QueryFilter(), false);
                            while (cursor.MoveNext())
                            {
                                using var row = cursor.Current;
                                long oid = Convert.ToInt64(row[oidIndex]); //获取当前行的oid 

                                // 1:检查字段值是否为 NULL
                                foreach (int i in checkFieldIndices)
                                {
                                    var value = row[i];
                                    if (value == null || value == DBNull.Value)
                                    {
                                        nullCount++;
                                        allIssues.Add(new FeatureIssue
                                        {
                                            LayerName = layer.Name,
                                            ObjectID = oid,
                                            IssueType = "NULL",
                                            Field = fields[i].Name,
                                            Description = $"字段 {fields[i].Name} 的值为 NULL"
                                        });
                                    }
                                }

                                // 2:检查几何图形是否为空或自相交
                                var feature = row as ArcGIS.Core.Data.Feature;
                                var geometry = feature?.GetShape();
                                if (geometry == null || geometry.IsEmpty)
                                {
                                    geometryErrorCount++;
                                    allIssues.Add(new FeatureIssue
                                    {
                                        LayerName = layer.Name,
                                        ObjectID = oid,
                                        IssueType = "GeometryError",
                                        Field = "Shape",
                                        Description = "几何图形为空或无效"
                                    });
                                }
                                else if (!GeometryEngine.Instance.IsSimpleAsFeature(geometry, true))
                                {
                                    geometryErrorCount++;
                                    allIssues.Add(new FeatureIssue
                                    {
                                        LayerName = layer.Name,
                                        ObjectID = oid,
                                        IssueType = "GeometryError",
                                        Field = "Shape",
                                        Description = "几何图形自相交或未闭合"
                                    });
                                }

                                // 3:检查重复要素（基于所有非 OID 和非几何字段的组合值）
                                var keyParts = new List<string>();
                                foreach (int i in checkFieldIndices)
                                {
                                    var value = row[i];
                                    keyParts.Add(value == null || value == DBNull.Value ? "[NULL]" : value.ToString());
                                }
                                string key = string.Join("|", keyParts);
                                if (keyToOids.ContainsKey(key))
                                    keyToOids[key].Add(oid);
                                else
                                    keyToOids[key] = new List<long> { oid };
                            }

                            // 遍历结束后，处理重复：出现多次的 key 下所有 OID 都算重复
                            int duplicateCount = 0;

                            foreach (var kvp in keyToOids) 
                            {
                                if (kvp.Value.Count > 1)
                                {
                                    duplicateCount += kvp.Value.Count - 1;
                                    foreach (var oid in kvp.Value)
                                    {
                                        allIssues.Add(new FeatureIssue
                                        {
                                            LayerName = layer.Name,
                                            ObjectID =oid,
                                            IssueType = "Duplicate",
                                            Field = "-",
                                            Description = $"重复要素,共{kvp.Value.Count}条相同记录"
                                        });
                                    }
                                }
                            }

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

                // 更新 UI
                LayerResults.Clear();
                foreach (var r in results)
                    LayerResults.Add(r);
                AllIssues.Clear();
                foreach (var i in allIssues)
                    AllIssues.Add(i);
            }
            finally
            {
                IsChecking = false;
            }

        }
    }
}

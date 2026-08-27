using ArcGIS.Core.Data;
using ArcGIS.Desktop.Mapping;
using System.Collections.Generic;
using XYL_Tools.Models;
using System.Linq;

namespace XYL_Tools.Services
{
    internal class DataQualityChecker
    {    
         /// <summary>
         /// 检查整张地图的所有要素图层
         /// </summary>
        public (List<LayerCheckResult> layerResults, List<FeatureIssue> allIssues) CheckMap(Map map)
        {
            var layerResults = new List<LayerCheckResult>();
            var allIssues = new List<FeatureIssue>();

            var layers = map.GetLayersAsFlattenedList();
            foreach (var layer in layers)
            {
                if (layer is not FeatureLayer featureLayer) continue;

                var (layerResult, issues) = CheckLayer(featureLayer);
                layerResults.Add(layerResult);
                allIssues.AddRange(issues);
            }

            return (layerResults, allIssues);
        }

        /// <summary>
        /// 检查单个要素图层
        /// </summary>
        private (LayerCheckResult, List<FeatureIssue>) CheckLayer(FeatureLayer featureLayer)
        {
            using var featureClass = featureLayer.GetFeatureClass();
            if (featureClass == null) return default;

            using var definition = featureClass.GetDefinition();
            var shapeType = definition.GetShapeType();
            var spatialReference = definition.GetSpatialReference();
            bool spatialRefOk = spatialReference != null && !spatialReference.IsUnknown;
            long featureCount = featureClass.GetCount();
            var fields = definition.GetFields();
            int fieldCount = fields.Count;
            string oidFieldName = definition.GetObjectIDField();
            int oidIndex = definition.FindField(oidFieldName);

            // 筛选需要检查的字段（排除 OID 和几何）
            var checkFieldIndices = new List<int>();
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].FieldType != FieldType.OID &&
                    fields[i].FieldType != FieldType.Geometry)
                    checkFieldIndices.Add(i);
            }

            // 初始化三个检查器
            string layerName = featureLayer.Name;
            var nullChecker = new NullValueChecker(layerName, fields, checkFieldIndices, oidIndex);
            var geometryChecker = new GeometryChecker(layerName, oidIndex);
            var duplicateChecker = new DuplicateChecker(layerName, checkFieldIndices, oidIndex);

            // 一次游标遍历，分发给所有检查器
            using var cursor = featureClass.Search(new QueryFilter(), false);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                nullChecker.CheckRow(row);
                geometryChecker.CheckRow(row);
                duplicateChecker.CheckRow(row);
            }

            // 重复检查需要收尾
            duplicateChecker.Finish();

            // 汇总图层结果
            var layerResult = new LayerCheckResult(
                layerName,
                shapeType.ToString(),
                featureCount,
                spatialReference?.Name ?? string.Empty,
                fieldCount,
                spatialRefOk,
                nullChecker.NullCount,
                geometryChecker.ErrorCount,
                duplicateChecker.DuplicateCount
            );

            // 汇总所有问题
            var allIssues = new List<FeatureIssue>();
            allIssues.AddRange(nullChecker.Issues);
            allIssues.AddRange(geometryChecker.Issues);
            allIssues.AddRange(duplicateChecker.Issues);

            return (layerResult, allIssues);
        }
    }
}

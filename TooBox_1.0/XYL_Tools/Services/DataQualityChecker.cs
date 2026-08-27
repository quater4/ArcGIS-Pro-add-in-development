using ArcGIS.Core.Data;
using ArcGIS.Desktop.Mapping;
using System.Collections.Generic;
using XYL_Tools.Models;

namespace XYL_Tools.Services
{
    internal class DataQualityChecker
    {

        /// <summary>
        /// 检查指定的图层集合
        /// </summary>
        /// <param name="layers"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public (List<LayerCheckResult> layerResults, List<FeatureIssue> allIssues)
            CheckLayers(IEnumerable<FeatureLayer> layers, CheckOptions options)
        {
            var layerResults = new List<LayerCheckResult>();
            var allIssues = new List<FeatureIssue>();

            foreach (var layer in layers)
            {
                var (layerResult, issues) = CheckLayer(layer, options);
                if (layerResult != null && issues != null)
                {
                    layerResults.Add(layerResult);
                    allIssues.AddRange(issues);
                }
            }
            return (layerResults, allIssues);
        }

        /// <summary>
        /// 检查整张地图的所有要素图层
        /// </summary>
        public (List<LayerCheckResult> layerResults, List<FeatureIssue> allIssues) 
            CheckMap(Map map, CheckOptions options)
        {
            var layerResults = new List<LayerCheckResult>();
            var allIssues = new List<FeatureIssue>();

            var layers = map.GetLayersAsFlattenedList();
            foreach (var layer in layers)
            {
                if (layer is not FeatureLayer featureLayer) continue;

                var (layerResult, issues) = CheckLayer(featureLayer,options);

                // 如果图层检查结果和问题列表都不为空，则添加到结果集合中
                if (layerResult != null && issues != null) {
                    layerResults.Add(layerResult);
                    allIssues.AddRange(issues);
                }
            }

            return (layerResults, allIssues);
        }

        /// <summary>
        /// 检查单个要素图层
        /// </summary>
        private (LayerCheckResult, List<FeatureIssue>) 
            CheckLayer(FeatureLayer featureLayer , CheckOptions options)
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
            CheckStatus status = CheckStatus.Normal;

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
            NullValueChecker? nullChecker = null;
            GeometryChecker? geometryChecker = null;
            DuplicateChecker? duplicateChecker = null;


            if (options.CheckNull)
                nullChecker = new NullValueChecker(layerName, fields, checkFieldIndices, oidIndex);
            if (options.CheckGeometry)
                geometryChecker = new GeometryChecker(layerName, oidIndex);
            if (options.CheckDuplicate)
                duplicateChecker = new DuplicateChecker(layerName, checkFieldIndices, oidIndex);


            // 一次游标遍历，只调用启用的检查器
            using var cursor = featureClass.Search(new QueryFilter(), false);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                nullChecker?.CheckRow(row);
                geometryChecker?.CheckRow(row);
                duplicateChecker?.CheckRow(row);
            }
            duplicateChecker?.Finish();

            // 错误级问题
            if (options.CheckGeometry && geometryChecker!.ErrorCount > 0)
            {
                status = CheckStatus.Error;
            }
            if (options.CheckSpatialReference && !spatialRefOk)
            {
                status = CheckStatus.Error;
            }
            // 警告级问题（只有当前为正常时才降级）
            if (status == CheckStatus.Normal)
            {
                if (options.CheckNull && nullChecker!.NullCount > 0)
                    status = CheckStatus.Warning;
                if (options.CheckDuplicate && duplicateChecker!.DuplicateCount > 0)
                    status = CheckStatus.Warning;
            }

            // 汇总图层结果(构造函数)
            var layerResult = new LayerCheckResult(
                layerName,
                shapeType.ToString(),
                featureCount,
                spatialReference?.Name ?? string.Empty,
                fieldCount,
                spatialRefOk,
                nullChecker?.NullCount ?? 0,
                geometryChecker?.ErrorCount ?? 0,
                duplicateChecker?.DuplicateCount ?? 0
            )
            {
                Status = status
            };

            // 只汇总启用检查的错误
            var allIssues = new List<FeatureIssue>();
            if(options.CheckNull) allIssues.AddRange(nullChecker.Issues);
            if(options.CheckGeometry) allIssues.AddRange(geometryChecker.Issues);
            if(options.CheckDuplicate) allIssues.AddRange(duplicateChecker.Issues);

            return (layerResult, allIssues);
        }
    }
}

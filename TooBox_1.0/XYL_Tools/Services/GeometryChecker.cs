using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.Generic;
using System.Text;
using XYL_Tools.Models;

namespace XYL_Tools.Services
{
    internal class GeometryChecker(string layerName, int oidIndex)
    {
        private readonly string _layerName = layerName;
        private readonly int _oidIndex = oidIndex;

        public int ErrorCount { get; private set; }
        public List<FeatureIssue> Issues { get; } = [];

        /// <summary>
        /// 检查当前行的几何
        /// </summary>
        public void CheckRow(Row row)
        {
            long oid = Convert.ToInt64(row[_oidIndex]);
            var feature = row as Feature;
            var geometry = feature?.GetShape();

            if (geometry == null || geometry.IsEmpty)
            {
                ErrorCount++;
                Issues.Add(new FeatureIssue
                {
                    LayerName = _layerName,
                    ObjectID = oid,
                    IssueType = "GeometryError",
                    Field = "Shape",
                    Description = "几何图形为空或无效"
                });
            }
            else if (!GeometryEngine.Instance.IsSimpleAsFeature(geometry, true))
            {
                ErrorCount++;
                Issues.Add(new FeatureIssue
                {
                    LayerName = _layerName,
                    ObjectID = oid,
                    IssueType = "GeometryError",
                    Field = "Shape",
                    Description = "几何图形自相交或未闭合"
                });
            }
        }
    }
}

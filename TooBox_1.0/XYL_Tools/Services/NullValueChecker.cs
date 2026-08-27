using ArcGIS.Core.Data;
using System;
using System.Collections.Generic;
using System.Text;
using XYL_Tools.Models;

namespace XYL_Tools.Services
{
    internal class NullValueChecker
    {
        // 私有成员
        private readonly List<int> _checkFieldIndices;
        private readonly IReadOnlyList<Field> _fields;
        private readonly string _layerName;
        private readonly int _oidIndex;

        // 公有成员
        public int NullCount { get; private set; }
        public List<FeatureIssue> Issues { get; } = new();

        public NullValueChecker(string layerName, IReadOnlyList<Field> fields, List<int> checkFieldIndices, int oidIndex)
        {
            _layerName = layerName;
            _fields = fields;
            _checkFieldIndices = checkFieldIndices;
            _oidIndex = oidIndex;
        }

        /// <summary>
        /// 检查当前行的空值
        /// </summary>
        public void CheckRow(Row row)
        {
            long oid = Convert.ToInt64(row[_oidIndex]);
            foreach (int i in _checkFieldIndices)
            {
                var value = row[i];
                if (value == null || value == DBNull.Value)
                {
                    NullCount++;
                    Issues.Add(new FeatureIssue
                    {
                        LayerName = _layerName,
                        ObjectID = oid,
                        IssueType = "NULL",
                        Field = _fields[i].Name,
                        Description = $"字段 {_fields[i].Name} 的值为 NULL"
                    });
                }
            }
        }
    }
}

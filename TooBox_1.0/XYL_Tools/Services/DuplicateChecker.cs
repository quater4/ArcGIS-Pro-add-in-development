using ArcGIS.Core.Data;
using System;
using System.Collections.Generic;
using System.Text;
using XYL_Tools.Models;

namespace XYL_Tools.Services
{
    internal class DuplicateChecker(string layerName, List<int> checkFieldIndices, int oidIndex)
    {
        private readonly List<int> _checkFieldIndices = checkFieldIndices;
        private readonly string _layerName = layerName;
        private readonly int _oidIndex = oidIndex;
        private readonly Dictionary<string, List<long>> _keyToOids = [];

        public int DuplicateCount { get; private set; }
        public List<FeatureIssue> Issues { get; } = [];

        /// <summary>
        /// 收集当前行的属性键
        /// </summary>
        public void CheckRow(Row row)
        {
            long oid = Convert.ToInt64(row[_oidIndex]);
            var keyParts = new List<string>();
            foreach (int i in _checkFieldIndices)
            {
                var value = row[i];
                keyParts.Add(value == null || value == DBNull.Value ? "[NULL]" : value.ToString());
            }
            string key = string.Join("|", keyParts);

            if (_keyToOids.ContainsKey(key))
                _keyToOids[key].Add(oid);
            else
                _keyToOids[key] = [oid];
        }

        /// <summary>
        /// 全部遍历完成后，计算重复结果
        /// </summary>
        public void Finish()
        {
            foreach (var kvp in _keyToOids)
            {
                if (kvp.Value.Count > 1)
                {
                    DuplicateCount += kvp.Value.Count - 1;
                    foreach (var oid in kvp.Value)
                    {
                        Issues.Add(new FeatureIssue
                        {
                            LayerName = _layerName,
                            ObjectID = oid,
                            IssueType = "Duplicate",
                            Field = "-",
                            Description = $"重复要素，共{kvp.Value.Count}条相同记录"
                        });
                    }
                }
            }
        }
    }
}

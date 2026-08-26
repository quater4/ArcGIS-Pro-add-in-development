using System;
using System.Collections.Generic;
using System.Text;

namespace XYL_Tools
{
    internal class FeatureIssue
    {
        // 图层名 OID 问题类型 描述
        public string LayerName { get; set; } = string.Empty;
        public long ObjectID { get; set; } = 0;
        public string Description { get; set; }
        public string Field { get; set; }
        public string IssueType { get; set; } = string.Empty;
        
    }
}

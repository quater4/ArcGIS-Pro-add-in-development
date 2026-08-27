namespace XYL_Tools.Models
{
    internal class LayerCheckResult(string layerName, string shapeType,
        long featureCount, string spatialReference,
        int fieldCount, bool spatialReferenceEnabled,
        int nullcnt, int geometryErrorCnt, int duplicateRows)
    {


        // 检查结果状态
        public string LayerName { get; set; } = layerName;
        public string ShapeType { get; set; } = shapeType;
        public long FeatureCount { get; set; } = featureCount;
        public string SpatialReference { get; set; } = spatialReference;
        public int FieldCount { get; set; } = fieldCount;
        public bool SpatialReferenceEnabled { get; set; } = spatialReferenceEnabled;
        public int NullCount { get; set; } = nullcnt;

        public int GeometryErrorCount { get; set; } = geometryErrorCnt;
        public int DuplicateCount { get; set; } = duplicateRows;

        // 检查结果状态
        public CheckStatus Status
        {
            get;
            set;
        }
    }
}

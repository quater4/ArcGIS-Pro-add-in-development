using ArcGIS.Core.CIM;
using System;
using System.Collections.Generic;
using System.Text;
namespace XYL_Tools
{
    internal class LayerCheckResult
    {


        // properties of the layer check result
        public string LayerName { get; set; } = string.Empty;
        public string ShapeType { get; set; } = string.Empty;
        public long FeatureCount { get; set; } = 0;
        public string SpatialReference { get; set; } = string.Empty;
        public int FieldCount { get; set; } = 0;
        public bool SpatialReferenceEnabled { get; set; }
        public int NullCount { get; set; } = 0;

        public int GeometryErrorCount { get; set; } = 0;
        public int DuplicateCount { get; set; } = 0;

        public CheckStatus Status
        {
            get
            {
                if(GeometryErrorCount > 0 || !SpatialReferenceEnabled)
                {
                    return CheckStatus.Error;
                }
                else if(NullCount > 0 || DuplicateCount > 0)
                {
                    return CheckStatus.Warning;
                }
                else
                {
                    return CheckStatus.Normal;
                }
            }

        }
        public LayerCheckResult(string layerName, string shapeType, 
            long featureCount , string spatialReference, 
            int fieldCount ,bool spatialReferenceEnabled,
            int nullcnt, int geometryErrorCnt, int duplicateRows)
        {
            LayerName = layerName;
            ShapeType = shapeType;
            FeatureCount = featureCount;
            SpatialReference = spatialReference;
            FieldCount = fieldCount;
            SpatialReferenceEnabled = spatialReferenceEnabled;
            NullCount = nullcnt;
            GeometryErrorCount = geometryErrorCnt;
            DuplicateCount = duplicateRows;
        }
    }
}

namespace cztApp1.Models
{
    public class GeoParameter
    {
        public string Name { get; set; } = "";
        public string DataSource { get; set; } = "";
        public string Classification { get; set; } = "";
        public List<string> Classes { get; set; } = new();
    }

    public class AnalysisConfig
    {
        public string ModuleName { get; set; } = "";
        public string DataSource { get; set; } = "";
        public string DataRange { get; set; } = "";
        public string DataTime { get; set; } = "";
        public string ModelMethod { get; set; } = "CF值法";
        public string OutputFolder { get; set; } = "D:\\GeoHazardOutput";
        public List<GeoParameter> Parameters { get; set; } = new();
    }

    public class StatResult
    {
        public string ParameterName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public int HazardCount { get; set; }
        public double AreaKm2 { get; set; }
        public double Density { get; set; }
        public double Percentage { get; set; }
        public double CF { get; set; }
    }

    public class ModuleInfo
    {
        public string Name { get; set; } = "";
        public string[] Parameters { get; set; } = Array.Empty<string>();
        public string[] Methods { get; set; } = Array.Empty<string>();
    }

    public static class ModuleRegistry
    {
        public static readonly ModuleInfo Geology = new()
        {
            Name = "地质地震",
            Parameters = new[] { "地层", "岩性", "工程岩组", "构造/断层", "地震烈度", "峰值加速度" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" }
        };

        public static readonly ModuleInfo Topography = new()
        {
            Name = "地形地貌",
            Parameters = new[] { "坡度", "坡向", "地表起伏度", "高程", "坡型", "坡位", "地貌类型" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" }
        };

        public static readonly ModuleInfo Vegetation = new()
        {
            Name = "土壤植被",
            Parameters = new[] { "土壤类型(2-3级)", "土壤湿度", "植被类型(2-3级)", "植被覆盖度", "NDVI" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" }
        };
    }
}

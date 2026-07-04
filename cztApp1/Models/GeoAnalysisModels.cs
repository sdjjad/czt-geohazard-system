namespace cztApp1.Models
{
    /// <summary>
    /// 单个分析指标参数
    /// </summary>
    public class GeoParameter
    {
        public string Name { get; set; } = "";
        public string DataSource { get; set; } = "";
        public string Classification { get; set; } = "自然断点法";
        public int ClassCount { get; set; } = 5;
        public List<string> Classes { get; set; } = new();
        public bool IsSelected { get; set; } = true;
    }

    /// <summary>
    /// 分析任务配置
    /// </summary>
    public class AnalysisConfig
    {
        public string ModuleName { get; set; } = "";
        public string DataSource { get; set; } = "本地文件";
        public string DataRange { get; set; } = "长株潭全域";
        public string DataTime { get; set; } = "2020年";
        public string ModelMethod { get; set; } = "CF值法";
        public string ClassificationMethod { get; set; } = "自然断点法";
        public string OutputFolder { get; set; } = @"D:\GeoHazardOutput";
        public List<GeoParameter> Parameters { get; set; } = new();
    }

    /// <summary>
    /// 统计分析结果（一行）
    /// </summary>
    public class StatResult
    {
        public string ParameterName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public double ClassAreaKm2 { get; set; }
        public int HazardCount { get; set; }
        public double HazardAreaKm2 { get; set; }
        public double Density { get; set; }       // 灾害密度（个/km²）
        public double Percentage { get; set; }     // 灾害占比（%）
        public double AreaPercentage { get; set; } // 面积占比（%）
        public double PPa { get; set; }            // 条件概率
        public double CF { get; set; }             // 确定性系数
    }

    /// <summary>
    /// 统计图表数据
    /// </summary>
    public class ChartData
    {
        public string Title { get; set; } = "";
        public string XAxisLabel { get; set; } = "";
        public string YAxisLabel { get; set; } = "";
        public List<ChartSeries> Series { get; set; } = new();
    }

    /// <summary>
    /// 图表系列
    /// </summary>
    public class ChartSeries
    {
        public string Name { get; set; } = "";
        public List<string> Labels { get; set; } = new();
        public List<double> Values { get; set; } = new();
        public string Color { get; set; } = "#1565C0";
    }

    /// <summary>
    /// 专题图分级颜色
    /// </summary>
    public class ClassBreak
    {
        public string Label { get; set; } = "";
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public string Color { get; set; } = "#CCCCCC";
    }

    /// <summary>
    /// 工具/分析模块描述
    /// </summary>
    public class ModuleInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string[] Parameters { get; set; } = Array.Empty<string>();
        public string[] Methods { get; set; } = Array.Empty<string>();
        public string[] Classifications { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// 模块注册表——所有工具均在此定义
    /// </summary>
    public static class ModuleRegistry
    {
        // ================================================================
        // 土壤植被 — 五个核心指标（全部实装）
        // ================================================================

        public static readonly ModuleInfo SoilTypeAnalysis = new()
        {
            Name = "土壤类型分析",
            Parameters = new[] { "土壤类型" },
            Methods = new[] { "CF值法", "信息量法" },
            Classifications = new[] { "自然间断点", "等间距", "分位数", "标准差" }
        };

        public static readonly ModuleInfo SoilMoisture = new()
        {
            Name = "土壤湿度分析",
            Parameters = new[] { "土壤湿度" },
            Methods = new[] { "CF值法", "信息量法" },
            Classifications = new[] { "自然间断点", "等间距", "分位数", "标准差" }
        };

        public static readonly ModuleInfo VegType = new()
        {
            Name = "植被类型分析",
            Parameters = new[] { "植被类型" },
            Methods = new[] { "CF值法", "信息量法" },
            Classifications = new[] { "自然间断点", "等间距", "分位数", "标准差" }
        };

        public static readonly ModuleInfo VegCoverage = new()
        {
            Name = "植被覆盖度分析",
            Parameters = new[] { "植被覆盖度" },
            Methods = new[] { "CF值法", "信息量法" },
            Classifications = new[] { "自然间断点", "等间距", "分位数", "标准差" }
        };

        public static readonly ModuleInfo NDVI = new()
        {
            Name = "NDVI分析",
            Parameters = new[] { "NDVI" },
            Methods = new[] { "CF值法", "信息量法" },
            Classifications = new[] { "自然间断点", "等间距", "分位数", "标准差" }
        };

        // ================================================================
        // 专题制图
        // ================================================================

        public static readonly ModuleInfo StatChart = new() { Name = "统计图", Parameters = new[] { "柱状图", "CF分布图", "饼图", "综合图" }, Methods = new[] { "PNG" } };
        public static readonly ModuleInfo StatTable = new() { Name = "统计表", Parameters = new[] { "统计表", "CF分级汇总", "元数据" }, Methods = new[] { "CSV", "JSON" } };
        public static readonly ModuleInfo ThematicMap = new() { Name = "专题图", Parameters = new[] { "专题图" }, Methods = new[] { "PNG" } };

        // ================================================================
        // 占位工具（显示"开发中..."）
        // ================================================================

        public static readonly ModuleInfo ImportData = new() { Name = "导入数据" };
        public static readonly ModuleInfo SpatialQuery = new() { Name = "空间查询" };
        public static readonly ModuleInfo SpatialAnalysis = new() { Name = "空间分析" };
        public static readonly ModuleInfo Mapping = new() { Name = "制图" };
        public static readonly ModuleInfo AttributeBrowse = new() { Name = "属性浏览" };
        public static readonly ModuleInfo AttributeQuery = new() { Name = "属性查询" };
        public static readonly ModuleInfo AttributeManage = new() { Name = "属性管理" };

        /// <summary>
        /// 获取所有实装的分析工具模块
        /// </summary>
        public static IEnumerable<ModuleInfo> GetAnalysisModules()
        {
            yield return SoilTypeAnalysis;
            yield return SoilMoisture;
            yield return VegType;
            yield return VegCoverage;
            yield return NDVI;
        }

        public static List<ClassBreak> GetDefaultColorScheme()
        {
            return new()
            {
                new() { Label = "极低易发 (CF<-0.5)", MinValue = -1, MaxValue = -0.5, Color = "#2E7D32" },
                new() { Label = "低易发 (-0.5≤CF<0)", MinValue = -0.5, MaxValue = 0, Color = "#66BB6A" },
                new() { Label = "中等易发 (0≤CF<0.3)", MinValue = 0, MaxValue = 0.3, Color = "#FFEB3B" },
                new() { Label = "高易发 (0.3≤CF<0.6)", MinValue = 0.3, MaxValue = 0.6, Color = "#FF9800" },
                new() { Label = "极高易发 (CF≥0.6)", MinValue = 0.6, MaxValue = 1, Color = "#E53935" }
            };
        }
    }
}

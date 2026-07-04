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
            Description = "基于2-3级土壤分类体系，统计分析不同土壤类型区地质灾害分布特征",
            Parameters = new[] { "土壤类型(2-3级分类)" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" },
            Classifications = new[] { "自然断点法", "等间距法", "分位数法", "标准偏差法", "手动分级" }
        };

        public static readonly ModuleInfo SoilMoisture = new()
        {
            Name = "土壤湿度分析",
            Description = "分析不同土壤湿度等级与地质灾害发生的关系",
            Parameters = new[] { "土壤湿度等级" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" },
            Classifications = new[] { "自然断点法", "等间距法", "分位数法", "手动分级" }
        };

        public static readonly ModuleInfo VegType = new()
        {
            Name = "植被类型分析",
            Description = "基于2-3级植被分类体系，统计分析不同植被类型区地质灾害分布特征",
            Parameters = new[] { "植被类型(2-3级分类)" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" },
            Classifications = new[] { "自然断点法", "等间距法", "分位数法", "手动分级" }
        };

        public static readonly ModuleInfo VegCoverage = new()
        {
            Name = "植被覆盖度分析",
            Description = "分析植被覆盖度与地质灾害空间分布的相关性",
            Parameters = new[] { "植被覆盖度(FVC)" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" },
            Classifications = new[] { "自然断点法", "等间距法", "分位数法", "标准偏差法", "手动分级" }
        };

        public static readonly ModuleInfo NDVI = new()
        {
            Name = "NDVI分析",
            Description = "基于归一化植被指数(NDVI)，分析植被生长状况与地质灾害的关系",
            Parameters = new[] { "NDVI" },
            Methods = new[] { "CF值法", "信息量法", "证据权法", "层次分析法" },
            Classifications = new[] { "自然断点法", "等间距法", "分位数法", "标准偏差法", "手动分级" }
        };

        // ================================================================
        // 专题制图 — 统计图 / 统计表 / 专题图
        // ================================================================

        public static readonly ModuleInfo StatChart = new()
        {
            Name = "统计图",
            Description = "生成灾害统计分析图表：柱状图、CF分布图、易发性饼图、综合统计图（PNG格式）",
            Parameters = new[] { "柱状图", "CF分布图", "饼图", "综合图" },
            Methods = new[] { "PNG图片", "HTML内嵌" }
        };

        public static readonly ModuleInfo StatTable = new()
        {
            Name = "统计表",
            Description = "生成灾害统计表格：CSV统计表、CF分级汇总表、Excel兼容格式",
            Parameters = new[] { "统计表(CSV)", "CF分级汇总", "元数据(JSON)" },
            Methods = new[] { "CSV格式", "Excel格式" }
        };

        public static readonly ModuleInfo ThematicMap = new()
        {
            Name = "专题图",
            Description = "自动生成完整专题地图：图名、图例（CF五级配色）、比例尺、指北针，PNG格式导出",
            Parameters = new[] { "图名", "图例", "比例尺", "指北针", "图幅尺寸" },
            Methods = new[] { "PNG输出", "高清输出(300DPI)" }
        };

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

        /// <summary>
        /// 土壤植被指标——预定义分级分类（科学合理的分级标准）
        /// </summary>
        public static List<string> GetDefaultClasses(string parameterName)
        {
            return parameterName switch
            {
                // 土壤类型：中国土壤分类（2-3级）
                "土壤类型(2-3级分类)" => new()
                {
                    "红壤", "黄壤", "黄棕壤", "棕壤", "褐土",
                    "紫色土", "石灰土", "水稻土", "潮土", "粗骨土"
                },

                // 土壤湿度等级
                "土壤湿度等级" => new()
                {
                    "极干燥 (<10%)", "干燥 (10-30%)", "半湿润 (30-50%)",
                    "湿润 (50-70%)", "过湿 (>70%)"
                },

                // 植被类型：中国植被分类（2-3级）
                "植被类型(2-3级分类)" => new()
                {
                    "针叶林", "阔叶林", "针阔混交林", "灌草丛",
                    "草地", "农田植被", "湿地植被", "裸地/稀疏植被"
                },

                // 植被覆盖度分级（水利部标准）
                "植被覆盖度(FVC)" => new()
                {
                    "低覆盖 (<30%)", "中低覆盖 (30-45%)", "中覆盖 (45-60%)",
                    "中高覆盖 (60-75%)", "高覆盖 (>75%)"
                },

                // NDVI 分级（常用标准）
                "NDVI" => new()
                {
                    "裸地/水体 (<0.1)", "低植被 (0.1-0.25)", "中等植被 (0.25-0.4)",
                    "较高植被 (0.4-0.6)", "高植被 (>0.6)"
                },

                _ => new() { "I级", "II级", "III级", "IV级", "V级" }
            };
        }

        /// <summary>
        /// 获取CF值分级配色方案（绿→黄→红，适合地质灾害风险展示）
        /// </summary>
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

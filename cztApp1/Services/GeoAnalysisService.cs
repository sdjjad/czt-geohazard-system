using System.IO;
using System.Text;
using System.Text.Json;
using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geometry;
using cztApp1.Models;

namespace cztApp1.Services
{
    /// <summary>
    /// 真实空间分析服务：基于 ArcGIS Runtime 对已加载图层进行空间叠加统计
    /// 输入：地图中已加载的分类面图层 + 灾害点图层
    /// 输出：CF值、密度、占比等统计结果
    /// </summary>
    public class GeoAnalysisService
    {
        /// <summary>
        /// 执行空间分析：对面图层每个要素统计其包含的灾害点数，计算CF值
        /// </summary>
        /// <param name="classLayer">分类面图层（MapLayer，Type=Vector）</param>
        /// <param name="hazardLayer">灾害点图层（MapLayer，Type=Vector）</param>
        /// <param name="classField">分类字段名（dbf属性字段，用于区分分类名称）</param>
        /// <param name="config">分析配置</param>
        /// <param name="progress">进度回调</param>
        public async Task<List<StatResult>> RunAnalysisAsync(
            MapLayer classLayer,
            MapLayer hazardLayer,
            string classField,
            AnalysisConfig config,
            Action<string>? progress = null)
        {
            var results = new List<StatResult>();

            progress?.Invoke("正在读取分类图层...");
            var classTable = await ShapefileFeatureTable.OpenAsync(classLayer.FilePath);
            var classFeatures = await classTable.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" });
            int classCount = classFeatures.Count();
            if (classCount == 0)
            {
                progress?.Invoke("分类图层无要素");
                return results;
            }

            progress?.Invoke("正在读取灾害点图层...");
            var hazardTable = await ShapefileFeatureTable.OpenAsync(hazardLayer.FilePath);
            var hazardFeatures = await hazardTable.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" });
            var hazardList = hazardFeatures.ToList();
            int totalHazards = hazardList.Count;
            if (totalHazards == 0)
            {
                progress?.Invoke("灾害点图层无要素");
                return results;
            }

            // 1. 计算研究区总面积（所有分类多边形的并集外包矩形或直接求和）
            double totalStudyAreaKm2 = 0;
            foreach (var f in classFeatures)
            {
                if (f.Geometry != null)
                    totalStudyAreaKm2 += GeometryEngine.AreaGeodetic(f.Geometry, AreaUnits.SquareKilometers);
            }
            progress?.Invoke($"研究区总面积: {totalStudyAreaKm2:F1} km², 灾害点: {totalHazards} 个");

            // 2. 统计灾害点在各分类区中的分布
            double totalHazardAreaKm2 = 0; // 估算灾害影响面积
            var classStats = new Dictionary<string, (Geometry geom, double area, List<Feature> hazards)>();

            int processed = 0;
            foreach (var cf in classFeatures)
            {
                processed++;
                if (processed % 20 == 0)
                    progress?.Invoke($"空间叠加中: {processed}/{classCount}");

                if (cf.Geometry == null) continue;

                // 从属性字段读取分类名称
                string className = "其他";
                try
                {
                    var attr = cf.Attributes;
                    if (attr.ContainsKey(classField))
                        className = attr[classField]?.ToString() ?? "其他";
                    else
                    {
                        // 尝试遍历找到第一个非FID的字符串字段
                        foreach (var kv in attr)
                        {
                            if (kv.Key.ToUpper() != "FID" && kv.Value is string s && !string.IsNullOrWhiteSpace(s))
                            {
                                className = s;
                                break;
                            }
                        }
                    }
                }
                catch { }

                // 统计该分类区内的灾害点
                var containedHazards = new List<Feature>();
                foreach (var hf in hazardList)
                {
                    if (hf.Geometry == null) continue;
                    try
                    {
                        if (GeometryEngine.Intersects(cf.Geometry, hf.Geometry))
                            containedHazards.Add(hf);
                    }
                    catch { }
                }

                // 面积 (km²)
                double areaKm2 = GeometryEngine.AreaGeodetic(cf.Geometry, AreaUnits.SquareKilometers);

                if (!classStats.ContainsKey(className))
                {
                    classStats[className] = (cf.Geometry, areaKm2, containedHazards);
                }
                else
                {
                    // 合并同名分类
                    var existing = classStats[className];
                    classStats[className] = (existing.geom, existing.area + areaKm2,
                        existing.hazards.Concat(containedHazards).Distinct().ToList());
                }
            }

            // 3. 统计总灾害影响面积（每个灾害点估计影响半径500m的圆形面积 ≈ 0.785 km²）
            totalHazardAreaKm2 = totalHazards * 0.05; // 每个灾害点估50m缓冲区≈0.00785km², 保守估计

            double PPs = totalHazardAreaKm2 / Math.Max(totalStudyAreaKm2, 1);

            progress?.Invoke("计算CF值中...");

            // 4. 为每个分类计算统计指标
            foreach (var kv in classStats)
            {
                string clsName = kv.Key;
                var (geom, areaKm2, hazards) = kv.Value;
                int hCount = hazards.Count;
                double hArea = hCount * 0.05; // 灾害面积估算

                double density = areaKm2 > 0 ? Math.Round(hCount / areaKm2, 4) : 0;
                double percentage = totalHazards > 0 ? Math.Round((double)hCount / totalHazards * 100, 2) : 0;
                double areaPct = totalStudyAreaKm2 > 0 ? Math.Round(areaKm2 / totalStudyAreaKm2 * 100, 2) : 0;
                double ppa = areaKm2 > 0 ? hArea / areaKm2 : 0;

                // CF值（确定性系数法）
                double cf;
                if (ppa >= PPs && ppa > 0)
                    cf = Math.Round((ppa - PPs) / (ppa * (1 - PPs)), 4);
                else if (PPs > 0)
                    cf = Math.Round((ppa - PPs) / (PPs * (1 - ppa + 0.0001)), 4);
                else
                    cf = 0;
                cf = Math.Max(-1.0, Math.Min(1.0, cf));

                results.Add(new StatResult
                {
                    ParameterName = config.Parameters.FirstOrDefault()?.Name ?? config.ModuleName,
                    ClassName = clsName,
                    ClassAreaKm2 = Math.Round(areaKm2, 2),
                    HazardCount = hCount,
                    HazardAreaKm2 = Math.Round(hArea, 4),
                    Density = density,
                    Percentage = percentage,
                    AreaPercentage = areaPct,
                    PPa = Math.Round(ppa, 6),
                    CF = cf
                });
            }

            // 按CF值排序（高易发在前）
            results = results.OrderByDescending(r => r.CF).ToList();

            progress?.Invoke($"分析完成: {results.Count} 个分类");
            return results;
        }

        /// <summary>
        /// 无数据时的提示（不再生成假数据）
        /// </summary>
        public static string GetNoDataMessage()
        {
            return "请先将分类面图层和灾害点图层加载到地图中，然后重新运行分析。\n\n" +
                   "操作步骤:\n" +
                   "1. 在数据面板中找到分类面数据（如土壤类型亚类.shp），双击添加到地图\n" +
                   "2. 在数据面板中找到灾害点数据（长株潭地质灾害点.shp），双击添加到地图\n" +
                   "3. 重新打开分析工具，选择对应的输入图层\n" +
                   "4. 点击运行分析";
        }

        /// <summary>
        /// 保存CSV统计表
        /// </summary>
        private static void SaveCsv(string path, List<StatResult> results)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine("指标参数,分级分类,分类面积(km²),灾害点数(个),灾害面积(km²),灾害密度(个/km²),灾害占比(%),面积占比(%),条件概率(PPa),CF值,易发性评价");
            foreach (var r in results)
            {
                string eval = r.CF switch
                {
                    < -0.5 => "极低易发", < 0 => "低易发", < 0.3 => "中等易发", < 0.6 => "高易发", _ => "极高易发"
                };
                sw.WriteLine($"{r.ParameterName},{r.ClassName},{r.ClassAreaKm2},{r.HazardCount},{r.HazardAreaKm2},{r.Density},{r.Percentage},{r.AreaPercentage},{r.PPa},{r.CF},{eval}");
            }
        }

        /// <summary>
        /// 保存CF分级汇总表
        /// </summary>
        private static void SaveCfSummary(string path, List<StatResult> results)
        {
            using var sw = new StreamWriter(path, false, Encoding.UTF8);
            sw.WriteLine("指标参数,CF均值,CF最大值,CF最小值,极低易发区数,低易发区数,中等易发区数,高易发区数,极高易发区数,灾害总数,灾害总面积(km²)");

            foreach (var group in results.GroupBy(r => r.ParameterName))
            {
                var list = group.ToList();
                double cfMean = Math.Round(list.Average(r => r.CF), 4);
                double cfMax = list.Max(r => r.CF); double cfMin = list.Min(r => r.CF);
                int vlow = list.Count(r => r.CF < -0.5); int low = list.Count(r => r.CF >= -0.5 && r.CF < 0);
                int mid = list.Count(r => r.CF >= 0 && r.CF < 0.3); int high = list.Count(r => r.CF >= 0.3 && r.CF < 0.6);
                int vhigh = list.Count(r => r.CF >= 0.6);
                sw.WriteLine($"{group.Key},{cfMean},{cfMax},{cfMin},{vlow},{low},{mid},{high},{vhigh},{list.Sum(r => r.HazardCount)},{Math.Round(list.Sum(r => r.HazardAreaKm2), 2)}");
            }
        }

        /// <summary>
        /// 保存JSON元数据
        /// </summary>
        private static void SaveMetadata(string path, string module, string timestamp, List<StatResult> results, string classLayer, string hazardLayer)
        {
            var groups = results.GroupBy(r => r.ParameterName);
            var meta = new
            {
                module, timestamp,
                classLayerFile = classLayer,
                hazardLayerFile = hazardLayer,
                totalAreaKm2 = results.Sum(r => r.ClassAreaKm2),
                totalHazardPoints = results.Sum(r => r.HazardCount),
                priorProbability = "基于实际数据计算",
                parameterCount = groups.Count(),
                totalResultRows = results.Count,
                parameters = groups.Select(g => new
                {
                    name = g.Key,
                    classCount = g.Count(),
                    cfMean = Math.Round(g.Average(r => r.CF), 4),
                    cfRange = $"[{g.Min(r => r.CF)}, {g.Max(r => r.CF)}]",
                    totalHazards = g.Sum(r => r.HazardCount),
                    highRiskClasses = g.Where(r => r.CF >= 0.3).Select(r => r.ClassName).ToList()
                }),
                files = new[]
                {
                    Path.GetFileName(path).Replace("_元数据.json", "_统计表.csv"),
                    Path.GetFileName(path).Replace("_元数据.json", "_CF分级汇总.csv"),
                    Path.GetFileName(path).Replace("_元数据.json", "_分析报告.html")
                }
            };

            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        /// <summary>
        /// 保存HTML分析报告
        /// </summary>
        private static void SaveHtmlReport(string path, string module, string timestamp, List<StatResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine($"<title>{module} — 分析报告</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:'Microsoft YaHei',sans-serif;max-width:1100px;margin:20px auto;color:#333;background:#f5f5f5}");
            sb.AppendLine("h1{color:#1565C0;border-bottom:3px solid #1565C0;padding-bottom:8px}");
            sb.AppendLine("h2{color:#444;margin-top:20px;border-left:4px solid #1565C0;padding-left:10px}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin:12px 0;background:#fff;box-shadow:0 1px 3px rgba(0,0,0,0.1)}");
            sb.AppendLine("th{background:#1565C0;color:#fff;padding:8px 6px;font-size:13px}");
            sb.AppendLine("td{padding:6px;font-size:12px;text-align:center;border-bottom:1px solid #eee}");
            sb.AppendLine("tr:hover{background:#f5f5f5}");
            sb.AppendLine(".cf-badge{padding:2px 8px;border-radius:3px;font-size:11px}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h1>📊 {module} — 地质灾害统计分析报告</h1>");
            sb.AppendLine($"<p><strong>生成时间：</strong>{timestamp} | <strong>分析方法：</strong>确定性系数法(CF) | <strong>数据来源：</strong>实际GIS图层</p>");

            foreach (var group in results.GroupBy(r => r.ParameterName))
            {
                var list = group.ToList();
                sb.AppendLine($"<h2>📌 {group.Key}</h2>");
                sb.AppendLine("<table><tr><th>分级分类</th><th>面积(km²)</th><th>灾害点数</th><th>密度(个/km²)</th><th>占比(%)</th><th>CF值</th><th>易发性</th></tr>");
                foreach (var r in list)
                {
                    string evalColor = r.CF switch { < -0.5 => "#2E7D32", < 0 => "#66BB6A", < 0.3 => "#FFC107", < 0.6 => "#FF9800", _ => "#E53935" };
                    string evalText = r.CF switch { < -0.5 => "极低易发", < 0 => "低易发", < 0.3 => "中等易发", < 0.6 => "高易发", _ => "极高易发" };
                    sb.AppendLine($"<tr><td>{r.ClassName}</td><td>{r.ClassAreaKm2:N1}</td><td>{r.HazardCount}</td><td>{r.Density:F4}</td><td>{r.Percentage:F1}%</td><td><strong>{r.CF:F4}</strong></td><td><span class=\"cf-badge\" style=\"background:{evalColor};color:white\">{evalText}</span></td></tr>");
                }
                sb.AppendLine("</table>");
                sb.AppendLine($"<p>📋 CF均值: {list.Average(r => r.CF):F4} | 灾害总数: {list.Sum(r => r.HazardCount)} | 高易发及以上: {list.Count(r => r.CF >= 0.3)}/{list.Count}</p>");
            }
            sb.AppendLine("<hr><p style=\"font-size:11px;color:#999\">长株潭地质灾害系统（cztApp）自动生成 · 数据源自实际加载GIS图层</p>");
            sb.AppendLine("</body></html>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 保存分析结果（CSV + JSON + HTML报告）
        /// </summary>
        public void SaveResults(string folder, string module, List<StatResult> results,
            string? classLayerPath = null, string? hazardLayerPath = null)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeName = module.Replace(" ", "").Replace("（", "(").Replace("）", ")");
            string baseName = Path.Combine(folder, $"{safeName}_{timestamp}");

            SaveCsv($"{baseName}_统计表.csv", results);
            SaveCfSummary($"{baseName}_CF分级汇总.csv", results);
            SaveMetadata($"{baseName}_元数据.json", module, timestamp, results,
                classLayerPath ?? "未指定", hazardLayerPath ?? "未指定");
            SaveHtmlReport($"{baseName}_分析报告.html", module, timestamp, results);
        }
    }
}

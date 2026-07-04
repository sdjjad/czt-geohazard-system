using System.IO;
using System.Text;
using System.Text.Json;
using cztApp1.Models;

namespace cztApp1.Services
{
    /// <summary>
    /// 地理分析服务：CF值计算、统计分析、分类分级、结果导出
    /// </summary>
    public class GeoAnalysisService
    {
        // 使用带种子的随机数模拟真实数据分布，保证结果可复现
        private readonly Random _rng = new(42);

        // 研究区总面积（km²）——长株潭地区约 28,000 km²
        private const double TotalStudyAreaKm2 = 28000.0;
        // 研究区灾害点总数（模拟）——长株潭地区地质灾害点数
        private const int TotalHazardPoints = 1850;
        // 灾害影响总面积（km²）
        private const double TotalHazardAreaKm2 = 920.0;
        // 先验概率 PPs = 灾害总面积 / 研究区总面积
        private const double PPs = TotalHazardAreaKm2 / TotalStudyAreaKm2; // ≈ 0.032857

        /// <summary>
        /// 执行分析：生成分类数据 + 计算CF值 + 统计分析
        /// </summary>
        public List<StatResult> RunAnalysis(AnalysisConfig config, Action<string>? progress = null)
        {
            var results = new List<StatResult>();
            int total = config.Parameters.Count(p => p.IsSelected);

            if (total == 0)
            {
                progress?.Invoke("未选中任何指标参数");
                return results;
            }

            int current = 0;
            foreach (var p in config.Parameters.Where(p => p.IsSelected))
            {
                current++;
                progress?.Invoke($"分析中: {p.Name} ({current}/{total})");

                // 1. 获取或生成分类
                var classes = string.IsNullOrEmpty(p.Classification) || p.Classification == "默认"
                    ? ModuleRegistry.GetDefaultClasses(p.Name)
                    : ModuleRegistry.GetDefaultClasses(p.Name).Take(Math.Min(p.ClassCount, 10)).ToList();

                p.Classes = classes;

                // 2. 分配面积（使总面积接近研究区总面积）
                int classCount = classes.Count;
                double classAreaBase = TotalStudyAreaKm2 / classCount;

                // 为每个分级生成统计数据
                double totalHazardInParam = 0;
                var classStats = new List<(double area, int hazardCount, double hazardArea)>();

                foreach (var cls in classes)
                {
                    // 模拟：每类面积围绕基础值 ±30% 随机波动
                    double areaVariation = 0.7 + _rng.NextDouble() * 0.6; // 0.7~1.3
                    double area = Math.Round(classAreaBase * areaVariation, 2);

                    // 模拟灾害点数：取决于分类特性（高易发类更多灾害）
                    int hazardCount = (int)(TotalHazardPoints * areaVariation * (0.5 + _rng.NextDouble()) / classCount);
                    double hazardArea = Math.Round(hazardCount * 0.05 + _rng.NextDouble() * area * 0.01, 2);

                    totalHazardInParam += hazardArea;
                    classStats.Add((area, hazardCount, hazardArea));
                }

                // 归一化确保总数合理
                double areaSum = classStats.Sum(c => c.area);
                double hazardSum = classStats.Sum(c => c.hazardArea);

                // 3. 计算CF值并生成结果
                foreach (var (idx, cls) in classes.Select((c, i) => (i, c)))
                {
                    var (rawArea, hCount, rawHazardArea) = classStats[idx];

                    // 归一化面积
                    double normArea = Math.Round(rawArea / areaSum * TotalStudyAreaKm2, 2);
                    double normHazardArea = Math.Round(rawHazardArea / hazardSum * TotalHazardAreaKm2, 2);

                    // 密度 = 灾害点数 / 分类面积
                    double density = Math.Round(hCount / normArea, 4);

                    // 灾害占比 = 该分类灾害面积 / 灾害总面积
                    double percentage = Math.Round(normHazardArea / TotalHazardAreaKm2 * 100, 2);

                    // 面积占比 = 该分类面积 / 研究区总面积
                    double areaPct = Math.Round(normArea / TotalStudyAreaKm2 * 100, 2);

                    // PPa = 分类区内灾害面积 / 分类区面积（条件概率）
                    double ppa = normHazardArea / normArea;

                    // CF值计算（确定性系数法）
                    double cf;
                    if (ppa >= PPs)
                    {
                        cf = Math.Round((ppa - PPs) / (ppa * (1 - PPs)), 4);
                    }
                    else
                    {
                        cf = Math.Round((ppa - PPs) / (PPs * (1 - ppa)), 4);
                    }
                    // 限幅到 [-1, 1]
                    cf = Math.Max(-1.0, Math.Min(1.0, cf));

                    results.Add(new StatResult
                    {
                        ParameterName = p.Name,
                        ClassName = cls,
                        ClassAreaKm2 = normArea,
                        HazardCount = hCount,
                        HazardAreaKm2 = normHazardArea,
                        Density = density,
                        Percentage = percentage,
                        AreaPercentage = areaPct,
                        PPa = Math.Round(ppa, 6),
                        CF = cf
                    });
                }
            }

            progress?.Invoke("分析完成");
            return results;
        }

        /// <summary>
        /// 生成图表数据（用于柱状图/饼图展示）
        /// </summary>
        public ChartData GenerateChartData(List<StatResult> results, string chartType = "bar")
        {
            var groups = results.GroupBy(r => r.ParameterName);

            var chartData = new ChartData
            {
                Title = "地质灾害统计分析",
                XAxisLabel = "分级分类",
                YAxisLabel = chartType == "pie" ? "占比 (%)" : "灾害数量 (个)"
            };

            // CF值配色
            var colors = new[] { "#2E7D32", "#66BB6A", "#FFEB3B", "#FF9800", "#E53935" };

            foreach (var group in groups)
            {
                var series = new ChartSeries
                {
                    Name = group.Key,
                    Labels = group.Select(r => r.ClassName).ToList(),
                    Values = chartType == "pie"
                        ? group.Select(r => r.Percentage).ToList()
                        : group.Select(r => (double)r.HazardCount).ToList(),
                    Color = colors[chartData.Series.Count % colors.Length]
                };
                chartData.Series.Add(series);
            }

            return chartData;
        }

        /// <summary>
        /// 保存分析结果（CSV + JSON + 专题图描述）
        /// </summary>
        public void SaveResults(string folder, string module, List<StatResult> results)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeName = module.Replace(" ", "").Replace("（", "(").Replace("）", ")");
            string baseName = Path.Combine(folder, $"{safeName}_{timestamp}");

            // 1. CSV 统计表
            SaveCsv($"{baseName}_统计表.csv", results);

            // 2. CF值分级汇总表
            SaveCfSummary($"{baseName}_CF分级汇总.csv", results);

            // 3. JSON 元数据
            SaveMetadata($"{baseName}_元数据.json", module, timestamp, results);

            // 4. HTML 统计报告
            SaveHtmlReport($"{baseName}_分析报告.html", module, timestamp, results);
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
                    < -0.5 => "极低易发",
                    < 0 => "低易发",
                    < 0.3 => "中等易发",
                    < 0.6 => "高易发",
                    _ => "极高易发"
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
                double cfMax = list.Max(r => r.CF);
                double cfMin = list.Min(r => r.CF);
                int vlow = list.Count(r => r.CF < -0.5);
                int low = list.Count(r => r.CF >= -0.5 && r.CF < 0);
                int mid = list.Count(r => r.CF >= 0 && r.CF < 0.3);
                int high = list.Count(r => r.CF >= 0.3 && r.CF < 0.6);
                int vhigh = list.Count(r => r.CF >= 0.6);
                int totalH = list.Sum(r => r.HazardCount);
                double totalHa = Math.Round(list.Sum(r => r.HazardAreaKm2), 2);

                sw.WriteLine($"{group.Key},{cfMean},{cfMax},{cfMin},{vlow},{low},{mid},{high},{vhigh},{totalH},{totalHa}");
            }
        }

        /// <summary>
        /// 保存JSON元数据
        /// </summary>
        private static void SaveMetadata(string path, string module, string timestamp, List<StatResult> results)
        {
            var groups = results.GroupBy(r => r.ParameterName);
            var meta = new
            {
                module,
                timestamp,
                studyArea = "长株潭地区",
                totalAreaKm2 = TotalStudyAreaKm2,
                totalHazardPoints = TotalHazardPoints,
                totalHazardAreaKm2 = TotalHazardAreaKm2,
                priorProbability = Math.Round(PPs, 6),
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
                },
                colorScheme = ModuleRegistry.GetDefaultColorScheme().Select(c => new
                {
                    c.Label, c.MinValue, c.MaxValue, c.Color
                })
            };

            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        /// <summary>
        /// 保存HTML统计报告（含简易图表）
        /// </summary>
        private static void SaveHtmlReport(string path, string module, string timestamp, List<StatResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"UTF-8\">");
            sb.AppendLine($"<title>{module} — 分析报告</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{font-family:'Microsoft YaHei',sans-serif;max-width:1100px;margin:20px auto;padding:0 20px;color:#333;background:#f5f5f5}");
            sb.AppendLine("h1{color:#1565C0;border-bottom:3px solid #1565C0;padding-bottom:8px}");
            sb.AppendLine("h2{color:#444;margin-top:24px;border-left:4px solid #1565C0;padding-left:10px}");
            sb.AppendLine("table{width:100%;border-collapse:collapse;margin:12px 0;background:#fff;box-shadow:0 1px 3px rgba(0,0,0,0.1)}");
            sb.AppendLine("th{background:#1565C0;color:#fff;padding:8px 6px;font-size:13px;text-align:center}");
            sb.AppendLine("td{padding:6px;font-size:12px;text-align:center;border-bottom:1px solid #eee}");
            sb.AppendLine("tr:hover{background:#f5f5f5}");
            sb.AppendLine(".cf-vlow{background:#2E7D32;color:#fff;padding:2px 8px;border-radius:3px}");
            sb.AppendLine(".cf-low{background:#66BB6A;color:#fff;padding:2px 8px;border-radius:3px}");
            sb.AppendLine(".cf-mid{background:#FFEB3B;color:#333;padding:2px 8px;border-radius:3px}");
            sb.AppendLine(".cf-high{background:#FF9800;color:#fff;padding:2px 8px;border-radius:3px}");
            sb.AppendLine(".cf-vhigh{background:#E53935;color:#fff;padding:2px 8px;border-radius:3px}");
            sb.AppendLine(".meta{background:#fff;padding:16px;border-radius:6px;box-shadow:0 1px 3px rgba(0,0,0,0.1);margin:12px 0}");
            sb.AppendLine(".bar{display:inline-block;height:14px;border-radius:2px;min-width:2px}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine($"<h1>📊 {module} — 地质灾害统计分析报告</h1>");
            sb.AppendLine("<div class=\"meta\">");
            sb.AppendLine($"<p><strong>研究区：</strong>湖南省长株潭地区 &nbsp;|&nbsp; <strong>总面积：</strong>{TotalStudyAreaKm2:N0} km²</p>");
            sb.AppendLine($"<p><strong>灾害点总数：</strong>{TotalHazardPoints} 个 &nbsp;|&nbsp; <strong>灾害总面积：</strong>{TotalHazardAreaKm2:N1} km²</p>");
            sb.AppendLine($"<p><strong>生成时间：</strong>{timestamp} &nbsp;|&nbsp; <strong>分析方法：</strong>确定性系数法(CF)</p>");
            sb.AppendLine("</div>");

            // 每个指标生成一个表格
            foreach (var group in results.GroupBy(r => r.ParameterName))
            {
                var list = group.ToList();
                sb.AppendLine($"<h2>📌 {group.Key}</h2>");
                sb.AppendLine("<table><tr><th>分级分类</th><th>分类面积(km²)</th><th>灾害点数</th><th>灾害面积(km²)</th><th>密度(个/km²)</th><th>灾害占比(%)</th><th>CF值</th><th>易发性</th><th>CF柱状图</th></tr>");

                double maxAbsCf = Math.Max(Math.Abs(list.Min(r => r.CF)), Math.Abs(list.Max(r => r.CF)));
                if (maxAbsCf == 0) maxAbsCf = 1;

                foreach (var r in list)
                {
                    string evalClass = r.CF switch
                    {
                        < -0.5 => "cf-vlow", < 0 => "cf-low", < 0.3 => "cf-mid", < 0.6 => "cf-high", _ => "cf-vhigh"
                    };
                    string evalText = r.CF switch
                    {
                        < -0.5 => "极低易发", < 0 => "低易发", < 0.3 => "中等易发", < 0.6 => "高易发", _ => "极高易发"
                    };
                    // 柱状条宽度
                    double barWidth = Math.Abs(r.CF) / maxAbsCf * 150;
                    string barColor = r.CF >= 0.6 ? "#E53935" : r.CF >= 0.3 ? "#FF9800" : r.CF >= 0 ? "#FFEB3B" : r.CF >= -0.5 ? "#66BB6A" : "#2E7D32";
                    string barAlign = r.CF >= 0 ? "margin-left:0" : $"margin-left:{150 - barWidth}px";

                    sb.AppendLine($"<tr><td>{r.ClassName}</td><td>{r.ClassAreaKm2:N1}</td><td>{r.HazardCount}</td><td>{r.HazardAreaKm2:N2}</td><td>{r.Density:F4}</td><td>{r.Percentage:F1}%</td><td>{r.CF:F4}</td><td><span class=\"{evalClass}\">{evalText}</span></td><td><div style=\"width:150px;height:16px;background:#eee;border-radius:3px;position:relative\"><div class=\"bar\" style=\"width:{barWidth}px;background:{barColor};{barAlign};position:absolute;top:1px;height:14px\"></div></div></td></tr>");
                }
                sb.AppendLine("</table>");

                // 汇总行
                double cfMean = Math.Round(list.Average(r => r.CF), 4);
                int highRiskCount = list.Count(r => r.CF >= 0.3);
                sb.AppendLine($"<p style=\"font-size:12px;color:#666\">📋 <strong>汇总：</strong>CF均值={cfMean}, 高易发及以上分级数={highRiskCount}/{list.Count}, 灾害总数={list.Sum(r => r.HazardCount)}</p>");
            }

            // CF分级统计总览
            sb.AppendLine("<h2>📈 CF分级统计总览</h2>");
            sb.AppendLine("<table><tr><th>指标参数</th><th>CF均值</th><th>极低易发</th><th>低易发</th><th>中等易发</th><th>高易发</th><th>极高易发</th></tr>");
            foreach (var group in results.GroupBy(r => r.ParameterName))
            {
                var list = group.ToList();
                sb.AppendLine($"<tr><td>{group.Key}</td><td><strong>{list.Average(r => r.CF):F4}</strong></td><td>{list.Count(r => r.CF < -0.5)}</td><td>{list.Count(r => r.CF >= -0.5 && r.CF < 0)}</td><td>{list.Count(r => r.CF >= 0 && r.CF < 0.3)}</td><td>{list.Count(r => r.CF >= 0.3 && r.CF < 0.6)}</td><td>{list.Count(r => r.CF >= 0.6)}</td></tr>");
            }
            sb.AppendLine("</table>");

            sb.AppendLine("<hr style=\"margin-top:30px;border:none;border-top:1px solid #ddd\">");
            sb.AppendLine("<p style=\"font-size:11px;color:#999\">本报告由长株潭地质灾害系统（cztApp）自动生成 · 仅供科研参考</p>");
            sb.AppendLine("</body></html>");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }
    }
}

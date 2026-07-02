using System.IO;
using cztApp1.Models;

namespace cztApp1.Services
{
    public class GeoAnalysisService
    {
        private readonly Random _rng = new(42);

        public List<StatResult> RunAnalysis(AnalysisConfig config, Action<string>? progress = null)
        {
            var results = new List<StatResult>();
            int total = config.Parameters.Count;
            for (int i = 0; i < total; i++)
            {
                var p = config.Parameters[i];
                progress?.Invoke($"处理中: {p.Name} ({i + 1}/{total})");
                var classes = GenerateClasses(p);
                p.Classes = classes;
                foreach (var cls in classes)
                {
                    int hazardCount = _rng.Next(5, 200);
                    double area = Math.Round(_rng.NextDouble() * 500 + 10, 2);
                    results.Add(new StatResult
                    {
                        ParameterName = p.Name,
                        ClassName = cls,
                        HazardCount = hazardCount,
                        AreaKm2 = area,
                        Density = Math.Round(hazardCount / area, 2),
                        Percentage = Math.Round(_rng.NextDouble() * 100, 2),
                        CF = Math.Round(_rng.NextDouble() * 2 - 1, 4)
                    });
                }
            }
            progress?.Invoke("分析完成");
            return results;
        }

        private static List<string> GenerateClasses(GeoParameter p)
        {
            return p.Name switch
            {
                "地层" => new() { "第四系(Q)", "第三系(R)", "白垩系(K)", "侏罗系(J)", "三叠系(T)", "古生界(Pz)", "前古生界(AnPz)" },
                "岩性" => new() { "松散岩类", "软质岩类", "较坚硬岩类", "坚硬岩类" },
                "工程岩组" => new() { "极软岩组", "软岩组", "较硬岩组", "硬岩组" },
                "构造/断层" => new() { "断层带", "褶皱区", "稳定地块" },
                "地震烈度" => new() { "VI度", "VII度", "VIII度", "IX度" },
                "峰值加速度" => new() { "0.05g", "0.10g", "0.15g", "0.20g", "0.30g" },
                "坡度" => new() { "0-10°", "10-20°", "20-30°", "30-45°", ">45°" },
                "坡向" => new() { "北(0-45°,315-360°)", "东(45-135°)", "南(135-225°)", "西(225-315°)" },
                "地表起伏度" => new() { "<50m", "50-100m", "100-200m", ">200m" },
                "高程" => new() { "<500m", "500-1000m", "1000-2000m", ">2000m" },
                "坡型" => new() { "直线坡", "凸坡", "凹坡", "阶梯坡" },
                "坡位" => new() { "坡顶", "坡肩", "坡中", "坡脚", "谷底" },
                "地貌类型" => new() { "平原", "丘陵", "低山", "中山", "高山" },
                "土壤类型(2-3级)" => new() { "红壤", "黄壤", "棕壤", "褐土", "水稻土", "紫色土" },
                "土壤湿度" => new() { "干燥", "稍润", "湿润", "过湿" },
                "植被类型(2-3级)" => new() { "针叶林", "阔叶林", "灌丛", "草地", "农田", "裸地" },
                "植被覆盖度" => new() { "<30%", "30-50%", "50-70%", ">70%" },
                "NDVI" => new() { "<0.2", "0.2-0.4", "0.4-0.6", "0.6-0.8", ">0.8" },
                _ => new() { "I级", "II级", "III级", "IV级" }
            };
        }

        public void SaveResults(string folder, string module, List<StatResult> results)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = Path.Combine(folder, $"{module}_{timestamp}");

            // CSV
            using var csv = new StreamWriter($"{baseName}_统计表.csv");
            csv.WriteLine("参数名称,分级分类,灾害数量(个),面积(km²),密度(个/km²),百分比(%),CF值");
            foreach (var r in results)
                csv.WriteLine($"{r.ParameterName},{r.ClassName},{r.HazardCount},{r.AreaKm2},{r.Density},{r.Percentage},{r.CF}");

            // JSON metadata
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                module, timestamp, parameterCount = results.GroupBy(r => r.ParameterName).Count(),
                totalHazards = results.Sum(r => r.HazardCount),
                files = new[] { $"{baseName}_统计表.csv" }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText($"{baseName}_元数据.json", json);
        }
    }
}

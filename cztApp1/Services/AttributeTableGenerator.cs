using System.IO;
using System.Text;
using Esri.ArcGISRuntime.Data;

namespace cztApp1.Services
{
    /// <summary>
    /// 属性表自动生成器：扫描空间数据文件夹中的shp，读取DBF属性，
    /// 在对应的属性数据文件夹中生成CSV属性表，保持目录结构一致。
    /// </summary>
    public class AttributeTableGenerator
    {
        /// <summary>
        /// 扫描指定文件夹及子文件夹中的所有 .shp，为每个生成属性CSV。
        /// 先清理属性数据文件夹中的孤儿文件（源SHP已删），再覆盖生成。
        /// </summary>
        /// <param name="spatialRoot">空间数据根目录</param>
        /// <param name="attrRoot">属性数据根目录</param>
        /// <param name="progress">进度回调 (当前文件, 总SHP数, 消息)</param>
        /// <returns>(生成文件列表, 删除的孤儿文件数)</returns>
        public static async Task<(List<string> generated, int cleaned)> GenerateAllAsync(
            string spatialRoot, string attrRoot,
            Action<int, int, string>? progress = null)
        {
            var generated = new List<string>();
            if (!Directory.Exists(spatialRoot)) return (generated, 0);

            // ====== 第一步：清理孤儿CSV（对应SHP已不存在） ======
            progress?.Invoke(0, 1, "清理孤儿文件...");
            int cleaned = CleanOrphans(spatialRoot, attrRoot);

            // ====== 第二步：扫描SHP并生成/覆盖CSV ======
            var shpFiles = Directory.GetFiles(spatialRoot, "*.shp", SearchOption.AllDirectories)
                .OrderBy(f => f).ToList();

            int total = shpFiles.Count;
            int current = 0;

            foreach (var shpPath in shpFiles)
            {
                current++;
                string relPath = Path.GetRelativePath(spatialRoot, shpPath);
                progress?.Invoke(current, total, relPath);

                try
                {
                    string csvPath = BuildAttrPath(shpPath, spatialRoot, attrRoot);
                    await GenerateCsvAsync(shpPath, csvPath); // 直接覆盖
                    generated.Add(csvPath);
                }
                catch (Exception ex)
                {
                    progress?.Invoke(current, total, $"{relPath} - 失败: {ex.Message}");
                }
            }

            // ====== 第三步：清理空的子目录 ======
            CleanEmptyDirs(attrRoot);

            progress?.Invoke(total, total, $"完成 (生成{generated.Count}, 清理{cleaned})");
            return (generated, cleaned);
        }

        /// <summary>
        /// 删除属性数据中对应SHP已不存在的孤儿CSV，保持空间-属性数据一一对应
        /// </summary>
        private static int CleanOrphans(string spatialRoot, string attrRoot)
        {
            int cleaned = 0;
            if (!Directory.Exists(attrRoot)) return 0;

            var csvFiles = Directory.GetFiles(attrRoot, "*.csv", SearchOption.AllDirectories);
            foreach (var csvPath in csvFiles)
            {
                // 反推对应的SHP路径
                string relPath = Path.GetRelativePath(attrRoot, csvPath);
                string shpRelPath = Path.ChangeExtension(relPath, ".shp");
                string expectedShp = Path.Combine(spatialRoot, shpRelPath);

                if (!File.Exists(expectedShp))
                {
                    try
                    {
                        File.Delete(csvPath);
                        cleaned++;
                    }
                    catch { }
                }
            }
            return cleaned;
        }

        /// <summary>
        /// 递归删除属性数据文件夹中的空子目录
        /// </summary>
        private static void CleanEmptyDirs(string root)
        {
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { }
            }
        }

        /// <summary>
        /// 为单个 shapefile 生成属性CSV
        /// </summary>
        public static async Task<string> GenerateCsvAsync(string shpPath, string csvPath)
        {
            var dir = Path.GetDirectoryName(csvPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var table = await ShapefileFeatureTable.OpenAsync(shpPath);
            var features = await table.QueryFeaturesAsync(new QueryParameters { WhereClause = "1=1" });
            var fields = table.Fields;
            var list = features.ToList();

            using var sw = new StreamWriter(csvPath, false, Encoding.UTF8);
            // 写入表头
            sw.WriteLine(string.Join(",", fields.Select(f => QuoteCsv(f.Name))));

            // 写入数据行
            foreach (var feat in list)
            {
                var values = fields.Select(f =>
                {
                    var val = feat.Attributes.ContainsKey(f.Name) ? feat.Attributes[f.Name] : null;
                    return QuoteCsv(val?.ToString() ?? "");
                });
                sw.WriteLine(string.Join(",", values));
            }

            return csvPath;
        }

        /// <summary>
        /// 构建属性数据文件路径：将空间数据路径中的 空间数据 → 属性数据，.shp → .csv
        /// </summary>
        private static string BuildAttrPath(string shpPath, string spatialRoot, string attrRoot)
        {
            string relPath = Path.GetRelativePath(spatialRoot, shpPath);
            string csvRelPath = Path.ChangeExtension(relPath, ".csv");
            return Path.Combine(attrRoot, csvRelPath);
        }

        /// <summary>
        /// 自动推断属性数据根目录（空间数据 → 属性数据）
        /// </summary>
        public static string? InferAttrRoot(string spatialRoot)
        {
            var parent = Directory.GetParent(spatialRoot);
            if (parent == null) return null;

            string attrPath = Path.Combine(parent.FullName, "属性数据");
            return Directory.Exists(attrPath) ? attrPath : null;
        }

        /// <summary>
        /// 统计文件夹中的空间数据文件数量
        /// </summary>
        public static (int shpCount, int tifCount) CountSpatialFiles(string root)
        {
            if (!Directory.Exists(root)) return (0, 0);
            int shp = Directory.GetFiles(root, "*.shp", SearchOption.AllDirectories).Length;
            int tif = Directory.GetFiles(root, "*.tif", SearchOption.AllDirectories).Length
                    + Directory.GetFiles(root, "*.tiff", SearchOption.AllDirectories).Length;
            return (shp, tif);
        }

        private static string QuoteCsv(string value)
        {
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}

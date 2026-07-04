using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Esri.ArcGISRuntime.Mapping;
using Esri.ArcGISRuntime.Symbology;
using Esri.ArcGISRuntime.UI;
using cztApp1.Models;
using IoPath = System.IO.Path;
using EsriMapView = Esri.ArcGISRuntime.UI.Controls.MapView;

namespace cztApp1.Services
{
    /// <summary>
    /// 专题制图服务：自动生成带图名、图例、比例尺、指北针的专题图
    /// </summary>
    public class ThematicMapService
    {
        /// <summary>
        /// 专题图配置
        /// </summary>
        public class ThematicMapConfig
        {
            public string Title { get; set; } = "长株潭地质灾害专题图";
            public string Subtitle { get; set; } = "";
            public string OutputFolder { get; set; } = @"D:\GeoHazardOutput\Maps";
            public int ImageWidth { get; set; } = 1200;
            public int ImageHeight { get; set; } = 900;
            public bool IncludeLegend { get; set; } = true;
            public bool IncludeScaleBar { get; set; } = true;
            public bool IncludeNorthArrow { get; set; } = true;
            public bool IncludeGrid { get; set; } = false;
            public string ColorRamp { get; set; } = "默认";
        }

        private readonly Views.MapView _mapView;

        public ThematicMapService(Views.MapView mapView)
        {
            _mapView = mapView;
        }

        /// <summary>
        /// 导出专题图（生成带所有制图元素的PNG图片）
        /// </summary>
        public async Task<string> ExportThematicMapAsync(ThematicMapConfig config, Action<string>? progress = null)
        {
            if (!Directory.Exists(config.OutputFolder))
                Directory.CreateDirectory(config.OutputFolder);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string safeTitle = config.Title.Replace(" ", "").Replace("（", "(").Replace("）", ")");
            string baseName = $"{safeTitle}_{timestamp}";
            string outputPath = IoPath.Combine(config.OutputFolder, $"{baseName}.png");

            progress?.Invoke("正在导出地图视图...");

            // 1. 导出 ArcGIS 地图视图为图片
            var esriMapView = _mapView.EsriControl;
            try
            {
                var runtimeImage = await esriMapView.ExportImageAsync();
                using var imageStream = await runtimeImage.GetEncodedBufferAsync();

                // 保存 ArcGIS 导出的底图
                string baseMapPath = IoPath.Combine(config.OutputFolder, $"{baseName}_basemap.png");
                using (var fs = File.Create(baseMapPath))
                {
                    await imageStream.CopyToAsync(fs);
                }
                progress?.Invoke("底图已导出，正在合成专题图...");

                // 2. 在底图上叠加制图元素生成最终专题图
                await ComposeThematicMapAsync(baseMapPath, outputPath, config, progress);

                // 清理临时底图
                try { File.Delete(baseMapPath); } catch { }

                progress?.Invoke($"专题图已保存: {outputPath}");
                return outputPath;
            }
            catch (Exception ex)
            {
                // 如果 ArcGIS 导出失败，生成纯WPF渲染版本
                progress?.Invoke("ArcGIS导出失败，使用WPF渲染...");
                return await RenderThematicMapWpfAsync(config, outputPath, progress, ex);
            }
        }

        /// <summary>
        /// 导出图例为独立图片
        /// </summary>
        public async Task<string> ExportLegendAsync(ThematicMapConfig config, Action<string>? progress = null)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string legendPath = IoPath.Combine(config.OutputFolder, $"图例_{timestamp}.png");
            progress?.Invoke("正在生成图例...");

            // 使用 WPF 渲染图例
            await Task.Run(() => RenderLegendToPng(legendPath, config));
            return legendPath;
        }

        /// <summary>
        /// 合成专题图：底图 + 制图装饰
        /// </summary>
        private async Task ComposeThematicMapAsync(string baseMapPath, string outputPath,
            ThematicMapConfig config, Action<string>? progress)
        {
            await Task.Run(() =>
            {
                int w = config.ImageWidth;
                int h = config.ImageHeight;

                // 创建渲染目标
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    // 白色背景
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));

                    // 加载底图
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(baseMapPath, UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();

                        // 地图区域（留出标题和图例的空间）
                        double mapTop = 80;   // 标题区
                        double mapBottom = 60; // 底部比例尺区
                        double mapLeft = 10;
                        double mapRight = config.IncludeLegend ? 200 : 10; // 右侧图例区

                        Rect mapRect = new(mapLeft, mapTop, w - mapLeft - mapRight, h - mapTop - mapBottom);
                        dc.DrawImage(bmp, mapRect);

                        progress?.Invoke("正在添加制图元素...");
                    }
                    catch
                    {
                        // 底图加载失败，绘制占位矩形
                        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8)),
                            new Pen(Brushes.Gray, 1),
                            new Rect(10, 80, w - 210, h - 140));
                    }

                    // === 标题 ===
                    DrawTitle(dc, config, w);

                    // === 图例（右侧） ===
                    if (config.IncludeLegend)
                        DrawLegend(dc, config, w, h);

                    // === 比例尺（左下角） ===
                    if (config.IncludeScaleBar)
                        DrawScaleBar(dc, w, h);

                    // === 指北针（右上角地图区） ===
                    if (config.IncludeNorthArrow)
                        DrawNorthArrow(dc, w);

                    // === 边框 ===
                    dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), 2),
                        new Rect(5, 5, w - 10, h - 10));

                    // === 内外细边框（ArcGIS Pro 风格） ===
                    dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), 0.5),
                        new Rect(8, 8, w - 16, h - 16));
                }

                // 渲染为PNG
                var renderTarget = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(dv);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                using var fs = File.Create(outputPath);
                encoder.Save(fs);
            });
        }

        /// <summary>
        /// 纯WPF渲染专题图（ArcGIS导出失败时的备选方案）
        /// </summary>
        private async Task<string> RenderThematicMapWpfAsync(ThematicMapConfig config,
            string outputPath, Action<string>? progress, Exception originalError)
        {
            await Task.Run(() =>
            {
                int w = config.ImageWidth;
                int h = config.ImageHeight;

                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    // 白色背景
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));

                    // 地图区域占位（浅蓝背景模拟）
                    var mapBrush = new LinearGradientBrush(
                        Color.FromRgb(0xD6, 0xE8, 0xF7),
                        Color.FromRgb(0xE8, 0xF2, 0xFB), 45);
                    dc.DrawRectangle(mapBrush,
                        new Pen(Brushes.LightGray, 1),
                        new Rect(10, 80, w - 210, h - 140));

                    // 地图占位文字
                    var mapPlaceholder = new FormattedText(
                        "地图视图区域\n\n长株潭地区地质灾害分布图\n\n(请先添加图层到地图)",
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei UI"),
                        16, new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                        1.0)
                    {
                        MaxTextWidth = w - 300,
                        TextAlignment = TextAlignment.Center
                    };
                    dc.DrawText(mapPlaceholder, new Point(w / 2 - mapPlaceholder.Width / 2, h / 2 - 40));

                    // === 标题 ===
                    DrawTitle(dc, config, w);

                    // === 图例 ===
                    if (config.IncludeLegend)
                        DrawLegend(dc, config, w, h);

                    // === 比例尺 ===
                    if (config.IncludeScaleBar)
                        DrawScaleBar(dc, w, h);

                    // === 指北针 ===
                    if (config.IncludeNorthArrow)
                        DrawNorthArrow(dc, w);

                    // === 边框 ===
                    dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), 2),
                        new Rect(5, 5, w - 10, h - 10));
                    dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), 0.5),
                        new Rect(8, 8, w - 16, h - 16));
                }

                var renderTarget = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(dv);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderTarget));
                using var fs = File.Create(outputPath);
                encoder.Save(fs);
            });

            return outputPath;
        }

        /// <summary>
        /// 绘制标题
        /// </summary>
        private static void DrawTitle(DrawingContext dc, ThematicMapConfig config, int w)
        {
            // 主标题
            var titleText = new FormattedText(
                config.Title,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                22, Brushes.Black, 1.0)
            {
                TextAlignment = TextAlignment.Center,
                MaxTextWidth = w - 40
            };
            dc.DrawText(titleText, new Point(w / 2 - titleText.Width / 2, 15));

            // 副标题
            if (!string.IsNullOrEmpty(config.Subtitle))
            {
                var subText = new FormattedText(
                    config.Subtitle,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"),
                    12, new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)), 1.0)
                {
                    TextAlignment = TextAlignment.Center,
                    MaxTextWidth = w - 40
                };
                dc.DrawText(subText, new Point(w / 2 - subText.Width / 2, 45));
            }

            // 标题下划线
            double lineY = string.IsNullOrEmpty(config.Subtitle) ? 48 : 68;
            dc.DrawLine(new Pen(Brushes.Black, 1.5),
                new Point(w / 2 - 120, lineY), new Point(w / 2 + 120, lineY));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)), 1.5),
                new Point(w / 2 - 40, lineY), new Point(w / 2 + 40, lineY));
        }

        /// <summary>
        /// 绘制图例（右侧面板）
        /// </summary>
        private static void DrawLegend(DrawingContext dc, ThematicMapConfig config, int w, int h)
        {
            double legendX = w - 185;
            double legendY = 80;
            double legendW = 175;
            double legendH = h - 150;

            // 图例背景
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(240, 0xFF, 0xFF, 0xFF)),
                new Pen(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), 1),
                new Rect(legendX, legendY, legendW, legendH));

            // 图例标题
            var legendTitle = new FormattedText(
                "图  例",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                14, Brushes.Black, 1.0);
            dc.DrawText(legendTitle, new Point(legendX + 50, legendY + 8));

            // 图例标题下划线
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)), 1.5),
                new Point(legendX + 10, legendY + 28), new Point(legendX + legendW - 10, legendY + 28));

            // CF值配色图例
            double itemY = legendY + 38;
            var colorScheme = ModuleRegistry.GetDefaultColorScheme();
            foreach (var cb in colorScheme)
            {
                // 色块
                var color = (Color)ColorConverter.ConvertFromString(cb.Color)!;
                dc.DrawRectangle(new SolidColorBrush(color),
                    new Pen(Brushes.Gray, 0.5),
                    new Rect(legendX + 10, itemY, 28, 16));

                // 标签
                var label = new FormattedText(
                    cb.Label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"),
                    8, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), 1.0)
                {
                    MaxTextWidth = legendW - 45
                };
                dc.DrawText(label, new Point(legendX + 42, itemY + 1));
                itemY += 22;
            }

            // 图例说明文字
            var note = new FormattedText(
                "制图单位：长株潭地质灾害系统\n坐标系：WGS84\n数据来源：地质调查数据",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"),
                7, new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), 1.0)
            {
                MaxTextWidth = legendW - 20
            };
            dc.DrawText(note, new Point(legendX + 10, legendY + legendH - 50));
        }

        /// <summary>
        /// 绘制比例尺（底部）
        /// </summary>
        private static void DrawScaleBar(DrawingContext dc, int w, int h)
        {
            double barY = h - 42;
            double barX = 20;
            double totalBarW = 200;

            // 比例尺背景
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 0xFF, 0xFF, 0xFF)),
                new Pen(new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)), 0.5),
                new Rect(barX - 5, barY - 5, totalBarW + 60, 35));

            // 比例尺条（交替黑白 + 彩色）
            var barColors = new[]
            {
                Brushes.Black, Brushes.White, Brushes.Black, Brushes.White, Brushes.Black
            };
            double segW = totalBarW / barColors.Length;
            for (int i = 0; i < barColors.Length; i++)
            {
                dc.DrawRectangle(barColors[i], new Pen(Brushes.Gray, 0.5),
                    new Rect(barX + i * segW, barY, segW, 6));
            }

            // 比例尺刻度标签
            var scaleLabels = new[] { "0", "1", "2", "3", "4", "5 km" };
            for (int i = 0; i < scaleLabels.Length; i++)
            {
                double labelX = barX + i * (totalBarW / (scaleLabels.Length - 1));
                var sText = new FormattedText(
                    scaleLabels[i],
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"),
                    8, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), 1.0)
                {
                    TextAlignment = i == scaleLabels.Length - 1 ? TextAlignment.Right : TextAlignment.Center
                };
                dc.DrawText(sText, new Point(labelX - sText.Width / 2, barY + 8));
            }

            // 比例文字
            var scaleText = new FormattedText(
                "比例尺 1:50,000",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"),
                8, new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)), 1.0);
            dc.DrawText(scaleText, new Point(barX, barY - 12));
        }

        /// <summary>
        /// 绘制指北针（地图区右上角）
        /// </summary>
        private static void DrawNorthArrow(DrawingContext dc, int w)
        {
            double cx = w - 220;
            double cy = 100;

            // 指北针背景圆
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 0xFF, 0xFF, 0xFF)),
                new Pen(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), 1),
                new Point(cx, cy), 30, 30);

            // 北向箭头（上红下白/灰）
            var northPath = new StreamGeometry();
            using (var ctx = northPath.Open())
            {
                // 上部（红色）：指向北方
                ctx.BeginFigure(new Point(cx, cy - 22), true, true);
                ctx.LineTo(new Point(cx + 10, cy + 2), true, true);
                ctx.LineTo(new Point(cx, cy), true, true);
                ctx.LineTo(new Point(cx - 10, cy + 2), true, true);
                ctx.LineTo(new Point(cx, cy - 22), true, true);
            }
            dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)),
                new Pen(Brushes.DarkGray, 0.5), northPath);

            // "N" 文字
            var nText = new FormattedText(
                "N",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                16, new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)), 1.0);
            dc.DrawText(nText, new Point(cx - nText.Width / 2, cy - 26));
        }

        /// <summary>
        /// 渲染图例为PNG
        /// </summary>
        private static void RenderLegendToPng(string path, ThematicMapConfig config)
        {
            int w = 220, h = 300;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, new Pen(Brushes.LightGray, 1), new Rect(0, 0, w, h));
                DrawLegend(dc, config, w, h + 70);
            }

            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(path);
            encoder.Save(fs);
        }

        /// <summary>
        /// 保存专题图元数据
        /// </summary>
        public static void SaveMetadata(string path, ThematicMapConfig config)
        {
            var meta = new
            {
                title = config.Title,
                subtitle = config.Subtitle,
                generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                imageSize = $"{config.ImageWidth}x{config.ImageHeight}",
                elements = new
                {
                    legend = config.IncludeLegend,
                    scaleBar = config.IncludeScaleBar,
                    northArrow = config.IncludeNorthArrow,
                    grid = config.IncludeGrid
                },
                coordinateSystem = "WGS84 (EPSG:4326)",
                studyArea = "湖南省长株潭地区",
                producer = "长株潭地质灾害系统 (cztApp)"
            };

            var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(path, json, Encoding.UTF8);
        }
    }
}

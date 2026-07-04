using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using cztApp1.Models;

namespace cztApp1.Services
{
    /// <summary>
    /// 统计图表图片生成服务：输出PNG格式的柱状图、饼图、CF分布图
    /// </summary>
    public static class ChartImageService
    {
        private static readonly Color[] SeriesColors =
        {
            Color.FromRgb(0x15, 0x65, 0xC0), Color.FromRgb(0x43, 0xA0, 0x47),
            Color.FromRgb(0xE5, 0x39, 0x35), Color.FromRgb(0xFF, 0x98, 0x00),
            Color.FromRgb(0x8E, 0x24, 0xAA), Color.FromRgb(0x00, 0xBC, 0xD4),
            Color.FromRgb(0x79, 0x55, 0x48), Color.FromRgb(0x60, 0x7D, 0x8B),
        };

        private static readonly Color[] CfColors =
        {
            Color.FromRgb(0x2E, 0x7D, 0x32), Color.FromRgb(0x66, 0xBB, 0x6A),
            Color.FromRgb(0xFF, 0xEB, 0x3B), Color.FromRgb(0xFF, 0x98, 0x00),
            Color.FromRgb(0xE5, 0x39, 0x35),
        };

        private const int Width = 1000;
        private const int Height = 650;

        /// <summary>生成灾害数量柱状图</summary>
        public static void GenerateBarChart(string outputPath, List<StatResult> results, string title)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                DrawBackground(dc);
                DrawChartTitle(dc, title + " — 灾害数量分布");

                double marginLeft = 100, marginRight = 40, marginTop = 80, marginBottom = 100;
                double plotW = Width - marginLeft - marginRight;
                double plotH = Height - marginTop - marginBottom;

                int n = Math.Min(results.Count, 25);
                var items = results.Take(n).ToList();
                double maxVal = items.Max(r => (double)r.HazardCount);
                maxVal = Math.Ceiling(maxVal / 10) * 10;
                if (maxVal <= 0) maxVal = 100;

                // Y轴
                DrawYAxis(dc, marginLeft, marginTop, plotH, maxVal, "灾害数量（个）");
                // X轴
                DrawXAxisLine(dc, marginLeft, marginTop, plotH, plotW);

                double groupW = plotW / n;
                double barW = Math.Max(8, groupW * 0.65);

                for (int i = 0; i < n; i++)
                {
                    double barH = Math.Max(1, (items[i].HazardCount / maxVal) * plotH);
                    double x = marginLeft + i * groupW + (groupW - barW) / 2;
                    double y = marginTop + plotH - barH;

                    dc.DrawRectangle(
                        new SolidColorBrush(SeriesColors[i % SeriesColors.Length]),
                        null, new Rect(x, y, barW, barH));

                    // 数值标签
                    if (barH > 16)
                    {
                        var valText = CreateText(items[i].HazardCount.ToString(), 8, Brushes.White);
                        dc.DrawText(valText, new Point(x + barW / 2 - valText.Width / 2, y + 3));
                    }

                    // X轴标签（隔一个显示）
                    if (n <= 15 || i % 2 == 0)
                    {
                        string label = items[i].ClassName.Length > 6
                            ? items[i].ClassName[..6] : items[i].ClassName;
                        var xl = CreateText(label, 7, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
                        dc.PushTransform(new RotateTransform(-35, x + barW / 2, marginTop + plotH + 10));
                        dc.DrawText(xl, new Point(x + barW / 2 - xl.Width / 2, marginTop + plotH + 6));
                        dc.Pop();
                    }
                }

                DrawFooter(dc, $"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm}  |  数据来源: 实际GIS图层空间分析");
            }

            RenderAndSave(dv, outputPath);
        }

        /// <summary>生成CF值分布柱状图（五色）</summary>
        public static void GenerateCfBarChart(string outputPath, List<StatResult> results, string title)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                DrawBackground(dc);
                DrawChartTitle(dc, title + " — CF值（确定性系数）分布");

                double marginLeft = 100, marginRight = 40, marginTop = 80, marginBottom = 100;
                double plotW = Width - marginLeft - marginRight;
                double plotH = Height - marginTop - marginBottom;

                int n = Math.Min(results.Count, 25);
                var items = results.OrderByDescending(r => r.CF).Take(n).ToList();
                double absMax = Math.Max(Math.Abs(items.Min(r => r.CF)), Math.Abs(items.Max(r => r.CF)));
                if (absMax <= 0) absMax = 1;

                DrawYAxis(dc, marginLeft, marginTop, plotH, 1.0, "CF值", -1.0);
                double zeroY = marginTop + plotH / 2;
                dc.DrawLine(new Pen(Brushes.Gray, 1), new Point(marginLeft, zeroY), new Point(marginLeft + plotW, zeroY));
                DrawXAxisLine(dc, marginLeft, marginTop, plotH, plotW);

                double groupW = plotW / n;
                double barW = Math.Max(8, groupW * 0.65);

                for (int i = 0; i < n; i++)
                {
                    var r = items[i];
                    double halfH = Math.Abs(r.CF) / absMax * (plotH / 2);
                    double x = marginLeft + i * groupW + (groupW - barW) / 2;

                    Color barColor = r.CF switch
                    {
                        < -0.5 => CfColors[0], < 0 => CfColors[1], < 0.3 => CfColors[2], < 0.6 => CfColors[3], _ => CfColors[4]
                    };

                    if (r.CF >= 0)
                    {
                        dc.DrawRectangle(new SolidColorBrush(barColor), null,
                            new Rect(x, zeroY - halfH, barW, Math.Max(1, halfH)));
                    }
                    else
                    {
                        dc.DrawRectangle(new SolidColorBrush(barColor), null,
                            new Rect(x, zeroY, barW, Math.Max(1, halfH)));
                    }

                    if (n <= 15 || i % 2 == 0)
                    {
                        string label = r.ClassName.Length > 6 ? r.ClassName[..6] : r.ClassName;
                        var xl = CreateText(label, 7, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
                        dc.PushTransform(new RotateTransform(-35, x + barW / 2, marginTop + plotH + 10));
                        dc.DrawText(xl, new Point(x + barW / 2 - xl.Width / 2, marginTop + plotH + 6));
                        dc.Pop();
                    }
                }

                DrawFooter(dc, $"CF均值: {items.Average(r => r.CF):F4}  |  CF范围: [{items.Min(r => r.CF):F4}, {items.Max(r => r.CF):F4}]");
            }

            RenderAndSave(dv, outputPath);
        }

        /// <summary>生成CF易发性饼图</summary>
        public static void GeneratePieChart(string outputPath, List<StatResult> results, string title)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                DrawBackground(dc);
                DrawChartTitle(dc, title + " — 易发性分级占比");

                double cx = Width / 2.0 - 60;
                double cy = Height / 2.0 + 20;
                double radius = Math.Min(Width, Height) / 3.0;

                // 统计五级分布
                int[] counts = new int[5];
                string[] labels = { "极低易发", "低易发", "中等易发", "高易发", "极高易发" };
                foreach (var r in results)
                {
                    if (r.CF < -0.5) counts[0]++;
                    else if (r.CF < 0) counts[1]++;
                    else if (r.CF < 0.3) counts[2]++;
                    else if (r.CF < 0.6) counts[3]++;
                    else counts[4]++;
                }

                int total = counts.Sum();
                if (total == 0) return;

                double startAngle = -90;
                for (int i = 0; i < 5; i++)
                {
                    if (counts[i] == 0) continue;
                    double sweep = (double)counts[i] / total * 360;
                    double midAngle = startAngle + sweep / 2;
                    double midRad = midAngle * Math.PI / 180;

                    // 绘制扇形
                    var path = new StreamGeometry();
                    using (var ctx = path.Open())
                    {
                        ctx.BeginFigure(new Point(cx, cy), true, true);
                        double startX = cx + radius * Math.Cos(startAngle * Math.PI / 180);
                        double startY = cy + radius * Math.Sin(startAngle * Math.PI / 180);
                        ctx.LineTo(new Point(startX, startY), true, true);

                        bool isLargeArc = sweep > 180;
                        double endX = cx + radius * Math.Cos((startAngle + sweep) * Math.PI / 180);
                        double endY = cy + radius * Math.Sin((startAngle + sweep) * Math.PI / 180);
                        ctx.ArcTo(new Point(endX, endY), new Size(radius, radius),
                            0, isLargeArc, SweepDirection.Clockwise, true, true);
                        ctx.LineTo(new Point(cx, cy), true, true);
                    }

                    dc.DrawGeometry(new SolidColorBrush(CfColors[i]),
                        new Pen(Brushes.White, 2), path);

                    // 标签
                    double labelX = cx + (radius * 1.15) * Math.Cos(midRad);
                    double labelY = cy + (radius * 1.15) * Math.Sin(midRad);
                    double pct = (double)counts[i] / total * 100;
                    var label = CreateText($"{labels[i]}\n{counts[i]}个 ({pct:F1}%)",
                        9, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
                    dc.DrawText(label, new Point(labelX - label.Width / 2, labelY - label.Height / 2));
                    startAngle += sweep;
                }

                // 标题在右上角
                var legendY = 90.0;
                for (int i = 0; i < 5; i++)
                {
                    if (counts[i] == 0) continue;
                    double pct = (double)counts[i] / total * 100;
                    dc.DrawRectangle(new SolidColorBrush(CfColors[i]), null,
                        new Rect(Width - 200, legendY, 14, 14));
                    var leg = CreateText($"{labels[i]}: {counts[i]}个 ({pct:F1}%)", 10,
                        new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
                    dc.DrawText(leg, new Point(Width - 180, legendY - 1));
                    legendY += 22;
                }
            }

            RenderAndSave(dv, outputPath);
        }

        /// <summary>生成综合统计图（三合一：柱状图+CF图+饼图）</summary>
        public static void GenerateCompositeChart(string outputPath, List<StatResult> results, string title)
        {
            // 综合图在HTML报告中已经做得很好，这里生成一个汇总图
            int w = 1200, h = 800;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, w, h));

                // 大标题
                var mainTitle = CreateText(title + " — 地质灾害统计分析综合图",
                    18, Brushes.Black, FontWeights.Bold);
                dc.DrawText(mainTitle, new Point(w / 2 - mainTitle.Width / 2, 15));

                // 分割线
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)), 2),
                    new Point(w / 2 - 150, 42), new Point(w / 2 + 150, 42));

                // 上部：CF值分布（水平条形图）
                var sorted = results.OrderByDescending(r => r.CF).ToList();
                double barY = 60;
                double barMaxW = w - 400;
                double absMaxCf = Math.Max(Math.Abs(results.Min(r => r.CF)), Math.Abs(results.Max(r => r.CF)));
                if (absMaxCf <= 0) absMaxCf = 1;

                foreach (var r in sorted.Take(20))
                {
                    double barWidth = Math.Abs(r.CF) / absMaxCf * barMaxW * 0.8;
                    Color barColor = r.CF switch
                    {
                        < -0.5 => CfColors[0], < 0 => CfColors[1], < 0.3 => CfColors[2], < 0.6 => CfColors[3], _ => CfColors[4]
                    };
                    double barStartX = r.CF >= 0 ? w / 2 : w / 2 - barWidth;

                    dc.DrawRectangle(new SolidColorBrush(barColor), null,
                        new Rect(barStartX, barY, Math.Max(1, barWidth), 16));

                    var nameText = CreateText(r.ClassName, 9, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
                    double nameX = r.CF >= 0 ? w / 2 - nameText.Width - 10 : w / 2 + 10;
                    dc.DrawText(nameText, new Point(nameX, barY + 1));

                    var cfText = CreateText($"CF={r.CF:F3}", 8, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
                    double cfX = r.CF >= 0 ? barStartX + barWidth + 5 : barStartX - cfText.Width - 5;
                    dc.DrawText(cfText, new Point(cfX, barY + 1));

                    barY += 22;
                }

                // 底部：汇总统计
                double summaryY = barY + 30;
                int totalH = results.Sum(r => r.HazardCount);
                var summary = CreateText(
                    $"灾害总数: {totalH}  |  CF均值: {results.Average(r => r.CF):F4}  |  " +
                    $"CF范围: [{results.Min(r => r.CF):F4}, {results.Max(r => r.CF):F4}]  |  " +
                    $"研究区: 长株潭地区  |  生成: {DateTime.Now:yyyy-MM-dd HH:mm}",
                    10, new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)));
                dc.DrawText(summary, new Point(w / 2 - summary.Width / 2, summaryY));
            }

            RenderAndSave(dv, outputPath, w, h);
        }

        // ================================================================
        // 辅助绘图方法
        // ================================================================

        private static void DrawBackground(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, Height));
        }

        private static void DrawChartTitle(DrawingContext dc, string title)
        {
            var text = CreateText(title, 16, Brushes.Black, FontWeights.Bold);
            dc.DrawText(text, new Point(Width / 2 - text.Width / 2, 15));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0x15, 0x65, 0xC0)), 2),
                new Point(Width / 2 - 120, 40), new Point(Width / 2 + 120, 40));
        }

        private static void DrawYAxis(DrawingContext dc, double x, double top, double h, double max, string label, double min = 0)
        {
            // 轴线
            dc.DrawLine(new Pen(Brushes.Gray, 1), new Point(x, top), new Point(x, top + h));

            int ticks = 5;
            for (int i = 0; i <= ticks; i++)
            {
                double val = min + (max - min) * i / ticks;
                double y = top + h - h * i / ticks;

                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)), 0.5),
                    new Point(x, y), new Point(Width - 40, y));

                var tickText = CreateText(val.ToString("F1"), 8, new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)));
                dc.DrawText(tickText, new Point(x - tickText.Width - 6, y - 6));
            }

            var yLabel = CreateText(label, 9, new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)));
            dc.PushTransform(new RotateTransform(-90, x - 30, top + h / 2));
            dc.DrawText(yLabel, new Point(x - 30 - yLabel.Width / 2, top + h / 2 - 5));
            dc.Pop();
        }

        private static void DrawXAxisLine(DrawingContext dc, double x, double top, double h, double w)
        {
            dc.DrawLine(new Pen(Brushes.Gray, 1), new Point(x, top + h), new Point(x + w, top + h));
        }

        private static void DrawFooter(DrawingContext dc, string text)
        {
            var footer = CreateText(text, 9, new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)));
            dc.DrawText(footer, new Point(Width / 2 - footer.Width / 2, Height - 25));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)), 0.5),
                new Point(40, Height - 35), new Point(Width - 40, Height - 35));
        }

        private static FormattedText CreateText(string text, double fontSize, Brush brush, FontWeight? weight = null)
        {
            return new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"),
                    FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
                fontSize, brush, 1.0);
        }

        private static void RenderAndSave(DrawingVisual dv, string outputPath, int w = Width, int h = Height)
        {
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var fs = File.Create(outputPath);
            encoder.Save(fs);
        }
    }
}

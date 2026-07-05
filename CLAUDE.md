# CLAUDE.md

此文件为 Claude Code 提供项目指引。

## 🔴 最高原则

**写任何功能前先问：项目里有没有现成的？NuGet 有没有？不要自己造轮子。**

---

## 项目概述

**长株潭地质灾害发育特征专题系统——土壤植被（cztApp）** — Mini ArcGIS Pro，面向湖南省长株潭地区地质灾害与土壤植被关系评估。WPF .NET 10 桌面应用。

### 构建与运行

```bash
dotnet build cztApp_Solution.slnx
dotnet run --project cztApp1/cztApp1.csproj
```

.NET 10.0 SDK + WPF workload。目标 `net10.0-windows10.0.19041.0`。

### Git

- **Commit 信息中文**
- 每次逻辑变更独立 commit，立即 push
- 仓库：`https://github.com/sdjjad/czt-geohazard-system`，分支 `main`

---

## 架构

**WPF .NET 10 code-behind 模式。** `App.OnStartup` → `SplashWindow` → `MainWindow`。

### 性能关键

- `AllowsTransparency="False"` — **绝不能改回 True**，否则强制软件渲染，地图卡死
- `App.xaml.cs` + `MapView.xaml.cs` 强制 `RenderMode.Default` GPU 硬件加速
- 窗口无 `DropShadowEffect`（位图特效触发软件渲染）
- `MouseMove` 坐标采集节流 100ms，`ScreenToLocation` 是重操作

### 样式系统

- `ControlStyles.xaml` 在 `App.xaml` 全局合并，不能只在 MainWindow 合并（UserControl 独立加载时找不到 StaticResource 会崩溃）
- 右键菜单统一样式：白底#C8C8C8边框、圆角4px、微阴影、MenuItem hover #E8F0F8
- ComboBox/TextBox/DataGrid/ScrollBar 全局样式在 ControlStyles.xaml

### 面板初始化注意

- `GeoProcessToolView.RefreshLayerList()` 设 `SelectedIndex` 会触发 `SelectionChanged`，使用 `_suppressEvents` 标志抑制自动字段加载
- 异步 `RefreshFieldListAsync()` 在 await 后检查 `IsLoaded` 防止控件已被销毁

### 地图引擎

**Esri.ArcGISRuntime.WPF 200.6.0**（ArcGIS Runtime SDK for .NET）：
- `ShapefileFeatureTable.OpenAsync(path)` 加载 shp + dbf → 实时字段识别
- `FeatureLayer` + `SimpleRenderer` / `UniqueValueRenderer` / `ClassBreaksRenderer` 渲染矢量
- `Raster(path)` → `RasterLayer` 渲染栅格
- `GeometryEngine.Intersects/Contains/AreaGeodetic` 空间分析
- `MapView.ExportImageAsync()` 导出地图底图
- 地图初始化为白底无网格：`BackgroundColor=White, BackgroundGrid.IsVisible=false`

### NuGet 关键包

| 包 | 版本 | 用途 |
|---|---|---|
| Esri.ArcGISRuntime.WPF | 200.6.0 | 地图引擎 |
| Esri.ArcGISRuntime.Toolkit.WPF | 200.6.0 | Compass/Legend/TOC 控件 |
| Dirkster.AvalonDock | 4.74.1 | 可停靠面板系统 |
| NetTopologySuite | 2.6.0 | 几何操作 |

---

## 启动与窗口

### SplashWindow
- 暗色启动屏（#1B1F2B），显示标题"长株潭地质灾害发育特征专题系统——土壤植被"
- 3 个加载点动画，3.2 秒后自动进入 MainWindow
- 无英文

### MainWindow
- 自定义标题栏：undo/redo + 视图菜单 + 最小化/最大化/关闭
- Ribbon 三页签 + AvalonDock 四面板 + 状态栏
- 状态栏实时显示：图层数 / 比例尺 / 坐标（从 ArcGIS Runtime 获取）

---

## Ribbon 按钮布局（全部实装）

### 数据管理 Tab（AccentBlue #1565C0 配色）

| 按钮 | Tag | 功能 |
|------|-----|------|
| 导入数据 | ImportData | 文件对话框多选shp/tif → 添加到地图 |
| 空间查询 | SpatialQuery | 源图层∩目标图层相交查询 |
| 空间分析 | SpatialAnalysis | 打开地理分析面板 |
| 制图 | Mapping | 打开专题制图面板 |
| 属性浏览 | AttributeBrowse | 读取shp的DBF → DataGrid → 导出CSV |
| 属性查询 | AttributeQuery | WHERE条件查询 |
| 属性管理 | AttributeManage | 可编辑DataGrid+保存到SHP+添加字段 |

### 土壤植被 Tab（语义色）

| 按钮 | Tag | 功能 |
|------|-----|------|
| 土壤类型 | SoilTypeAnalysis | 面图层+灾害点 → CF值空间分析 |
| 土壤湿度 | SoilMoisture | 同上 |
| 植被类型 | VegType | 同上 |
| 植被覆盖度 | VegCoverage | 同上 |
| NDVI | NDVI | 同上 |

### 专题制图 Tab

| 按钮 | Tag | 功能 |
|------|-----|------|
| 统计图 | StatChart | 查看输出目录中已生成的PNG统计图 |
| 统计表 | StatTable | 查看输出目录中CSV统计表 |
| 专题图 | ThematicMap | 专题图面板（真实地图+可拖动图例/比例尺/指北针） |

---

## 面板系统

### 默认加载
只显示数据面板和图层面板。符号面板和地理处理面板默认隐藏，点击相关功能时自动弹出。

### 面板 API
```csharp
DataPanelAnchor.Show();     DataPanelAnchor.Hide();
LayerPanelAnchor.Show();    LayerPanelAnchor.Hide();
SymbolPanelAnchor.Show();   SymbolPanelAnchor.Hide();
GeoPanelAnchor.Show();      GeoPanelAnchor.Hide();

// 强制前置
SymbolPanelAnchor.IsActive = true;
SymbolPanelAnchor.IsSelected = true;
GeoPanelAnchor.IsActive = true;
GeoPanelAnchor.IsSelected = true;
```

### 图层面板交互
- 点击图层名整行 → 打开符号系统
- 点击符号色块 → 打开符号系统
- 右键菜单：缩放/符号系统/属性浏览/上移/下移/移除
- 双击图层名 → 缩放至图层

---

## 符号系统面板

- **单一符号**：填充色/透明度/轮廓色/线宽 可编辑
- **按字段配色**（新增）：选择字段 + 色带 → 自动 UniqueValue/ClassBreaks 渲染器
  - 5 种预设色带：蓝白红 / 绿黄红 / 蓝青绿 / 彩虹 / ArcGIS默认
  - 文本字段 → UniqueValueRenderer
  - 数值字段（>10唯一值）→ ClassBreaksRenderer（分位数5级）
- 双击颜色块 → 弹出20色预设色板

---

## 专题制图面板（重做版）

- 从 ArcGIS Runtime 导出现实地图作为底图
- 读取图层真实 Renderer 生成彩色图例（非假图例）
- 真实比例尺（根据当前地图比例计算）+ 黑白指北针
- 图例/比例尺/指北针全部可鼠标拖动定位
- 刷新底图→调整位置→生成PNG

---

## 地理分析面板

### 分析流程
```
选择分类面图层 → 选择灾害点图层 → 选择分类字段
→ [可选] 分级分区方法 + 分类数 → 运行分析
→ CF值统计表 + 柱状图 + CF分级汇总
```

### 分级分区（新增）
数值字段自动重分类，4 种方法：
- 自然间断点分级法（Jenks 优化算法）
- 等间距分级
- 分位数分级
- 标准差分级

分类数 3/4/5/7/10 可选。不选则按字段原值分组。

### CF 值计算
确定性系数法：`CF = (PPa - PPs) / (PPa * (1 - PPs))`
基于 `GeometryEngine.Intersects` 真实空间叠加，不用模拟数据。

---

## 核心文件

### 根目录
| 文件 | 功能 |
|------|------|
| `App.xaml(.cs)` | 启动入口，强制 GPU 渲染 |
| `MainWindow.xaml(.cs)` | 完整布局+事件路由+符号编辑器+数据目录树+面板管理 |
| `SplashWindow.xaml(.cs)` | 启动屏 |
| `SystemIconProvider.cs` | Windows 系统图标提供 |
| `AssemblyInfo.cs` | 程序集信息 |

### Services/
| 文件 | 功能 |
|------|------|
| `MapLayerService.cs` | MapLayer 类 + 图层集合 + AddLayer/Remove/Reorder + 按字段配色代理 |
| `GeoAnalysisService.cs` | 真实空间分析：GeometryEngine 点面叠加 → CF 值计算 → CSV/JSON/HTML/PNG 输出 |
| `Reclassifier`（同一文件） | 分级分区：EqualInterval/Quantile/StdDev/Jenks 四种算法 |
| `ChartImageService.cs` | WPF DrawingVisual 渲染统计图表 PNG |
| `ThematicMapService.cs` | WPF DrawingVisual 合成专题图 PNG |
| `AttributeTableGenerator.cs` | 扫描空间数据 → 读取 DBF → 镜像生成 CSV 到属性数据目录 |

### Models/
| 文件 | 功能 |
|------|------|
| `GeoAnalysisModels.cs` | ModuleRegistry（全部模块定义）、AnalysisConfig、StatResult、ChartData、ClassBreak |
| `SpatialDataModels.cs` | SpatialDataType 枚举、SpatialFileInfo、SpatialDataHelper |
| `SymbolModels.cs` | VectorSymbol、RasterSymbol、SymbolItem（INotifyPropertyChanged） |

### Views/
| 文件 | 功能 |
|------|------|
| `MapView.xaml(.cs)` | ArcGIS MapView 控件、图层管理、符号渲染、按字段配色渲染器、坐标/比例尺事件 |
| `GeoProcessToolView.xaml(.cs)` | 地理分析工具面板：图层选择→分级分区→CF分析→表格+图表 |
| `ThematicMapToolView.xaml(.cs)` | 专题制图面板：真实底图+可拖动图例/比例尺/指北针+PNG输出 |
| `AnalysisPanel.xaml(.cs)` | 旧版分析面板（已废弃，提示迁移） |

### Views/Tools/
| 文件 | 功能 |
|------|------|
| `AttributeTableView.xaml(.cs)` | 属性浏览：DataGrid + 选择图层 + 导出CSV + SelectLayerByFilePath |
| `AttributeQueryView.xaml(.cs)` | 属性查询：WHERE条件 + 字段选择 + 结果导出 |
| `AttributeManageView.xaml(.cs)` | 属性管理：可编辑 DataGrid + 保存到 SHP + 添加字段 |
| `SpatialQueryView.xaml(.cs)` | 空间查询：Intersect/Contains + 结果展示 |

### Views/ Converters
| 文件 | 功能 |
|------|------|
| `GeoConverters.cs` | CfToRiskConverter、CountToIndexConverter |
| `SymbolPreviewConverter.cs` | 符号预览图生成 |
| `HexToBrushConverter.cs` | 十六进制颜色转换 |
| `MapLayerTypeConverter.cs` | 图层类型转换 |

### Styles/
| 文件 | 功能 |
|------|------|
| `Brushes.xaml` | 全局颜色：AccentBlue=#1565C0、AppBg=#F2F2F2 等 |
| `ButtonStyles.xaml` | RbnBtn、RbnSeparator、RbnIcon、RbnLabel |
| `TabStyles.xaml` | RibbonTC、RibbonTabItem、GrpLbl |
| `PanelStyles.xaml` | PanelBorderStyle、PanelTitleStyle |
| `ControlStyles.xaml` | 🔥 ArcGIS Pro 风格全局控件样式：ComboBox/TextBox/DataGrid/ScrollBar/Button |

---

## 样式约定

- **字体**：Microsoft YaHei UI
- **AccentBlue**：#1565C0 / **AppBg**：#F2F2F2
- **悬停**：#BBDEFB（浅蓝）/ **选中**：#0D47A1（深蓝边框）
- **面板外框**：PanelBorderStyle（#FBFBFB 背景，#C8C8C8 边框，CornerRadius=6）
- **Ribbon 按钮**：RbnBtn（56px高，hover时蓝灰边框+淡蓝底）
- **全局控件**：ControlStyles.xaml（ComboBox白底蓝边框、TextBox聚焦蓝框、DataGrid浅灰表头、ScrollBar 6px细条）
- **Commit 信息**：中文
- **按钮路由**：Tag 字符串 → `OpenTool_Click` → switch 分发

---

## 数据路径

用户通过数据面板顶部 📂 按钮动态连接本地文件夹：
- 空间数据根目录：用户自选
- 属性数据根目录：自动推断（空间数据的兄弟文件夹 `属性数据`）
- 📋 按钮一键生成所有 shp 对应的属性 CSV
- 空间数据只读不写

---

## 右键菜单（已统一白底蓝hover样式）

| 触发位置 | 菜单项 |
|---------|--------|
| 数据面板树（SHP文件） | 添加到地图 / 缩放至图层 / 属性浏览 / 属性 |
| 图层面板树 | 缩放至图层 / 符号系统 / 属性浏览(矢) / 上移 / 下移 / 移除 |

## 项目文档

位于 `docs/` 目录，截图在 `docs/screenshots/`：
- 系统架构设计.md / 功能设计.md / 界面设计.md
- 算法流程设计.md / 数据库设计.md / 函数接口设计.md
- `tools/EmbedImages/` — 截图嵌入工具（dotnet run 即可）

## 🔴 课程设计要求（题目8）

**课题**：湖南省长株潭地区地质灾害发育特征专题系统设计与实践

### 核心指标（5个模块，全部实装）

| 指标 | 分类要求 | 对应按钮 Tag |
|------|---------|-------------|
| 土壤类型 | 2-3级分类 | SoilTypeAnalysis |
| 土壤湿度 | 数值分级 | SoilMoisture |
| 植被类型 | 2-3级分类 | VegType |
| 植被覆盖度 | 数值分级 | VegCoverage |
| NDVI | 数值分级 | NDVI |

### 功能要求 → 代码对照

| 课程要求 | 实现位置 |
|---------|---------|
| 选择数据源 | MainWindow.ConnectFolder_Click → 数据面板 |
| 选择数据范围 | GeoProcessToolView 图层下拉（已加载的全部矢量图层） |
| 选择数据时间 | 数据文件名含年份，AnalysisConfig.DataTime |
| 选择参数 | ClassFieldCombo 字段下拉（自动识别SHP字段） |
| 选择模型方法 | ModelMethod ComboBox（CF值法/信息量法） |
| 分级分区 | ClassMethod ComboBox（4种方法）+ ClassCountCombo（3/4/5/7/10） |
| 自动处理计算 | GeoAnalysisService.RunAnalysisAsync() |
| 专题图输出 | ThematicMapService.ExportThematicMapAsync() → PNG |
| 统计图输出 | ChartImageService → 4种PNG |
| 统计表输出 | SaveResults → CSV统计表 + CF分级汇总 |
| 自动命名编码 | `{模块名}_{时间戳}_{类型}.{扩展名}` |
| 多维度展示 | DataGrid + 柱状图(Canvas) + CF汇总(Panel) + HTML报告 |

### 文档要求

| 文档 | 对应个人文档位置 |
|------|----------------|
| 系统架构设计 | `.../2详细设计/01系统架构/系统架构设计（个人）.md` |
| 功能设计（含NS图） | `.../2详细设计/02功能设计/功能设计（个人）.md` |
| 界面设计 | `.../2详细设计/03界面设计/界面设计（个人）.md` |
| 算法流程设计（含NS图） | `.../2详细设计/04算法流程设计/算法流程设计（个人）.md` |
| 数据库设计 | `.../2详细设计/05数据库设计/数据库设计（个人）.md` |
| 函数接口设计 | `.../2详细设计/06函数接口设计/函数接口设计（个人）.md` |

### 交付时间线

| 阶段 | 截止日期 | 内容 |
|------|---------|------|
| 需求分析 | 5月30日 | 需求分析说明书 |
| 概要设计 | 6月7日 | 概要设计说明书 |
| 详细设计 | 6月11日 | 详细设计说明书 |
| 原型开发 | 6月17日 | 原型系统+源代码+测试 |
| 运维文档 | 6月22日 | 操作手册+技术文档 |
| 汇报考核 | 7月9日 | 第18周现场考核 |

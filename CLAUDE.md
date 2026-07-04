# CLAUDE.md

此文件为 Claude Code 提供项目指引，优先于所有默认行为。

## 🔴 最高原则

**写任何功能前先问：项目里有没有现成的？NuGet 有没有？不要自己造轮子。**

---

## 项目概述

**长株潭地质灾害系统（cztApp）** — Mini ArcGIS Pro，面向湖南省长株潭地区地质灾害评估。WPF .NET 10 桌面应用。

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

### 地图引擎

**Esri.ArcGISRuntime.WPF 200.6.0**（ArcGIS Runtime SDK for .NET）：
- `ShapefileFeatureTable.OpenAsync(path)` 加载 shp + dbf
- `FeatureLayer` + `SimpleRenderer` 渲染矢量
- `Raster(path)` → `RasterLayer` 渲染栅格
- `GeometryEngine.Intersects/Contains/AreaGeodetic` 空间分析
- 地图初始化为白底无网格：`BackgroundColor=White, BackgroundGrid.IsVisible=false`

### NuGet 关键包

| 包 | 版本 | 用途 |
|---|---|---|
| Esri.ArcGISRuntime.WPF | 200.6.0 | 地图引擎 |
| Esri.ArcGISRuntime.Toolkit.WPF | 200.6.0 | Compass/Legend/TOC 控件 |
| Dirkster.AvalonDock | 4.74.1 | 可停靠面板系统 |
| FluentIcons.Wpf | 2.1.331 | 图标库（已安装，未使用） |
| NetTopologySuite | 2.6.0 | 几何操作 |

### 窗口布局（AvalonDock 可停靠面板）

```
┌─ 标题栏 ──── [视图] ────────────── [─][□][✕] ─┐
├─ Ribbon ───────────────────────────────────────┤
│  数据管理 │ 土壤植被 │ 专题制图                 │
├────────────────────────────────────────────────┤
│  DockingManager                                │
│  ┌ 数据面板 ┐ ┌─ 地图(中心) ─┐ ┌ 图层/符号/地理┐│
│  │📂连接..  │ │ ArcGIS       │ │(可停靠浮动)   ││
│  │📋生成..  │ │ Runtime      │ │               ││
│  │🔍搜索..  │ │              │ │               ││
│  │📁目录树  │ │              │ │               ││
│  └─────────┘ └──────────────┘ └───────────────┘│
├────────────────────────────────────────────────┤
│ 状态栏 ── 图层数 ── 比例尺 ── 坐标 ────────────┘
```

---

## Ribbon 按钮布局（全部实装，无占位）

### 数据管理 Tab（AccentBlue #1565C0 统一配色）

| 组 | 按钮 | Tag | 功能 |
|----|------|-----|------|
| 空间数据 | 导入数据 | ImportData | 文件对话框多选shp/tif → 添加到地图 |
| | 空间查询 | SpatialQuery | 源图层∩目标图层相交查询 → DataGrid |
| | 空间分析 | SpatialAnalysis | 打开地理分析面板（CF值/缓冲区/叠加） |
| | 制图 | Mapping | 打开专题制图面板 |
| 属性数据 | 属性浏览 | AttributeBrowse | 读取shp的DBF → DataGrid → 复制/导出CSV |
| | 属性查询 | AttributeQuery | WHERE条件查询 → 结果导出CSV |
| | 属性管理 | AttributeManage | 字段编辑 |

### 土壤植被 Tab（语义色：棕/蓝/绿/绿/红）

| 按钮 | Tag | 功能 |
|------|-----|------|
| 土壤类型 | SoilTypeAnalysis | 选择分类面图层+灾害点图层 → CF值空间分析 |
| 土壤湿度 | SoilMoisture | 同上，针对土壤湿度指标 |
| 植被类型 | VegType | 同上，针对植被类型指标 |
| 植被覆盖度 | VegCoverage | 同上，针对FVC指标 |
| NDVI | NDVI | 同上，针对NDVI指标 |

### 专题制图 Tab（AccentBlue 统一配色）

| 按钮 | Tag | 功能 |
|------|-----|------|
| 统计图 | StatChart | 生成柱状图/CF分布图/饼图/综合图PNG |
| 统计表 | StatTable | 生成CSV统计表/CF分级汇总/JSON元数据 |
| 专题图 | ThematicMap | 生成专题地图PNG（图名+图例+比例尺+指北针） |

### 图标系统

所有 Ribbon 按钮图标使用**内联 WPF Path 几何图形**（`Viewbox > Canvas > Path`），不使用 PNG 文件。仅 `undo.png` 和 `redo.png` 保留用于标题栏。

---

## 面板 API

```csharp
// 每个面板套 PanelBorderStyle 白色圆角边框
DataPanelAnchor.Show();     DataPanelAnchor.Hide();
LayerPanelAnchor.Show();    LayerPanelAnchor.Hide();
SymbolPanelAnchor.Show();   SymbolPanelAnchor.Hide();
GeoPanelAnchor.Show();      GeoPanelAnchor.Hide();

// 强制置顶（解决被其他面板遮挡）
GeoPanelAnchor.IsActive = true;
GeoPanelAnchor.IsSelected = true;
```

---

## 核心文件

| 文件 | 功能 |
|------|------|
| `MainWindow.xaml` | 完整布局：标题栏、视图菜单、Ribbon（3个Tab）、4个面板、状态栏 |
| `MainWindow.xaml.cs` | 面板开关逻辑、工具路由、符号编辑器、数据目录树、属性表生成触发 |
| `Views/MapView.xaml(.cs)` | ArcGIS MapView 控件，图层管理、符号渲染 |
| `Services/MapLayerService.cs` | MapLayer 类 + 图层集合 ObservableCollection |
| `Services/GeoAnalysisService.cs` | **真实空间分析**：GeometryEngine点面叠加 → CF值计算 → 输出CSV/JSON/HTML/PNG |
| `Services/ChartImageService.cs` | WPF DrawingVisual 渲染统计图表 PNG |
| `Services/ThematicMapService.cs` | WPF DrawingVisual 合成专题图 PNG（图名+图例+比例尺+指北针） |
| `Services/AttributeTableGenerator.cs` | 扫描空间数据shp → 读取DBF → 镜像生成CSV到属性数据目录 |
| `Models/GeoAnalysisModels.cs` | ModuleRegistry、AnalysisConfig、StatResult、ChartData 等 |
| `Models/SpatialDataModels.cs` | SpatialDataType 枚举、SpatialDataHelper |
| `Models/SymbolModels.cs` | VectorSymbol、RasterSymbol、SymbolItem |
| `Views/GeoProcessToolView.xaml(.cs)` | 地理分析工具面板：图层选择→运行→表格+图表 |
| `Views/ThematicMapToolView.xaml(.cs)` | 专题制图配置面板：图名/尺寸/元素开关 → 生成PNG |
| `Views/Tools/AttributeTableView.xaml(.cs)` | 属性浏览：DataGrid + 导出CSV |
| `Views/Tools/AttributeQueryView.xaml(.cs)` | 属性查询：WHERE条件 + 结果导出 |
| `Views/Tools/SpatialQueryView.xaml(.cs)` | 空间查询：Intersect/Contains + 结果展示 |
| `Styles/ButtonStyles.xaml` | RbnBtn、RbnSeparator、RbnIcon、RbnLabel 样式 |
| `Styles/TabStyles.xaml` | RibbonTC、RibbonTabItem、GrpLbl 样式 |
| `Styles/PanelStyles.xaml` | PanelBorderStyle、PanelTitleStyle、PanelIconBtnStyle |
| `SystemIconProvider.cs` | Windows 系统图标提供 |

---

## 数据路径

用户通过数据面板顶部 📂 按钮**动态连接**本地文件夹，不再硬编码：
- 空间数据根目录：用户自选
- 属性数据根目录：自动推断（空间数据的兄弟文件夹 `属性数据`）
- 📋 按钮一键生成所有 shp 对应的属性 CSV（镜像目录结构）

---

## 样式约定

- **字体**：Microsoft YaHei UI
- **AccentBlue**：#1565C0
- **AppBg**：#F2F2F2
- **面板外框**：PanelBorderStyle（#FBFBFB 背景，#C8C8C8 边框，CornerRadius=6）
- **Ribbon 按钮**：RbnBtn（56px高，hover时蓝灰边框+淡蓝底）
- **Ribbon 图标**：Viewbox > Canvas > Path 内联几何图形
- **Commit 信息**：中文
- **按钮标签**：Tag 字符串路由到对应功能

---

## 分析引擎

### 真实空间分析流程

```
用户加载图层到地图 → 打开分析工具 → 选择分类面图层+灾害点图层
→ GeometryEngine.Intersects 点面叠加
→ GeometryEngine.AreaGeodetic 测地面积
→ CF值 = (PPa-PPs)/(PPa(1-PPs)) 确定性系数法
→ 输出: CSV统计表 + CF汇总 + JSON元数据 + HTML报告 + 4张统计图PNG
```

**不使用模拟/随机数据。** 必须基于实际加载的 GIS 图层数据。

### 属性表同步

```
空间数据/xxx/yyy.shp  ←→  属性数据/xxx/yyy.csv
生成前先清理孤儿CSV（源SHP已删除的）→ 覆盖生成 → 清理空目录
```

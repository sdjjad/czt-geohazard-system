# CLAUDE.md

This file provides guidance to Claude Code when working with code in this repository.

## 🔴 HIGHEST PRIORITY — 优先调用已有模块，禁止重复造轮子

**写任何功能之前，先问：有没有现成的库/模块已经做好了？直接拿来用，不要自己写。**

- NuGet 上有现成的就用 NuGet
- 开源社区已经解决过的问题，直接用现成方案
- 这条规则优先级高于其他所有规则

---

## Project vision

**Mini ArcGIS Pro** for CZT (长株潭) geological hazard assessment. Every feature mirrors an ArcGIS Pro workflow.

## Build & Run

```bash
dotnet build cztApp_Solution.slnx
dotnet run --project cztApp1/cztApp1.csproj
```

Requires .NET 10.0 SDK with WPF workload. Target: `net10.0-windows10.0.19041.0`. No tests, no CI/CD.

## Git — automated version control

- **Commit 信息必须用中文写**
- Every logical change gets its own commit, push immediately
- SSL backend: `openssl`. Repo: `https://github.com/sdjjad/czt-geohazard-system`, branch `main`

## Architecture

**WPF .NET 10, code-behind pattern.** `App.OnStartup` → `SplashWindow` → `MainWindow`.

## Map engine

**Esri.ArcGISRuntime.WPF 200.6.0** (ArcGIS Runtime SDK for .NET). NuGet: `Esri.ArcGISRuntime.WPF`.

- `ShapefileFeatureTable.OpenAsync(path)` → loads shp + dbf directly
- `FeatureLayer` + `SimpleRenderer` → renders vector data
- `Raster(path)` → `await raster.LoadAsync()` → `RasterLayer` → renders GeoTIFF
- Built-in symbology: `SimpleFillSymbol`, `SimpleLineSymbol`, `SimpleMarkerSymbol`
- No WebView2, no Leaflet, no JS interop, no GeoJSON conversion
- Toolkit: `Esri.ArcGISRuntime.Toolkit.WPF` (Compass, Legend, TOC controls available)

## Window layout

**Dirkster.AvalonDock** (v4.74.1, `net10.0-windows7.0`) replaces fixed Grid layout with dockable panels.

```
┌─ 标题栏 ── [↩][↘] | 视图 ─ [─][□][✕] ─────────────────────┐
├─ Ribbon: 数据管理 | 土壤植被 | 专题制图 ────────────────────┤
├──────────────────────────────────────────────────────────┤
│  DockingManager                                         │
│  ┌ 数据面板 ┐ ┌── 地图(中心) ──┐ ┌ 图层面板 | 符号 | 地理 ┐ │
│  │(可停靠)  │ │ ArcGIS Runtime │ │(可停靠、可浮动、可拖拽) │ │
│  └─────────┘ └───────────────┘ └────────────────────────┘ │
├──────────────────────────────────────────────────────────┤
│ 状态栏 ── 图层数 ── 比例尺 ── 经纬度 ──────────────────────┘
```

### Panels (all dockable via AvalonDock)

All panels are `LayoutAnchorable` inside `DockingManager`. Users can:
- **Drag** title to dock left/right/top/bottom
- **Float** by dragging outside the window
- **Auto-hide** (pin/unpin)
- **Close/reopen** via View menu
- **Reorder** by dragging tabs

| Panel | ContentId | Content |
|-------|-----------|---------|
| 数据面板 | `DataPanel` | search box + 空间/属性 toggle + TreeView catalog |
| 图层面板 | `LayerPanel` | TreeView with checkbox + 符号预览 |
| 符号面板 | `SymbolPanel` | symbol editor, hidden by default |
| 地理处理面板 | `GeoPanel` | geoprocessing tool view, opens on ribbon click |
| 地图视图 | `MapView` | `LayoutDocument`, always in center, cannot close |

### Panel API

```csharp
// Show/hide
DataPanelAnchor.Show();     DataPanelAnchor.Hide();
LayerPanelAnchor.Show();    LayerPanelAnchor.Hide();
SymbolPanelAnchor.Show();   SymbolPanelAnchor.Hide();
GeoPanelAnchor.Show();      GeoPanelAnchor.Hide();

// Check visibility
DataPanelAnchor.IsVisible

// AutoHide / Float
anchorable.ToggleAutoHide();
anchorable.Float();
```

### Panel content structure

Each panel still follows the unified template internally:
- `Style="{StaticResource PanelBorderStyle}"`
- Title row with `PanelTitleStyle` + 3× `PanelIconBtnStyle`
- Content row filling remaining space

### Title bar

- Left: project icon | separator | undo/redo buttons | separator | **视图** button
- Right: minimize/maximize/close
- **视图** button: opens ContextMenu with toggles for all panels (dynamically shows ✓/blank based on actual visibility)

### Symbol system

- Click symbol preview in layer tree → opens 符号面板 with ArcGIS Pro-style editor
- Preview: WPF shape (Rectangle/Line/Ellipse) in Border
- Properties: color swatch buttons (Popup with 20-color palette), opacity slider, line width
- `VectorSymbol` model → `OnSymbolEdited` → `MapView.UpdateLayerStyleAsync` → ArcGIS Runtime `SimpleRenderer`
- `SymbolItem` implements `INotifyPropertyChanged` for auto tree preview refresh

## Key files

| File | Role |
|------|------|
| `Views/MapView.xaml` | `esri:MapView` ArcGIS Runtime control |
| `Views/MapView.xaml.cs` | Layer management: AddVector/RasterLayerAsync, UpdateLayerStyleAsync, symbology |
| `Services/MapLayerService.cs` | MapLayer class + service with ObservableCollection, DetectGeometryType, ShapefileToGeoJson |
| `Models/SymbolModels.cs` | VectorSymbol (INPC), RasterSymbol, SymbolItem (INPC with NotifyRefresh) |
| `Views/SymbolPreviewConverter.cs` | IValueConverter: SymbolItem → WPF shape for tree preview |
| `MainWindow.xaml` | Full layout: title bar, view menu, ribbon, all 4 panels, status bar |
| `MainWindow.xaml.cs` | Panel open/close logic, symbol editor (BuildVectorSymbolEditor, AddColorPicker, etc.), view menu |
| `Styles/PanelStyles.xaml` | PanelBorderStyle, PanelTitleStyle, PanelIconBtnStyle |
| `SystemIconProvider.cs` | Windows system icon for catalog tree |

## Data paths

```csharp
SpatialDataPath = @"D:\...\数据\空间数据";   // const in MainWindow.xaml.cs
AttributeDataPath = @"D:\...\数据\属性数据";
```

## Styles

- `PanelStyles.xaml`: **PanelBorderStyle** (#FBFBFB bg, #C8C8C8 border, CornerRadius=6), **PanelTitleStyle** (FontSize=15), **PanelIconBtnStyle** (22×22 transparent hover #E0E0E0)
- `ButtonStyles.xaml`: QatBtn, RbnBtn
- `TabStyles.xaml`: RibbonTC, RibbonTabItem, GrpLbl
- ContextMenu/MenuItem styles in `MainWindow.xaml` Window.Resources (white bg, #1A1A1A text, #F0F0F0 hover)
- Font: Microsoft YaHei UI. AccentBlue = #1565C0, AppBg = #F2F2F2

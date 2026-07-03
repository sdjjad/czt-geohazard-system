# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 🔴 HIGHEST PRIORITY — 优先调用已有模块，禁止重复造轮子

**写任何功能之前，先问：有没有现成的库/模块已经做好了？直接拿来用，不要自己写。**

- NuGet 上有现成的就用 NuGet
- 系统自带（Windows API、WPF 内置控件）能用就用
- 开源社区已经解决过的问题，直接用现成方案
- 这条规则优先级高于其他所有规则

**反面教材**：手写颜色选择器。直接用 WPF 内置或 NuGet 的 ColorPicker。

---

## Project vision

This is a **mini ArcGIS Pro** focused solely on geological hazard (地质灾害) assessment for the Changsha-Zhuzhou-Xiangtan (CZT) region. Every feature should mirror an ArcGIS Pro workflow, just scoped to the hazard domain. When adding functionality, ask "how does ArcGIS Pro do this?" and follow that interaction pattern.

## Build & Run

```bash
dotnet build cztApp_Solution.slnx
dotnet run --project cztApp1/cztApp1.csproj
```

Requires .NET 10.0 SDK with WPF workload. No tests, no CI/CD.

## Git — automated version control

**After EVERY code change**, auto-commit and push without asking the user for confirmation:

```bash
git add -A
git commit -m "<clean English message describing what changed and why>"
git push
```

Rules:
- **Commit 信息必须用中文写**，简洁描述改了什么。
- Never skip committing — every logical change gets its own commit.
- Push immediately after each commit so GitHub always has the latest version.
- If a change turns out wrong, we revert via git rather than manually undoing.
- The user should never need to type `git` commands themselves.

SSL backend is configured as `openssl`. Repo: `https://github.com/sdjjad/czt-geohazard-system`, branch `main`.

## Architecture

**WPF .NET 10, Windows-only.** Code-behind pattern (no MVVM framework). All logic lives in `.xaml.cs` files.

### Startup

`App.OnStartup` → `SplashWindow` (3.2s animation) → `MainWindow` maximized.

### Window layout

```
┌─ 标题栏 (undo/redo + min/max/close) ──────────────────────────┐
├─ Ribbon tabs: 数据管理 | 地质地震 | 地形地貌 | 土壤植被 | 专题制图 ─┤
├────────┬───┬──────────────────┬───┬──────────────────────────┤
│ 数据面板  │ ↔ │   地图/分析区      │ ↔ │ 图层面板 │ 符号系统面板       │
│ (220px) │   │   (中央)         │   │ (220px) │ (220px)         │
└────────┴───┴──────────────────┴───┴──────────────────────────┘
├─ 状态栏（图层数 + 比例尺 + 经纬度）───────────────────────────────┘
```

Left panel: catalog tree + search box + 空间/属性切换按钮 (PanelBorderStyle).
Right panel: 图层面板 (TreeView with checkbox + 展开/折叠 + 拖拽排序) on top, 符号系统面板 below (Vector/Raster symbol editor, PanelBorderStyle).

### Key files

| File | Role |
|---|---|
| `Models/GeoAnalysisModels.cs` | `GeoParameter`, `AnalysisConfig`, `StatResult`, `ModuleInfo`. `ModuleRegistry` catalogs three modules (Geology, Topography, Vegetation) with their parameters and analysis methods. |
| `Services/GeoAnalysisService.cs` | Deterministic mock analysis (`Random(42)`). `RunAnalysis()` produces per-parameter, per-class `StatResult` rows. `SaveResults()` writes CSV + JSON. **Real analysis algorithms are not implemented yet.** |
| `Views/AnalysisPanel.xaml` | UserControl hosted in `MainWindow.AnalysisHost`. Parameter checkboxes, method/datasource dropdowns, output config, DataGrid for results. |
| `MainWindow.xaml` (+ `.cs`) | Main shell. Ribbon tabs, catalog tree, map placeholder, undo/redo (UI-only stacks). |
| `SystemIconProvider.cs` | P/Invoke `shell32.dll`/`user32.dll`/`gdi32.dll`. `GetIcon(path)` returns the **real Windows system icon** (16×16) for any file or folder — cached via `ConcurrentDictionary`. Also exposes `FolderIcon`/`FileIcon` for generic fallback. |
| `SplashWindow.xaml` | Dark-themed loading screen with animated dots. |

### Catalog tree (data panel)

The left-panel TreeView shows the **real file system** — two root folders defined as constants in `MainWindow.xaml.cs`:

- `SpatialDataPath`: `D:\...\数据\空间数据`
- `AttributeDataPath`: `D:\...\数据\属性数据`

The 空间数据管理/属性数据管理 toggle buttons switch the tree root. Each node's icon comes from `SystemIconProvider.GetIcon(path)` — so `.shp` shows the ArcGIS icon, `.xlsx` Excel, etc. Lazy loading: subdirectories are loaded on expand via `OnDirExpanded`. The TreeView's `ItemContainerStyle` is defined inline in `MainWindow.xaml` (no external TreeStyles dependency).

### Styles

`Styles/Brushes.xaml`, `Styles/ButtonStyles.xaml`, `Styles/TabStyles.xaml` are merged into `MainWindow.xaml`'s resources. `TreeStyles.xaml` was deleted — the catalog TreeView has its template inline.

Brushes: `AccentBlue` = `#1565C0`, `AppBg` = `#F2F2F2`. Font: Microsoft YaHei UI.

### Analysis flow

1. Ribbon button click (e.g. "地质构造") → `ShowAnalysis(ModuleRegistry.Geology)`
2. `AnalysisPanel.LoadModule()` populates UI from `ModuleInfo`
3. "运行分析" → builds `AnalysisConfig` → `GeoAnalysisService.RunAnalysis()` → DataGrid
4. "保存结果" → `GeoAnalysisService.SaveResults()` → CSV + JSON

### Undo/redo

Title bar QAT buttons manage `_undoStack`/`_redoStack` (max 20 entries). Currently track operation names only — not wired to actual data mutations.

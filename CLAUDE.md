# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the solution
dotnet build cztApp_Solution.slnx

# Build a single project
dotnet build cztApp1/cztApp1.csproj

# Run the app
dotnet run --project cztApp1/cztApp1.csproj
```

The solution requires the .NET 10.0 SDK with WPF workload. There are no test projects or CI/CD.

## Architecture

This is a **WPF (.NET 10, Windows-only)** desktop application for geological hazard analysis in the Changsha-Zhuzhou-Xiangtan (长株潭/CZT) region. It uses code-behind with no MVVM framework — logic lives directly in `.xaml.cs` files.

### Startup flow

`App.OnStartup` → shows `SplashWindow` (animated loading dots for 3.2s) → opens `MainWindow` maximized. `MainWindow` has custom window chrome (`WindowStyle="None"` + `WindowChrome`) — title bar drag, min/max/close buttons are all handled manually in `MainWindow.xaml.cs`.

### Key types

| File | Role |
|---|---|
| `Models/GeoAnalysisModels.cs` | `GeoParameter`, `AnalysisConfig`, `StatResult`, `ModuleInfo` — all data models. `ModuleRegistry` is a static catalog of three analysis modules (Geology, Topography, Vegetation), each declaring parameters and available methods. |
| `Services/GeoAnalysisService.cs` | Generates deterministic mock analysis results (`Random(42)`). `RunAnalysis()` produces per-parameter, per-class `StatResult` rows. `SaveResults()` writes CSV + JSON metadata to disk. |
| `Views/AnalysisPanel.xaml` (+ `.cs`) | UserControl hosted inside `MainWindow`'s `AnalysisHost` grid. Loads a `ModuleInfo` to populate parameter checkboxes, method dropdown, and output config. Running analysis fills a `DataGrid` with `StatResult` rows. |
| `MainWindow.xaml` (+ `.cs`) | Main shell: ribbon tabs (数据管理, 地质地震, 地形地貌, 土壤植被, 专题制图), left data panel with TreeView, center map placeholder, right attribute panel, status bar. Undo/redo stacks track operation names (UI only, not wired to data mutations). |
| `SystemIconProvider.cs` | P/Invoke into `shell32.dll`/`user32.dll`/`gdi32.dll` to extract system file/folder icons as WPF `BitmapSource`. |
| `Styles/` | XAML resource dictionaries: `Brushes.xaml` (color palette), `ButtonStyles.xaml` (QAT and Ribbon button templates), `TabStyles.xaml` (ribbon tab items + group label), `TreeStyles.xaml` (custom TreeViewItem with expand arrows and file/folder icons). |

### Data flow

1. User clicks a ribbon button (e.g. "地质构造") → `MainWindow` calls `ShowAnalysis(ModuleRegistry.Geology)`
2. `ShowAnalysis` creates or reuses an `AnalysisPanel`, which calls `LoadModule(module)` to populate UI from `ModuleInfo`
3. "运行分析" click → `AnalysisPanel.RunAnalysis_Click` builds an `AnalysisConfig`, calls `GeoAnalysisService.RunAnalysis()`, and displays results in DataGrid
4. "保存结果" click → `GeoAnalysisService.SaveResults()` writes CSV + JSON

### Data persistence

`MainWindow` serializes the data TreeView to `data_store.json` (working directory) via `System.Text.Json`. Imported data nodes persist across sessions. The tree is pre-populated with hardcoded spatial/attribute data nodes from `BuildSpatialTree()` / `BuildAttributeTree()`.

### UI conventions

- Icons: PNG resources under `Resources/` (24×24-ish), plus Segoe MDL2 Assets glyphs (`&#xE8BB;` = close X, `&#xE922;` = maximize, etc.)
- Font: Microsoft YaHei UI throughout
- Accent color: `#1565C0` (blue)
- Custom `ContextMenu` and `MenuItem` styles in `MainWindow.xaml` override WPF defaults to remove the icon column gutter and ensure white backgrounds

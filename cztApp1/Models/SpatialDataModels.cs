using System;
using System.Collections.Generic;
using System.IO;

namespace cztApp1.Models;

/// <summary>
/// Spatial data type classification
/// </summary>
public enum SpatialDataType
{
    Folder,
    Vector,   // .shp
    Raster,   // .tif, .tiff
    Other
}

/// <summary>
/// Represents a recognized spatial data file in the catalog tree
/// </summary>
public class SpatialFileInfo
{
    public string FilePath { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public SpatialDataType DataType { get; init; }
    public string LayerName => DisplayName;

    /// <summary>
    /// For vector data, the .shp path. For raster, the .tif path.
    /// </summary>
    public string PrimaryPath => FilePath;
}

/// <summary>
/// Static helpers for spatial file classification
/// </summary>
public static class SpatialDataHelper
{
    /// <summary>
    /// Extensions that belong to a shapefile but are not the primary file.
    /// Only .shp should be shown in the tree.
    /// </summary>
    private static readonly HashSet<string> ShapefileCompanions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dbf", ".shx", ".prj", ".cpg", ".sbn", ".sbx",
        ".shp.xml", ".qix", ".fix", ".fbn", ".fbx", ".ain", ".aih", ".atx"
    };

    /// <summary>
    /// Raster data extensions
    /// </summary>
    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tif", ".tiff", ".img", ".dem", ".asc", ".grd", ".bil", ".bip", ".bsq", ".sid", ".jpg", ".jp2", ".png"
    };

    /// <summary>
    /// Raster companion files that should be hidden
    /// </summary>
    private static readonly HashSet<string> RasterCompanions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tfw", ".tif.aux.xml", ".tif.xml", ".tif.ovr",
        ".tif.vat.cpg", ".tif.vat.dbf", ".aux.xml", ".ovr",
        ".jpg.aux.xml", ".png.aux.xml"
    };

    /// <summary>
    /// Files that should be hidden from the catalog tree (shapefile companions + raster companions)
    /// </summary>
    public static bool IsCompanionFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        if (ShapefileCompanions.Contains(ext)) return true;

        var name = Path.GetFileName(filePath);
        foreach (var companion in RasterCompanions)
        {
            if (name.EndsWith(companion, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Classify a file path as Vector, Raster, or Other
    /// </summary>
    public static SpatialDataType ClassifyFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".shp" => SpatialDataType.Vector,
            ".tif" or ".tiff" or ".img" => SpatialDataType.Raster,
            _ => SpatialDataType.Other
        };
    }

    /// <summary>
    /// Check if a file is a displayable spatial data format (.shp or .tif)
    /// </summary>
    public static bool IsSpatialDataFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext is ".shp" or ".tif" or ".tiff" or ".img";
    }

    /// <summary>
    /// Get a display-friendly data type name
    /// </summary>
    public static string GetDataTypeLabel(SpatialDataType type) => type switch
    {
        SpatialDataType.Vector => "矢量",
        SpatialDataType.Raster => "栅格",
        SpatialDataType.Folder => "文件夹",
        _ => "其他"
    };
}

# Unity Terrain Generator

Generates real-world Unity terrain from USGS GeoTIFF elevation data. Built for the Grand Tetons but works with any USGS 1/3 arc-second DEM tile.

## Setup

### Prerequisites
- Unity 6
- Python 3 with: `pip3 install tifffile imagecodecs numpy`

### Download DEM Data
1. Go to [USGS National Map](https://apps.nationalmap.gov/downloader/)
2. Select **Elevation Products (3DEP)** → **1/3 arc-second DEM** → **Current**
3. Draw a bounding box around your area of interest
4. Download the GeoTIFF (`.tif`) file
5. Place it in `Data/`

The Grand Tetons are covered by tile `USGS_13_n44w111` (~396MB).

## Usage

1. In Unity, open **Tools > Terrain Generator**
2. Select your `.tif` file (auto-detects the default if present)
3. Adjust the crop region lat/lon bounds to your area of interest
4. Click **Load & Preview** — first run converts the GeoTIFF via Python (~30 sec, cached after)
5. Review the heightmap preview and elevation range
6. Click **Generate Terrain**

### Default Crop (Grand Tetons)
- South: 43.60°N, North: 43.85°N
- West: -110.90°, East: -110.65°
- Approximately 20km × 28km

### Settings
- **Max Height**: Terrain height ceiling in meters (default 5000m, covers the Tetons' peak at ~4195m)
- **Tile Grid**: Split into 1×1, 2×2, or 3×3 tiles for better heightmap resolution over large areas

## How It Works

1. **Python conversion** (`convert_geotiff.py`): Decodes the LZW-compressed GeoTIFF into a flat float32 binary + JSON metadata, cached in `Library/TerrainCache/`
2. **C# reader** (`GeoTiffReader.cs`): Loads the cached data, invokes Python if the cache is missing
3. **Editor window** (`TerrainGeneratorWindow.cs`): Crops to lat/lon bounds, resamples to Unity's required power-of-2+1 heightmap resolution, and creates Terrain GameObjects with proper real-world scaling

## File Structure
```
UnityTerrain/
├── Data/                  # DEM source files (.tif, gitignored)
├── Editor/
│   ├── convert_geotiff.py          # Python GeoTIFF decoder
│   ├── GeoTiffReader.cs            # C# wrapper for Python conversion
│   └── TerrainGeneratorWindow.cs   # Unity Editor window
└── GeneratedTerrainData/  # Terrain assets (created at runtime)
```

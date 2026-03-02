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

## Train Route Finder

Routes train tracks up the terrain as high as possible while respecting realistic railway engineering constraints.

### Usage

1. Generate terrain first via **Tools > Terrain Generator**
2. Open **Tools > Train Route Finder**
3. Click **Pick** and click a valley floor location on the terrain in the Scene view
4. Optionally enable **Use End Point** and pick a destination
5. Choose a preset (**Mountain Railway**, **Cog Railway**, or **Mountain With Cuts**)
6. Click **Find Route** — explores the terrain grid in under a second
7. Route appears color-coded: green (normal), orange (steep), blue (bridge), brown (cut section)
8. Click **Create Route Object** for a persistent LineRenderer, or **Apply to Terrain** to carve shelves

### Presets
| Parameter | Mountain Railway | Cog Railway | Mountain With Cuts |
|-----------|-----------------|-------------|--------------------|
| Max grade | 3.5% | 8% | 3.5% |
| Min turn radius | 150m | 80m | 150m |
| Max cut depth | 0m | 0m | 20m |
| Max fill height | 0m | 0m | 10m |

### Terrain Modification

Enable **Max Cut Depth** to allow the route to carve into steep mountain faces — like real mountain shelf roads. The algorithm tracks the track elevation independently from the terrain, ensuring cuts never exceed the configured depth at any point. After finding a route, click **Apply to Terrain** to carve a smooth shelf into the actual heightmap.

### How It Works

Max-elevation priority queue on a 50m grid with direction-tracking state (position + heading + turn cooldown). With terrain modification disabled, the track follows the terrain surface. With cuts/fills enabled, the algorithm tracks achievable track elevation at each state, allowing it to traverse steep slopes by carving shelves while respecting cumulative cut depth limits.

## File Structure
```
UnityTerrain/
├── Data/                  # DEM source files (.tif, gitignored)
├── Editor/
│   ├── convert_geotiff.py          # Python GeoTIFF decoder
│   ├── GeoTiffReader.cs            # C# wrapper for Python conversion
│   ├── TerrainGeneratorWindow.cs   # Terrain Generator editor window
│   ├── TrainRouteFinder.cs         # Train route BFS algorithm
│   └── TrainRouteWindow.cs         # Train Route Finder editor window
└── GeneratedTerrainData/  # Terrain assets (created at runtime)
```

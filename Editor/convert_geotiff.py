#!/usr/bin/env python3
"""
Converts a GeoTIFF DEM to a simple float32 binary format readable by Unity.
Outputs to a specified output directory:
  {name}.raw  - float32 elevation data, row-major, north-to-south
  {name}.json - metadata (dimensions, geo bounds, elevation range)

Usage:
  python3 convert_geotiff.py <input.tif> <output_dir>

Requires: pip install tifffile imagecodecs numpy
"""

import sys
import os
import json
import numpy as np

def main():
    if len(sys.argv) < 3:
        print("Usage: convert_geotiff.py <input.tif> <output_dir>", file=sys.stderr)
        sys.exit(1)

    input_path = sys.argv[1]
    output_dir = sys.argv[2]
    os.makedirs(output_dir, exist_ok=True)

    base_name = os.path.splitext(os.path.basename(input_path))[0]
    output_raw = os.path.join(output_dir, base_name + ".raw")
    output_json = os.path.join(output_dir, base_name + ".json")

    import tifffile

    print(f"Reading {input_path}...")
    tif = tifffile.TiffFile(input_path)
    page = tif.pages[0]

    # Read image data
    img = page.asarray().astype(np.float32)
    height, width = img.shape
    print(f"  Dimensions: {width} x {height}")

    # Read geo tags
    origin_lon = -111.0
    origin_lat = 44.0
    pixel_scale_x = 1.0 / 10800.0
    pixel_scale_y = 1.0 / 10800.0

    for tag in page.tags.values():
        if tag.code == 33550:  # ModelPixelScaleTag
            vals = tag.value
            pixel_scale_x = float(vals[0])
            pixel_scale_y = float(vals[1])
        elif tag.code == 33922:  # ModelTiepointTag
            vals = tag.value
            # Tiepoint: (I, J, K, X, Y, Z)
            origin_lon = float(vals[3]) - float(vals[0]) * pixel_scale_x
            origin_lat = float(vals[4]) + float(vals[1]) * pixel_scale_y

    # Compute elevation range (excluding nodata)
    valid = img[img > -10000]
    min_elev = float(np.min(valid)) if len(valid) > 0 else 0.0
    max_elev = float(np.max(valid)) if len(valid) > 0 else 1.0

    print(f"  Origin: ({origin_lon:.6f}, {origin_lat:.6f})")
    print(f"  Pixel scale: ({pixel_scale_x:.10f}, {pixel_scale_y:.10f})")
    print(f"  Elevation: {min_elev:.1f}m to {max_elev:.1f}m")

    # Replace nodata with 0
    img[img < -10000] = 0.0

    # Write raw float32 data
    print(f"Writing {output_raw}...")
    img.tofile(output_raw)

    # Write metadata
    metadata = {
        "width": width,
        "height": height,
        "originLon": origin_lon,
        "originLat": origin_lat,
        "pixelScaleX": pixel_scale_x,
        "pixelScaleY": pixel_scale_y,
        "minElevation": min_elev,
        "maxElevation": max_elev
    }
    with open(output_json, 'w') as f:
        json.dump(metadata, f, indent=2)
    print(f"Writing {output_json}...")

    print("Done!")

if __name__ == "__main__":
    main()

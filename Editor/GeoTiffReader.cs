using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace UnityTerrain
{
    /// <summary>
    /// Reads GeoTIFF elevation data by invoking a Python script for decoding.
    /// The Python script converts the GeoTIFF to a simple float32 .raw + .json format,
    /// cached in Library/TerrainCache/ so it doesn't bloat the Assets folder.
    /// Subsequent loads use the cached files directly.
    /// </summary>
    public static class GeoTiffReader
    {
        public class GeoTiffData
        {
            public int Width;
            public int Height;
            public float[] Elevations; // row-major, row 0 = northernmost
            public double OriginLon;   // longitude of pixel (0,0) top-left
            public double OriginLat;   // latitude of pixel (0,0) top-left
            public double PixelScaleX; // degrees per pixel (longitude direction)
            public double PixelScaleY; // degrees per pixel (latitude direction, positive going south)
            public float MinElevation;
            public float MaxElevation;
        }

        static string CacheDir
        {
            get
            {
                // Library/ is project-local, not version-controlled, not imported as assets
                string dir = Path.Combine(Application.dataPath, "..", "Library", "TerrainCache");
                return Path.GetFullPath(dir);
            }
        }

        public static GeoTiffData Read(string tifPath, Action<string, float> progress = null)
        {
            string baseName = Path.GetFileNameWithoutExtension(tifPath);
            string rawPath = Path.Combine(CacheDir, baseName + ".raw");
            string jsonPath = Path.Combine(CacheDir, baseName + ".json");

            // Convert if needed
            if (!File.Exists(rawPath) || !File.Exists(jsonPath))
            {
                progress?.Invoke("Converting GeoTIFF (first time only)...", 0.1f);
                ConvertWithPython(tifPath);
            }

            // Read metadata
            progress?.Invoke("Reading metadata...", 0.3f);
            string jsonText = File.ReadAllText(jsonPath);
            var meta = JsonUtility.FromJson<GeoTiffMeta>(jsonText);

            // Read elevation data
            progress?.Invoke("Reading elevation data...", 0.4f);
            byte[] rawBytes = File.ReadAllBytes(rawPath);

            int expectedBytes = meta.width * meta.height * 4;
            if (rawBytes.Length != expectedBytes)
                throw new Exception($"Raw file size mismatch: got {rawBytes.Length}, expected {expectedBytes}");

            progress?.Invoke("Processing elevation values...", 0.7f);
            float[] elevations = new float[meta.width * meta.height];
            Buffer.BlockCopy(rawBytes, 0, elevations, 0, rawBytes.Length);

            progress?.Invoke("Done", 1f);

            Debug.Log($"[GeoTiffReader] {meta.width}x{meta.height}, " +
                $"Origin: ({meta.originLon:F4}, {meta.originLat:F4}), " +
                $"Elevation: {meta.minElevation:F0}m to {meta.maxElevation:F0}m");

            return new GeoTiffData
            {
                Width = meta.width,
                Height = meta.height,
                Elevations = elevations,
                OriginLon = meta.originLon,
                OriginLat = meta.originLat,
                PixelScaleX = meta.pixelScaleX,
                PixelScaleY = meta.pixelScaleY,
                MinElevation = meta.minElevation,
                MaxElevation = meta.maxElevation
            };
        }

        static void ConvertWithPython(string tifPath)
        {
            string scriptPath = Path.GetFullPath("Assets/UnityTerrain/Editor/convert_geotiff.py");
            if (!File.Exists(scriptPath))
                throw new Exception($"Python conversion script not found at: {scriptPath}\n" +
                    "Expected at Assets/UnityTerrain/Editor/convert_geotiff.py");

            string cacheDir = CacheDir;
            Debug.Log($"[GeoTiffReader] Converting {Path.GetFileName(tifPath)} → {cacheDir}");

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{scriptPath}\" \"{tifPath}\" \"{cacheDir}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(stdout))
                    Debug.Log($"[GeoTiffReader] {stdout.TrimEnd()}");

                if (process.ExitCode != 0)
                    throw new Exception(
                        $"Python conversion failed (exit code {process.ExitCode}):\n{stderr}\n\n" +
                        "Make sure Python 3 is installed with: pip3 install tifffile imagecodecs numpy");
            }

            string baseName = Path.GetFileNameWithoutExtension(tifPath);
            string rawPath = Path.Combine(cacheDir, baseName + ".raw");
            string jsonPath = Path.Combine(cacheDir, baseName + ".json");

            if (!File.Exists(rawPath) || !File.Exists(jsonPath))
                throw new Exception("Python conversion completed but output files not found.");

            Debug.Log("[GeoTiffReader] Conversion complete.");
        }

        [Serializable]
        class GeoTiffMeta
        {
            public int width;
            public int height;
            public double originLon;
            public double originLat;
            public double pixelScaleX;
            public double pixelScaleY;
            public float minElevation;
            public float maxElevation;
        }
    }
}

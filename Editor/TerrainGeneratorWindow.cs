using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityTerrain
{
    public class TerrainGeneratorWindow : EditorWindow
    {
        // --- File ---
        string geoTiffPath = "";

        // --- Loaded data ---
        GeoTiffReader.GeoTiffData loadedData;
        string loadedInfo = "";

        // --- Crop region (lat/lon) ---
        double cropSouth = 43.60;
        double cropNorth = 43.85;
        double cropWest = -110.90;
        double cropEast = -110.65;

        // --- Terrain settings ---
        float terrainHeight = 2500f;
        int tileGrid = 1; // 1=1x1, 2=2x2, 3=3x3
        static readonly string[] TileGridOptions = { "1x1", "2x2", "3x3" };
        static readonly int[] TileGridValues = { 1, 2, 3 };

        // --- Preview ---
        Texture2D previewTexture;
        float[] croppedElevations;
        int croppedWidth, croppedHeight;
        float croppedMinElev, croppedMaxElev;

        // --- Scroll ---
        Vector2 scrollPos;

        [MenuItem("Tools/Terrain Generator")]
        static void Open()
        {
            var window = GetWindow<TerrainGeneratorWindow>("Terrain Generator");
            window.minSize = new Vector2(380, 600);
        }

        void OnEnable()
        {
            // Default path
            string defaultPath = Path.Combine(Application.dataPath, "UnityTerrain/Data/USGS_13_n44w111_20250122.tif");
            if (File.Exists(defaultPath))
                geoTiffPath = defaultPath;
        }

        void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.LabelField("Terrain Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            DrawFileSection();
            EditorGUILayout.Space(8);
            DrawCropSection();
            EditorGUILayout.Space(8);
            DrawTerrainSettings();
            EditorGUILayout.Space(8);
            DrawLoadButton();

            if (croppedElevations != null)
            {
                EditorGUILayout.Space(8);
                DrawPreview();
                EditorGUILayout.Space(8);
                DrawGenerateButton();
            }

            EditorGUILayout.EndScrollView();
        }

        // ─── UI Sections ───

        void DrawFileSection()
        {
            EditorGUILayout.LabelField("GeoTIFF File", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            geoTiffPath = EditorGUILayout.TextField(geoTiffPath);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFilePanel("Select GeoTIFF", Application.dataPath, "tif");
                if (!string.IsNullOrEmpty(path))
                    geoTiffPath = path;
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(loadedInfo))
                EditorGUILayout.HelpBox(loadedInfo, MessageType.Info);
        }

        void DrawCropSection()
        {
            EditorGUILayout.LabelField("Crop Region (Lat/Lon)", EditorStyles.miniBoldLabel);
            cropNorth = EditorGUILayout.DoubleField("North", cropNorth);
            EditorGUILayout.BeginHorizontal();
            cropWest = EditorGUILayout.DoubleField("West", cropWest);
            cropEast = EditorGUILayout.DoubleField("East", cropEast);
            EditorGUILayout.EndHorizontal();
            cropSouth = EditorGUILayout.DoubleField("South", cropSouth);

            // Show approximate physical size
            double centerLat = (cropNorth + cropSouth) / 2.0;
            double nsKm = (cropNorth - cropSouth) * 110.574;
            double ewKm = (cropEast - cropWest) * 111.320 * Math.Cos(centerLat * Math.PI / 180.0);
            EditorGUILayout.LabelField($"  ≈ {ewKm:F1} km (E-W) × {nsKm:F1} km (N-S)");
        }

        void DrawTerrainSettings()
        {
            EditorGUILayout.LabelField("Terrain Settings", EditorStyles.miniBoldLabel);
            terrainHeight = EditorGUILayout.FloatField("Max Height (m)", terrainHeight);
            tileGrid = TileGridValues[EditorGUILayout.Popup("Tile Grid", Array.IndexOf(TileGridValues, tileGrid), TileGridOptions)];
        }

        void DrawLoadButton()
        {
            if (GUILayout.Button("Load & Preview", GUILayout.Height(30)))
            {
                LoadAndPreview();
            }
        }

        void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField($"Crop: {croppedWidth} × {croppedHeight} px");
            EditorGUILayout.LabelField($"Elevation: {croppedMinElev:F0}m – {croppedMaxElev:F0}m");

            if (previewTexture != null)
            {
                float aspect = (float)previewTexture.width / previewTexture.height;
                float previewWidth = Mathf.Min(position.width - 20, 360);
                float previewHeight = previewWidth / aspect;
                Rect r = GUILayoutUtility.GetRect(previewWidth, previewHeight);
                EditorGUI.DrawPreviewTexture(r, previewTexture);
            }
        }

        void DrawGenerateButton()
        {
            if (GUILayout.Button("Generate Terrain", GUILayout.Height(36)))
            {
                GenerateTerrain();
            }
        }

        // ─── Load & Preview ───

        void LoadAndPreview()
        {
            if (!File.Exists(geoTiffPath))
            {
                EditorUtility.DisplayDialog("Error", "GeoTIFF file not found.", "OK");
                return;
            }

            try
            {
                loadedData = GeoTiffReader.Read(geoTiffPath, (msg, pct) =>
                {
                    EditorUtility.DisplayProgressBar("Loading GeoTIFF", msg, pct);
                });

                loadedInfo = $"{loadedData.Width}×{loadedData.Height} px\n" +
                    $"Lon: {loadedData.OriginLon:F4} to {loadedData.OriginLon + loadedData.Width * loadedData.PixelScaleX:F4}\n" +
                    $"Lat: {loadedData.OriginLat:F4} to {loadedData.OriginLat - loadedData.Height * loadedData.PixelScaleY:F4}\n" +
                    $"Elevation: {loadedData.MinElevation:F0}m – {loadedData.MaxElevation:F0}m";

                CropAndPreview();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to load GeoTIFF:\n{e.Message}", "OK");
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        void CropAndPreview()
        {
            if (loadedData == null) return;

            // Convert lat/lon crop to pixel coordinates
            int px0 = Mathf.Clamp(LonToPixelX(cropWest), 0, loadedData.Width - 1);
            int px1 = Mathf.Clamp(LonToPixelX(cropEast), 0, loadedData.Width - 1);
            int py0 = Mathf.Clamp(LatToPixelY(cropNorth), 0, loadedData.Height - 1); // north = smaller Y
            int py1 = Mathf.Clamp(LatToPixelY(cropSouth), 0, loadedData.Height - 1);

            if (px1 <= px0 || py1 <= py0)
            {
                EditorUtility.DisplayDialog("Error", "Invalid crop region — check lat/lon values.", "OK");
                return;
            }

            croppedWidth = px1 - px0;
            croppedHeight = py1 - py0;
            croppedElevations = new float[croppedWidth * croppedHeight];
            croppedMinElev = float.MaxValue;
            croppedMaxElev = float.MinValue;

            for (int y = 0; y < croppedHeight; y++)
            {
                for (int x = 0; x < croppedWidth; x++)
                {
                    float v = loadedData.Elevations[(py0 + y) * loadedData.Width + (px0 + x)];
                    croppedElevations[y * croppedWidth + x] = v;
                    if (v > 0)
                    {
                        if (v < croppedMinElev) croppedMinElev = v;
                        if (v > croppedMaxElev) croppedMaxElev = v;
                    }
                }
            }

            // Generate preview texture (downsampled)
            int previewMax = 512;
            int pw = croppedWidth, ph = croppedHeight;
            if (pw > previewMax || ph > previewMax)
            {
                float scale = previewMax / (float)Mathf.Max(pw, ph);
                pw = Mathf.Max(1, (int)(pw * scale));
                ph = Mathf.Max(1, (int)(ph * scale));
            }

            if (previewTexture != null) DestroyImmediate(previewTexture);
            previewTexture = new Texture2D(pw, ph, TextureFormat.RGB24, false);

            float elevRange = croppedMaxElev - croppedMinElev;
            if (elevRange < 1f) elevRange = 1f;

            for (int y = 0; y < ph; y++)
            {
                for (int x = 0; x < pw; x++)
                {
                    // Sample from cropped data (bilinear)
                    float sx = x * (croppedWidth - 1f) / (pw - 1f);
                    float sy = y * (croppedHeight - 1f) / (ph - 1f);
                    float v = SampleBilinear(croppedElevations, croppedWidth, croppedHeight, sx, sy);
                    float t = Mathf.Clamp01((v - croppedMinElev) / elevRange);
                    // Preview: flip Y since texture (0,0) is bottom-left, but data row 0 is north (top)
                    previewTexture.SetPixel(x, ph - 1 - y, new Color(t, t, t));
                }
            }
            previewTexture.Apply();
            Repaint();
        }

        // ─── Terrain Generation ───

        void GenerateTerrain()
        {
            if (croppedElevations == null)
            {
                EditorUtility.DisplayDialog("Error", "Load and preview first.", "OK");
                return;
            }

            try
            {
                // Physical dimensions
                double centerLat = (cropNorth + cropSouth) / 2.0;
                float terrainWidthM = (float)((cropEast - cropWest) * 111320.0 * Math.Cos(centerLat * Math.PI / 180.0));
                float terrainLengthM = (float)((cropNorth - cropSouth) * 110574.0);

                // Heightmap resolution per tile (must be power-of-2 + 1)
                int hmRes = PickHeightmapResolution(croppedWidth, croppedHeight, tileGrid);

                float tileWidthM = terrainWidthM / tileGrid;
                float tileLengthM = terrainLengthM / tileGrid;

                // Create output directory for terrain data assets
                string assetDir = "Assets/UnityTerrain/GeneratedTerrainData";
                if (!AssetDatabase.IsValidFolder(assetDir))
                {
                    string parent = "Assets/UnityTerrain";
                    if (!AssetDatabase.IsValidFolder(parent))
                        AssetDatabase.CreateFolder("Assets", "UnityTerrain");
                    AssetDatabase.CreateFolder(parent, "GeneratedTerrainData");
                }

                // Parent object
                var parent_go = new GameObject("Grand Tetons");
                Undo.RegisterCreatedObjectUndo(parent_go, "Generate Grand Tetons Terrain");

                var terrains = new Terrain[tileGrid, tileGrid];

                for (int tz = 0; tz < tileGrid; tz++)
                {
                    for (int tx = 0; tx < tileGrid; tx++)
                    {
                        string tileName = tileGrid > 1 ? $"Terrain_{tx}_{tz}" : "Terrain";
                        float progress = (float)(tz * tileGrid + tx) / (tileGrid * tileGrid);
                        EditorUtility.DisplayProgressBar("Generating Terrain", $"Creating {tileName}...", progress);

                        // Tile X range in source data (left to right = west to east)
                        float srcX0 = (float)tx / tileGrid * croppedWidth;
                        float srcX1 = (float)(tx + 1) / tileGrid * croppedWidth;

                        // Tile Y range in source data.
                        // Data: row 0 = north, row max = south.
                        // Unity: hy=0 = Z=0 (south), hy=max = Z=tileLengthM (north).
                        // So hy=0 reads from the SOUTH end (high row), hy=max reads from NORTH end (low row).
                        // tz=0 is the southernmost tile, tz=tileGrid-1 is the northernmost.
                        float dataSouthRow = (float)(tileGrid - tz) / tileGrid * croppedHeight - 1;
                        float dataNorthRow = (float)(tileGrid - 1 - tz) / tileGrid * croppedHeight;

                        float[,] heights = new float[hmRes, hmRes];
                        for (int hy = 0; hy < hmRes; hy++)
                        {
                            for (int hx = 0; hx < hmRes; hx++)
                            {
                                float sx = Mathf.Lerp(srcX0, srcX1 - 1, (float)hx / (hmRes - 1));
                                // hy=0 → south row, hy=max → north row
                                float sy = Mathf.Lerp(dataSouthRow, dataNorthRow, (float)hy / (hmRes - 1));

                                float elev = SampleBilinear(croppedElevations, croppedWidth, croppedHeight, sx, sy);
                                // Normalize: 0 = sea level, 1 = terrainHeight
                                heights[hy, hx] = Mathf.Clamp01(elev / terrainHeight);
                            }
                        }

                        // Create TerrainData
                        var td = new TerrainData();
                        td.heightmapResolution = hmRes;
                        td.size = new Vector3(tileWidthM, terrainHeight, tileLengthM);
                        td.SetHeights(0, 0, heights);

                        AssetDatabase.CreateAsset(td, $"{assetDir}/{tileName}.asset");

                        // Create GameObject
                        var go = Terrain.CreateTerrainGameObject(td);
                        go.name = tileName;
                        go.transform.parent = parent_go.transform;
                        go.transform.localPosition = new Vector3(tx * tileWidthM, 0, tz * tileLengthM);

                        var terrain = go.GetComponent<Terrain>();
                        terrain.heightmapPixelError = 2f;
                        terrain.basemapDistance = 20000f;
                        terrain.drawInstanced = true;

                        terrains[tx, tz] = terrain;
                    }
                }

                // Set neighbors for LOD seam matching
                if (tileGrid > 1)
                {
                    for (int tz = 0; tz < tileGrid; tz++)
                    {
                        for (int tx = 0; tx < tileGrid; tx++)
                        {
                            Terrain left = tx > 0 ? terrains[tx - 1, tz] : null;
                            Terrain right = tx < tileGrid - 1 ? terrains[tx + 1, tz] : null;
                            Terrain top = tz < tileGrid - 1 ? terrains[tx, tz + 1] : null;
                            Terrain bottom = tz > 0 ? terrains[tx, tz - 1] : null;
                            terrains[tx, tz].SetNeighbors(left, top, right, bottom);
                        }
                    }
                }

                // Set camera far clip plane so the full terrain is visible
                var mainCam = Camera.main;
                if (mainCam != null && mainCam.farClipPlane < 35000f)
                {
                    mainCam.farClipPlane = 35000f;
                    Debug.Log("[TerrainGenerator] Set main camera far clip plane to 35000m");
                }

                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog("Success",
                    $"Terrain generated!\n\n" +
                    $"Size: {terrainWidthM:F0}m × {terrainLengthM:F0}m\n" +
                    $"Height: {terrainHeight:F0}m\n" +
                    $"Tiles: {tileGrid}×{tileGrid}\n" +
                    $"Resolution: {hmRes}×{hmRes} per tile",
                    "OK");

                Selection.activeGameObject = parent_go;
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Terrain generation failed:\n{e.Message}", "OK");
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ─── Helpers ───

        int LonToPixelX(double lon) =>
            (int)((lon - loadedData.OriginLon) / loadedData.PixelScaleX);

        int LatToPixelY(double lat) =>
            (int)((loadedData.OriginLat - lat) / loadedData.PixelScaleY);

        static int PickHeightmapResolution(int srcWidth, int srcHeight, int tiles)
        {
            int maxDim = Mathf.Max(srcWidth, srcHeight) / tiles;
            // Pick the smallest power-of-2+1 that's >= source dimension
            int[] options = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
            foreach (int res in options)
            {
                if (res >= maxDim) return res;
            }
            return 4097;
        }

        static float SampleBilinear(float[] data, int w, int h, float x, float y)
        {
            int x0 = Mathf.Clamp((int)x, 0, w - 1);
            int y0 = Mathf.Clamp((int)y, 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1);
            int y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = x - x0;
            float fy = y - y0;

            float v00 = data[y0 * w + x0];
            float v10 = data[y0 * w + x1];
            float v01 = data[y1 * w + x0];
            float v11 = data[y1 * w + x1];

            return Mathf.Lerp(
                Mathf.Lerp(v00, v10, fx),
                Mathf.Lerp(v01, v11, fx),
                fy);
        }

        void OnDestroy()
        {
            if (previewTexture != null) DestroyImmediate(previewTexture);
        }
    }
}

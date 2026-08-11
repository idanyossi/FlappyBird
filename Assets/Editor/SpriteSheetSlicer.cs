using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FlappyBird.EditorTools
{
    /// <summary>
    /// Splits a packed sprite sheet into individual sprite PNGs.
    ///
    /// Rather than requiring hand-drawn slice rectangles, this finds each sprite
    /// by flood-filling islands of non-transparent pixels. That works whatever
    /// layout the sheet happens to use, and survives the sheet being swapped for
    /// a different one later.
    ///
    /// Drop a sheet into Assets/Art/Source, then run
    /// Tools > Flappy Bird > Slice Sprite Sheets. Results land in
    /// Assets/Art/Sliced as piece_00, piece_01, ... ordered top-to-bottom then
    /// left-to-right, which is the reading order of most sheets.
    /// </summary>
    public static class SpriteSheetSlicer
    {
        private const string SourceFolder = "Assets/Art/Source";
        private const string OutputFolder = "Assets/Art/Sliced";
        private const int PixelsPerUnit = 100;

        /// <summary>Alpha above which a pixel counts as part of a sprite.</summary>
        private const byte AlphaThreshold = 8;

        /// <summary>
        /// Islands smaller than this are treated as stray pixels or compression
        /// noise rather than real sprites.
        /// </summary>
        private const int MinimumIslandArea = 24;

        /// <summary>
        /// Islands closer than this are merged, for sheets whose sprites contain
        /// detached parts that should stay together.
        ///
        /// Zero disables merging. On a densely packed sheet any positive value
        /// chains — each merge grows a box until it touches the next neighbour —
        /// and the whole sheet collapses into one piece. Crisp pixel art needs no
        /// merging, because every sprite is already one connected region.
        /// </summary>
        private const int MergeGap = 0;

        [MenuItem("Tools/Flappy Bird/Slice Sprite Sheets")]
        public static void SliceAll()
        {
            if (!Directory.Exists(SourceFolder))
            {
                Directory.CreateDirectory(SourceFolder);
                AssetDatabase.Refresh();
                Debug.LogWarning($"Created {SourceFolder}. Put a sprite sheet PNG in it and run this again.");
                return;
            }

            string[] sheets = Directory.GetFiles(SourceFolder, "*.png", SearchOption.TopDirectoryOnly);
            if (sheets.Length == 0)
            {
                Debug.LogWarning($"No PNG files found in {SourceFolder}.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);

            foreach (string sheetPath in sheets)
            {
                SliceSheet(sheetPath.Replace('\\', '/'));
            }

            AssetDatabase.Refresh();
        }

        private static void SliceSheet(string sheetPath)
        {
            // Read the bytes directly rather than going through the imported
            // asset: the import settings may have compressed or resized it,
            // and slicing needs the original pixels.
            Texture2D sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!sheet.LoadImage(File.ReadAllBytes(sheetPath)))
            {
                Debug.LogError($"Could not read {sheetPath} as an image.");
                return;
            }

            Color32[] pixels = sheet.GetPixels32();
            List<RectInt> islands = FindIslands(pixels, sheet.width, sheet.height);

            if (islands.Count == 0)
            {
                Debug.LogWarning($"{Path.GetFileName(sheetPath)}: found no opaque regions to slice.");
                Object.DestroyImmediate(sheet);
                return;
            }

            islands = MergeNearbyIslands(islands);
            islands.Sort(CompareReadingOrder);

            string sheetName = Path.GetFileNameWithoutExtension(sheetPath);
            string folder = $"{OutputFolder}/{sheetName}";
            Directory.CreateDirectory(folder);

            for (int i = 0; i < islands.Count; i++)
            {
                WritePiece(sheet, pixels, islands[i], $"{folder}/piece_{i:00}.png");
            }

            Object.DestroyImmediate(sheet);
            Debug.Log($"{Path.GetFileName(sheetPath)}: wrote {islands.Count} sprites to {folder}.");
        }

        /// <summary>
        /// Flood-fills each connected run of non-transparent pixels and returns
        /// the bounding box of every one found.
        /// </summary>
        private static List<RectInt> FindIslands(Color32[] pixels, int width, int height)
        {
            bool[] visited = new bool[pixels.Length];
            List<RectInt> islands = new List<RectInt>();
            Stack<int> pending = new Stack<int>();

            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || pixels[start].a < AlphaThreshold)
                {
                    continue;
                }

                int minX = int.MaxValue, maxX = int.MinValue;
                int minY = int.MaxValue, maxY = int.MinValue;
                int area = 0;

                pending.Push(start);
                visited[start] = true;

                while (pending.Count > 0)
                {
                    int index = pending.Pop();
                    int x = index % width;
                    int y = index / width;

                    area++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;

                    // Eight-way so diagonally touching pixels stay one sprite.
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;

                            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                            {
                                continue;
                            }

                            int neighbour = ny * width + nx;
                            if (visited[neighbour] || pixels[neighbour].a < AlphaThreshold)
                            {
                                continue;
                            }

                            visited[neighbour] = true;
                            pending.Push(neighbour);
                        }
                    }
                }

                if (area >= MinimumIslandArea)
                {
                    islands.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
                }
            }

            return islands;
        }

        /// <summary>
        /// Repeatedly unions any two boxes that overlap once expanded by
        /// <see cref="MergeGap"/>, so the separate parts of one sprite end up in
        /// a single piece.
        /// </summary>
        private static List<RectInt> MergeNearbyIslands(List<RectInt> islands)
        {
            bool merged = true;

            while (merged)
            {
                merged = false;

                for (int i = 0; i < islands.Count && !merged; i++)
                {
                    for (int j = i + 1; j < islands.Count && !merged; j++)
                    {
                        if (!AreNear(islands[i], islands[j]))
                        {
                            continue;
                        }

                        islands[i] = Union(islands[i], islands[j]);
                        islands.RemoveAt(j);
                        merged = true;
                    }
                }
            }

            return islands;
        }

        private static bool AreNear(RectInt a, RectInt b)
        {
            return a.xMin - MergeGap < b.xMax && b.xMin - MergeGap < a.xMax
                && a.yMin - MergeGap < b.yMax && b.yMin - MergeGap < a.yMax;
        }

        private static RectInt Union(RectInt a, RectInt b)
        {
            int xMin = Mathf.Min(a.xMin, b.xMin);
            int yMin = Mathf.Min(a.yMin, b.yMin);
            int xMax = Mathf.Max(a.xMax, b.xMax);
            int yMax = Mathf.Max(a.yMax, b.yMax);

            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>
        /// Top-to-bottom, then left-to-right. Texture Y runs upward, so a larger
        /// yMax means higher on the sheet and therefore earlier.
        /// </summary>
        private static int CompareReadingOrder(RectInt a, RectInt b)
        {
            // Treat rows within a sprite-height of each other as the same row,
            // otherwise slight vertical offsets scramble the order.
            int rowTolerance = Mathf.Max(a.height, b.height) / 2;

            if (Mathf.Abs(a.yMax - b.yMax) > rowTolerance)
            {
                return b.yMax.CompareTo(a.yMax);
            }

            return a.xMin.CompareTo(b.xMin);
        }

        private static void WritePiece(Texture2D sheet, Color32[] pixels, RectInt bounds, string path)
        {
            Color32[] region = new Color32[bounds.width * bounds.height];

            for (int y = 0; y < bounds.height; y++)
            {
                int sourceRow = (bounds.yMin + y) * sheet.width + bounds.xMin;
                int targetRow = y * bounds.width;

                for (int x = 0; x < bounds.width; x++)
                {
                    region[targetRow + x] = pixels[sourceRow + x];
                }
            }

            Texture2D piece = new Texture2D(bounds.width, bounds.height, TextureFormat.RGBA32, false);
            piece.SetPixels32(region);
            piece.Apply();

            File.WriteAllBytes(path, piece.EncodeToPNG());
            Object.DestroyImmediate(piece);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ApplySpriteSettings(path);
        }

        private static void ApplySpriteSettings(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            // Point filtering keeps the original pixel art crisp instead of
            // blurring it at the small sizes this game draws at.
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.SaveAndReimport();
        }
    }
}

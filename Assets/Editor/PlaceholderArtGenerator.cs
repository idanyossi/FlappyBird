using System.IO;
using UnityEditor;
using UnityEngine;

namespace FlappyBird.EditorTools
{
    /// <summary>
    /// Generates the game's sprite sheet procedurally into Assets/Art.
    ///
    /// The art is drawn in code from primitive shapes so the project ships with
    /// no third-party image files and nothing to attribute. Run it from
    /// Tools > Flappy Bird > Generate Placeholder Art.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        private const string ArtFolder = "Assets/Art";
        private const int PixelsPerUnit = 100;

        private static readonly Color BirdBody = new Color32(0xF6, 0xD6, 0x3B, 0xFF);
        private static readonly Color BirdBelly = new Color32(0xFA, 0xEC, 0xA0, 0xFF);
        private static readonly Color BirdBeak = new Color32(0xF0, 0x83, 0x2E, 0xFF);
        private static readonly Color BirdEye = new Color32(0x2B, 0x2B, 0x2B, 0xFF);
        private static readonly Color BirdOutline = new Color32(0x53, 0x40, 0x0F, 0xFF);

        private static readonly Color PipeBody = new Color32(0x5C, 0xC3, 0x3E, 0xFF);
        private static readonly Color PipeShade = new Color32(0x3E, 0x8E, 0x28, 0xFF);
        private static readonly Color PipeHighlight = new Color32(0x8E, 0xE0, 0x6E, 0xFF);
        private static readonly Color PipeOutline = new Color32(0x24, 0x4F, 0x18, 0xFF);

        private static readonly Color GroundTop = new Color32(0xDE, 0xD8, 0x95, 0xFF);
        private static readonly Color GroundBody = new Color32(0xC0, 0xA6, 0x5C, 0xFF);
        private static readonly Color GroundGrass = new Color32(0x74, 0xBF, 0x2E, 0xFF);

        private static readonly Color SkyTop = new Color32(0x4E, 0xC0, 0xCA, 0xFF);
        private static readonly Color SkyBottom = new Color32(0x9C, 0xE0, 0xE4, 0xFF);

        [MenuItem("Tools/Flappy Bird/Generate Placeholder Art")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(ArtFolder);

            SaveSprite("bird", DrawBird(34, 24));
            SaveSprite("pipe_body", DrawPipeBody(52, 16));
            SaveSprite("pipe_cap", DrawPipeCap(60, 26));
            // 140px tall so that at 100 pixels-per-unit one vertical tile is
            // exactly the 1.4-unit ground strip, keeping the grass on top
            // instead of repeating through the middle of the band.
            SaveSprite("ground", DrawGround(64, 140));
            SaveSprite("sky", DrawSky(64, 128));

            AssetDatabase.Refresh();
            Debug.Log($"Placeholder art written to {ArtFolder}.");
        }

        private static Texture2D DrawBird(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);
            float cx = width * 0.45f;
            float cy = height * 0.5f;
            float radiusX = width * 0.34f;
            float radiusY = height * 0.46f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f - cx) / radiusX;
                    float ny = (y + 0.5f - cy) / radiusY;
                    float distance = nx * nx + ny * ny;

                    if (distance > 1f)
                    {
                        continue;
                    }

                    // Darken the rim to give the silhouette a readable edge.
                    Color colour = distance > 0.82f
                        ? BirdOutline
                        : (ny < -0.15f ? BirdBelly : BirdBody);

                    texture.SetPixel(x, y, colour);
                }
            }

            DrawFilledRect(texture, (int)(width * 0.78f), (int)(height * 0.40f),
                           (int)(width * 0.20f), (int)(height * 0.18f), BirdBeak);
            DrawFilledCircle(texture, width * 0.66f, height * 0.66f, height * 0.10f, BirdEye);

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawPipeBody(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, ShadePipeColumn(x, width));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawPipeCap(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool edge = y < 2 || y >= height - 2;
                    texture.SetPixel(x, y, edge ? PipeOutline : ShadePipeColumn(x, width));
                }
            }

            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Vertical banding that reads as a cylinder: dark at both edges, a bright
        /// highlight just left of centre.
        /// </summary>
        private static Color ShadePipeColumn(int x, int width)
        {
            float t = (x + 0.5f) / width;

            if (t < 0.06f || t > 0.94f)
            {
                return PipeOutline;
            }

            if (t < 0.20f || t > 0.78f)
            {
                return PipeShade;
            }

            if (t > 0.26f && t < 0.40f)
            {
                return PipeHighlight;
            }

            return PipeBody;
        }

        private static Texture2D DrawGround(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);
            int grassHeight = Mathf.RoundToInt(height * 0.13f);
            int topHeight = Mathf.RoundToInt(height * 0.06f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color colour;

                    if (y >= height - grassHeight)
                    {
                        colour = GroundGrass;
                    }
                    else if (y >= height - grassHeight - topHeight)
                    {
                        colour = GroundTop;
                    }
                    else
                    {
                        // Diagonal hatching so the scroll is visible on plain dirt.
                        bool stripe = ((x + y) / 6) % 2 == 0;
                        colour = stripe ? GroundBody : GroundTop;
                    }

                    texture.SetPixel(x, y, colour);
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture2D DrawSky(int width, int height)
        {
            Texture2D texture = NewTexture(width, height);

            for (int y = 0; y < height; y++)
            {
                Color row = Color.Lerp(SkyBottom, SkyTop, (float)y / (height - 1));
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, row);
                }
            }

            texture.Apply();
            return texture;
        }

        private static void DrawFilledRect(Texture2D texture, int x0, int y0, int w, int h, Color colour)
        {
            for (int y = y0; y < y0 + h; y++)
            {
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x >= 0 && x < texture.width && y >= 0 && y < texture.height)
                    {
                        texture.SetPixel(x, y, colour);
                    }
                }
            }
        }

        private static void DrawFilledCircle(Texture2D texture, float cx, float cy, float radius, Color colour)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int maxX = Mathf.Min(texture.width - 1, Mathf.CeilToInt(cx + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int maxY = Mathf.Min(texture.height - 1, Mathf.CeilToInt(cy + radius));

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        texture.SetPixel(x, y, colour);
                    }
                }
            }
        }

        private static Texture2D NewTexture(int width, int height)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] blank = new Color[width * height];
            texture.SetPixels(blank);
            return texture;
        }

        private static void SaveSprite(string name, Texture2D texture)
        {
            string path = $"{ArtFolder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Repeat;

                // Tiled draw mode requires a full-rect mesh; the default tight
                // mesh silently falls back to a single stretched quad.
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }
        }
    }
}

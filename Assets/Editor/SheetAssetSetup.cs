using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FlappyBird.EditorTools
{
    /// <summary>
    /// Promotes the handful of pieces the game actually uses out of the sliced
    /// sheet and into Assets/Art/Game under readable names.
    ///
    /// Each sprite gets its own pixels-per-unit, chosen so the sprite's natural
    /// world size is the size the game wants. That avoids scattering transform
    /// scales through the scene, where they would silently fight the collider
    /// sizes.
    ///
    /// The piece indices come from <see cref="SpriteSheetSlicer"/>'s reading
    /// order. Re-slicing a different sheet will renumber them, so re-identify
    /// before re-running.
    /// </summary>
    public static class SheetAssetSetup
    {
        private const string SlicedFolder = "Assets/Art/Sliced/flappy_sheet";
        private const string GameFolder = "Assets/Art/Game";

        /// <summary>
        /// source piece -> output name and pixels-per-unit.
        ///
        /// Pipe: 26px wide at 16.25 ppu = 1.6 units, matching the gap the
        /// spawner was tuned around, and 160px tall = 9.85 units, tall enough to
        /// run off-screen at any gap position.
        /// Bird: 17x12 at 24 ppu = 0.71 x 0.5 units.
        /// Ground: 56px tall at 40 ppu = 1.4 units, the existing ground height.
        /// Background: 256px tall at 25.6 ppu = 10 units, the full camera height.
        /// </summary>
        private static readonly List<(string Piece, string Name, float Ppu)> Assets = new()
        {
            ("piece_64", "bird_0", 24f),
            ("piece_65", "bird_1", 24f),
            ("piece_66", "bird_2", 24f),
            ("piece_40", "pipe", 16.25f),
            ("piece_02", "ground", 40f),
            ("piece_00", "background", 25.6f),
        };

        [MenuItem("Tools/Flappy Bird/Prepare Sheet Sprites")]
        public static void Prepare()
        {
            if (!Directory.Exists(SlicedFolder))
            {
                Debug.LogError($"{SlicedFolder} does not exist. Run Slice Sprite Sheets first.");
                return;
            }

            Directory.CreateDirectory(GameFolder);
            int copied = 0;

            foreach ((string piece, string name, float ppu) in Assets)
            {
                string source = $"{SlicedFolder}/{piece}.png";
                if (!File.Exists(source))
                {
                    Debug.LogError($"Missing {source}. The sheet may have sliced differently.");
                    continue;
                }

                string target = $"{GameFolder}/{name}.png";
                File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                ApplySettings(target, ppu);
                copied++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"Prepared {copied} sprites in {GameFolder}.");
        }

        private static void ApplySettings(string path, float ppu)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            // Point filtering: this is pixel art, and bilinear turns it to mush
            // at the sizes the game draws it.
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

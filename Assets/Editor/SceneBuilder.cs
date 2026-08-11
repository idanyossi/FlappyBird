using System.IO;
using FlappyBird.Audio;
using FlappyBird.Core;
using FlappyBird.Environment;
using FlappyBird.Obstacles;
using FlappyBird.Player;
using FlappyBird.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FlappyBird.EditorTools
{
    /// <summary>
    /// Builds the playable scene and the pipe prefab from scratch.
    ///
    /// Keeping scene construction in source rather than in a hand-edited .unity
    /// file means the layout is reviewable in a diff, reproducible on any
    /// machine, and cheap to re-run after a tuning change. Run it from
    /// Tools > Flappy Bird > Build Scene.
    /// </summary>
    public static class SceneBuilder
    {
        private const string ArtFolder = "Assets/Art/Game";
        private const string AudioFolder = "Assets/Audio";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        // Playfield geometry, in world units. Orthographic size is half the
        // visible height, so 5 gives a 10-unit-tall camera.
        private const float CameraSize = 5f;
        private const float GroundTopY = -3.6f;
        private const float BirdStartX = -3f;

        // Pipe dimensions are no longer declared here: the sheet sprite is
        // imported at a pixels-per-unit that already gives the right world size,
        // so BuildPipeArm reads them off the sprite instead.

        [MenuItem("Tools/Flappy Bird/Build Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory("Assets/Scenes");

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject pipePrefab = BuildPipePrefab();

            BuildCamera();
            GameObject sky = BuildSky();
            GameObject ground = BuildGround();
            BirdController bird = BuildBird();
            PipeSpawner spawner = BuildSystems(pipePrefab);
            BuildAudio(bird);
            BuildHud();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();

            Debug.Log($"Scene built and saved to {ScenePath}. " +
                      $"Press Play to run. Sky={sky.name}, Ground={ground.name}, " +
                      $"Bird={bird.name}, Spawner={spawner.name}");
        }

        // ---------------------------------------------------------------- pipe

        /// <summary>
        /// Builds the pipe pair prefab.
        ///
        /// Each pipe hangs off an empty anchor positioned at the mouth of the gap,
        /// with the sprite offset half its own height away. That lets
        /// <see cref="Pipe.Launch"/> place the anchors at +/- half the gap and get
        /// an opening of exactly the requested height, regardless of sprite size.
        /// </summary>
        private static GameObject BuildPipePrefab()
        {
            GameObject root = new GameObject("Pipe");
            Pipe pipe = root.AddComponent<Pipe>();

            Transform topAnchor = BuildPipeArm(root.transform, "TopArm", 1f);
            Transform bottomAnchor = BuildPipeArm(root.transform, "BottomArm", -1f);

            // A thin, very tall trigger. The bird can only ever occupy the gap,
            // so height does not need to track the configured gap size, and a
            // narrow width guarantees exactly one crossing per pipe.
            GameObject scoreZone = new GameObject("ScoreZone");
            scoreZone.transform.SetParent(root.transform, false);
            scoreZone.AddComponent<ScoreZone>();
            BoxCollider2D scoreCollider = scoreZone.AddComponent<BoxCollider2D>();
            scoreCollider.isTrigger = true;
            scoreCollider.size = new Vector2(0.15f, 40f);

            SerializedObject so = new SerializedObject(pipe);
            so.FindProperty("topPipe").objectReferenceValue = topAnchor;
            so.FindProperty("bottomPipe").objectReferenceValue = bottomAnchor;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{PrefabFolder}/Pipe.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            return saved;
        }

        /// <summary>
        /// One arm of the pair: an anchor sitting at the mouth of the gap, with
        /// the pipe pushed a full half-length away from it so the sprite's near
        /// end lands exactly on the anchor.
        ///
        /// The sheet's pipe already includes its cap, at the bottom of the
        /// image. That suits the upper pipe as-is; the lower one is flipped so
        /// its cap points up into the gap.
        /// </summary>
        private static Transform BuildPipeArm(Transform parent, string name, float direction)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);

            Sprite sprite = LoadSprite("pipe");
            float pipeWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float pipeHeight = sprite.rect.height / sprite.pixelsPerUnit;

            GameObject body = new GameObject("Body");
            body.transform.SetParent(anchor.transform, false);
            body.transform.localPosition = new Vector3(0f, direction * pipeHeight * 0.5f, 0f);

            SpriteRenderer renderer = body.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.flipY = direction < 0f;
            renderer.sortingOrder = 5;

            BoxCollider2D collider = body.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(pipeWidth, pipeHeight);

            return anchor.transform;
        }

        // --------------------------------------------------------------- scene

        private static void BuildCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x4E, 0xC0, 0xCA, 0xFF);
        }

        private static GameObject BuildSky()
        {
            Sprite sprite = LoadSprite("background");

            GameObject sky = new GameObject("Sky");
            sky.transform.position = new Vector3(0f, 0f, 10f);

            SpriteRenderer renderer = sky.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            // Imported so one tile is exactly the camera height; repeated
            // sideways to span well past both edges.
            renderer.size = new Vector2(48f, 2f * CameraSize);
            renderer.sortingOrder = -10;

            return sky;
        }

        private static GameObject BuildGround()
        {
            Sprite sprite = LoadSprite("ground");
            float tileWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float groundHeight = 1.4f;

            GameObject ground = new GameObject("Ground");
            ground.transform.position = new Vector3(0f, GroundTopY - groundHeight * 0.5f, 0f);

            SpriteRenderer renderer = ground.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            // Wide enough that a full tile of scroll never exposes the right edge.
            renderer.size = new Vector2(44f, groundHeight);
            // Above the pipes, so they appear to run behind the ground rather
            // than sitting on top of it.
            renderer.sortingOrder = 10;

            BoxCollider2D collider = ground.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(44f, groundHeight);

            ScrollingLayer scrolling = ground.AddComponent<ScrollingLayer>();
            SerializedObject so = new SerializedObject(scrolling);
            so.FindProperty("scrollSpeed").floatValue = 3.5f;
            // Wrapping by exactly one tile keeps the seam invisible.
            so.FindProperty("tileWidth").floatValue = tileWidth;
            so.FindProperty("scrollWhileGameOver").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            return ground;
        }

        private static BirdController BuildBird()
        {
            GameObject bird = new GameObject("Bird");
            bird.transform.position = new Vector3(BirdStartX, 0.5f, 0f);

            SpriteRenderer renderer = bird.AddComponent<SpriteRenderer>();
            renderer.sprite = LoadSprite("bird_1");
            renderer.sortingOrder = 20;

            Rigidbody2D body = bird.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            // The bird only ever moves vertically; locking X stops a glancing
            // pipe hit from shoving it out of the camera.
            body.constraints = RigidbodyConstraints2D.FreezePositionX;

            CircleCollider2D collider = bird.AddComponent<CircleCollider2D>();
            // Deliberately tighter than the sprite. A forgiving hitbox is what
            // makes near misses feel fair rather than cheap.
            collider.radius = 0.2f;

            return bird.AddComponent<BirdController>();
        }

        private static PipeSpawner BuildSystems(GameObject pipePrefab)
        {
            GameObject systems = new GameObject("Systems");
            systems.AddComponent<GameManager>();

            PipeSpawner spawner = systems.AddComponent<PipeSpawner>();
            SerializedObject so = new SerializedObject(spawner);
            so.FindProperty("pipePrefab").objectReferenceValue = pipePrefab.GetComponent<Pipe>();
            so.ApplyModifiedPropertiesWithoutUndo();

            return spawner;
        }

        // --------------------------------------------------------------- audio

        /// <summary>
        /// Wires the sound effects up. Clips are looked up by name and left null
        /// if absent, which <see cref="GameAudio"/> tolerates, so a missing file
        /// costs you that one sound rather than breaking the scene.
        /// </summary>
        private static void BuildAudio(BirdController bird)
        {
            GameObject audioObject = new GameObject("Audio");
            audioObject.AddComponent<AudioSource>();
            GameAudio audio = audioObject.AddComponent<GameAudio>();

            SerializedObject so = new SerializedObject(audio);
            so.FindProperty("bird").objectReferenceValue = bird;
            so.FindProperty("flapClip").objectReferenceValue = LoadClip("sfx_wing");
            so.FindProperty("scoreClip").objectReferenceValue = LoadClip("sfx_point");
            so.FindProperty("hitClip").objectReferenceValue = LoadClip("sfx_hit");
            so.FindProperty("dieClip").objectReferenceValue = LoadClip("sfx_die");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AudioClip LoadClip(string name)
        {
            string path = $"{AudioFolder}/{name}.wav";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip == null)
            {
                Debug.LogWarning($"Audio clip '{path}' not found; that sound will be silent.");
            }

            return clip;
        }

        // ----------------------------------------------------------------- HUD

        private static void BuildHud()
        {
            GameObject canvasObject = new GameObject("HUD");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            TMP_Text scoreLabel = CreateLabel(canvasObject.transform, "ScoreLabel", "0", 130f,
                                              new Vector2(0f, 1f), new Vector2(0f, -220f));

            GameObject readyPanel = CreatePanel(canvasObject.transform, "ReadyPanel");
            CreateLabel(readyPanel.transform, "Title", "FLAPPY BIRD", 96f,
                        new Vector2(0f, 1f), new Vector2(0f, -420f));
            CreateLabel(readyPanel.transform, "Prompt", "PRESS SPACE TO FLAP", 52f,
                        new Vector2(0f, 0f), new Vector2(0f, 0f));

            GameObject gameOverPanel = CreatePanel(canvasObject.transform, "GameOverPanel");
            CreateLabel(gameOverPanel.transform, "Title", "GAME OVER", 96f,
                        new Vector2(0f, 1f), new Vector2(0f, -420f));
            CreateLabel(gameOverPanel.transform, "ScoreCaption", "SCORE", 44f,
                        new Vector2(0f, 0f), new Vector2(0f, 140f));
            TMP_Text finalScore = CreateLabel(gameOverPanel.transform, "FinalScore", "0", 96f,
                                              new Vector2(0f, 0f), new Vector2(0f, 40f));
            CreateLabel(gameOverPanel.transform, "BestCaption", "BEST", 44f,
                        new Vector2(0f, 0f), new Vector2(0f, -80f));
            TMP_Text bestScore = CreateLabel(gameOverPanel.transform, "BestScore", "0", 96f,
                                             new Vector2(0f, 0f), new Vector2(0f, -180f));
            CreateLabel(gameOverPanel.transform, "Prompt", "PRESS SPACE TO RETRY", 46f,
                        new Vector2(0f, 0f), new Vector2(0f, -360f));

            GameHud hud = canvasObject.AddComponent<GameHud>();
            SerializedObject so = new SerializedObject(hud);
            so.FindProperty("scoreLabel").objectReferenceValue = scoreLabel;
            so.FindProperty("readyPanel").objectReferenceValue = readyPanel;
            so.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
            so.FindProperty("finalScoreLabel").objectReferenceValue = finalScore;
            so.FindProperty("bestScoreLabel").objectReferenceValue = bestScore;
            so.ApplyModifiedPropertiesWithoutUndo();

            EnsureEventSystem();
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return panel;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, string text,
                                            float fontSize, Vector2 anchor, Vector2 position)
        {
            GameObject label = new GameObject(name, typeof(RectTransform));
            label.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)label.transform;
            // Anchored across the full width so text stays centred at any aspect.
            rect.anchorMin = new Vector2(0f, anchor.y);
            rect.anchorMax = new Vector2(1f, anchor.y);
            rect.pivot = new Vector2(0.5f, anchor.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(0f, fontSize * 1.6f);

            TextMeshProUGUI tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            ApplyOutline(tmp);

            return tmp;
        }

        /// <summary>
        /// Adds a dark outline for legibility against the sky.
        ///
        /// Setting the outline instantiates a material from the font's shared
        /// material. If TextMeshPro's essential resources have not been imported
        /// there is no font and no material, and the assignment throws — which
        /// would abort the whole scene build over a cosmetic detail. The text
        /// itself renders fine without an outline, so this degrades quietly.
        /// </summary>
        private static void ApplyOutline(TMP_Text label)
        {
            if (label.font == null || label.fontSharedMaterial == null)
            {
                Debug.LogWarning(
                    "TextMeshPro has no default font asset, so label outlines were skipped. " +
                    "Import them via Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }

            label.outlineWidth = 0.25f;
            label.outlineColor = new Color32(0x2B, 0x2B, 0x2B, 0xFF);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // --------------------------------------------------------------- utils

        private static Sprite LoadSprite(string name)
        {
            string path = $"{ArtFolder}/{name}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (sprite == null)
            {
                throw new FileNotFoundException(
                    $"Sprite '{path}' is missing. Run Tools > Flappy Bird > " +
                    "Generate Placeholder Art first.");
            }

            return sprite;
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;

            foreach (EditorBuildSettingsScene entry in existing)
            {
                if (entry.path == ScenePath)
                {
                    return;
                }
            }

            EditorBuildSettingsScene[] updated = new EditorBuildSettingsScene[existing.Length + 1];
            existing.CopyTo(updated, 0);
            updated[existing.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}

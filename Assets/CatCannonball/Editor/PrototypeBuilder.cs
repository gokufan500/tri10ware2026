using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CatCannonball.Editor
{
    /// <summary>
    /// Creates the supplied starter assets from ordinary Unity objects. Designers work with the
    /// resulting prefabs and scenes; this builder is only needed to restore the original demo.
    /// </summary>
    public static class PrototypeBuilder
    {
        public const string Root = "Assets/CatCannonball";
        public const string TitlePath = Root + "/Scenes/TitleScreen.unity";
        public const string FirstLevelPath = Root + "/Scenes/FirstLevel.unity";
        public const string TutorialPath = Root + "/Scenes/Tutorial.unity";
        public const string TemplatePath = Root + "/Scenes/LevelTemplate.unity";
        public const string SettingsPath = Root + "/Settings/DefaultMovement.asset";
        public const string TutorialSettingsPath = Root + "/Settings/StageOneMovement.asset";
        public const string CameraSettingsPath = Root + "/Settings/DefaultCamera.asset";
        public const string TutorialCameraSettingsPath = Root + "/Settings/StageOneCamera.asset";
        public const string StageTwoCameraSettingsPath = Root + "/Settings/StageTwoCamera.asset";
        public const string CombatFeedbackSettingsPath = Root + "/Settings/CombatFeedback.asset";
        public const string CombatFeedbackPath = Root + "/Prefabs/Gameplay/CombatFeedback.prefab";
        public const string SpeedEffectPath = Root + "/Prefabs/Effects/DamageSpeedParticles.prefab";
        public const string ImpactEffectPath = Root + "/Prefabs/Effects/EnemyImpactParticles.prefab";
        public const string TutorialSequencePath = Root + "/Prefabs/Gameplay/TutorialSequence.prefab";
        public const string PlayerPath = Root + "/Prefabs/Player/Player.prefab";
        public const string SystemsPath = Root + "/Prefabs/Gameplay/GameSystems.prefab";
        public const string CameraRigPath = Root + "/Prefabs/Gameplay/CameraRig.prefab";
        public const string CameraBoundsPath = Root + "/Prefabs/Level/CameraBounds.prefab";
        public const string MenuSystemsPath = Root + "/Prefabs/Gameplay/MenuSystems.prefab";
        public const string StageOneTutorialPath = Root + "/Prefabs/Gameplay/StageOneTutorial.prefab";
        public const string StageTwoTutorialPath = Root + "/Prefabs/Gameplay/StageTwoTutorial.prefab";
        public const int PlayerLayer = 8;
        public const int SolidLayer = 9;
        public const int PlatformLayer = 10;
        public const int TargetLayer = 11;
        public const int HookOnlyLayer = 12;
        public const int TriggerLayer = 13;
        private static readonly Color s_solidColor = new Color(0.43f, 0.47f, 0.52f);
        private static readonly Color s_platformColor = new Color(0.68f, 0.73f, 0.79f);
        private static readonly Color s_hookColor = new Color(0.1f, 0.88f, 0.95f);
        private static readonly Color s_targetColor = new Color(1f, 0.5f, 0.13f);
        private static readonly Color s_dangerColor = new Color(0.95f, 0.24f, 0.25f, 0.45f);
        private static Sprite s_square;
        private static Sprite s_circle;
        private static Material s_ropeMaterial;
        private static Material s_particleMaterial;
        private static PhysicsMaterial2D s_contactMaterial;
        public static string[] ScenePaths => new[]
        {
            TitlePath,
            FirstLevelPath,
            TutorialPath,
            TemplatePath
        };

        [MenuItem("Cat Cannonball/Rebuild Demo Assets", priority = 30)]
        private static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if ((File.Exists(FirstLevelPath) || File.Exists(TemplatePath)) && !EditorUtility.DisplayDialog("Restore the supplied demonstration?", "This restores the supplied prefabs, movement/camera/combat profiles, TitleScreen, FirstLevel, Tutorial and LevelTemplate. " + "Your other scenes and profiles are kept, but they may reference these shared prefabs. Duplicate your assets or commit changes before rebuilding.", "Rebuild Demo Assets", "Cancel"))
            {
                return;
            }

            Build();
        }

        /// <summary>Entry point for Unity -batchmode -executeMethod CatCannonball.Editor.PrototypeBuilder.Build.</summary>
        public static void Build()
        {
            EnsureFolders();
            MigrateSuppliedScenes();
            ConfigureProject();
            CreateArtAndMaterials();
            MovementSettings settings = RestoreSuppliedProfile<MovementSettings>(SettingsPath);
            RestoreSuppliedProfile<MovementSettings>(TutorialSettingsPath);
            CameraSettings cameraSettings = RestoreSuppliedProfile<CameraSettings>(CameraSettingsPath);
            RestoreTutorialCamera();
            RestoreStageTwoCamera();
            RestoreSuppliedProfile<CombatFeedbackSettings>(CombatFeedbackSettingsPath);
            CreatePlayerPrefab(settings);
            CreateLevelPrefabs();
            CreateGameplayPrefabs();
            CreateSystemsPrefab();
            CreateCameraPrefabs(cameraSettings);
            CreateCombatFeedbackPrefab();
            CreateMenuAndTutorialPrefabs();
            SetSuppliedBuildScenes();
            BuildTitleScene();
            BuildScene(FirstLevelPath, SceneKind.FirstLevel);
            BuildScene(TutorialPath, SceneKind.Tutorial);
            BuildScene(TemplatePath, SceneKind.Template);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(TitlePath);
            Debug.Log("Cat Cannonball: title, first level, tutorial, level template, profiles and prefabs are ready. Open Cat Cannonball > Level Designer to begin.");
        }

        /// <summary>Refreshes the combined tutorial and controllers while retaining existing profiles.</summary>
        public static void RebuildStageOneTutorial()
        {
            EnsureFolders();
            if (!AssetDatabase.LoadAssetAtPath<MovementSettings>(TutorialSettingsPath))
            {
                RestoreSuppliedProfile<MovementSettings>(TutorialSettingsPath);
            }

            if (!AssetDatabase.LoadAssetAtPath<CameraSettings>(TutorialCameraSettingsPath))
            {
                RestoreTutorialCamera();
            }

            if (!AssetDatabase.LoadAssetAtPath<CameraSettings>(StageTwoCameraSettingsPath))
            {
                RestoreStageTwoCamera();
            }

            CreateStageOneTutorialPrefab();
            CreateStageTwoTutorialPrefab();
            CreateTutorialSequencePrefab();
            BuildScene(TutorialPath, SceneKind.Tutorial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Cat Cannonball: combined tutorial rebuilt. Existing tuning profiles and other authored scenes are retained.");
        }

        /// <summary>Update scene organization without restoring designer tuning or rebuilding geometry.</summary>
        public static void MigrateExistingProject()
        {
            MigrateSuppliedScenes();
            SetSuppliedBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetSuppliedBuildScenes()
        {
            string[] paths = ScenePaths;
            EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                scenes[i] = new EditorBuildSettingsScene(paths[i], true);
            }

            EditorBuildSettings.scenes = scenes;
        }

        private static void MigrateSuppliedScenes()
        {
            const string PreviousFirstLevelPath = Root + "/Scenes/CombatSandbox.unity";
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(FirstLevelPath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviousFirstLevelPath))
            {
                string error = AssetDatabase.MoveAsset(PreviousFirstLevelPath, FirstLevelPath);
                if (!string.IsNullOrEmpty(error))
                {
                    throw new InvalidOperationException("Could not rename the first level: " + error);
                }
            }

            foreach (string name in new[]
            {
                "FlowCourse",
                "MechanicsPlayground",
                "TutorialStageTwo"
            })
            {
                AssetDatabase.DeleteAsset(Root + "/Scenes/" + name + ".unity");
            }
        }

        // Explicit restore only: retain existing GUIDs while refreshing the supplied presets.
        // Duplicated team profiles are never touched by this builder.

        private static T RestoreSuppliedProfile<T>(string path)
            where T : ScriptableObject
        {
            T defaults = ScriptableObject.CreateInstance<T>();
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(defaults, path);
                return defaults;
            }

            EditorUtility.CopySerialized(defaults, asset);
            UnityEngine.Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void RestoreTutorialCamera()
        {
            CameraSettings framing = RestoreSuppliedProfile<CameraSettings>(TutorialCameraSettingsPath);
            framing.OrthographicSize = 12f;
            framing.Offset = new Vector2(1f, 5f);
            framing.MaxLagY = 6f;
            EditorUtility.SetDirty(framing);
        }

        private static void RestoreStageTwoCamera()
        {
            CameraSettings framing = RestoreSuppliedProfile<CameraSettings>(StageTwoCameraSettingsPath);
            framing.OrthographicSize = 9f;
            framing.Offset = new Vector2(0, 2f);
            framing.MaxLagY = 4f;
            EditorUtility.SetDirty(framing);
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                "Scenes",
                "Prefabs/Player",
                "Prefabs/Level",
                "Prefabs/Gameplay",
                "Prefabs/Effects",
                "Settings",
                "Materials",
                "Art"
            })
            {
                Directory.CreateDirectory(Root + "/" + folder);
            }

            AssetDatabase.Refresh();
        }

        private static void ConfigureProject()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(PlayerLayer).stringValue = "Player";
            layers.GetArrayElementAtIndex(SolidLayer).stringValue = "Solid";
            layers.GetArrayElementAtIndex(PlatformLayer).stringValue = "Platform";
            layers.GetArrayElementAtIndex(TargetLayer).stringValue = "Target";
            layers.GetArrayElementAtIndex(HookOnlyLayer).stringValue = "HookOnly";
            layers.GetArrayElementAtIndex(TriggerLayer).stringValue = "Trigger";
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            Physics2D.gravity = new Vector2(0, -20);
            Physics2D.velocityIterations = 12;
            Physics2D.positionIterations = 8;
            Physics2D.maxTranslationSpeed = 10000;
            Physics2D.IgnoreLayerCollision(PlayerLayer, HookOnlyLayer, true);
            Physics2D.IgnoreLayerCollision(PlayerLayer, PlayerLayer, true);
            Physics2D.IgnoreLayerCollision(PlayerLayer, TargetLayer, false);
            // Unity 6000.4.1f1 lists DX12 device-crash issues. This simple Built-in project uses DX11.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            // The motor owns friction and impact response, so prefab colliders have no implicit friction.
            Time.fixedDeltaTime = 1f / 120f;
            // Legacy keyboard/mouse input keeps the starter project independent of input packages.
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty inputHandler = playerSettings.FindProperty("activeInputHandler");
            if (inputHandler != null)
            {
                inputHandler.intValue = 0;
            }

            playerSettings.ApplyModifiedPropertiesWithoutUndo();
            var timeSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            SerializedProperty timestep = timeSettings.FindProperty("Fixed Timestep");
            if (timestep == null)
            {
                timestep = timeSettings.FindProperty("m_FixedTimestep");
            }

            if (timestep == null)
            {
                throw new InvalidOperationException("Could not find Unity's Fixed Timestep project setting.");
            }

            if (timestep.propertyType == SerializedPropertyType.Float)
            {
                timestep.floatValue = 1f / 120f;
            }
            else
            {
                // Unity 6000.4 stores this as RationalTime rather than a float.
                // Preserve its tick rate and save an exact 1/120-second count.
                SerializedProperty count = timestep.FindPropertyRelative("m_Count");
                SerializedProperty rate = timestep.FindPropertyRelative("m_Rate");
                SerializedProperty numerator = rate?.FindPropertyRelative("m_Numerator");
                SerializedProperty denominator = rate?.FindPropertyRelative("m_Denominator");
                if (count == null || numerator == null || denominator == null)
                {
                    throw new InvalidOperationException("Unsupported Fixed Timestep serialization. Set it to 1/120 second in Project Settings > Time.");
                }

                count.longValue = (long)Math.Round((double)numerator.longValue / Math.Max(1L, denominator.longValue) / 120d);
            }

            timeSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(timeSettings.targetObject);
        }

        private static void CreateArtAndMaterials()
        {
            s_square = CreateSprite("Square", false);
            s_circle = CreateSprite("Circle", true);
            string ropePath = Root + "/Materials/Rope.mat";
            s_ropeMaterial = AssetDatabase.LoadAssetAtPath<Material>(ropePath);
            if (s_ropeMaterial == null)
            {
                s_ropeMaterial = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(s_ropeMaterial, ropePath);
            }

            s_ropeMaterial.color = s_hookColor;
            string particlePath = Root + "/Materials/CombatParticles.mat";
            s_particleMaterial = AssetDatabase.LoadAssetAtPath<Material>(particlePath);
            if (!s_particleMaterial)
            {
                s_particleMaterial = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(s_particleMaterial, particlePath);
            }

            s_particleMaterial.mainTexture = s_circle.texture;
            s_particleMaterial.color = Color.white;
            EditorUtility.SetDirty(s_particleMaterial);
            string materialPath = Root + "/Materials/NoFriction.physicsMaterial2D";
            s_contactMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
            if (s_contactMaterial == null)
            {
                s_contactMaterial = new PhysicsMaterial2D("No Friction");
                AssetDatabase.CreateAsset(s_contactMaterial, materialPath);
            }

            s_contactMaterial.friction = 0;
            s_contactMaterial.bounciness = 0;
            EditorUtility.SetDirty(s_ropeMaterial);
            EditorUtility.SetDirty(s_contactMaterial);
        }

        private static Sprite CreateSprite(string name, bool round)
        {
            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(Size / 2f, Size / 2f));
                    pixels[y * Size + x] = round && distance > Size / 2f - 1 ? Color.clear : Color.white;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            string path = Root + "/Art/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void CreatePlayerPrefab(MovementSettings settings)
        {
            var player = new GameObject("Player");
            player.layer = PlayerLayer;
            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = s_circle;
            renderer.color = Color.white;
            renderer.sortingOrder = 5;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            CircleCollider2D collider = player.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;
            collider.sharedMaterial = s_contactMaterial;
            PlayerInputReader input = player.AddComponent<PlayerInputReader>();
            PlayerMotor motor = player.AddComponent<PlayerMotor>();
            GrappleController grapple = player.AddComponent<GrappleController>();
            motor.Settings = settings;
            motor.Input = input;
            motor.Grapple = grapple;
            grapple.Settings = settings;
            grapple.Input = input;
            grapple.Motor = motor;
            LineRenderer line = player.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = player.AddComponent<LineRenderer>();
            }

            line.sharedMaterial = s_ropeMaterial;
            line.startColor = s_hookColor;
            line.endColor = s_hookColor;
            line.startWidth = 0.07f;
            line.endWidth = 0.07f;
            line.positionCount = 0;
            line.useWorldSpace = true;
            line.sortingOrder = 4;
            SavePrefab(player, PlayerPath);
        }

        private static void CreateLevelPrefabs()
        {
            GameObject solid = MakeBox("SolidBlock", SolidLayer, s_solidColor, false);
            SavePrefab(solid, LevelPrefabPath("SolidBlock"));
            GameObject platform = MakeBox("OneWayPlatform", PlatformLayer, s_platformColor, false);
            platform.transform.localScale = new Vector3(5, 0.35f, 1);
            platform.GetComponent<BoxCollider2D>().usedByEffector = true;
            PlatformEffector2D effector = platform.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.useOneWayGrouping = false;
            effector.surfaceArc = 150;
            platform.AddComponent<OneWayPlatform>();
            SavePrefab(platform, LevelPrefabPath("OneWayPlatform"));
            var anchor = new GameObject("HookAnchor");
            anchor.layer = HookOnlyLayer;
            SpriteRenderer renderer = anchor.AddComponent<SpriteRenderer>();
            renderer.sprite = s_circle;
            renderer.color = s_hookColor;
            renderer.sortingOrder = 2;
            anchor.AddComponent<CircleCollider2D>().radius = 0.48f;
            anchor.transform.localScale = new Vector3(0.8f, 0.8f, 1);
            SavePrefab(anchor, LevelPrefabPath("HookAnchor"));
        }

        private static void CreateGameplayPrefabs()
        {
            GameObject target = MakeTarget("SpeedTarget", 1, 8, 0.45f);
            SavePrefab(target, GameplayPrefabPath("SpeedTarget"));
            GameObject heavyTarget = MakeTarget("HeavyTarget", 8, 8, 0.6f);
            heavyTarget.transform.localScale = new Vector3(1.5f, 1.5f, 1);
            SavePrefab(heavyTarget, GameplayPrefabPath("HeavyTarget"));
            var pair = new GameObject("WeakPointPair");
            GameObject first = MakeTarget("Weak Point A", 8, 8, 0.6f);
            GameObject second = MakeTarget("Weak Point B", 8, 8, 0.6f);
            first.transform.SetParent(pair.transform, false);
            second.transform.SetParent(pair.transform, false);
            first.transform.localPosition = new Vector3(-1.4f, 0, 0);
            second.transform.localPosition = new Vector3(1.4f, 0, 0);
            first.transform.localScale = second.transform.localScale = new Vector3(1.3f, 1.3f, 1);
            WeakPointGroup group = pair.AddComponent<WeakPointGroup>();
            group.RequireAlternatingHits = true;
            group.WeakPoints = new[]
            {
                first.GetComponent<SpeedTarget>(),
                second.GetComponent<SpeedTarget>()
            };
            foreach (SpeedTarget point in group.WeakPoints)
            {
                point.WeakPointGroup = group;
            }

            SavePrefab(pair, GameplayPrefabPath("WeakPointPair"));
            GameObject deathZone = MakeBox("DeathZone", TriggerLayer, s_dangerColor, true);
            deathZone.transform.localScale = new Vector3(4, 0.7f, 1);
            deathZone.AddComponent<DeathZone>();
            SavePrefab(deathZone, GameplayPrefabPath("DeathZone"));
        }

        private static GameObject MakeTarget(string name, int hitPoints, float speedPerDamage, float cooldown)
        {
            var target = new GameObject(name);
            target.layer = TargetLayer;
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = s_circle;
            renderer.color = s_targetColor;
            renderer.sortingOrder = 2;
            CircleCollider2D collider = target.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.isTrigger = true;
            SpeedTarget speedTarget = target.AddComponent<SpeedTarget>();
            speedTarget.HitPoints = hitPoints;
            speedTarget.SpeedPerDamage = speedPerDamage;
            speedTarget.HitCooldown = cooldown;
            speedTarget.ResetOnRespawn = true;
            speedTarget.CombatSettings = AssetDatabase.LoadAssetAtPath<CombatFeedbackSettings>(CombatFeedbackSettingsPath);
            target.AddComponent<EnemyHealthDisplay>().Target = speedTarget;
            return target;
        }

        private static void CreateCombatFeedbackPrefab()
        {
            CreateParticlePrefab("DamageSpeedParticles", SpeedEffectPath, true);
            CreateParticlePrefab("EnemyImpactParticles", ImpactEffectPath, false);
            var feedbackObject = new GameObject("CombatFeedback");
            CombatFeedbackController feedback = feedbackObject.AddComponent<CombatFeedbackController>();
            feedback.Settings = AssetDatabase.LoadAssetAtPath<CombatFeedbackSettings>(CombatFeedbackSettingsPath);
            feedback.SpeedEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpeedEffectPath).GetComponent<ParticleSystem>();
            feedback.ImpactEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactEffectPath).GetComponent<ParticleSystem>();
            SavePrefab(feedbackObject, CombatFeedbackPath);
        }

        private static void CreateParticlePrefab(string name, string path, bool loop)
        {
            var effectObject = new GameObject(name);
            ParticleSystem particles = effectObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.duration = 0.6f;
            main.startLifetime = 0.25f;
            main.startSpeed = loop ? 0f : 3f;
            main.startSize = 0.12f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = loop ? 200 : 96;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = !loop;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f;
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = s_particleMaterial;
            renderer.sortingOrder = 5;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SavePrefab(effectObject, path);
        }

        private static void CreateSystemsPrefab()
        {
            var systems = new GameObject("GameSystems");
            systems.AddComponent<LevelFlow>();
            SceneTransition transition = systems.AddComponent<SceneTransition>();
            systems.AddComponent<PrototypeHud>().Transitions = transition;
            SavePrefab(systems, SystemsPath);
        }

        private static void CreateCameraPrefabs(CameraSettings settings)
        {
            var rig = new GameObject("CameraRig");
            rig.tag = "MainCamera";
            rig.transform.position = new Vector3(0, 0, -10);
            Camera camera = rig.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = settings.OrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.065f, 0.075f, 0.095f);
            rig.AddComponent<AudioListener>();
            rig.AddComponent<FollowCamera2D>().Settings = settings;
            SavePrefab(rig, CameraRigPath);
            var bounds = new GameObject("CameraBounds");
            bounds.AddComponent<CameraBounds2D>().Size = new Vector2(38, 24);
            SavePrefab(bounds, CameraBoundsPath);
        }

        private static void CreateMenuAndTutorialPrefabs()
        {
            var menu = new GameObject("MenuSystems");
            SceneTransition transition = menu.AddComponent<SceneTransition>();
            TitleScreen title = menu.AddComponent<TitleScreen>();
            title.Transitions = transition;
            title.PlaySceneIndex = 1;
            title.TutorialSceneIndex = 2;
            SavePrefab(menu, MenuSystemsPath);
            CreateStageOneTutorialPrefab();
            CreateStageTwoTutorialPrefab();
            CreateTutorialSequencePrefab();
        }

        private static void CreateStageOneTutorialPrefab()
        {
            var tutorial = new GameObject("StageOneTutorial");
            StageOneTutorial controller = tutorial.AddComponent<StageOneTutorial>();
            controller.PromptView = tutorial.GetComponent<TutorialPromptView>();
            controller.AutoStart = false;
            SavePrefab(tutorial, StageOneTutorialPath);
        }

        private static void CreateStageTwoTutorialPrefab()
        {
            var tutorial = new GameObject("StageTwoTutorial");
            StageTwoTutorial controller = tutorial.AddComponent<StageTwoTutorial>();
            controller.PromptView = tutorial.AddComponent<TutorialPromptView>();
            controller.TitleSceneIndex = 0;
            controller.FreePlaySeconds = 10f;
            controller.AutoStart = false;
            SavePrefab(tutorial, StageTwoTutorialPath);
        }

        private static void CreateTutorialSequencePrefab()
        {
            var sequenceObject = new GameObject("TutorialSequence");
            sequenceObject.AddComponent<TutorialSequence>();
            SavePrefab(sequenceObject, TutorialSequencePath);
        }

        private static void BuildTitleScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var systems = new GameObject("00 Systems");
            Instantiate(MenuSystemsPath, Vector3.zero, systems.transform);
            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(systems.transform);
            cameraObject.transform.position = new Vector3(0, 0, -10);
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.065f, 0.075f, 0.095f);
            cameraObject.AddComponent<AudioListener>();
            EditorSceneManager.SaveScene(scene, TitlePath);
        }

        private static GameObject MakeBox(string name, int layer, Color color, bool trigger)
        {
            var obj = new GameObject(name);
            obj.layer = layer;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = s_square;
            renderer.color = color;
            renderer.sortingOrder = trigger ? 1 : 0;
            BoxCollider2D collider = obj.AddComponent<BoxCollider2D>();
            collider.isTrigger = trigger;
            collider.sharedMaterial = s_contactMaterial;
            return obj;
        }

        private static void SavePrefab(GameObject obj, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(obj, path);
            UnityEngine.Object.DestroyImmediate(obj);
        }

        public static string LevelPrefabPath(string name) => Root + "/Prefabs/Level/" + name + ".prefab";
        public static string GameplayPrefabPath(string name) => Root + "/Prefabs/Gameplay/" + name + ".prefab";
        private enum SceneKind
        {
            FirstLevel,
            Tutorial,
            Template
        }

        private static void BuildScene(string path, SceneKind kind)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateFoundation(kind);
            EditorSceneManager.SaveScene(scene, path);
        }

        /// <summary>Shared foundation used by both shipped scenes and the new-level wizard.</summary>
        public static void CreateLevelTemplate()
        {
            CreateFoundation(SceneKind.Template);
        }

        private static void CreateFoundation(SceneKind kind)
        {
            var systemsFolder = new GameObject("00 Systems");
            var geometry = new GameObject("01 Level Geometry");
            var gameplay = new GameObject("02 Gameplay");
            var playerFolder = new GameObject("03 Player");
            var labels = new GameObject("04 Labels");
            bool firstLevel = kind == SceneKind.FirstLevel;
            bool tutorial = kind == SceneKind.Tutorial;
            GameObject cameraObject = Instantiate(CameraRigPath, new Vector3(0, 0, -10), systemsFolder.transform);
            Camera camera = cameraObject.GetComponent<Camera>();
            FollowCamera2D follow = cameraObject.GetComponent<FollowCamera2D>();
            if (tutorial)
            {
                follow.Settings = AssetDatabase.LoadAssetAtPath<CameraSettings>(TutorialCameraSettingsPath);
            }

            GameObject boundsObject = Instantiate(CameraBoundsPath, tutorial ? new Vector2(25, 0) : firstLevel ? Vector2.zero : new Vector2(16, 3), systemsFolder.transform);
            CameraBounds2D bounds = boundsObject.GetComponent<CameraBounds2D>();
            bounds.Size = tutorial ? new Vector2(78, 50) : new Vector2(38, 24);
            follow.Bounds = bounds;
            GameObject systems = Instantiate(SystemsPath, Vector3.zero, systemsFolder.transform);
            LevelFlow level = systems.GetComponent<LevelFlow>();
            PrototypeHud hud = systems.GetComponent<PrototypeHud>();
            var spawn = new GameObject("Start Spawn (move this to change the start)");
            spawn.transform.SetParent(playerFolder.transform);
            spawn.transform.position = firstLevel ? new Vector3(-13, -7.35f, 0) : tutorial ? new Vector3(0, 0.46f, 0) : new Vector3(1, 1.1f, 0);
            GameObject player = Instantiate(PlayerPath, spawn.transform.position, playerFolder.transform);
            PlayerMotor motor = player.GetComponent<PlayerMotor>();
            if (tutorial)
            {
                motor.Settings = AssetDatabase.LoadAssetAtPath<MovementSettings>(TutorialSettingsPath);
                motor.Grapple.Settings = motor.Settings;
            }

            PlayerInputReader input = player.GetComponent<PlayerInputReader>();
            input.AimCamera = camera;
            level.Player = motor;
            level.SpawnPoint = spawn.transform;
            level.KillY = tutorial ? -25 : -13;
            level.FollowCamera = follow;
            follow.Target = player.transform;
            hud.Motor = motor;
            hud.Input = input;
            hud.LevelFlow = level;
            hud.ViewCamera = camera;
            hud.Transitions = systems.GetComponent<SceneTransition>();
            hud.TitleSceneIndex = 0;
            hud.ShowDiagnostics = !tutorial;
            hud.ShowHelp = !tutorial;
            if (!tutorial)
            {
                CombatFeedbackController feedback = Instantiate(CombatFeedbackPath, Vector3.zero, systemsFolder.transform).GetComponent<CombatFeedbackController>();
                feedback.Player = motor;
                feedback.FollowCamera = follow;
                feedback.SceneTransition = hud.Transitions;
            }

            if (tutorial)
            {
                FillCombinedTutorial(geometry.transform, gameplay.transform, labels.transform, systemsFolder.transform, level, bounds);
            }
            else if (firstLevel)
            {
                Solid("Floor", new Vector2(0, -8.75f), new Vector2(36, 1), geometry.transform);
                Solid("Left Wall", new Vector2(-17.75f, 0), new Vector2(0.5f, 18), geometry.transform);
                Solid("Right Wall", new Vector2(17.75f, 0), new Vector2(0.5f, 18), geometry.transform);
                Solid("Ceiling", new Vector2(0, 9.75f), new Vector2(36, 0.5f), geometry.transform);
                FillFirstLevel(geometry.transform, gameplay.transform, labels.transform);
            }
            else
            {
                Solid("Floor (resize to extend your level)", new Vector2(16, -0.5f), new Vector2(36, 1), geometry.transform);
                GameObject platform = Instantiate(LevelPrefabPath("OneWayPlatform"), new Vector2(12, 3), geometry.transform);
                platform.name = "Example One-Way Platform";
                Instantiate(LevelPrefabPath("HookAnchor"), new Vector2(5, 8), geometry.transform);
                Instantiate(LevelPrefabPath("HookAnchor"), new Vector2(13, 9), geometry.transform);
                Instantiate(LevelPrefabPath("HookAnchor"), new Vector2(21, 8), geometry.transform);
                Label("LEVEL TEMPLATE", new Vector2(8, 11.5f), labels.transform, 0.25f);
                Label("Place prefabs. Move Start Spawn. Resize CameraBounds. Tune the shared profiles.", new Vector2(8, 10.5f), labels.transform, 0.14f);
            }

            follow.SnapToTarget();
            foreach (DeathZone deathZone in UnityEngine.Object.FindObjectsByType<DeathZone>())
            {
                deathZone.LevelFlow = level;
            }

            // Runtime references point between scene instances, so they must be recorded as prefab
            // overrides. This also preserves placement/scale when reopening a generated scene.
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void FillCombinedTutorial(Transform geometry, Transform gameplay, Transform labels, Transform systems, LevelFlow level, CameraBounds2D firstBounds)
        {
            var rooms = new GameObject("01 Tutorial Rooms");
            var firstRoom = new GameObject("01 Swing Route");
            firstRoom.transform.SetParent(rooms.transform, false);
            geometry.SetParent(firstRoom.transform, true);
            gameplay.SetParent(firstRoom.transform, true);
            labels.SetParent(firstRoom.transform, true);
            FillTutorial(geometry, gameplay, labels, systems, level);
            StageOneTutorial first = systems.GetComponentInChildren<StageOneTutorial>();
            var secondRoom = new GameObject("02 Yank Playground");
            secondRoom.transform.SetParent(rooms.transform, false);
            var secondGeometry = new GameObject("01 Level Geometry");
            secondGeometry.transform.SetParent(secondRoom.transform, false);
            StageTwoTutorial second = FillStageTwo(secondGeometry.transform, systems, level, new Vector2(100, 0));
            CameraBounds2D secondBounds = Instantiate(CameraBoundsPath, new Vector2(100, 7), systems).GetComponent<CameraBounds2D>();
            secondBounds.name = "CameraBounds - Yank Playground";
            secondBounds.Size = new Vector2(28, 14);
            var secondSpawn = new GameObject("Practice Spawn - R resets here during free play");
            secondSpawn.transform.SetParent(level.SpawnPoint.parent, false);
            secondSpawn.transform.position = new Vector3(100, 0.46f, 0);
            TutorialSequence sequence = Instantiate(TutorialSequencePath, Vector3.zero, systems).GetComponent<TutorialSequence>();
            sequence.Player = level.Player;
            sequence.SceneTransition = level.GetComponent<SceneTransition>();
            sequence.FollowCamera = level.FollowCamera;
            sequence.LevelFlow = level;
            sequence.StageOne = first;
            sequence.StageTwo = second;
            sequence.StageOneRoot = firstRoom;
            sequence.StageTwoRoot = secondRoom;
            sequence.StageOneSpawn = level.SpawnPoint;
            sequence.StageTwoSpawn = secondSpawn.transform;
            sequence.StageOneBounds = firstBounds;
            sequence.StageTwoBounds = secondBounds;
            sequence.StageOneCameraSettings = AssetDatabase.LoadAssetAtPath<CameraSettings>(TutorialCameraSettingsPath);
            sequence.StageTwoCameraSettings = AssetDatabase.LoadAssetAtPath<CameraSettings>(StageTwoCameraSettingsPath);
            first.Sequence = second.Sequence = sequence;
            first.enabled = second.enabled = false;
            secondRoom.SetActive(false);
        }

        private static void FillTutorial(Transform geometry, Transform gameplay, Transform labels, Transform systems, LevelFlow level)
        {
            // The whole lesson is one continuous physical route. The gates are editor handles,
            // not teleport stations, and are invisible during play.
            Solid("Stage 1 - Starting Runway", new Vector2(0, -0.5f), new Vector2(18, 1), geometry);
            Solid("Stage 1 - Lower Safety Floor", new Vector2(26, -16.5f), new Vector2(70, 1), geometry);
            Solid("Stage 1 - Left Boundary", new Vector2(-9, 5), new Vector2(0.5f, 12), geometry);
            var gateFolder = new GameObject("05 Stage 1 Gates (invisible during play)");
            gateFolder.transform.SetParent(geometry, false);
            GameObject directorObject = Instantiate(StageOneTutorialPath, Vector3.zero, systems);
            StageOneTutorial director = directorObject.GetComponent<StageOneTutorial>();
            director.Player = level.Player;
            director.SceneTransition = level.GetComponent<SceneTransition>();
            director.FollowCamera = level.FollowCamera;
            director.TitleSceneIndex = 0;
            director.AutoStart = false;
            director.Gate1 = Gate("01 - Pause for Jump", 4);
            director.Gate2 = Gate("02 - Pause for Swing Drive", 18f);
            director.Gate3 = Gate("03 - Pause for Mouse Release", 21.4f);
            director.Gate4 = Gate("04 - Pause for Second Hook", 23.4f);
            director.Gate5 = Gate("05 - Pause for Space Release", 34.5f);
            director.FirstAnchor = Anchor("Stage 1 - First Hook Anchor", new Vector2(13, 15));
            director.SecondAnchor = Anchor("Stage 1 - Second Hook Anchor", new Vector2(30, 10));
            Transform Gate(string name, float x)
            {
                var marker = new GameObject(name);
                marker.transform.SetParent(gateFolder.transform);
                marker.transform.position = new Vector3(x, 4, 0);
                return marker.transform;
            }

            Collider2D Anchor(string name, Vector2 position)
            {
                GameObject anchor = Instantiate(LevelPrefabPath("HookAnchor"), position, geometry);
                anchor.name = name;
                anchor.GetComponent<SpriteRenderer>().color = s_targetColor;
                return anchor.GetComponent<Collider2D>();
            }
        }

        private static StageTwoTutorial FillStageTwo(Transform geometry, Transform systems, LevelFlow level, Vector2 offset)
        {
            // Interior: X=-14..14, Y=0..14. Thick exterior walls keep the practice space enclosed.
            Solid("Practice Floor", offset + new Vector2(0, -0.5f), new Vector2(30, 1), geometry);
            Solid("Practice Ceiling", offset + new Vector2(0, 14.5f), new Vector2(30, 1), geometry);
            Solid("Practice Left Wall", offset + new Vector2(-14.5f, 7), new Vector2(1, 14), geometry);
            Solid("Practice Right Wall", offset + new Vector2(14.5f, 7), new Vector2(1, 14), geometry);
            Collider2D middle = null;
            for (int i = 0; i < 3; i++)
            {
                GameObject anchor = Instantiate(LevelPrefabPath("HookAnchor"), offset + new Vector2((i - 1) * 8, 7), geometry);
                anchor.name = i == 1 ? "Middle Hook Anchor" : i == 0 ? "Left Hook Anchor" : "Right Hook Anchor";
                anchor.GetComponent<SpriteRenderer>().color = s_targetColor;
                if (i == 1)
                {
                    middle = anchor.GetComponent<Collider2D>();
                }
            }

            StageTwoTutorial controller = Instantiate(StageTwoTutorialPath, Vector3.zero, systems).GetComponent<StageTwoTutorial>();
            controller.Player = level.Player;
            controller.SceneTransition = level.GetComponent<SceneTransition>();
            controller.FollowCamera = level.FollowCamera;
            controller.CenterAnchor = middle;
            controller.TitleSceneIndex = 0;
            controller.FreePlaySeconds = 10;
            controller.AutoStart = false;
            return controller;
        }

        private static void FillFirstLevel(Transform geometry, Transform gameplay, Transform labels)
        {
            Instantiate(LevelPrefabPath("OneWayPlatform"), new Vector2(-9, -3), geometry);
            Instantiate(LevelPrefabPath("OneWayPlatform"), new Vector2(8, -2), geometry);
            Instantiate(LevelPrefabPath("OneWayPlatform"), new Vector2(0, 2), geometry);
            Solid("Wall Movement Practice", new Vector2(5, -5.5f), new Vector2(0.6f, 5.5f), geometry);
            foreach (Vector2 position in new[]
            {
                new Vector2(-12, 4),
                new Vector2(-4, 7),
                new Vector2(5, 7),
                new Vector2(13, 4)
            })
            {
                Instantiate(LevelPrefabPath("HookAnchor"), position, geometry);
            }

            Instantiate(GameplayPrefabPath("SpeedTarget"), new Vector2(-4, -3), gameplay);
            Instantiate(GameplayPrefabPath("HeavyTarget"), new Vector2(11, -5), gameplay);
            Instantiate(GameplayPrefabPath("WeakPointPair"), new Vector2(0, 4), gameplay);
            Label("FIRST LEVEL", new Vector2(0, 8.5f), labels, 0.25f);
            Label("SWING INTO ORANGE TARGETS\nFASTER CONTACT = MORE DAMAGE", new Vector2(-9, 1), labels, 0.14f);
            Label("BUILD COMBAT ENCOUNTERS WITH PREFABS\nR: RESET TO START   ESC: TITLE", new Vector2(0, -6), labels, 0.14f);
        }

        private static void Solid(string name, Vector2 position, Vector2 scale, Transform parent)
        {
            GameObject solid = Instantiate(LevelPrefabPath("SolidBlock"), position, parent);
            solid.name = name;
            solid.transform.localScale = new Vector3(scale.x, scale.y, 1);
        }

        public static GameObject Instantiate(string path, Vector3 position, Transform parent = null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                throw new InvalidOperationException("Missing prefab: " + path + ". Use Cat Cannonball > Rebuild Demo Assets first.");
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            return instance;
        }

        private static void Label(string text, Vector2 position, Transform parent, float size)
        {
            var obj = new GameObject("Label - " + text.Split('\n')[0]);
            obj.transform.SetParent(parent);
            obj.transform.position = new Vector3(position.x, position.y, -0.05f);
            TextMesh mesh = obj.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size * 0.45f;
            mesh.color = new Color(0.88f, 0.91f, 0.95f);
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (mesh.font != null)
            {
                obj.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            }
        }
    }
}

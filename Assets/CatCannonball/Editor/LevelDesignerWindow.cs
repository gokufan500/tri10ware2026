using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatCannonball.Editor
{
    /// <summary>A small prefab palette: each button adds a fully configured, editable building block.</summary>
    public sealed class LevelDesignerWindow : EditorWindow
    {
        private Vector2 _scroll;
        [MenuItem("Cat Cannonball/Level Designer", priority = 0)]
        public static void Open()
        {
            GetWindow<LevelDesignerWindow>("Level Designer");
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(8);
            GUILayout.Label("CAT CANNONBALL", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Build a level with prefabs", EditorStyles.largeLabel);
            EditorGUILayout.HelpBox("1. Create a level below.\n2. Add blocks and anchors, then move and resize them.\n3. Resize CameraBounds to cover the playable area.\n4. Press Play. Duplicate profiles before tuning a separate movement feel.", MessageType.Info);
            if (GUILayout.Button("Create New Level\u2026", GUILayout.Height(30)))
            {
                NewLevel();
            }

            if (GUILayout.Button("Open Title Screen (start here)"))
            {
                OpenScene(PrototypeBuilder.TitlePath);
            }

            if (GUILayout.Button("Open First Level"))
            {
                OpenScene(PrototypeBuilder.FirstLevelPath);
            }

            if (GUILayout.Button("Open Guided Tutorial"))
            {
                OpenScene(PrototypeBuilder.TutorialPath);
            }

            if (GUILayout.Button("Open Level Template"))
            {
                OpenScene(PrototypeBuilder.TemplatePath);
            }

            if (GUILayout.Button("Select Shared Movement Settings"))
            {
                SelectAsset(PrototypeBuilder.SettingsPath);
            }

            if (GUILayout.Button("Select Stage 1 Movement Settings"))
            {
                SelectAsset(PrototypeBuilder.TutorialSettingsPath);
            }

            if (GUILayout.Button("Select Shared Camera Settings"))
            {
                SelectAsset(PrototypeBuilder.CameraSettingsPath);
            }

            if (GUILayout.Button("Select Combat Feedback Settings"))
            {
                SelectAsset(PrototypeBuilder.CombatFeedbackSettingsPath);
            }

            if (GUILayout.Button("Select Combat Feedback Prefab"))
            {
                SelectAsset(PrototypeBuilder.CombatFeedbackPath);
            }

            if (GUILayout.Button("Select Stage 1 Camera Settings"))
            {
                SelectAsset(PrototypeBuilder.TutorialCameraSettingsPath);
            }

            if (GUILayout.Button("Select Stage 2 Camera Settings"))
            {
                SelectAsset(PrototypeBuilder.StageTwoCameraSettingsPath);
            }

            if (GUILayout.Button("Duplicate Selected Settings Profile\u2026"))
            {
                DuplicateSelectedProfile();
            }

            EditorGUILayout.Space(10);
            GUILayout.Label("ROOM PIECES", EditorStyles.boldLabel);
            Piece("Solid Block", "Floor, wall, ceiling or ramp. Resize or rotate it; solid surfaces also accept the hook.", PrototypeBuilder.LevelPrefabPath("SolidBlock"), "01 Level Geometry");
            Piece("One-Way Platform", "Jump up through it; stand on top. S drops through it, and hooking allows swinging through.", PrototypeBuilder.LevelPrefabPath("OneWayPlatform"), "01 Level Geometry");
            Piece("Hook Anchor", "A cyan hook point that the player can pass through. Its collider defines the hook target.", PrototypeBuilder.LevelPrefabPath("HookAnchor"), "01 Level Geometry");
            EditorGUILayout.Space(10);
            GUILayout.Label("CAMERA", EditorStyles.boldLabel);
            Piece("Camera Rig", "One per level. Follows the player with slight lag; its CameraSettings profile controls framing and smoothing. Adding is disabled while a rig already exists.", PrototypeBuilder.CameraRigPath, "00 Systems");
            Piece("Camera Bounds", "Optional view limits. Move the object and resize its Size field; Scene-view gizmos show the extent. Adding connects it to the camera rig.", PrototypeBuilder.CameraBoundsPath, "00 Systems");
            EditorGUILayout.Space(10);
            GUILayout.Label("GAMEPLAY PIECES", EditorStyles.boldLabel);
            Piece("Speed Target (1 HP)", "An orange target. Its health number stays above it. Contact must reach the damage-speed threshold; tune Hit Points, Speed Per Damage and the combat profile.", PrototypeBuilder.GameplayPrefabPath("SpeedTarget"), "02 Gameplay");
            Piece("Heavy Target (8 HP)", "A target that needs repeated hits or a fast impact. Increase Hit Cooldown to space out weak-point hits.", PrototypeBuilder.GameplayPrefabPath("HeavyTarget"), "02 Gameplay");
            Piece("Alternating Weak-Point Pair", "Two reusable 8 HP weak points. Their WeakPointGroup requires alternating successful hits; On All Defeated can open a door.", PrototypeBuilder.GameplayPrefabPath("WeakPointPair"), "02 Gameplay");
            Piece("Death Zone", "Red reset area. Resize to cover pits or hazards. It automatically connects to this level's flow.", PrototypeBuilder.GameplayPrefabPath("DeathZone"), "02 Gameplay");
            EditorGUILayout.Space(10);
            GUILayout.Label("TITLE AND TUTORIAL", EditorStyles.boldLabel);
            if (GUILayout.Button("Select Title Menu Prefab"))
            {
                SelectAsset(PrototypeBuilder.MenuSystemsPath);
            }

            if (GUILayout.Button("Select Stage 1 Controller In Scene"))
            {
                StageOneTutorial director = FindSceneComponent<StageOneTutorial>();
                if (director)
                {
                    Selection.activeGameObject = director.gameObject;
                    EditorGUIUtility.PingObject(director);
                }
                else
                {
                    OpenScene(PrototypeBuilder.TutorialPath);
                }
            }

            if (GUILayout.Button("Select Stage 2 Controller In Scene"))
            {
                StageTwoTutorial director = FindSceneComponent<StageTwoTutorial>();
                if (director)
                {
                    Selection.activeGameObject = director.gameObject;
                    EditorGUIUtility.PingObject(director);
                }
                else
                {
                    OpenScene(PrototypeBuilder.TutorialPath);
                }
            }

            if (GUILayout.Button("Select Tutorial Sequence In Scene"))
            {
                TutorialSequence sequence = FindSceneComponent<TutorialSequence>();
                if (sequence)
                {
                    Selection.activeGameObject = sequence.gameObject;
                    EditorGUIUtility.PingObject(sequence);
                }
                else
                {
                    OpenScene(PrototypeBuilder.TutorialPath);
                }
            }

            EditorGUILayout.HelpBox("The single Tutorial scene contains both stages. TutorialSequence switches room roots, spawn and camera settings under a fade. Teaching prompts preserve the player's position. Stage 2 unlocks every control for ten seconds, including R reset. Select either controller even while inactive; tune lesson timing there and text layout on TutorialPromptView.", MessageType.Info);
            EditorGUILayout.Space(10);
            GUILayout.Label("CHECK AND LEARN", EditorStyles.boldLabel);
            if (GUILayout.Button("Validate Open Scene"))
            {
                PrototypeValidator.ValidateOpenScene();
            }

            if (GUILayout.Button("Select Player Prefab"))
            {
                SelectAsset(PrototypeBuilder.PlayerPath);
            }

            if (GUILayout.Button("Open Quick Start"))
            {
                OpenQuickStart();
            }

            EditorGUILayout.HelpBox("Add places an instance at the center of the Scene view. Select shows its original prefab in the Project panel so you can drag out more. Use Prefab Variants for reusable designs with different settings.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void Piece(string title, string description, string path, string folder)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            bool cameraAlreadyPresent = path == PrototypeBuilder.CameraRigPath && Object.FindAnyObjectByType<FollowCamera2D>() != null;
            using (new EditorGUI.DisabledScope(!exists || EditorApplication.isPlaying || cameraAlreadyPresent))
            {
                if (GUILayout.Button("Add to Scene"))
                {
                    AddPiece(path, folder);
                }

                if (GUILayout.Button("Select Prefab", GUILayout.Width(105)))
                {
                    SelectAsset(path);
                }
            }

            EditorGUILayout.EndHorizontal();
            if (!exists)
            {
                EditorGUILayout.HelpBox("Missing starter asset. Use Cat Cannonball > Rebuild Demo Assets.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private static void AddPiece(string path, string folder)
        {
            Vector3 position = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            // Gameplay pieces live on Z=0; a replacement camera retains its prefab's
            // viewing depth so snapping to the target cannot put it inside that plane.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            position.z = path == PrototypeBuilder.CameraRigPath && prefab != null ? prefab.transform.position.z : 0;
            position.x = Mathf.Round(position.x * 2) / 2;
            position.y = Mathf.Round(position.y * 2) / 2;
            GameObject parent = GameObject.Find(folder);
            if (parent == null)
            {
                parent = new GameObject(folder);
                Undo.RegisterCreatedObjectUndo(parent, "Create level folder");
            }

            GameObject instance = PrototypeBuilder.Instantiate(path, position, parent.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Add " + instance.name);
            LevelFlow flow = Object.FindAnyObjectByType<LevelFlow>();
            if (instance.TryGetComponent(out DeathZone zone))
            {
                zone.LevelFlow = flow;
            }

            CombatFeedbackController combat = FindSceneComponent<CombatFeedbackController>();
            if (combat && combat.Settings)
            {
                foreach (SpeedTarget target in instance.GetComponentsInChildren<SpeedTarget>(true))
                {
                    target.CombatSettings = combat.Settings;
                }
            }

            if (instance.TryGetComponent(out FollowCamera2D follow))
            {
                PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>();
                follow.Target = player ? player.transform : null;
                follow.Bounds = Object.FindAnyObjectByType<CameraBounds2D>();
                Camera camera = instance.GetComponent<Camera>();
                if (player && player.Input)
                {
                    player.Input.AimCamera = camera;
                }

                if (flow)
                {
                    flow.FollowCamera = follow;
                }

                if (combat)
                {
                    combat.FollowCamera = follow;
                }

                foreach (PrototypeHud hud in Object.FindObjectsByType<PrototypeHud>())
                {
                    hud.ViewCamera = camera;
                }

                follow.SnapToTarget();
            }

            if (instance.TryGetComponent(out CameraBounds2D bounds))
            {
                FollowCamera2D rig = Object.FindAnyObjectByType<FollowCamera2D>();
                if (rig)
                {
                    rig.Bounds = bounds;
                }
            }

            // Cross-instance links can change too, so record scene-wide prefab overrides.
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component && PrefabUtility.IsPartOfPrefabInstance(component))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                    }
                }
            }

            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("Cat Cannonball/New Empty Level\u2026", priority = 1)]
        public static void NewLevel()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            foreach (string required in new[]
            {
                PrototypeBuilder.PlayerPath,
                PrototypeBuilder.SystemsPath,
                PrototypeBuilder.CameraRigPath,
                PrototypeBuilder.CameraBoundsPath,
                PrototypeBuilder.CombatFeedbackPath,
                PrototypeBuilder.LevelPrefabPath("SolidBlock"),
                PrototypeBuilder.LevelPrefabPath("OneWayPlatform"),
                PrototypeBuilder.LevelPrefabPath("HookAnchor")
            })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(required) == null)
                {
                    EditorUtility.DisplayDialog("Starter prefabs are missing", "Run Cat Cannonball > Rebuild Demo Assets, then create a level.", "OK");
                    return;
                }
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject("Create a new level", "MyLevel", "unity", "Choose a name for the new scene. The supplied demo scenes are kept.", PrototypeBuilder.Root + "/Scenes");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            foreach (string suppliedPath in PrototypeBuilder.ScenePaths)
            {
                if (path == suppliedPath)
                {
                    EditorUtility.DisplayDialog("Choose a new level name", "Use a different name to keep the supplied scenes available.", "OK");
                    return;
                }
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrototypeBuilder.CreateLevelTemplate();
            EditorSceneManager.SaveScene(scene, path);
            Selection.activeGameObject = GameObject.Find("01 Level Geometry");
            Open();
        }

        private static void OpenScene(string path)
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                EditorSceneManager.OpenScene(path);
            }
        }

        [MenuItem("Cat Cannonball/Open First Level", priority = 2)]
        public static void OpenFirstLevel() => OpenScene(PrototypeBuilder.FirstLevelPath);
        [MenuItem("Cat Cannonball/Open Title Screen", priority = 3)]
        public static void OpenTitle() => OpenScene(PrototypeBuilder.TitlePath);
        [MenuItem("Cat Cannonball/Open Guided Tutorial", priority = 4)]
        public static void OpenTutorial() => OpenScene(PrototypeBuilder.TutorialPath);
        [MenuItem("Cat Cannonball/Open Level Template", priority = 5)]
        public static void OpenTemplate() => OpenScene(PrototypeBuilder.TemplatePath);
        private static void DuplicateSelectedProfile()
        {
            Object source = Selection.activeObject;
            if (!(source is MovementSettings) && !(source is CameraSettings) && !(source is CombatFeedbackSettings))
            {
                EditorUtility.DisplayDialog("Select a profile first", "Select a MovementSettings, CameraSettings or CombatFeedbackSettings asset, then duplicate it. Assign the copy on its Player, CameraRig or CombatFeedbackController; targets should reference the same combat profile.", "OK");
                return;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string destination = EditorUtility.SaveFilePanelInProject("Duplicate tuning profile", source.name + " Copy", "asset", "Choose a new profile name. This creates independent tuning values.", PrototypeBuilder.Root + "/Settings");
            if (string.IsNullOrEmpty(destination) || destination == sourcePath)
            {
                return;
            }

            if (AssetDatabase.CopyAsset(sourcePath, destination))
            {
                AssetDatabase.ImportAsset(destination);
                SelectAsset(destination);
            }
        }

        private static T FindSceneComponent<T>()
            where T : Component
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component)
                {
                    return component;
                }
            }

            return null;
        }

        private static void SelectAsset(string path)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("Cat Cannonball/Open Quick Start", priority = 50)]
        private static void OpenQuickStart()
        {
            string path = Path.GetFullPath("README.md");
            if (File.Exists(path))
            {
                EditorUtility.OpenWithDefaultApp(path);
            }
            else
            {
                EditorUtility.DisplayDialog("Quick Start", "Open the README.md file in the project folder for controls, setup, and level-design instructions.", "OK");
            }
        }
    }
}

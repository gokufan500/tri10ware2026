using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatCannonball.Editor
{
    /// <summary>Friendly authoring checks, also callable by batch-mode project validation.</summary>
    public static class PrototypeValidator
    {
        [MenuItem("Cat Cannonball/Validate Open Scene", priority = 20)]
        public static void ValidateOpenScene()
        {
            List<string> errors = CollectErrors();
            if (errors.Count == 0)
            {
                Debug.Log("Cat Cannonball: scene validation passed. Scene navigation and the appropriate menu, tutorial or gameplay links are configured.");
                EditorUtility.DisplayDialog("Scene is ready", "All authoring checks passed. Press Play to try your level.", "OK");
                return;
            }

            foreach (string error in errors)
            {
                Debug.LogError("Cat Cannonball validation: " + error);
            }

            EditorUtility.DisplayDialog("Level needs attention", string.Join("\n\n", errors), "OK");
        }

        public static void ValidateForBatch()
        {
            List<string> errors = CollectErrors();
            errors.AddRange(CollectPrefabErrors());
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Cat Cannonball validation failed:\n" + string.Join("\n", errors));
            }

            Debug.Log("CAT_CANNONBALL_VALIDATION_PASSED");
        }

        public static List<string> CollectErrors()
        {
            var errors = new List<string>();
            Scene scene = SceneManager.GetActiveScene();
            List<SceneTransition> transitions = FindInScene<SceneTransition>(scene);
            if (transitions.Count != 1)
            {
                errors.Add("Use exactly one SceneTransition in this scene. Found " + transitions.Count + ".");
            }

            foreach (SceneTransition transition in transitions)
            {
                if (!Finite(transition.FadeInSeconds) || !Finite(transition.FadeOutSeconds) || transition.FadeInSeconds < 0 || transition.FadeOutSeconds < 0)
                {
                    errors.Add("SceneTransition: fade durations must be finite and cannot be negative.");
                }
            }

            List<TitleScreen> titles = FindInScene<TitleScreen>(scene);
            if (titles.Count > 0)
            {
                if (titles.Count != 1)
                {
                    errors.Add("Use exactly one TitleScreen component.");
                }

                foreach (TitleScreen title in titles)
                {
                    if (title.Transitions == null || title.Transitions.gameObject.scene != scene)
                    {
                        errors.Add("TitleScreen: assign this scene's SceneTransition.");
                    }

                    CheckSceneIndex(title.PlaySceneIndex, "TitleScreen / Play Scene Index", errors);
                    CheckSceneIndex(title.TutorialSceneIndex, "TitleScreen / Tutorial Scene Index", errors);
                    if (EnabledScenePath(title.PlaySceneIndex) == scene.path || EnabledScenePath(title.TutorialSceneIndex) == scene.path)
                    {
                        errors.Add("TitleScreen: Play and Tutorial must load gameplay scenes rather than the title scene itself.");
                    }
                }

                if (FindInScene<PlayerMotor>(scene).Count != 0)
                {
                    errors.Add("TitleScreen: keep the menu scene separate from gameplay players.");
                }

                if (FindInScene<Camera>(scene).Count != 1)
                {
                    errors.Add("TitleScreen: use one background camera.");
                }

                CheckMissingScripts(scene, errors);
                return errors;
            }

            List<PlayerMotor> players = FindInScene<PlayerMotor>(scene);
            if (players.Count != 1)
            {
                errors.Add("Use exactly one Player prefab. Found " + players.Count + " players.");
            }

            foreach (PlayerMotor player in players)
            {
                if (player.Settings == null)
                {
                    errors.Add("Player: assign a MovementSettings asset (usually Settings/DefaultMovement).");
                }

                if (player.Input == null)
                {
                    errors.Add("Player: assign its PlayerInputReader to the Input field.");
                }

                if (player.Grapple == null)
                {
                    errors.Add("Player: assign its GrappleController to the Grapple field.");
                }

                if (player.GetComponent<Rigidbody2D>() == null)
                {
                    errors.Add("Player: missing Rigidbody2D. Restore the Player prefab.");
                }

                if (player.GetComponent<Collider2D>() == null)
                {
                    errors.Add("Player: missing Collider2D. Restore the Player prefab.");
                }

                GrappleController grapple = player.GetComponent<GrappleController>();
                if (grapple == null || grapple.Settings == null)
                {
                    errors.Add("Player: missing GrappleController or its MovementSettings asset.");
                }
                else if (grapple.Settings != player.Settings)
                {
                    errors.Add("Player: Motor and Grapple must reference the same MovementSettings profile.");
                }

                if (player.Settings != null)
                {
                    CheckMovementSettings(player.Settings, errors);
                }

                if (player.gameObject.layer != PrototypeBuilder.PlayerLayer)
                {
                    errors.Add("Player: choose the Player layer so platform and hook filtering work.");
                }
            }

            List<LevelFlow> flows = FindInScene<LevelFlow>(scene);
            if (flows.Count != 1)
            {
                errors.Add("Use exactly one GameSystems prefab. Found " + flows.Count + " LevelFlow components.");
            }

            foreach (LevelFlow flow in flows)
            {
                if (flow.Player == null)
                {
                    errors.Add("GameSystems / LevelFlow: drag the Player into its Player field.");
                }

                if (flow.SpawnPoint == null)
                {
                    errors.Add("GameSystems / LevelFlow: assign Start Spawn.");
                }
            }

            List<Camera> cameras = FindInScene<Camera>(scene);
            Camera main = null;
            int mainCount = 0;
            foreach (Camera camera in cameras)
            {
                if (camera.CompareTag("MainCamera"))
                {
                    main = camera;
                    mainCount++;
                }
            }

            if (mainCount != 1)
            {
                errors.Add("Tag exactly one camera MainCamera. Found " + mainCount + ".");
            }

            if (main != null && !main.orthographic)
            {
                errors.Add("Main Camera: enable Orthographic for this 2D prototype.");
            }

            if (main != null)
            {
                if (main.transform.position.z >= 0)
                {
                    errors.Add("Main Camera: place it behind the 2D gameplay plane (negative Z, normally -10).");
                }

                float planeDistance = Vector3.Dot(-main.transform.position, main.transform.forward);
                if (planeDistance <= main.nearClipPlane || planeDistance >= main.farClipPlane)
                {
                    errors.Add("Main Camera: near/far clipping planes must include the Z=0 gameplay plane. Restore the CameraRig prefab's depth and clipping settings.");
                }

                if (Vector3.Dot(main.transform.forward, Vector3.forward) < 0.999f)
                {
                    errors.Add("Main Camera: use zero rotation so it faces the Z=0 gameplay plane squarely.");
                }
            }

            List<FollowCamera2D> follows = FindInScene<FollowCamera2D>(scene);
            if (follows.Count != 1)
            {
                errors.Add("Use exactly one CameraRig prefab with FollowCamera2D. Found " + follows.Count + ".");
            }

            foreach (FollowCamera2D follow in follows)
            {
                if (follow.Target == null)
                {
                    errors.Add("CameraRig: assign the scene Player to Target.");
                }
                else if (players.Count == 1 && follow.Target != players[0].transform)
                {
                    errors.Add("CameraRig: Target must be this level's Player transform.");
                }

                if (follow.GetComponent<Camera>() != main)
                {
                    errors.Add("CameraRig: its Camera must be the tagged MainCamera used for aiming.");
                }

                if (follow.Settings == null)
                {
                    errors.Add("CameraRig: assign a CameraSettings profile (usually DefaultCamera).");
                }
                else
                {
                    CheckCameraSettings(follow.Settings, errors);
                }

                if (follow.Bounds != null && follow.Bounds.gameObject.scene != scene)
                {
                    errors.Add("CameraRig: Bounds must belong to the open scene.");
                }
            }

            foreach (CameraBounds2D bounds in FindInScene<CameraBounds2D>(scene))
            {
                if (bounds.Size.x <= 0 || bounds.Size.y <= 0)
                {
                    errors.Add(bounds.name + ": CameraBounds Size must be positive on both axes.");
                }
            }

            foreach (LevelFlow flow in flows)
            {
                if (follows.Count == 1 && flow.FollowCamera != follows[0])
                {
                    errors.Add("GameSystems / LevelFlow: assign this scene's CameraRig to Follow Camera.");
                }
            }

            foreach (PlayerInputReader input in FindInScene<PlayerInputReader>(scene))
            {
                if (input.AimCamera == null || input.AimCamera != main)
                {
                    errors.Add(input.name + ": assign this scene's Main Camera to PlayerInputReader / Aim Camera.");
                }
            }

            foreach (PrototypeHud hud in FindInScene<PrototypeHud>(scene))
            {
                if (hud.ViewCamera != main)
                {
                    errors.Add("GameSystems / PrototypeHud: assign this scene's Main Camera to View Camera.");
                }

                if (hud.Motor == null || hud.Input == null || hud.LevelFlow == null)
                {
                    errors.Add("GameSystems / PrototypeHud: assign Player Motor, Input, and Level Flow.");
                }

                if (hud.Transitions == null || hud.Transitions.gameObject.scene != scene)
                {
                    errors.Add("GameSystems / PrototypeHud: assign this scene's SceneTransition.");
                }

                CheckSceneIndex(hud.TitleSceneIndex, "PrototypeHud / Title Scene Index", errors);
            }

            foreach (DeathZone zone in FindInScene<DeathZone>(scene))
            {
                if (zone.LevelFlow == null)
                {
                    errors.Add(zone.name + ": assign GameSystems / LevelFlow.");
                }

                CheckTrigger(zone, errors);
            }

            foreach (SpeedTarget target in FindInScene<SpeedTarget>(scene))
            {
                CheckTrigger(target, errors);
                if (!target.CombatSettings)
                {
                    errors.Add(target.name + ": assign the shared CombatFeedbackSettings profile.");
                }
                else
                {
                    CheckCombatSettings(target.CombatSettings, errors);
                }

                if (target.HitPoints < 1 || target.SpeedPerDamage <= 0 || target.HitCooldown < 0)
                {
                    errors.Add(target.name + ": health and damage divisor must be positive, and hit cooldown cannot be negative.");
                }

                CheckFiniteNumbers(target, errors);
                EnemyHealthDisplay health = target.GetComponent<EnemyHealthDisplay>();
                if (!health || health.Target != target)
                {
                    errors.Add(target.name + ": include EnemyHealthDisplay referencing this SpeedTarget.");
                }
                else if (health.HeightAboveEnemy < 0 || health.CharacterSize <= 0 || health.FontSize < 1)
                {
                    errors.Add(target.name + ": health-number height must be nonnegative; size and font size must be positive.");
                }
            }

            List<CombatFeedbackController> feedbacks = FindInScene<CombatFeedbackController>(scene);
            if (feedbacks.Count > 1)
            {
                errors.Add("Use at most one CombatFeedbackController per scene.");
            }

            if (scene.path != PrototypeBuilder.TutorialPath && feedbacks.Count != 1)
            {
                errors.Add("Gameplay: include one CombatFeedback prefab for speed and impact effects.");
            }

            foreach (CombatFeedbackController feedback in feedbacks)
            {
                if (!feedback.Player || feedback.Player.gameObject.scene != scene)
                {
                    errors.Add("CombatFeedback: assign this scene's Player.");
                }

                if (!feedback.FollowCamera || feedback.FollowCamera.gameObject.scene != scene || (follows.Count == 1 && feedback.FollowCamera != follows[0]))
                {
                    errors.Add("CombatFeedback: assign this scene's FollowCamera2D.");
                }

                if (!feedback.SceneTransition || feedback.SceneTransition.gameObject.scene != scene)
                {
                    errors.Add("CombatFeedback: assign this scene's SceneTransition.");
                }

                if (!feedback.Settings)
                {
                    errors.Add("CombatFeedback: assign a CombatFeedbackSettings profile.");
                }
                else
                {
                    CheckCombatSettings(feedback.Settings, errors);
                }

                if (!feedback.SpeedEffectPrefab || !feedback.ImpactEffectPrefab || feedback.ImpactPoolSize < 1)
                {
                    errors.Add("CombatFeedback: assign both ParticleSystem prefabs and a positive impact pool size.");
                }

                foreach (SpeedTarget target in FindInScene<SpeedTarget>(scene))
                {
                    if (target.MinimumDamageSpeedOverride < 0 && target.CombatSettings != feedback.Settings)
                    {
                        errors.Add(target.name + ": use the same combat profile as the scene's CombatFeedbackController so damage and readiness agree.");
                    }
                }
            }

            foreach (OneWayPlatform platform in FindInScene<OneWayPlatform>(scene))
            {
                BoxCollider2D collider = platform.GetComponent<BoxCollider2D>();
                if (collider == null || !collider.usedByEffector || platform.GetComponent<PlatformEffector2D>() == null)
                {
                    errors.Add(platform.name + ": keep its BoxCollider2D (Used By Effector) and PlatformEffector2D components.");
                }
            }

            List<StageOneTutorial> tutorials = FindInScene<StageOneTutorial>(scene);
            if (tutorials.Count > 1)
            {
                errors.Add("Use at most one StageOneTutorial in a scene.");
            }

            if (scene.path == PrototypeBuilder.TutorialPath && tutorials.Count != 1)
            {
                errors.Add("Tutorial: use exactly one StageOneTutorial controller.");
            }

            foreach (StageOneTutorial tutorial in tutorials)
            {
                CheckTutorial(tutorial, scene, errors);
            }

            List<StageTwoTutorial> stageTwos = FindInScene<StageTwoTutorial>(scene);
            if (stageTwos.Count > 1)
            {
                errors.Add("Use at most one StageTwoTutorial controller in a scene.");
            }

            List<TutorialSequence> sequences = FindInScene<TutorialSequence>(scene);
            if (sequences.Count > 1)
            {
                errors.Add("Use exactly one TutorialSequence to coordinate the tutorial.");
            }

            if (scene.path == PrototypeBuilder.TutorialPath && (sequences.Count != 1 || stageTwos.Count != 1))
            {
                errors.Add("Tutorial: include both stage controllers and one TutorialSequence in this single scene.");
            }

            foreach (TutorialSequence sequence in sequences)
            {
                CheckSequence(sequence, scene, errors);
            }

            foreach (StageTwoTutorial tutorial in stageTwos)
            {
                CheckStageTwo(tutorial, scene, errors);
            }

            if (tutorials.Count + stageTwos.Count > 0)
            {
                foreach (PrototypeHud hud in FindInScene<PrototypeHud>(scene))
                {
                    if (hud.ShowDiagnostics || hud.ShowHelp)
                    {
                        errors.Add("Tutorial HUD: disable Show Diagnostics and Show Help to keep instructions clear.");
                    }
                }
            }

            CheckMissingScripts(scene, errors);
            return errors;
        }

        private static void CheckTutorial(StageOneTutorial tutorial, Scene scene, List<string> errors)
        {
            const string Prefix = "StageOneTutorial: ";
            if (tutorial.Player == null || tutorial.Player.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign the scene's Player.");
            }

            if (tutorial.SceneTransition == null || tutorial.SceneTransition.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign this scene's SceneTransition.");
            }

            if (tutorial.FollowCamera == null || tutorial.FollowCamera.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign this scene's FollowCamera2D from the CameraRig.");
            }
            else if (tutorial.Player && tutorial.Player.Input && tutorial.FollowCamera.GetComponent<Camera>() != tutorial.Player.Input.AimCamera)
            {
                errors.Add(Prefix + "Follow Camera must use the same camera as the player's Aim Camera.");
            }

            CheckSceneIndex(tutorial.TitleSceneIndex, "StageOneTutorial / Title Scene Index", errors);
            if (EnabledScenePath(tutorial.TitleSceneIndex) == scene.path)
            {
                errors.Add(Prefix + "Title Scene Index must return to a separate title scene.");
            }

            if (!tutorial.Sequence || tutorial.Sequence.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign this scene's TutorialSequence; both stages now share one scene.");
            }

            CheckPromptView(tutorial.PromptView, scene, Prefix, errors);
            if (tutorial.FailDelay <= 0 || tutorial.CompletionDelay < 0 || tutorial.HookAimTolerance <= 0 || tutorial.ApexVelocityTolerance < 0 || tutorial.GateVerticalTolerance <= 0 || tutorial.TravelTimeout <= 0 || (tutorial.NormalizeJumpHorizontalSpeed && tutorial.GuidedJumpHorizontalSpeed <= 0))
            {
                errors.Add(Prefix + "failure delay, travel timeout, aim/gate tolerances and enabled guided launch speed must be positive; completion delay and apex tolerance cannot be negative.");
            }

            if (tutorial.PauseEaseInSeconds < 0 || tutorial.ResumeSeconds < 0 || tutorial.ResumeSeconds > 0.05f || tutorial.PromptFadeSeconds < 0 || tutorial.MinimumPromptPresentationSeconds < 0 || tutorial.InputBufferSeconds < 0 || tutorial.CameraSettleSeconds < 0)
            {
                errors.Add(Prefix + "pause approach, prompt fade, minimum presentation, input buffer and camera settling durations cannot be negative; Resume Seconds must stay between 0 and 0.05.");
            }

            CheckPauseCurve(tutorial.PauseEaseCurve, errors);
            Transform[] gates = new[]
            {
                tutorial.Gate1,
                tutorial.Gate2,
                tutorial.Gate3,
                tutorial.Gate4,
                tutorial.Gate5
            };
            float previousX = float.NegativeInfinity;
            for (int i = 0; i < gates.Length; i++)
            {
                Transform gate = gates[i];
                if (gate == null || gate.gameObject.scene != scene)
                {
                    errors.Add(Prefix + "assign gate " + (i + 1) + " to an invisible marker in this scene.");
                    continue;
                }

                if (gate.position.x <= previousX)
                {
                    errors.Add(Prefix + "gate markers must progress from left to right.");
                }

                previousX = gate.position.x;
                if (gate.GetComponent<Renderer>() != null || gate.GetComponent<Collider2D>() != null)
                {
                    errors.Add(Prefix + gate.name + " must be an invisible marker without a collider or renderer.");
                }
            }

            CheckAnchor(tutorial.FirstAnchor, "First Anchor");
            CheckAnchor(tutorial.SecondAnchor, "Second Anchor");
            if (tutorial.FirstAnchor && tutorial.SecondAnchor && tutorial.FirstAnchor == tutorial.SecondAnchor)
            {
                errors.Add(Prefix + "use two distinct hook anchors.");
            }

            if (string.IsNullOrWhiteSpace(tutorial.WalkPrompt) || string.IsNullOrWhiteSpace(tutorial.JumpPrompt) || string.IsNullOrWhiteSpace(tutorial.FirstHookPrompt) || string.IsNullOrWhiteSpace(tutorial.DrivePrompt) || string.IsNullOrWhiteSpace(tutorial.ReleasePrompt) || string.IsNullOrWhiteSpace(tutorial.SecondHookPrompt) || string.IsNullOrWhiteSpace(tutorial.SpacePrompt))
            {
                errors.Add(Prefix + "write an instruction for every directed phase.");
            }

            CheckFiniteNumbers(tutorial, errors);
            void CheckAnchor(Collider2D anchor, string label)
            {
                if (anchor == null || anchor.gameObject.scene != scene)
                {
                    errors.Add(Prefix + "assign " + label + " to a HookAnchor collider in this scene.");
                    return;
                }

                if (tutorial.Player && tutorial.Player.Settings && (tutorial.Player.Settings.HookableLayers.value & (1 << anchor.gameObject.layer)) == 0)
                {
                    errors.Add(Prefix + label + " must use a layer included in the player's Hookable Layers.");
                }
            }
        }

        private static void CheckStageTwo(StageTwoTutorial tutorial, Scene scene, List<string> errors)
        {
            const string Prefix = "StageTwoTutorial: ";
            if (!tutorial.Player || tutorial.Player.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign the scene's Player.");
            }

            if (!tutorial.SceneTransition || tutorial.SceneTransition.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign the scene's SceneTransition.");
            }

            if (!tutorial.FollowCamera || tutorial.FollowCamera.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign this scene's FollowCamera2D.");
            }
            else if (tutorial.Player && tutorial.Player.Input && tutorial.FollowCamera.GetComponent<Camera>() != tutorial.Player.Input.AimCamera)
            {
                errors.Add(Prefix + "use the same follow and aiming camera.");
            }

            CheckPromptView(tutorial.PromptView, scene, Prefix, errors);
            CheckSceneIndex(tutorial.TitleSceneIndex, "StageTwoTutorial / Title Scene Index", errors);
            if (EnabledScenePath(tutorial.TitleSceneIndex) == scene.path)
            {
                errors.Add(Prefix + "Title Scene Index must return to a separate title scene.");
            }

            if (tutorial.FreePlaySeconds <= 0)
            {
                errors.Add(Prefix + "Free Play Seconds must be positive.");
            }

            if (tutorial.FreePlayHintSeconds < 0 || tutorial.FreePlayHintSeconds > tutorial.FreePlaySeconds || tutorial.FailDelay <= 0 || tutorial.TravelTimeout <= 0 || tutorial.PauseEaseInSeconds < 0 || tutorial.PromptFadeSeconds < 0 || tutorial.InputBufferSeconds < 0 || tutorial.MinimumPromptPresentationSeconds < 0 || tutorial.CameraSettleSeconds < 0 || tutorial.ApexVelocityTolerance < 0 || tutorial.HookAimTolerance <= 0)
            {
                errors.Add(Prefix + "use nonnegative pause, presentation, buffer, camera and apex timing; keep fail/travel delays and aim tolerance positive, and practice hint within the practice interval.");
            }

            CheckPauseCurve(tutorial.PauseEaseCurve, errors, "StageTwoTutorial");
            if (string.IsNullOrWhiteSpace(tutorial.JumpPrompt) || string.IsNullOrWhiteSpace(tutorial.HookPrompt) || string.IsNullOrWhiteSpace(tutorial.YankPrompt))
            {
                errors.Add(Prefix + "write an instruction for jump, hook and yank.");
            }

            CheckFiniteNumbers(tutorial, errors);
            if (!tutorial.Sequence || tutorial.Sequence.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign this scene's TutorialSequence.");
            }

            GameObject room = tutorial.Sequence ? tutorial.Sequence.StageTwoRoot : null;
            List<Collider2D> anchors = room ? new List<Collider2D>(room.GetComponentsInChildren<Collider2D>(true)).FindAll(anchor => anchor.gameObject.layer == PrototypeBuilder.HookOnlyLayer) : new List<Collider2D>();
            // Inactive rooms have empty physics bounds. Authoring checks use the saved transforms.
            anchors.Sort((a, b) => ColliderCenter(a).x.CompareTo(ColliderCenter(b).x));
            if (anchors.Count != 3)
            {
                errors.Add(Prefix + "use exactly three HookAnchor colliders in the enclosed playground.");
            }
            else
            {
                const float Tolerance = 0.01f;
                float spacing = ColliderCenter(anchors[1]).x - ColliderCenter(anchors[0]).x;
                if (spacing <= 0 || Mathf.Abs(spacing - (ColliderCenter(anchors[2]).x - ColliderCenter(anchors[1]).x)) > Tolerance)
                {
                    errors.Add(Prefix + "space the three anchors equally from left to right.");
                }

                if (Mathf.Abs(ColliderCenter(anchors[0]).y - ColliderCenter(anchors[1]).y) > Tolerance || Mathf.Abs(ColliderCenter(anchors[2]).y - ColliderCenter(anchors[1]).y) > Tolerance)
                {
                    errors.Add(Prefix + "place all three anchors at the same elevation.");
                }

                if (tutorial.CenterAnchor != anchors[1])
                {
                    errors.Add(Prefix + "assign the middle hook collider to Center Anchor.");
                }

                Transform spawn = tutorial.Sequence ? tutorial.Sequence.StageTwoSpawn : null;
                if (!spawn || Mathf.Abs(spawn.position.x - ColliderCenter(anchors[1]).x) > Tolerance || spawn.position.y >= ColliderCenter(anchors[1]).y)
                {
                    errors.Add(Prefix + "place Stage Two Spawn directly beneath the middle anchor.");
                }
            }

            if (!tutorial.CenterAnchor || tutorial.CenterAnchor.gameObject.scene != scene)
            {
                errors.Add(Prefix + "assign Center Anchor to this scene's middle hook collider.");
            }

            if (tutorial.Player && tutorial.Player.Settings && tutorial.CenterAnchor && (tutorial.Player.Settings.HookableLayers.value & (1 << tutorial.CenterAnchor.gameObject.layer)) == 0)
            {
                errors.Add(Prefix + "Center Anchor must use a hookable layer.");
            }

            // Verify the supplied arena's interior boundaries rather than relying on object names.
            if (scene.path == PrototypeBuilder.TutorialPath && room)
            {
                float centerX = tutorial.Sequence && tutorial.Sequence.StageTwoSpawn ? tutorial.Sequence.StageTwoSpawn.position.x : 100f;
                List<BoxCollider2D> solids = new List<BoxCollider2D>(room.GetComponentsInChildren<BoxCollider2D>(true)).FindAll(box => !box.isTrigger && box.gameObject.layer == PrototypeBuilder.SolidLayer);
                bool floor = false, ceiling = false, left = false, right = false;
                foreach (BoxCollider2D solid in solids)
                {
                    Bounds b = BoxWorldBounds(solid);
                    floor |= Mathf.Abs(b.max.y) <= 0.01f && b.min.x <= centerX - 14 && b.max.x >= centerX + 14;
                    ceiling |= Mathf.Abs(b.min.y - 14f) <= 0.01f && b.min.x <= centerX - 14 && b.max.x >= centerX + 14;
                    left |= Mathf.Abs(b.max.x - (centerX - 14f)) <= 0.01f && b.min.y <= 0 && b.max.y >= 14;
                    right |= Mathf.Abs(b.min.x - (centerX + 14f)) <= 0.01f && b.min.y <= 0 && b.max.y >= 14;
                }

                if (!floor || !ceiling || !left || !right)
                {
                    errors.Add(Prefix + "keep the supplied room enclosed by a floor, ceiling and both walls (interior X \u00C2\u00B114, Y 0\u00E2\u20AC\u201C14).");
                }
            }
        }

        private static void CheckSequence(TutorialSequence sequence, Scene scene, List<string> errors)
        {
            const string Prefix = "TutorialSequence: ";
            CheckReference(sequence.StageOne, "Stage One");
            CheckReference(sequence.StageTwo, "Stage Two");
            CheckReference(sequence.Player, "Player");
            CheckReference(sequence.SceneTransition, "Scene Transition");
            CheckReference(sequence.FollowCamera, "Follow Camera");
            CheckReference(sequence.LevelFlow, "Level Flow");
            CheckReference(sequence.StageOneSpawn, "Stage One Spawn");
            CheckReference(sequence.StageTwoSpawn, "Stage Two Spawn");
            CheckReference(sequence.StageOneBounds, "Stage One Bounds");
            CheckReference(sequence.StageTwoBounds, "Stage Two Bounds");
            if (!sequence.StageOneRoot || sequence.StageOneRoot.scene != scene || !sequence.StageTwoRoot || sequence.StageTwoRoot.scene != scene || sequence.StageOneRoot == sequence.StageTwoRoot)
            {
                errors.Add(Prefix + "assign two distinct room roots from this scene.");
            }

            if (!sequence.StageOneCameraSettings || !sequence.StageTwoCameraSettings)
            {
                errors.Add(Prefix + "assign both stage camera profiles.");
            }

            if (sequence.StageOne)
            {
                if (sequence.StageOne.Sequence != sequence || sequence.StageOne.Player != sequence.Player)
                {
                    errors.Add(Prefix + "Stage One must reference this sequence and its shared player.");
                }

                if (sequence.StageOne.AutoStart)
                {
                    errors.Add(Prefix + "disable Auto Start on Stage One; the sequence starts it.");
                }
            }

            if (sequence.StageTwo)
            {
                if (sequence.StageTwo.Sequence != sequence || sequence.StageTwo.Player != sequence.Player)
                {
                    errors.Add(Prefix + "Stage Two must reference this sequence and its shared player.");
                }

                if (sequence.StageTwo.AutoStart)
                {
                    errors.Add(Prefix + "disable Auto Start on Stage Two; the sequence starts it under its fade.");
                }
            }

            if (!EditorApplication.isPlaying)
            {
                if (sequence.StageOneRoot && !sequence.StageOneRoot.activeSelf)
                {
                    errors.Add(Prefix + "save Stage One Root active for the initial lesson.");
                }

                if (sequence.StageTwoRoot && sequence.StageTwoRoot.activeSelf)
                {
                    errors.Add(Prefix + "save Stage Two Root inactive; it is enabled under the internal fade.");
                }

                if (sequence.LevelFlow && sequence.LevelFlow.SpawnPoint != sequence.StageOneSpawn)
                {
                    errors.Add(Prefix + "initial LevelFlow spawn must be Stage One Spawn.");
                }
            }

            CheckFiniteNumbers(sequence, errors);
            void CheckReference(Component component, string label)
            {
                if (!component || component.gameObject.scene != scene)
                {
                    errors.Add(Prefix + "assign " + label + " from this scene.");
                }
            }
        }

        private static Vector2 ColliderCenter(Collider2D collider) => collider.transform.TransformPoint(collider.offset);
        private static Bounds BoxWorldBounds(BoxCollider2D collider)
        {
            Vector2 half = collider.size * 0.5f;
            var result = new Bounds(collider.transform.TransformPoint(collider.offset - half), Vector3.zero);
            result.Encapsulate(collider.transform.TransformPoint(collider.offset + half));
            result.Encapsulate(collider.transform.TransformPoint(collider.offset + new Vector2(half.x, -half.y)));
            result.Encapsulate(collider.transform.TransformPoint(collider.offset + new Vector2(-half.x, half.y)));
            return result;
        }

        private static void CheckPromptView(TutorialPromptView view, Scene scene, string prefix, List<string> errors)
        {
            if (!view || view.gameObject.scene != scene)
            {
                errors.Add(prefix + "assign this scene's TutorialPromptView.");
            }
            else
            {
                CheckFiniteNumbers(view, errors);
            }
        }

        private static string EnabledScenePath(int index)
        {
            if (index < 0)
            {
                return null;
            }

            int enabledIndex = 0;
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled)
                {
                    continue;
                }

                if (enabledIndex++ == index)
                {
                    return entry.path;
                }
            }

            return null;
        }

        private static void CheckSceneIndex(int index, string label, List<string> errors)
        {
            string path = EnabledScenePath(index);
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                errors.Add(label + ": choose an enabled scene index from File > Build Profiles > Scene List.");
            }
        }

        private static void CheckMissingScripts(Scene scene, List<string> errors)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    {
                        errors.Add(child.name + ": a script is missing. Restore its prefab or remove the missing component.");
                    }
                }
            }
        }

        public static List<string> CollectPrefabErrors()
        {
            var errors = new List<string>();
            foreach (string id in AssetDatabase.FindAssets("t:Prefab", new[] { PrototypeBuilder.Root + "/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(id);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors.Add("Could not load prefab: " + path);
                    continue;
                }

                foreach (Transform child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    {
                        errors.Add(path + ": missing script on " + child.name);
                    }
                }

                foreach (SpriteRenderer renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (renderer.sprite == null)
                    {
                        errors.Add(path + ": SpriteRenderer has no sprite.");
                    }
                }

                foreach (SpeedTarget target in prefab.GetComponentsInChildren<SpeedTarget>(true))
                {
                    EnemyHealthDisplay health = target.GetComponent<EnemyHealthDisplay>();
                    if (!health || health.Target != target)
                    {
                        errors.Add(path + ": each enemy needs EnemyHealthDisplay referencing its own target.");
                    }

                    if (!target.CombatSettings)
                    {
                        errors.Add(path + ": each enemy needs the shared combat settings profile.");
                    }
                }

                foreach (CombatFeedbackController feedback in prefab.GetComponentsInChildren<CombatFeedbackController>(true))
                {
                    if (!feedback.Settings || !feedback.SpeedEffectPrefab || !feedback.ImpactEffectPrefab)
                    {
                        errors.Add(path + ": CombatFeedback needs its settings and both ParticleSystem prefabs.");
                    }
                }
            }

            return errors;
        }

        private static void CheckMovementSettings(MovementSettings settings, List<string> errors)
        {
            string prefix = settings.name + ": ";
            if (settings.Mass <= 0)
            {
                errors.Add(prefix + "Mass must be positive.");
            }

            if (settings.YankSpeedThreshold <= 0)
            {
                errors.Add(prefix + "Yank Speed Threshold must be positive to enable both S modes.");
            }

            if (settings.YankVelocityChange < 0 || settings.SpaceReleaseBump < 0 || settings.SpaceStationaryMultiplier < 0)
            {
                errors.Add(prefix + "Release strengths and stationary multiplier cannot be negative.");
            }

            if (settings.RedirectPivotDominance <= 0.5f || settings.RedirectPivotDominance > 1)
            {
                errors.Add(prefix + "Redirect Pivot Dominance must be greater than 0.5 and at most 1 so opposing directions cannot cancel.");
            }

            if (settings.HookRange <= 0 || settings.HookTravelSpeed <= 0 || settings.MinimumRopeLength <= 0 || settings.MinimumRopeLength > settings.HookRange)
            {
                errors.Add(prefix + "Hook range, travel speed and minimum rope length must be positive; minimum length must fit within range.");
            }

            AnimationCurve curve = settings.SpaceDirectionalMultiplier;
            if (curve == null || curve.length < 2)
            {
                errors.Add(prefix + "Space Directional Multiplier needs a curve with at least two keys (-1 upward, +1 downward).");
            }
            else
            {
                foreach (Keyframe key in curve.keys)
                {
                    if (!Finite(key.time) || !Finite(key.value) || key.value < 0)
                    {
                        errors.Add(prefix + "Space Directional Multiplier keys must contain finite, nonnegative values.");
                        break;
                    }
                }
            }

            if (settings.HookableLayers.value == 0 || settings.MovementLayers.value == 0)
            {
                errors.Add(prefix + "Hookable Layers and Movement Layers cannot be empty.");
            }

            if (settings.SupportNormalY < 0 || settings.SupportNormalY > 1 || settings.WallNormalX < 0 || settings.WallNormalX > 1)
            {
                errors.Add(prefix + "Support Normal Y and Wall Normal X must be between 0 and 1.");
            }

            CheckFiniteNumbers(settings, errors);
        }

        private static void CheckCombatSettings(CombatFeedbackSettings settings, List<string> errors)
        {
            string prefix = settings.name + ": ";
            if (settings.MinimumDamageSpeed < 0 || settings.FullIntensitySpeed <= settings.MinimumDamageSpeed)
            {
                errors.Add(prefix + "Minimum Damage Speed must be nonnegative and Full Intensity Speed must be greater.");
            }

            if (settings.MaximumSpeedParticles < 16 || settings.ImpactParticlesAtThreshold < 1 || settings.ImpactParticlesAtMaximum < settings.ImpactParticlesAtThreshold)
            {
                errors.Add(prefix + "use a speed-particle budget of at least 16 and positive, increasing impact counts.");
            }

            if (settings.HitStopAtThreshold < 0 || settings.HitStopAtMaximum < settings.HitStopAtThreshold || settings.ShakeAmplitudeAtThreshold < 0 || settings.ShakeAmplitudeAtMaximum < settings.ShakeAmplitudeAtThreshold || settings.ShakeDurationAtThreshold < 0 || settings.ShakeDurationAtMaximum < settings.ShakeDurationAtThreshold || settings.ShakeFrequency < 0)
            {
                errors.Add(prefix + "hit-stop and shake controls must be nonnegative and maximum values cannot be below threshold values.");
            }

            if (settings.SpeedParticlesPerSecondAtThreshold < 0 || settings.SpeedParticlesPerSecondAtMaximum < settings.SpeedParticlesPerSecondAtThreshold || settings.SpeedParticleSizeAtThreshold <= 0 || settings.SpeedParticleSizeAtMaximum < settings.SpeedParticleSizeAtThreshold || settings.SpeedParticleLifetime <= 0 || settings.ImpactParticleLifetime <= 0)
            {
                errors.Add(prefix + "particle rate must be nonnegative, size/lifetime positive, and maximum rates/sizes cannot be below threshold values.");
            }

            AnimationCurve curve = settings.IntensityBySpeed;
            if (curve == null || curve.length < 2)
            {
                errors.Add(prefix + "Intensity By Speed needs a finite increasing 0\u20131 curve.");
            }
            else
            {
                float previous = 0f;
                for (int sample = 0; sample <= 64; sample++)
                {
                    float value = curve.Evaluate(sample / 64f);
                    if (!Finite(value) || value < -0.0001f || value > 1.0001f || value < previous - 0.0001f)
                    {
                        errors.Add(prefix + "Intensity By Speed must increase within 0\u20131 as speed increases.");
                        break;
                    }

                    previous = value;
                }
            }

            CheckFiniteNumbers(settings, errors);
        }

        private static void CheckCameraSettings(CameraSettings settings, List<string> errors)
        {
            if (settings.OrthographicSize <= 0 || settings.SmoothTimeX < 0 || settings.SmoothTimeY < 0 || settings.MaxLagX < 0 || settings.MaxLagY < 0)
            {
                errors.Add(settings.name + ": framing size must be positive; camera smoothing and lag distances cannot be negative.");
            }

            if (settings.ViewportMargin < 0 || settings.ViewportMargin >= 0.5f)
            {
                errors.Add(settings.name + ": Viewport Margin must be between 0 and 0.5 (exclusive). ");
            }

            CheckFiniteNumbers(settings, errors);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void CheckPauseCurve(AnimationCurve curve, List<string> errors, string controllerName = "StageOneTutorial")
        {
            string message = controllerName + ": Pause Ease Curve must decrease smoothly from speed fraction 1 at time 0 to 0 at time 1, with finite values between 0 and 1.";
            if (curve == null || curve.length < 2)
            {
                errors.Add(message);
                return;
            }

            const float Tolerance = 0.0001f;
            Keyframe[] keys = curve.keys;
            foreach (Keyframe key in keys)
            {
                if (!Finite(key.time) || !Finite(key.value) || !Finite(key.inTangent) || !Finite(key.outTangent) || !Finite(key.inWeight) || !Finite(key.outWeight))
                {
                    errors.Add(message);
                    return;
                }
            }

            if (Mathf.Abs(keys[0].time) > Tolerance || Mathf.Abs(keys[0].value - 1f) > Tolerance || Mathf.Abs(keys[keys.Length - 1].time - 1f) > Tolerance || Mathf.Abs(keys[keys.Length - 1].value) > Tolerance)
            {
                errors.Add(message);
                return;
            }

            // Check interpolation too: finite, ordered keys can still overshoot
            // or reverse when their tangents are edited in the curve Inspector.
            float previous = 1f;
            for (int sample = 0; sample <= 64; sample++)
            {
                float value = curve.Evaluate(sample / 64f);
                if (!Finite(value) || value < -Tolerance || value > 1f + Tolerance || value > previous + Tolerance)
                {
                    errors.Add(message);
                    return;
                }

                previous = value;
            }
        }

        private static void CheckFiniteNumbers(UnityEngine.Object settings, List<string> errors)
        {
            var serialized = new SerializedObject(settings);
            SerializedProperty property = serialized.GetIterator();
            bool children = true;
            while (property.NextVisible(children))
            {
                children = false;
                if (property.propertyType == SerializedPropertyType.Float && !Finite(property.floatValue))
                {
                    errors.Add(settings.name + ": " + property.displayName + " must be a finite number.");
                }
            }
        }

        private static List<T> FindInScene<T>(Scene scene)
            where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static void CheckTrigger(Component component, List<string> errors)
        {
            Collider2D collider = component.GetComponent<Collider2D>();
            if (collider == null || !collider.isTrigger)
            {
                errors.Add(component.name + ": enable Is Trigger on its Collider2D.");
            }
        }
    }
}

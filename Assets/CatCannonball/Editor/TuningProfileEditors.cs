using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CatCannonball.Editor
{
    /// <summary>Small inspectors that keep common feel controls visible and technical filters nearby.</summary>
    public abstract class TuningProfileEditor : UnityEditor.Editor
    {
        private bool _advanced;
        protected abstract string[] BasicFields
        {
            get;
        }
        protected abstract string Description
        {
            get;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            EditorGUILayout.HelpBox(Description + "\nProvisional defaults: human playtesting is still required. Shared profile changes affect every referencing object.", MessageType.Info);
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Asset edits during Play mode can persist. Duplicate this profile first when trying an independent feel.", MessageType.Warning);
            }

            if (GUILayout.Button("Duplicate Profile for Experiment\u2026"))
            {
                DuplicateProfile();
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Basic - feel controls", EditorStyles.boldLabel);
            var drawn = new HashSet<string>
            {
                "m_Script"
            };
            foreach (string name in BasicFields)
            {
                SerializedProperty property = serializedObject.FindProperty(name);
                if (property == null)
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, true);
                drawn.Add(name);
            }

            EditorGUILayout.Space(5);
            _advanced = EditorGUILayout.Foldout(_advanced, "Advanced \u2014 physics, filters and contact rules", true);
            if (_advanced)
            {
                EditorGUI.indentLevel++;
                SerializedProperty property = serializedObject.GetIterator();
                bool enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (!drawn.Contains(property.name))
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                }

                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DuplicateProfile()
        {
            // Flush any edit before copying so the new profile matches what the Inspector shows.
            serializedObject.ApplyModifiedProperties();
            string source = AssetDatabase.GetAssetPath(target);
            string destination = EditorUtility.SaveFilePanelInProject("Duplicate tuning profile", target.name + " Copy", "asset", "Assign the copy to the Player (movement) or CameraRig (camera). Duplicating does not change scene assignments.", PrototypeBuilder.Root + "/Settings");
            if (string.IsNullOrEmpty(destination) || source == destination)
            {
                return;
            }

            if (!AssetDatabase.CopyAsset(source, destination))
            {
                return;
            }

            AssetDatabase.ImportAsset(destination);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(destination);
        }
    }

    [CustomEditor(typeof(MovementSettings))]
    public sealed class MovementSettingsEditor : TuningProfileEditor
    {
        protected override string Description => "Tune the Player's shared movement profile. S uses total incoming speed: below threshold it adds a pull; otherwise it redirects while preserving speed. Both abilities release the rope.";
        protected override string[] BasicFields => new[]
        {
            nameof(MovementSettings.GravityScale),
            nameof(MovementSettings.AirDrag),
            nameof(MovementSettings.GroundFriction),
            nameof(MovementSettings.GroundForce),
            nameof(MovementSettings.AirForce),
            nameof(MovementSettings.JumpImpulse),
            nameof(MovementSettings.SpaceReleaseBump),
            nameof(MovementSettings.SpaceDirectionalMultiplier),
            nameof(MovementSettings.SpaceStationaryMultiplier),
            nameof(MovementSettings.YankSpeedThreshold),
            nameof(MovementSettings.YankVelocityChange),
            nameof(MovementSettings.RedirectPivotDominance),
            nameof(MovementSettings.WallRunForce),
            nameof(MovementSettings.WallSlideFriction),
            nameof(MovementSettings.FastFallMultiplier),
            nameof(MovementSettings.HookRange),
            nameof(MovementSettings.HookTravelSpeed),
            nameof(MovementSettings.AimAssistRadius),
            nameof(MovementSettings.MinimumImpactSpeed),
            nameof(MovementSettings.BadImpactAngle),
            nameof(MovementSettings.MinimumBadImpactSpeed)
        };
    }

    [CustomEditor(typeof(CameraSettings))]
    public sealed class CameraSettingsEditor : TuningProfileEditor
    {
        protected override string Description => "Tune smooth player following. Lower smoothing times catch up sooner. CameraBounds are edited on their scene object. Framing does not change hook reach.";
        protected override string[] BasicFields => new[]
        {
            nameof(CameraSettings.OrthographicSize),
            nameof(CameraSettings.Offset),
            nameof(CameraSettings.SmoothTimeX),
            nameof(CameraSettings.SmoothTimeY),
            nameof(CameraSettings.MaxLagX),
            nameof(CameraSettings.MaxLagY)
        };
    }

    [CustomEditor(typeof(CombatFeedbackSettings))]
    public sealed class CombatFeedbackSettingsEditor : TuningProfileEditor
    {
        protected override string Description => "Tune the shared damage threshold and speed-scaled feedback. Normal enemies and the speed trail use this same threshold. Hit stop briefly pauses gameplay and camera before shake.";
        protected override string[] BasicFields => new[]
        {
            nameof(CombatFeedbackSettings.MinimumDamageSpeed),
            nameof(CombatFeedbackSettings.FullIntensitySpeed),
            nameof(CombatFeedbackSettings.EnableHitStop),
            nameof(CombatFeedbackSettings.HitStopAtThreshold),
            nameof(CombatFeedbackSettings.HitStopAtMaximum),
            nameof(CombatFeedbackSettings.EnableCameraShake),
            nameof(CombatFeedbackSettings.ShakeAmplitudeAtThreshold),
            nameof(CombatFeedbackSettings.ShakeAmplitudeAtMaximum)
        };
    }
}

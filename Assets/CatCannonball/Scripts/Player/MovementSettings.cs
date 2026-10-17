using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [CreateAssetMenu(menuName = "Cat Cannonball/Movement Settings", fileName = "MovementSettings")]
    public sealed class MovementSettings : ScriptableObject
    {
        [Header("Body - provisional playtest values")]
        [Min(0.1f), Tooltip("Body mass in kg Changes force-driven movement -> release abilities use velocity changes and are mass independent")]
        public float Mass = 1f;
        [Min(0f), Tooltip("Multiplier on Unity's project gravity, there is no gameplay speed cap")]
        public float GravityScale = 1f;
        [Min(0f), Tooltip("Opposing force coefficient: drag = coefficient * speed^2")]
        public float AirDrag = 0.025f;
        [Min(0f)]
        public float GroundFriction = 9f;
        [Header("A / D drive and ordinary ground jump")]
        [Min(0f)]
        public float GroundForce = 38f;
        [Min(0f)]
        public float AirForce = 10f;
        [Min(0f)]
        public float JumpImpulse = 12f;
        [Min(0f)]
        public float CoyoteTime = 0.10f;
        [Min(0f)]
        public float JumpBuffer = 0.12f;
        [Header("Space - release plus upward bump")]
        [Min(0f), Tooltip("Base upward velocity added on release -> no impact on horizontal velocity")]
        public float SpaceReleaseBump = 3f;
        [Tooltip("Bump multiplier by downward alignment: -1 = straight up, 0 = horizontal, +1 = straight down. Intended for the down end to be higher than up end")]
        public AnimationCurve SpaceDirectionalMultiplier = new AnimationCurve(new Keyframe(-1f, 0.65f), new Keyframe(0f, 1f), new Keyframe(1f, 1.35f));
        [Min(0f), Tooltip("Multiplier when moving too slowly to have a stable direction")]
        public float SpaceStationaryMultiplier = 1f;
        [Min(0f), Tooltip("Speed below which Space uses the stationary multiplier (world units/second)")]
        public float SpaceStationarySpeed = 0.1f;
        [Header("S - low-speed yank / high-speed redirect")]
        [Min(0f)]
        public float YankSpeedThreshold = 8f;
        [Min(0f), Tooltip("This can exceed threshold")]
        public float YankVelocityChange = 10f;
        [Range(0.51f, 1f), Tooltip("Intensity of the high speed redirect: 1 = directly toward pivot; 0.51 = almost equal contributions. Above 0.5 prevents opposite directions cancelling")]
        public float RedirectPivotDominance = 0.85f;
        [Header("W wall movement and unhooked S fall")]
        [Min(0f)]
        public float WallRunForce = 32f;
        [Min(0f)]
        public float WallSlideFriction = 12f;
        [Range(0f, 1f)]
        public float WallSlideGravityLimit = 0.9f;
        [Min(1f)]
        public float FastFallMultiplier = 1.4f;
        [Header("Traveling hook - world units, independent of camera")]
        [Min(0.1f)]
        public float HookRange = 12.4f;
        [Min(0.1f)]
        public float HookTravelSpeed = 82f;
        [Min(0.01f)]
        public float MinimumRopeLength = 0.1f;
        [Min(0f)]
        public float AimAssistRadius = 1.2f;
        public LayerMask HookableLayers = (1 << 9) | (1 << 10) | (1 << 11) | (1 << 12);
        public LayerMask HookBlockingLayers = 1 << 9;
        public LayerMask MovementLayers = (1 << 9) | (1 << 10);
        [Header("Advanced - probes and support")]
        [Min(0f)]
        public float GroundProbeDistance = 0.10f;
        [Min(0f)]
        public float WallProbeDistance = 0.10f;
        [Range(0.01f, 1f)]
        public float SupportNormalY = 0.55f;
        [Range(0.01f, 1f)]
        public float WallNormalX = 0.65f;
        [Min(0f)]
        public float GroundedUpwardSpeed = 0.5f;
        [Min(0f)]
        public float PlatformSurfaceTolerance = 0.12f;
        [Header("Advanced - flow-breaking impacts")]
        [Min(0f)]
        public float MinimumImpactSpeed = 7f;
        [Range(0f, 90f)]
        public float BadImpactAngle = 55f;
        [Min(0f)]
        public float MinimumBadImpactSpeed = 1.5f;
        private void OnValidate()
        {
            Mass = Mathf.Max(0.1f, Mass);
            HookRange = Mathf.Max(0.1f, HookRange);
            HookTravelSpeed = Mathf.Max(0.1f, HookTravelSpeed);
            MinimumRopeLength = Mathf.Max(0.01f, MinimumRopeLength);
            RedirectPivotDominance = Mathf.Clamp(RedirectPivotDominance, 0.51f, 1f);
            if (SpaceDirectionalMultiplier == null || SpaceDirectionalMultiplier.length == 0)
            {
                SpaceDirectionalMultiplier = AnimationCurve.Linear(-1f, 0.65f, 1f, 1.35f);
            }
        }
    }
}

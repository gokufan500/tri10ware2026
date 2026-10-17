using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary>Collection of values for tuning combat feedback effects</summary>
    [CreateAssetMenu(menuName = "Cat Cannonball/Combat Feedback Settings", fileName = "CombatFeedbackSettings")]
    public sealed class CombatFeedbackSettings : ScriptableObject
    {
        [Header("Speed and intensity")]
        [Min(0f)]
        public float MinimumDamageSpeed = 8f;
        [Min(0.01f)]
        public float FullIntensitySpeed = 36f;
        [Tooltip("Maps speed between the damage threshold and full intensity")]
        public AnimationCurve IntensityBySpeed = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [Header("Speed particles")]
        public Color SpeedColor = new Color(1f, 0.8f, 0.25f, 0.8f);
        [Min(0f)]
        public float SpeedParticlesPerSecondAtThreshold = 14f;
        [Min(0f)]
        public float SpeedParticlesPerSecondAtMaximum = 90f;
        [Min(0.001f)]
        public float SpeedParticleSizeAtThreshold = 0.06f;
        [Min(0.001f)]
        public float SpeedParticleSizeAtMaximum = 0.16f;
        [Min(0.01f)]
        public float SpeedParticleLifetime = 0.22f;
        [Min(0f)]
        public float SpeedParticleScatterAtThreshold = 0.3f;
        [Min(0f)]
        public float SpeedParticleScatterAtMaximum = 2.5f;
        [Min(0f)]
        public float SpeedEmissionRadius = 0.34f;
        [Tooltip("Fraction of player velocity inherited by particles at spawn-> lower values leave a clearer trail")]
        [Range(0f, 1f)]
        public float InheritedVelocityFraction = 0.12f;
        [Min(16)]
        public int MaximumSpeedParticles = 160;
        [Header("Impact particles")]
        public Color ImpactColor = new Color(1f, 0.65f, 0.18f, 1f);
        [Min(1)]
        public int ImpactParticlesAtThreshold = 8;
        [Min(1)]
        public int ImpactParticlesAtMaximum = 28;
        [Min(0f)]
        public float ImpactParticleSpeedAtThreshold = 2f;
        [Min(0f)]
        public float ImpactParticleSpeedAtMaximum = 8f;
        [Min(0.001f)]
        public float ImpactParticleSizeAtThreshold = 0.08f;
        [Min(0.001f)]
        public float ImpactParticleSizeAtMaximum = 0.18f;
        [Min(0.01f)]
        public float ImpactParticleLifetime = 0.3f;
        [Header("Hit stop (real seconds)")]
        public bool EnableHitStop = true;
        [Min(0f)]
        public float HitStopAtThreshold = 0.025f;
        [Min(0f)]
        public float HitStopAtMaximum = 0.09f;
        [Header("Camera shake")]
        public bool EnableCameraShake = true;
        [Tooltip("Max displacement in world units")]
        [Min(0f)]
        public float ShakeAmplitudeAtThreshold = 0.05f;
        [Min(0f)]
        public float ShakeAmplitudeAtMaximum = 0.3f;
        [Min(0f)]
        public float ShakeDurationAtThreshold = 0.12f;
        [Min(0f)]
        public float ShakeDurationAtMaximum = 0.28f;
        [Min(0f)]
        public float ShakeFrequency = 28f;
        public AnimationCurve ShakeDecay = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        public float IntensityAtSpeed(float speed)
        {
            float normalized = Mathf.InverseLerp(Mathf.Max(0f, MinimumDamageSpeed), Mathf.Max(MinimumDamageSpeed + 0.01f, FullIntensitySpeed), speed);
            return Mathf.Clamp01(IntensityBySpeed == null ? normalized : IntensityBySpeed.Evaluate(normalized));
        }

        public float HitStopAtSpeed(float speed) => EnableHitStop ? Mathf.Max(0f, Mathf.Lerp(HitStopAtThreshold, HitStopAtMaximum, IntensityAtSpeed(speed))) : 0f;
        private void OnValidate()
        {
            MinimumDamageSpeed = Mathf.Max(0f, MinimumDamageSpeed);
            FullIntensitySpeed = Mathf.Max(MinimumDamageSpeed + 0.01f, FullIntensitySpeed);
            MaximumSpeedParticles = Mathf.Max(16, MaximumSpeedParticles);
            ImpactParticlesAtThreshold = Mathf.Max(1, ImpactParticlesAtThreshold);
            ImpactParticlesAtMaximum = Mathf.Max(ImpactParticlesAtThreshold, ImpactParticlesAtMaximum);
        }
    }
}

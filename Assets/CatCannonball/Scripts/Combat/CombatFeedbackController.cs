using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary> Scene-local combat presentation. It observes damage -> never changes movement speed, player position, or target health </summary>
    
    [DisallowMultipleComponent]
    public sealed class CombatFeedbackController : MonoBehaviour
    {
        [Header("Scene references")]
        public PlayerMotor Player;
        public FollowCamera2D FollowCamera;
        public SceneTransition SceneTransition;
        public CombatFeedbackSettings Settings;
        [Header("Reusable particle prefabs")]
        public ParticleSystem SpeedEffectPrefab;
        public ParticleSystem ImpactEffectPrefab;
        [Min(1)]
        public int ImpactPoolSize = 8;
        private ParticleSystem _speedEffect;
        private ParticleSystem[] _impacts;
        private int _nextImpact;
        private float _emissionBudget;
        private float _hitStopUntil;
        private float _previousTimeScale = 1f;
        private bool _ownsHitStop;
        public float CurrentSpeed
        {
            get;
            private set;
        }
        public float CurrentIntensity
        {
            get;
            private set;
        }
        public bool IsDamageReady
        {
            get;
            private set;
        }
        public bool IsHitStopped => _ownsHitStop;
        public float RemainingHitStop => _ownsHitStop ? Mathf.Max(0f, _hitStopUntil - Time.unscaledTime) : 0f;
        public int ImpactEffectCount
        {
            get;
            private set;
        }
        public float LastImpactIntensity
        {
            get;
            private set;
        }
        public float LastHitStopDuration
        {
            get;
            private set;
        }
        public ParticleSystem SpeedEffect => _speedEffect;

        private void Awake() => EnsureEffects();
        private void OnEnable() => SpeedTarget.HitApplied += OnHitApplied;
        private void OnDisable()
        {
            SpeedTarget.HitApplied -= OnHitApplied;
            ResetFeedback();
        }

        public void ResetFeedback()
        {
            FinishHitStop();
            ClearEffects();
            if (FollowCamera)
            {
                FollowCamera.CancelImpact();
            }
        }

        private void ClearEffects()
        {
            if (_speedEffect)
            {
                _speedEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_impacts != null)
            {
                foreach (ParticleSystem effect in _impacts)
                {
                    if (effect)
                    {
                        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            _emissionBudget = 0f;
            _nextImpact = 0;
            CurrentSpeed = CurrentIntensity = 0f;
            IsDamageReady = false;
        }

        private void EnsureEffects()
        {
            if (!Settings)
            {
                return;
            }

            if (!_speedEffect && SpeedEffectPrefab)
            {
                _speedEffect = Instantiate(SpeedEffectPrefab, transform);
                _speedEffect.name = "Speed particles (pooled)";
                ConfigureParticles(_speedEffect, true, Settings.MaximumSpeedParticles);
            }

            if (_impacts != null || !ImpactEffectPrefab)
            {
                return;
            }

            _impacts = new ParticleSystem[Mathf.Max(1, ImpactPoolSize)];
            for (int i = 0; i < _impacts.Length; i++)
            {
                _impacts[i] = Instantiate(ImpactEffectPrefab, transform);
                _impacts[i].name = "Impact particles (pooled " + (i + 1) + ")";
                ConfigureParticles(_impacts[i], false, Mathf.Max(1, Settings.ImpactParticlesAtMaximum));
            }
        }

        private static void ConfigureParticles(ParticleSystem effect, bool looping, int capacity)
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = effect.main;
            main.playOnAwake = false;
            main.loop = looping;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.maxParticles = Mathf.Max(1, capacity);
            ParticleSystem.EmissionModule emission = effect.emission;

            // NOTE: Manual emission gives the whole pool one shared speed/intensity rule.
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = effect.shape;
            shape.enabled = false;
        }

        private void Update()
        {
            UpdateHitStop();
            if (!Settings || !Player || !Player.Body)
            {
                return;
            }

            EnsureEffects();
            CurrentSpeed = Player.Body.linearVelocity.magnitude;
            IsDamageReady = CurrentSpeed >= Mathf.Max(0f, Settings.MinimumDamageSpeed);
            CurrentIntensity = IsDamageReady ? Settings.IntensityAtSpeed(CurrentSpeed) : 0f;
            if (!IsDamageReady || Time.deltaTime <= 0f)
            {
                _emissionBudget = 0f;
                return;
            }

            if (!_speedEffect)
            {
                return;
            }

            _speedEffect.transform.position = Player.Body.position;
            if (!_speedEffect.isPlaying)
            {
                _speedEffect.Play();
            }

            float rate = Mathf.Max(0f, Mathf.Lerp(Settings.SpeedParticlesPerSecondAtThreshold, Settings.SpeedParticlesPerSecondAtMaximum, CurrentIntensity));
            _emissionBudget += rate * Time.deltaTime;
            int count = Mathf.Min(Mathf.FloorToInt(_emissionBudget), Mathf.Max(16, Settings.MaximumSpeedParticles));
            _emissionBudget -= count;
            float scatter = Mathf.Max(0f, Mathf.Lerp(Settings.SpeedParticleScatterAtThreshold, Settings.SpeedParticleScatterAtMaximum, CurrentIntensity));
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, Settings.SpeedEmissionRadius);
                var particle = new ParticleSystem.EmitParams
                {
                    position = Player.Body.position + offset,
                    velocity = Player.Body.linearVelocity * Settings.InheritedVelocityFraction + Random.insideUnitCircle * scatter,
                    startColor = Settings.SpeedColor,
                    startSize = Mathf.Max(0.001f, Mathf.Lerp(Settings.SpeedParticleSizeAtThreshold, Settings.SpeedParticleSizeAtMaximum, CurrentIntensity)),
                    startLifetime = Mathf.Max(0.01f, Settings.SpeedParticleLifetime)
                };
                _speedEffect.Emit(particle, 1);
            }
        }

        private void OnHitApplied(CombatHit hit)
        {
            if (hit.Player != Player || !hit.Target || hit.Target.gameObject.scene != gameObject.scene || !Settings)
            {
                return;
            }

            PlayImpact(hit.Speed, hit.Point);
        }

        // We can reuse this for receiving damage once enemy attacks implemented
        public void PlayImpact(float speed, Vector2 point)
        {
            if (!isActiveAndEnabled || !Settings)
            {
                return;
            }

            EnsureEffects();
            LastImpactIntensity = Settings.IntensityAtSpeed(speed);
            EmitImpact(point, LastImpactIntensity);
            float stop = Settings.HitStopAtSpeed(speed);
            bool paused = BeginHitStop(stop);
            LastHitStopDuration = paused ? stop : 0f;
            if (FollowCamera && Settings.EnableCameraShake)
            {
                FollowCamera.PlayImpact(Mathf.Lerp(Settings.ShakeAmplitudeAtThreshold, Settings.ShakeAmplitudeAtMaximum, LastImpactIntensity), Mathf.Lerp(Settings.ShakeDurationAtThreshold, Settings.ShakeDurationAtMaximum, LastImpactIntensity), Settings.ShakeFrequency, paused ? stop : 0f, Settings.ShakeDecay);
            }
        }

        private void EmitImpact(Vector2 point, float intensity)
        {
            if (_impacts == null || _impacts.Length == 0)
            {
                return;
            }

            ParticleSystem effect = _impacts[_nextImpact];
            _nextImpact = (_nextImpact + 1) % _impacts.Length;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.transform.position = point;
            effect.Play();
            int count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(Settings.ImpactParticlesAtThreshold, Settings.ImpactParticlesAtMaximum, intensity)));
            float speed = Mathf.Max(0f, Mathf.Lerp(Settings.ImpactParticleSpeedAtThreshold, Settings.ImpactParticleSpeedAtMaximum, intensity));
            for (int i = 0; i < count; i++)
            {
                var particle = new ParticleSystem.EmitParams
                {
                    position = point,
                    velocity = Random.insideUnitCircle.normalized * Random.Range(speed * 0.4f, speed),
                    startColor = Settings.ImpactColor,
                    startSize = Mathf.Max(0.001f, Mathf.Lerp(Settings.ImpactParticleSizeAtThreshold, Settings.ImpactParticleSizeAtMaximum, intensity)),
                    startLifetime = Mathf.Max(0.01f, Settings.ImpactParticleLifetime)
                };
                effect.Emit(particle, 1);
            }

            ImpactEffectCount++;
        }

        private bool BeginHitStop(float realSeconds)
        {
            if (realSeconds <= 0f || (SceneTransition && SceneTransition.IsTransitioning) || (Player && Player.Input && Player.Input.GameplayLocked))
            {
                return false;
            }

            if (!_ownsHitStop)
            {
                // Hit stop ownership is tied to the controller not the causer of damage. If we want to add fx from receiving damage it should be done on the same controller
                if (Time.timeScale <= 0f)
                {
                    return false;
                }

                _previousTimeScale = Time.timeScale;
                _ownsHitStop = true;
            }

            _hitStopUntil = Mathf.Max(_hitStopUntil, Time.unscaledTime + realSeconds);
            Time.timeScale = 0f;
            return true;
        }

        private void UpdateHitStop()
        {
            if (!_ownsHitStop)
            {
                return;
            }

            if (Time.timeScale > 0f)
            {
                _ownsHitStop = false;
                _hitStopUntil = 0f;
                return;
            }

            if ((SceneTransition && SceneTransition.IsTransitioning) || (Player && Player.Input && Player.Input.GameplayLocked))
            {
                // The next pause owner chooses when gameplay resumes
                _ownsHitStop = false;
                _hitStopUntil = 0f;
                return;
            }

            if (Time.unscaledTime >= _hitStopUntil)
            {
                FinishHitStop();
            }
        }

        public void FinishHitStop()
        {
            if (!_ownsHitStop)
            {
                return;
            }

            _ownsHitStop = false;
            _hitStopUntil = 0f;
            bool otherPause = (SceneTransition && SceneTransition.IsTransitioning) || (Player && Player.Input && Player.Input.GameplayLocked);
            if (!otherPause && Time.timeScale <= 0f)
            {
                Time.timeScale = _previousTimeScale;
            }
        }

        public void CancelHitStop() => FinishHitStop();
        public static void CancelSceneHitStops(Scene scene)
        {
            foreach (CombatFeedbackController feedback in FindObjectsByType<CombatFeedbackController>())
            {
                if (feedback.gameObject.scene == scene)
                {
                    feedback.FinishHitStop();
                }
            }
        }
    }
}

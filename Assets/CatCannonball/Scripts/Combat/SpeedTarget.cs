using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [Serializable]
    public sealed class IntEvent : UnityEvent<int>
    {
    }

    public readonly struct CombatHit
    {
        public readonly SpeedTarget Target;
        public readonly PlayerMotor Player;
        public readonly float Speed;
        public readonly Vector2 Point;
        public readonly int Damage;
        public readonly int RemainingHealth;
        public CombatHit(SpeedTarget target, PlayerMotor player, float speed, Vector2 point, int damage, int remainingHealth)
        {
            Target = target;
            Player = player;
            Speed = speed;
            Point = point;
            Damage = damage;
            RemainingHealth = remainingHealth;
        }
    }

    /// <summary>Practice enemy / weak point: body contact turns movement speed into damage.</summary>
    [DisallowMultipleComponent]
    public sealed class SpeedTarget : MonoBehaviour
    {
        [Header("Damage tuning")]
        [Tooltip("For the love of god please keep this the same as the Combat Feedback prefab")]
        public CombatFeedbackSettings CombatSettings;
        [Tooltip("Negative uses the shared profile -> only mess with this value if you want special behavior for this target")]
        public float MinimumDamageSpeedOverride = -1f;
        [Min(1)]
        public int HitPoints = 3;
        [Tooltip("Speed converted to damage by dividing by this value. Min damage is 1")]
        [Min(0.01f)]
        public float SpeedPerDamage = 8f;
        [Tooltip("In addition to the cooldown a hit needs an additional collider entry -> avoids multiple hits from a single collision")]
        [Min(0f)]
        public float HitCooldown = 0.35f;
        public bool ResetOnRespawn = true;
        [Header("Optional boss rule")]
        [Tooltip("Assign a group to prevent repeat hits on this point until another point is hit")]
        public WeakPointGroup WeakPointGroup;
        [Header("Designer actions")]
        [Tooltip("Integer argument = damage dealt by the accepted hit")]
        public IntEvent OnHit = new IntEvent();
        [Tooltip("Integer argument = remaining health")]
        public IntEvent OnHealthChanged = new IntEvent();
        public UnityEvent OnDefeated = new UnityEvent();
        public UnityEvent OnReset = new UnityEvent();
        private Collider2D[] _targetColliders;
        private Renderer[] _targetRenderers;
        private bool[] _colliderDefaults;
        private bool[] _rendererDefaults;
        private float _nextHitTime;
        private bool _initialized;
        public int CurrentHealth
        {
            get;
            private set;
        }
        public bool IsAlive => CurrentHealth > 0;
        public int LastDamage
        {
            get;
            private set;
        }
        public float LastHitSpeed
        {
            get;
            private set;
        }
        public float DamageThreshold => MinimumDamageSpeedOverride >= 0f ? MinimumDamageSpeedOverride : CombatSettings ? Mathf.Max(0f, CombatSettings.MinimumDamageSpeed) : 8f;

        public static event Action<CombatHit> HitApplied;
        private void Awake() => EnsureInitialized();
        private void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _targetColliders = GetComponentsInChildren<Collider2D>(true);
            _targetRenderers = GetComponentsInChildren<Renderer>(true);
            _colliderDefaults = new bool[_targetColliders.Length];
            _rendererDefaults = new bool[_targetRenderers.Length];
            for (int i = 0; i < _targetColliders.Length; i++)
            {
                _colliderDefaults[i] = _targetColliders[i].enabled;
            }

            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                _rendererDefaults[i] = _targetRenderers[i].enabled;
            }

            CurrentHealth = Mathf.Max(1, HitPoints);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other.GetComponentInParent<PlayerMotor>());
        private void OnCollisionEnter2D(Collision2D collision) => TryHit(collision.collider.GetComponentInParent<PlayerMotor>());

        public bool TryHit(PlayerMotor player)
        {
            EnsureInitialized();
            if (player == null || !IsAlive || Time.time < _nextHitTime)
            {
                return false;
            }

            if (WeakPointGroup != null && !WeakPointGroup.CanHit(this))
            {
                return false;
            }

            float incomingSpeed = player.PreStepVelocity.magnitude;
            if (incomingSpeed < DamageThreshold)
            {
                return false;
            }

            Vector2 impactPoint = transform.position;
            foreach (Collider2D collider in _targetColliders)
            {
                if (collider && collider.enabled)
                {
                    impactPoint = collider.ClosestPoint(player.Body.position);
                    break;
                }
            }

            _nextHitTime = Time.time + HitCooldown;
            LastHitSpeed = incomingSpeed;
            LastDamage = Mathf.Max(1, Mathf.FloorToInt(LastHitSpeed / Mathf.Max(0.01f, SpeedPerDamage)));
            CurrentHealth = Mathf.Max(0, CurrentHealth - LastDamage);
            if (!IsAlive)
            {
                SetPresentationEnabled(false);
            }

            HitApplied?.Invoke(new CombatHit(this, player, LastHitSpeed, impactPoint, LastDamage, CurrentHealth));
            if (WeakPointGroup != null)
            {
                WeakPointGroup.RegisterHit(this);
            }

            OnHit.Invoke(LastDamage);
            OnHealthChanged.Invoke(CurrentHealth);
            if (!IsAlive)
            {
                OnDefeated.Invoke();
            }

            return true;
        }

        public void ResetTarget()
        {
            EnsureInitialized();
            CurrentHealth = Mathf.Max(1, HitPoints);
            LastDamage = 0;
            LastHitSpeed = 0f;
            _nextHitTime = 0f;
            SetPresentationEnabled(true);
            OnHealthChanged.Invoke(CurrentHealth);
            OnReset.Invoke();
        }

        private void SetPresentationEnabled(bool visible)
        {
            for (int i = 0; i < _targetColliders.Length; i++)
            {
                if (_targetColliders[i] != null)
                {
                    _targetColliders[i].enabled = visible && _colliderDefaults[i];
                }
            }

            for (int i = 0; i < _targetRenderers.Length; i++)
            {
                if (_targetRenderers[i] != null)
                {
                    _targetRenderers[i].enabled = visible && _rendererDefaults[i];
                }
            }
        }
    }
}

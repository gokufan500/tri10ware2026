using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [RequireComponent(typeof(BoxCollider2D), typeof(PlatformEffector2D))]
    [DisallowMultipleComponent]
    public sealed class OneWayPlatform : MonoBehaviour
    {
        public static readonly HashSet<OneWayPlatform> Active = new HashSet<OneWayPlatform>();
        [Min(0f)]
        public float ReleaseDelay = 0.15f;
        [Min(0f)]
        public float Clearance = 0.05f;
        private BoxCollider2D _platformCollider;
        private readonly Dictionary<Collider2D, float> _ignoredPlayers = new Dictionary<Collider2D, float>();
        private void Awake() => Configure();
        private void Reset() => Configure();
        private void OnValidate()
        {
            ReleaseDelay = Mathf.Max(0f, ReleaseDelay);
            Clearance = Mathf.Max(0f, Clearance);
            Configure();
        }

        private void Configure()
        {
            _platformCollider = GetComponent<BoxCollider2D>();
            PlatformEffector2D effector = GetComponent<PlatformEffector2D>();
            if (_platformCollider != null)
            {
                _platformCollider.usedByEffector = true;
            }

            if (effector != null)
            {
                effector.useOneWay = true;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }
        }

        private void OnEnable() => Active.Add(this);
        private void OnDisable()
        {
            Active.Remove(this);
            ResetCollisions();
        }

        public void UpdatePassThrough(Collider2D player, bool isHooked, bool dropPressed)
        {
            if (player == null || _platformCollider == null)
            {
                return;
            }

            if (isHooked || dropPressed)
            {
                Physics2D.IgnoreCollision(player, _platformCollider, true);
                _ignoredPlayers[player] = Time.time + ReleaseDelay;
                return;
            }

            if (!_ignoredPlayers.TryGetValue(player, out float releaseAt) || Time.time < releaseAt)
            {
                return;
            }

            Bounds safeBounds = _platformCollider.bounds;
            safeBounds.Expand(Clearance * 2f);
            if (safeBounds.Intersects(player.bounds))
            {
                return;
            }

            Physics2D.IgnoreCollision(player, _platformCollider, false);
            _ignoredPlayers.Remove(player);
        }

        public void ResetCollisions()
        {
            if (_platformCollider != null)
            {
                foreach (KeyValuePair<Collider2D, float> pair in _ignoredPlayers)
                {
                    if (pair.Key != null)
                    {
                        Physics2D.IgnoreCollision(pair.Key, _platformCollider, false);
                    }
                }
            }

            _ignoredPlayers.Clear();
        }
    }
}

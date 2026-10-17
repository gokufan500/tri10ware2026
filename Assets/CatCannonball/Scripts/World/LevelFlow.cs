using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary>Owns the current spawn location and resets a practice attempt.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelFlow : MonoBehaviour
    {
        [Header("Scene references")]
        public PlayerMotor Player;
        public Transform SpawnPoint;
        public FollowCamera2D FollowCamera;
        [Header("Safety net")]
        public float KillY = -14f;
        [Header("Designer actions")]
        public UnityEvent OnRespawn = new UnityEvent();
        private bool _respawning;
        public int RespawnCount
        {
            get;
            private set;
        }
        public Vector2 SpawnPosition => SpawnPoint != null ? (Vector2)SpawnPoint.position : (Vector2)transform.position;

        private void Start()
        {
            if (Player != null && SpawnPoint != null)
            {
                Player.ResetAt(SpawnPosition);
            }

            SnapFollowCamera();
        }

        private void Update()
        {
            if (Player != null && Player.transform.position.y < KillY)
            {
                Respawn();
            }
        }

        public void Respawn()
        {
            if (Player == null || _respawning)
            {
                return;
            }

            _respawning = true;
            foreach (OneWayPlatform platform in OneWayPlatform.Active)
            {
                if (platform.gameObject.scene == gameObject.scene)
                {
                    platform.ResetCollisions();
                }
            }

            foreach (CombatFeedbackController feedback in FindObjectsByType<CombatFeedbackController>())
            {
                if (feedback.gameObject.scene == gameObject.scene && feedback.Player == Player)
                {
                    feedback.ResetFeedback();
                }
            }

            Player.ResetAt(SpawnPosition);
            foreach (SpeedTarget target in FindObjectsByType<SpeedTarget>(FindObjectsInactive.Include))
            {
                if (target.gameObject.scene == gameObject.scene && target.ResetOnRespawn)
                {
                    target.ResetTarget();
                }
            }

            foreach (WeakPointGroup group in FindObjectsByType<WeakPointGroup>(FindObjectsInactive.Include))
            {
                if (group.gameObject.scene == gameObject.scene)
                {
                    group.ResetGroup();
                }
            }

            RespawnCount++;
            _respawning = false;
            OnRespawn.Invoke();
            SnapFollowCamera();
        }

        private void SnapFollowCamera()
        {
            if (!FollowCamera)
            {
                foreach (FollowCamera2D candidate in FindObjectsByType<FollowCamera2D>())
                {
                    if (candidate.gameObject.scene == gameObject.scene)
                    {
                        FollowCamera = candidate;
                        break;
                    }
                }
            }

            if (!FollowCamera || !Player)
            {
                return;
            }

            if (!FollowCamera.Target)
            {
                FollowCamera.Target = Player.transform;
            }

            FollowCamera.SnapToTarget();
        }
    }
}

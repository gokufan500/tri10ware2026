using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [DisallowMultipleComponent]
    public sealed class DeathZone : MonoBehaviour
    {
        public LevelFlow LevelFlow;
        public UnityEvent OnEntered = new UnityEvent();
        private void Reset()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryEnter(other.GetComponentInParent<PlayerMotor>());
        }

        public void TryEnter(PlayerMotor visitor)
        {
            if (LevelFlow == null || visitor == null || visitor != LevelFlow.Player)
            {
                return;
            }

            OnEntered.Invoke();
            LevelFlow.Respawn();
        }
    }
}

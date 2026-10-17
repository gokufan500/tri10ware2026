using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace CatCannonball
{
    public sealed class WeakPointGroup : MonoBehaviour
    {
        [Tooltip("Drag the target prefab instances belonging to this boss into this list")]
        public SpeedTarget[] WeakPoints = new SpeedTarget[0];
        [Tooltip("Requires a different weak point to be hit before this one can be hit again")]
        public bool RequireAlternatingHits = true;
        public UnityEvent OnAllDefeated = new UnityEvent();
        public UnityEvent OnReset = new UnityEvent();
        public SpeedTarget LastHit
        {
            get;
            private set;
        }
        public bool IsDefeated
        {
            get;
            private set;
        }

        private void Awake()
        {
            foreach (SpeedTarget point in WeakPoints)
            {
                if (point != null)
                {
                    point.WeakPointGroup = this;
                }
            }
        }

        public bool CanHit(SpeedTarget point)
        {
            if (!RequireAlternatingHits || LastHit != point)
            {
                return true;
            }

            foreach (SpeedTarget other in WeakPoints)
            {
                if (other != null && other != point && other.IsAlive)
                {
                    return false;
                }
            }

            return true;
        }

        public void RegisterHit(SpeedTarget point)
        {
            LastHit = point;
            if (IsDefeated || WeakPoints.Length == 0)
            {
                return;
            }

            foreach (SpeedTarget other in WeakPoints)
            {
                if (other != null && other.IsAlive)
                {
                    return;
                }
            }

            IsDefeated = true;
            OnAllDefeated.Invoke();
        }

        public void ResetGroup()
        {
            LastHit = null;
            IsDefeated = false;
            OnReset.Invoke();
        }
    }
}

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace CatCannonball
{
    public enum HookState
    {
        Idle,
        Flying,
        Attached
    }

    [RequireComponent(typeof(Rigidbody2D), typeof(DistanceJoint2D), typeof(LineRenderer))]
    public sealed class GrappleController : MonoBehaviour
    {
        [HideInInspector]
        public MovementSettings Settings;
        public PlayerMotor Motor;
        public PlayerInputReader Input;
        [Header("Designer events")]
        public UnityEvent OnAttached = new UnityEvent();
        public UnityEvent OnReleased = new UnityEvent();
        private DistanceJoint2D _joint;
        private LineRenderer _rope;
        private Collider2D _target;
        private Vector2 _localAnchor;
        private Vector2 _hookPosition;
        private Vector2 _shotOrigin;
        private float _travelBudget;
        private float _shotRange;
        private float _shotTravelSpeed;
        private float _shotMinimumRopeLength;
        private LayerMask _shotBlockingLayers;
        private readonly Collider2D[] _candidates = new Collider2D[128];
        private readonly RaycastHit2D[] _rayHits = new RaycastHit2D[128];
        public HookState State
        {
            get;
            private set;
        }
        public bool IsAttached => State == HookState.Attached && _target && _target.enabled && _target.gameObject.activeInHierarchy;
        public Collider2D TargetCollider => _target;
        public float RopeLength => _joint ? _joint.distance : 0f;
        public Vector2 AnchorPoint => _target ? (Vector2)_target.transform.TransformPoint(_localAnchor) : _hookPosition;
        public float Range => Motor && Motor.Settings ? Motor.Settings.HookRange : Settings ? Settings.HookRange : 0f;
        public bool CanInitiateTether => Motor && Motor.Settings && Motor.Body && State == HookState.Idle && !Motor.IsGroundedForTether();

        private void Awake()
        {
            if (Motor && Motor.Settings)
            {
                Settings = Motor.Settings;
            }

            _joint = GetComponent<DistanceJoint2D>();
            _rope = GetComponent<LineRenderer>();
            _joint.enabled = false;
            _joint.autoConfigureDistance = false;
            _joint.autoConfigureConnectedAnchor = false;
            _joint.maxDistanceOnly = true;
            _joint.anchor = Vector2.zero;
            _rope.useWorldSpace = true;
            _rope.positionCount = 2;
            _rope.enabled = false;
        }

        // only called by PlayerMotor
        public void Step(InputFrame controls, float step)
        {
            if (Motor && Motor.Settings)
            {
                Settings = Motor.Settings;
            }

            if (!Settings)
            {
                return;
            }

            if (controls.HookReleased || (!controls.HookHeld && State != HookState.Idle))
            {
                Release();
            }

            if (controls.HookPressed && controls.HookHeld && State == HookState.Idle)
            {
                TryFire(controls.AimWorld);
            }

            if (State == HookState.Idle)
            {
                return;
            }

            if (!_target || !_target.enabled || !_target.gameObject.activeInHierarchy)
            {
                Release();
                return;
            }

            Vector2 anchor = AnchorPoint;
            if (State == HookState.Flying)
            {
                float advance = _shotTravelSpeed * step;
                _travelBudget -= advance;
                Vector2 next = Vector2.MoveTowards(_hookPosition, anchor, advance);
                if (FirstOnRay(_hookPosition, next, _shotBlockingLayers, out var obstacle) && obstacle.collider != _target)
                {
                    _target = obstacle.collider;
                    _localAnchor = _target.transform.InverseTransformPoint(obstacle.point);
                    anchor = obstacle.point;
                    next = anchor;
                }

                _hookPosition = next;
                if (Vector2.Distance(_hookPosition, anchor) < 0.02f)
                {
                    if (Vector2.Distance(Motor.Body.position, anchor) > _shotRange + 0.05f)
                    {
                        Release();
                        return;
                    }

                    Attach();
                }
                else if (_travelBudget <= 0f || Vector2.Distance(_shotOrigin, anchor) > _shotRange + 0.1f)
                {
                    Release();
                }
            }
            else
            {
                _joint.connectedAnchor = anchor;
            }
        }

        public bool TryFire(Vector2 aim)
        {
            if (Motor && Motor.Settings)
            {
                Settings = Motor.Settings;
            }

            if (!Settings || !CanInitiateTether)
            {
                return false;
            }

            if (!TrySelectTarget(aim, out var selected, out var point))
            {
                return false;
            }

            _target = selected;
            _localAnchor = selected.transform.InverseTransformPoint(point);
            _shotOrigin = _hookPosition = Motor.Body.position;
            _shotRange = Mathf.Max(0.1f, Settings.HookRange);
            _shotTravelSpeed = Mathf.Max(0.1f, Settings.HookTravelSpeed);
            _shotMinimumRopeLength = Mathf.Max(0.01f, Settings.MinimumRopeLength);
            _shotBlockingLayers = Settings.HookBlockingLayers;
            _travelBudget = _shotRange;
            State = HookState.Flying;
            return true;
        }

        public bool CanAimAt(Vector2 aim, Collider2D expectedTarget)
        {
            if (Motor && Motor.Settings)
            {
                Settings = Motor.Settings;
            }

            return Settings && CanInitiateTether && expectedTarget && TrySelectTarget(aim, out var selected, out _) && selected == expectedTarget;
        }

        private bool TrySelectTarget(Vector2 aim, out Collider2D selected, out Vector2 point)
        {
            selected = null;
            point = aim;
            Vector2 origin = Motor.Body.position;
            Vector2 delta = aim - origin;
            if (delta.sqrMagnitude < 0.001f)
            {
                return false;
            }

            Vector2 end = origin + delta.normalized * Mathf.Min(delta.magnitude, Range);
            point = end;
            float best = float.PositiveInfinity;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Settings.HookableLayers,
                useTriggers = true
            };
            int count = Physics2D.OverlapCircle(aim, Settings.AimAssistRadius, filter, _candidates);
            for (int i = 0; i < count; i++)
            {
                Collider2D candidate = _candidates[i];
                if (!candidate || candidate.attachedRigidbody == Motor.Body)
                {
                    continue;
                }

                Vector2 candidatePoint = candidate.ClosestPoint(aim);
                if (Vector2.Distance(origin, candidatePoint) > Range)
                {
                    continue;
                }

                if (FirstOnRay(origin, candidatePoint, Settings.HookBlockingLayers, out var wall) && wall.collider != candidate && wall.distance < Vector2.Distance(origin, candidatePoint) - 0.03f)
                {
                    continue;
                }

                float score = (candidatePoint - aim).sqrMagnitude;
                if (score < best)
                {
                    best = score;
                    selected = candidate;
                    point = candidatePoint;
                }
            }

            if (!selected && FirstOnRay(origin, end, Settings.HookableLayers, out var hit))
            {
                selected = hit.collider;
                point = hit.point;
            }

            if (!selected)
            {
                return false;
            }

            if (FirstOnRay(origin, point, Settings.HookBlockingLayers, out var blocker) && blocker.collider != selected && blocker.distance < Vector2.Distance(origin, point) - 0.03f)
            {
                selected = blocker.collider;
                point = blocker.point;
            }

            return true;
        }

        private bool FirstOnRay(Vector2 from, Vector2 to, LayerMask mask, out RaycastHit2D first)
        {
            Vector2 delta = to - from;
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = mask,
                useTriggers = true
            };
            int count = Physics2D.Raycast(from, delta.normalized, filter, _rayHits, delta.magnitude + 0.01f);
            for (int i = 0; i < count; i++)
            {
                if (_rayHits[i].collider && _rayHits[i].collider.attachedRigidbody != Motor.Body)
                {
                    first = _rayHits[i];
                    return true;
                }
            }

            first = default;
            return false;
        }

        private void Attach()
        {
            _joint.connectedBody = null;
            _joint.connectedAnchor = AnchorPoint;
            _joint.distance = Mathf.Max(_shotMinimumRopeLength, Vector2.Distance(Motor.Body.position, AnchorPoint));
            _joint.enabled = true;
            State = HookState.Attached;
            OnAttached.Invoke();
        }

        public void Release()
        {
            bool wasActive = State != HookState.Idle;
            if (_joint)
            {
                _joint.enabled = false;
            }

            State = HookState.Idle;
            _target = null;
            if (_rope)
            {
                _rope.enabled = false;
            }

            if (wasActive)
            {
                OnReleased.Invoke();
            }
        }

        private void LateUpdate()
        {
            if (!_rope)
            {
                return;
            }

            _rope.enabled = State != HookState.Idle;
            if (!_rope.enabled)
            {
                return;
            }

            _rope.SetPosition(0, transform.position);
            _rope.SetPosition(1, State == HookState.Attached ? AnchorPoint : _hookPosition);
        }

        private void OnDisable()
        {
            Release();
        }
    }
}

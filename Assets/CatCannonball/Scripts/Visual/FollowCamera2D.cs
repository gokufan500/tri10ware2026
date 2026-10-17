using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class FollowCamera2D : MonoBehaviour
    {
        [Header("Scene references")]
        public Transform Target;
        public CameraSettings Settings;
        public CameraBounds2D Bounds;
        private Camera _view;
        private float _velocityX;
        private float _velocityY;
        private bool _initialized;
        private float _tutorialSettleUntil;
        private Vector2 _impactOffset;
        private float _impactHoldUntil;
        private float _impactElapsed;
        private float _impactDuration;
        private float _impactAmplitude;
        private float _impactFrequency;
        private AnimationCurve _impactDecay;
        public bool IsTutorialSettling => Time.unscaledTime < _tutorialSettleUntil;
        public bool IsImpactHolding => Time.unscaledTime < _impactHoldUntil;
        public bool IsImpactShaking => _impactDuration > 0f && _impactElapsed < _impactDuration;
        public Vector2 ImpactOffset => _impactOffset;

        private void Awake() => EnsureCamera();
        private void Start() => SnapToTarget();
        private void LateUpdate()
        {
            RemoveImpactOffset();
            Tick(IsTutorialSettling ? Time.unscaledDeltaTime : Time.deltaTime);
            ApplyImpactOffset(Time.deltaTime);
        }

        public void BeginTutorialSettle(float realSeconds)
        {
            _tutorialSettleUntil = Time.unscaledTime + Mathf.Max(0f, realSeconds);
        }

        public void CancelTutorialSettle() => _tutorialSettleUntil = 0f;
        private void OnDisable()
        {
            CancelTutorialSettle();
            CancelImpact();
        }
                public void PlayImpact(float amplitude, float duration, float frequency, float holdSeconds = 0f, AnimationCurve decay = null)
        {
            RemoveImpactOffset();
            _impactAmplitude = Mathf.Max(IsImpactShaking ? _impactAmplitude : 0f, Mathf.Max(0f, amplitude));
            _impactDuration = Mathf.Max(IsImpactShaking ? _impactDuration - _impactElapsed : 0f, Mathf.Max(0f, duration));
            _impactElapsed = 0f;
            _impactFrequency = Mathf.Max(0f, frequency);
            _impactDecay = decay;
            _impactHoldUntil = Mathf.Max(_impactHoldUntil, Time.unscaledTime + Mathf.Max(0f, holdSeconds));
        }

        public void CancelImpact()
        {
            RemoveImpactOffset();
            _impactHoldUntil = _impactElapsed = _impactDuration = _impactAmplitude = 0f;
        }

        private void RemoveImpactOffset()
        {
            if (_impactOffset == Vector2.zero)
            {
                return;
            }

            transform.position -= (Vector3)_impactOffset;
            _impactOffset = Vector2.zero;
        }

        private void ApplyImpactOffset(float deltaTime)
        {
            if (!IsImpactShaking || IsImpactHolding || deltaTime <= 0f)
            {
                return;
            }

            _impactElapsed = Mathf.Min(_impactDuration, _impactElapsed + deltaTime);
            float progress = _impactDuration > 0f ? _impactElapsed / _impactDuration : 1f;
            float envelope = Mathf.Clamp01(_impactDecay == null ? 1f - progress : _impactDecay.Evaluate(progress));
            float phase = _impactElapsed * _impactFrequency * Mathf.PI * 2f;
            Vector2 noise = new Vector2(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f + 0.7f));
            _impactOffset = Vector2.ClampMagnitude(noise, 1f) * _impactAmplitude * envelope;
            transform.position += (Vector3)_impactOffset;
        }

        private void EnsureCamera()
        {
            if (!_view)
            {
                _view = GetComponent<Camera>();
            }

            if (_view && Settings)
            {
                _view.orthographic = true;
                _view.orthographicSize = Mathf.Max(0.1f, Settings.OrthographicSize);
            }
        }

        public void SnapToTarget()
        {
            CancelTutorialSettle();
            CancelImpact();
            EnsureCamera();
            _velocityX = _velocityY = 0f;
            if (!Target || !Settings || !_view)
            {
                _initialized = false;
                return;
            }

            Vector2 desired = (Vector2)Target.position + Settings.Offset;
            desired = KeepPlayerVisible(desired, Target.position);
            if (Bounds)
            {
                desired = Bounds.ClampCenter(desired, ViewportHalfSize);
            }

            SetPosition(desired);
            _initialized = true;
        }

        public void Tick(float deltaTime)
        {
            RemoveImpactOffset();
            EnsureCamera();
            if (!Target || !Settings || !_view)
            {
                return;
            }

            if (!_initialized)
            {
                SnapToTarget();
                return;
            }

            if (deltaTime <= 0f || IsImpactHolding)
            {
                return;
            }

            Vector2 playerPosition = Target.position;
            Vector2 desired = playerPosition + Settings.Offset;
            if (Bounds)
            {
                desired = Bounds.ClampCenter(desired, ViewportHalfSize);
            }

            Vector2 current = transform.position;
            Vector2 next = new Vector2(Mathf.SmoothDamp(current.x, desired.x, ref _velocityX, Mathf.Max(0.001f, Settings.SmoothTimeX), Mathf.Infinity, deltaTime), Mathf.SmoothDamp(current.y, desired.y, ref _velocityY, Mathf.Max(0.001f, Settings.SmoothTimeY), Mathf.Infinity, deltaTime));
            Vector2 lag = EffectiveMaxLag;
            next.x = CatchUp(next.x, desired.x, lag.x, deltaTime);
            next.y = CatchUp(next.y, desired.y, lag.y, deltaTime);
            Vector2 safe = KeepPlayerVisible(next, playerPosition);
            if (!Mathf.Approximately(safe.x, next.x))
            {
                _velocityX = 0f;
            }

            if (!Mathf.Approximately(safe.y, next.y))
            {
                _velocityY = 0f;
            }

            next = safe;
            if (Bounds)
            {
                Vector2 limited = Bounds.ClampCenter(next, ViewportHalfSize);
                if (!Mathf.Approximately(limited.x, next.x))
                {
                    _velocityX = 0f;
                }

                if (!Mathf.Approximately(limited.y, next.y))
                {
                    _velocityY = 0f;
                }

                next = limited;
            }

            SetPosition(next);
        }

        private float CatchUp(float current, float desired, float maximumLag, float deltaTime)
        {
            float fraction = Mathf.Clamp(Settings.CatchUpStartFraction, 0f, 0.99f);
            float urgency = Mathf.InverseLerp(maximumLag * fraction, maximumLag, Mathf.Abs(desired - current));
            float response = 1f - Mathf.Exp(-Mathf.Max(0f, Settings.CatchUpSharpness) * urgency * deltaTime);
            return Mathf.Lerp(current, desired, response);
        }

        private Vector2 ViewportHalfSize => new Vector2(_view.orthographicSize * Mathf.Max(0.01f, _view.aspect), _view.orthographicSize);

        private Vector2 EffectiveMaxLag
        {
            get
            {
                Vector2 safeHalfSize = ViewportHalfSize * (1f - 2f * Mathf.Clamp(Settings.ViewportMargin, 0f, 0.45f));
                return new Vector2(Mathf.Min(Mathf.Max(0.01f, Settings.MaxLagX), safeHalfSize.x), Mathf.Min(Mathf.Max(0.01f, Settings.MaxLagY), safeHalfSize.y));
            }
        }

        private Vector2 KeepPlayerVisible(Vector2 center, Vector2 playerPosition)
        {
            Vector2 lag = EffectiveMaxLag;
            return new Vector2(Mathf.Clamp(center.x, playerPosition.x - lag.x, playerPosition.x + lag.x), Mathf.Clamp(center.y, playerPosition.y - lag.y, playerPosition.y + lag.y));
        }

        private void SetPosition(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        private void OnDrawGizmosSelected()
        {
            Camera ownCamera = GetComponent<Camera>();
            if (!ownCamera)
            {
                return;
            }

            float halfHeight = Settings ? Mathf.Max(0.1f, Settings.OrthographicSize) : ownCamera.orthographicSize;
            Vector3 framing = new Vector3(halfHeight * ownCamera.aspect * 2f, halfHeight * 2f, 0f);
            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.9f);
            Gizmos.DrawWireCube(transform.position, framing);
            if (Target)
            {
                Gizmos.DrawLine(transform.position, Target.position);
            }
        }
    }
}

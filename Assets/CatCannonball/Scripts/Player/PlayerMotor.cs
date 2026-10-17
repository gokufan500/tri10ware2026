using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(PlayerInputReader))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField]
        private MovementSettings _settings;
        public PlayerInputReader Input;
        [SerializeField]
        private GrappleController _grapple;
        [Header("Designer events")]
        public UnityEvent OnJump = new UnityEvent();
        public UnityEvent OnYank = new UnityEvent();
        [Tooltip("Event that breaks 'flow'")]
        public UnityEvent OnImpact = new UnityEvent();
        private float _groundedAt = -100f;
        private float _jumpRequestedAt = -100f;
        private bool _suppressFastFallUntilRelease;
        private bool _flowBreakAppliedThisStep;
        private Vector2 _previousPosition;
        private LevelFlow _levelFlow;
        private readonly HashSet<Collider2D> _supportingContacts = new HashSet<Collider2D>();
        private readonly HashSet<Collider2D> _contactEpisodes = new HashSet<Collider2D>();
        private readonly RaycastHit2D[] _probeResults = new RaycastHit2D[32];
        private readonly RaycastHit2D[] _sweptTriggers = new RaycastHit2D[64];
        public Rigidbody2D Body
        {
            get;
            private set;
        }
        public Collider2D Collider
        {
            get;
            private set;
        }
        public GrappleController Grapple
        {
            get => _grapple;
            set => _grapple = value;
        }
        public MovementSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }
        public Vector2 PreStepVelocity
        {
            get;
            private set;
        }

        public bool IsGrounded
        {
            get;
            private set;
        }
        public bool IsSupported
        {
            get;
            private set;
        }
        public bool IsBesideWall
        {
            get;
            private set;
        }
        public float LastImpactAngle
        {
            get;
            private set;
        }
        public string LastReleaseAction
        {
            get;
            private set;
        } = "None";
        public string LastYankMode
        {
            get;
            private set;
        } = "None";
        public string LastFlowBreakReason
        {
            get;
            private set;
        } = "None";

        private void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Collider = GetComponent<Collider2D>();
            if (!Input)
            {
                Input = GetComponent<PlayerInputReader>();
            }

            if (!_grapple)
            {
                _grapple = GetComponent<GrappleController>();
            }

            if (!_settings)
            {
                Debug.LogError("Player needs a Movement Settings asset. Assign it in the Inspector.", this);
                enabled = false;
                return;
            }

            Body.freezeRotation = true;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Body.linearDamping = Body.angularDamping = 0f;
            Body.mass = Mathf.Max(0.1f, _settings.Mass);
            _previousPosition = Body.position;
        }

        private void OnValidate()
        {
            if (!Input)
            {
                Input = GetComponent<PlayerInputReader>();
            }

            if (!_grapple)
            {
                _grapple = GetComponent<GrappleController>();
            }

            if (!_grapple)
            {
                return;
            }

            _grapple.Motor = this;
            _grapple.Input = Input;
            _grapple.Settings = _settings; // Mirror only; the motor owns the profile.
        }

        private void FixedUpdate()
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            InputFrame controls = Input.Consume();
            if (controls.RestartPressed)
            {
                if (!_levelFlow || _levelFlow.Player != this || _levelFlow.gameObject.scene != gameObject.scene)
                {
                    foreach (LevelFlow candidate in FindObjectsByType<LevelFlow>())
                    {
                        if (candidate.Player == this && candidate.gameObject.scene == gameObject.scene)
                        {
                            _levelFlow = candidate;
                            break;
                        }
                    }
                }

                if (_levelFlow)
                {
                    _levelFlow.Respawn();
                }

                return;
            }

            Step(controls, Time.fixedDeltaTime);
        }

        public void Step(InputFrame controls, float step)
        {
            if (!_settings || !Body)
            {
                return;
            }

            _flowBreakAppliedThisStep = false;
            if (SweepInteractions())
            {
                return;
            }

            _previousPosition = Body.position;
            step = Mathf.Max(0.0001f, step);
            Body.mass = Mathf.Max(0.1f, _settings.Mass);
            PreStepVelocity = Body.linearVelocity;
            RefreshSupport();
            if (controls.FastFallReleased || !controls.FastFall)
            {
                _suppressFastFallUntilRelease = false;
            }

            bool attachedAtInput = _grapple && _grapple.IsAttached;
            bool spaceRelease = attachedAtInput && controls.JumpPressed;
            bool yankRelease = attachedAtInput && controls.DropPressed && !spaceRelease;
            bool abilityReleased = spaceRelease || yankRelease;
            if (controls.JumpPressed)
            {
                _jumpRequestedAt = Time.time;
            }

            if (spaceRelease)
            {
                Body.linearVelocity = MovementMath.SpaceReleaseVelocity(Body.linearVelocity, _settings);
                _grapple.Release();
                LastReleaseAction = "Space release";
                _jumpRequestedAt = _groundedAt = -100f;
                OnJump.Invoke();
            }
            else if (yankRelease)
            {
                Vector2 towardPivot = _grapple.AnchorPoint - Body.position;
                Body.linearVelocity = MovementMath.YankVelocity(Body.linearVelocity, towardPivot, _settings, out bool lowSpeedYank);
                _grapple.Release();
                LastYankMode = lowSpeedYank ? "Low-speed yank" : "High-speed redirect";
                LastReleaseAction = LastYankMode;
                _jumpRequestedAt = _groundedAt = -100f;
                OnYank.Invoke();
            }

            if (abilityReleased && controls.DropPressed)
            {
                _suppressFastFallUntilRelease = true;
            }

            InputFrame hookControls = controls;
            if (abilityReleased)
            {
                hookControls.HookPressed = false;
                hookControls.HookHeld = false;
                hookControls.HookReleased = false;
            }

            if (_grapple)
            {
                if (!abilityReleased && attachedAtInput && (controls.HookReleased || !controls.HookHeld))
                {
                    LastReleaseAction = "Ordinary release";
                }

                _grapple.Step(hookControls, step);
            }

            bool attached = _grapple && _grapple.IsAttached;
            bool unhookedDrop = controls.DropPressed && !abilityReleased && !controls.JumpPressed;
            foreach (OneWayPlatform platform in OneWayPlatform.Active)
            {
                platform.UpdatePassThrough(Collider, attached, unhookedDrop);
            }

            IsGrounded = Probe(Vector2.down, true);
            IsBesideWall = Probe(Vector2.left, false) || Probe(Vector2.right, false);
            if (IsSupported && !abilityReleased && Body.linearVelocity.y <= _settings.GroundedUpwardSpeed)
            {
                _groundedAt = Time.time;
            }

            if (!abilityReleased && !attached && !unhookedDrop && Time.time - _jumpRequestedAt <= _settings.JumpBuffer && Time.time - _groundedAt <= _settings.CoyoteTime)
            {
                Body.AddForce(Vector2.up * _settings.JumpImpulse, ForceMode2D.Impulse);
                _jumpRequestedAt = _groundedAt = -100f;
                IsGrounded = false;
                OnJump.Invoke();
            }

            bool fastFall = controls.FastFall && !_suppressFastFallUntilRelease && !attached;
            Body.gravityScale = _settings.GravityScale * (fastFall ? _settings.FastFallMultiplier : 1f);
            float drive = IsGrounded && !attached ? _settings.GroundForce : _settings.AirForce;
            Body.AddForce(Vector2.right * (Mathf.Clamp(controls.Move, -1f, 1f) * drive));
            Body.AddForce(MovementMath.DragForce(Body.linearVelocity, _settings.AirDrag, Body.mass, step));
            if (IsGrounded && Mathf.Abs(Body.linearVelocity.x) > 0.001f)
            {
                float friction = Mathf.Min(_settings.GroundFriction, Mathf.Abs(Body.linearVelocity.x) * Body.mass / step);
                Body.AddForce(Vector2.left * (Mathf.Sign(Body.linearVelocity.x) * friction));
            }

            if (controls.WallRun && IsBesideWall)
            {
                if (Body.linearVelocity.y >= 0f)
                {
                    Body.AddForce(Vector2.up * _settings.WallRunForce);
                }
                else
                {
                    float gravityForce = Mathf.Abs(Physics2D.gravity.y * Body.gravityScale * Body.mass);
                    Body.AddForce(Vector2.up * Mathf.Min(_settings.WallSlideFriction, gravityForce * Mathf.Clamp01(_settings.WallSlideGravityLimit)));
                }
            }

            PreStepVelocity = Body.linearVelocity;
        }

        public bool IsGroundedForTether()
        {
            if (!_settings || !Body || !Collider)
            {
                return true;
            }

            if (Body.linearVelocity.y > _settings.GroundedUpwardSpeed)
            {
                return false;
            }

            return Probe(Vector2.down, true);
        }

        private bool Probe(Vector2 direction, bool ground)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = _settings.MovementLayers,
                useTriggers = false
            };
            float distance = ground ? _settings.GroundProbeDistance : _settings.WallProbeDistance;
            int count = Collider.Cast(direction, filter, _probeResults, Mathf.Max(0f, distance));
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _probeResults[i];
                if (!hit.collider || Physics2D.GetIgnoreCollision(Collider, hit.collider))
                {
                    continue;
                }

                if (ground && hit.collider.gameObject.layer == 10 && (Body.linearVelocity.y > _settings.GroundedUpwardSpeed || Collider.bounds.min.y < hit.collider.bounds.max.y - _settings.PlatformSurfaceTolerance))
                {
                    continue;
                }

                if (ground && hit.normal.y >= _settings.SupportNormalY)
                {
                    return true;
                }

                if (!ground && Mathf.Abs(hit.normal.x) >= _settings.WallNormalX && hit.collider.gameObject.layer != 10)
                {
                    return true;
                }
            }

            return false;
        }

        private bool SweepInteractions()
        {
            Vector2 travel = Body.position - _previousPosition;
            if (travel.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            float radius = ((CircleCollider2D)Collider).radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = (1 << 11) | (1 << 13),
                useTriggers = true
            };
            int count = Physics2D.CircleCast(_previousPosition, radius, travel.normalized, filter, _sweptTriggers, travel.magnitude);
            Vector2 arrival = Body.position;
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _sweptTriggers[i].collider;
                if (!hit || !hit.isTrigger)
                {
                    continue;
                }

                if (hit.bounds.Intersects(Collider.bounds))
                {
                    continue;
                }

                if (((Vector2)hit.ClosestPoint(_previousPosition) - _previousPosition).sqrMagnitude <= radius * radius)
                {
                    continue;
                }

                if (hit.TryGetComponent<SpeedTarget>(out var target))
                {
                    target.TryHit(this);
                }

                if (hit.TryGetComponent<DeathZone>(out var danger))
                {
                    danger.TryEnter(this);
                }

                if ((Body.position - arrival).sqrMagnitude > 0.001f)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshSupport()
        {
            _supportingContacts.RemoveWhere(contact => !contact || !contact.enabled || !contact.gameObject.activeInHierarchy || Physics2D.GetIgnoreCollision(Collider, contact));
            _contactEpisodes.RemoveWhere(contact => !contact || !contact.enabled || !contact.gameObject.activeInHierarchy);
            IsSupported = _supportingContacts.Count > 0;
        }

        private void OnCollisionEnter2D(Collision2D collision) => RecordContact(collision);
        private void OnCollisionStay2D(Collision2D collision) => RecordContact(collision);
        private void OnCollisionExit2D(Collision2D collision)
        {
            _supportingContacts.Remove(collision.collider);
            _contactEpisodes.Remove(collision.collider);
        }

        private void RecordContact(Collision2D collision)
        {
            if (!_settings || !Body || !collision.collider || ((1 << collision.collider.gameObject.layer) & _settings.MovementLayers.value) == 0 || Physics2D.GetIgnoreCollision(Collider, collision.collider))
            {
                return;
            }

            bool newContactEpisode = _contactEpisodes.Add(collision.collider);
            bool supports = false;
            Vector2 inwardNormal = Vector2.zero;
            float inwardSpeed = 0f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector2 normal = collision.GetContact(i).normal;
                if (normal.y >= _settings.SupportNormalY)
                {
                    supports = true;
                }
                else
                {
                    float inward = -Vector2.Dot(PreStepVelocity, normal);
                    if (inward > inwardSpeed)
                    {
                        inwardSpeed = inward;
                        inwardNormal = normal;
                    }
                }
            }

            if (supports)
            {
                _supportingContacts.Add(collision.collider);
                if (!IsSupported)
                {
                    IsSupported = true;
                    BreakFlow("Landing");
                }
            }
            else
            {
                _supportingContacts.Remove(collision.collider);
            }

            if (!newContactEpisode || inwardNormal == Vector2.zero)
            {
                return;
            }

            float speed = PreStepVelocity.magnitude;
            LastImpactAngle = speed > 0.00001f ? Mathf.Asin(Mathf.Clamp01(inwardSpeed / speed)) * Mathf.Rad2Deg : 0f;
            if (inwardSpeed >= _settings.MinimumImpactSpeed)
            {
                BreakFlow("Hard impact");
            }
            else if (inwardSpeed >= _settings.MinimumBadImpactSpeed && LastImpactAngle >= _settings.BadImpactAngle)
            {
                BreakFlow("Bad-angle impact");
            }
        }

        private void BreakFlow(string reason)
        {
            if (_flowBreakAppliedThisStep)
            {
                return;
            }

            _flowBreakAppliedThisStep = true;
            Body.linearVelocity = Vector2.zero;
            LastFlowBreakReason = reason;
            OnImpact.Invoke();
        }

        public void ResetAt(Vector2 position)
        {
            if (_grapple)
            {
                _grapple.Release();
            }

            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.position = position;
            transform.position = position;
            Input.Clear();
            _groundedAt = _jumpRequestedAt = -100f;
            PreStepVelocity = Vector2.zero;
            _previousPosition = position;
            _suppressFastFallUntilRelease = false;
            _flowBreakAppliedThisStep = false;
            _supportingContacts.Clear();
            _contactEpisodes.Clear();
            IsGrounded = IsSupported = IsBesideWall = false;
            LastReleaseAction = LastYankMode = LastFlowBreakReason = "None";
        }
    }
}

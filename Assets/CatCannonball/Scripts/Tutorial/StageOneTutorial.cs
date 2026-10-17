using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary> One continuous sequence / journey. Staged inputs meaning the same thing happens every time </summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent, RequireComponent(typeof(TutorialPromptView))]
    public sealed class StageOneTutorial : MonoBehaviour
    {
        [Header("Scene references")]
        public PlayerMotor Player;
        public SceneTransition SceneTransition;
        public FollowCamera2D FollowCamera;
        public TutorialPromptView PromptView;
        public TutorialSequence Sequence;
        [Min(0)]
        public int TitleSceneIndex;
        public bool AutoStart = true;
        [Header("Repeatable guided launch")]
        public bool NormalizeJumpHorizontalSpeed = true;
        [Min(0f)]
        public float GuidedJumpHorizontalSpeed = 14f;
        [Header("Five ordered invisible gates (left to right)")]
        public Transform Gate1;
        public Transform Gate2;
        public Transform Gate3;
        public Transform Gate4;
        public Transform Gate5;
        [Min(0.1f)]
        public float GateVerticalTolerance = 100f;
        [Header("The two intended hook targets")]
        public Collider2D FirstAnchor;
        public Collider2D SecondAnchor;
        [Min(0.01f)]
        public float HookAimTolerance = 0.5f;
        [Min(0f)]
        public float ApexVelocityTolerance = 0.05f;
        [Header("Pause and response feel (real seconds)")]
        [Min(0f)]
        public float PauseEaseInSeconds = 0.15f;
        public AnimationCurve PauseEaseCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [Range(0f, 0.05f)]
        public float ResumeSeconds;
        [Min(0f)]
        public float PromptFadeSeconds = 0.10f;
        [Min(0f)]
        public float MinimumPromptPresentationSeconds = 0.10f;
        [Min(0f)]
        public float InputBufferSeconds = 0.12f;
        [Min(0f)]
        public float CameraSettleSeconds = 0.15f;
        [Range(0.65f, 1.5f)]
        public float InstructionScale = 1f;
        [Header("Real-time ending and retry delays")]
        [Min(0f)]
        public float FailDelay = 2f;
        [Min(0f)]
        public float CompletionDelay = 1.2f;
        [Min(1f)]
        public float TravelTimeout = 15f;
        [Header("Editable focused instructions")]
        [TextArea]
        public string WalkPrompt = "Move with A / D";
        [TextArea]
        public string JumpPrompt = "Press SPACE to jump";
        [TextArea]
        public string FirstHookPrompt = "Hold LEFT CLICK on the highlighted anchor";
        [TextArea]
        public string FirstSwingPrompt = "Keep LEFT CLICK held. Feel the swing.";
        [TextArea]
        public string DrivePrompt = "Hold D to swing right";
        [TextArea]
        public string DriveSwingPrompt = "Hold D and LEFT CLICK to drive the swing.";
        [TextArea]
        public string ReleasePrompt = "Release LEFT CLICK";
        [TextArea]
        public string CoastPrompt = "Feel your momentum carry you forward.";
        [TextArea]
        public string SecondHookPrompt = "Hold LEFT CLICK on the highlighted anchor";
        [TextArea]
        public string SecondSwingPrompt = "Keep LEFT CLICK held. Follow the swing.";
        [TextArea]
        public string SpacePrompt = "Press SPACE to release";
        [TextArea]
        public string HookSupport = "Keep holding until prompted to release.";
        [TextArea]
        public string HeldMouseSupport = "Keep LEFT CLICK held.";
        [TextArea]
        public string CompletionMessage = "";
        [TextArea]
        public string FailureMessage = "Keep LEFT CLICK held until prompted to release.";
        [TextArea]
        public string InterruptedRouteMessage = "Let's try that again.";
        [TextArea]
        public string FailureSupport = "Restarting\u2026";
        private PlayerInputReader _controls;
        private bool _sessionOwned;
        private bool _previousInputLock;
        private bool _holdRequired;
        private bool _jumpObserved;
        private bool _completionObserved;
        private bool _returnRequested;
        private bool _easingIntoPause;
        private bool _resuming;
        private bool _hasBufferedInput;
        private bool _walkInstructionDismissed;
        private float _previousTimeScale;
        private float _resumeTimeScale;
        private float _previousFixedDeltaTime;
        private float _terminalStarted;
        private float _travelStarted;
        private float _promptStarted;
        private float _pauseStarted;
        private float _pauseElapsed;
        private float _pauseDuration;
        private float _resumeStarted;
        private float _bufferedUntil;
        private Coroutine _physicsObserver;
        private int _failedPhaseNumber;
        private Phase _approachFrom;
        private InputFrame _latestRaw;
        private InputFrame _bufferedInput;
        private Vector2 _previousPosition;
        private Vector2 _previousVelocity;
        public enum Phase
        {
            Walk,
            JumpPrompt,
            Jumping,
            FirstHookPrompt,
            FirstHookFlight,
            FirstSwing,
            DPrompt,
            DriveSwing,
            ReleasePrompt,
            Coast,
            SecondHookPrompt,
            SecondHookFlight,
            SecondSwing,
            SpacePrompt,
            Finishing,
            Failed
        }

        public Phase State
        {
            get;
            private set;
        } = Phase.Walk;
        public string Status
        {
            get;
            private set;
        } = "";
        public bool WaitingForInput => IsPrompt(State);
        public bool HasFailed => State == Phase.Failed;
        public bool IsComplete => State == Phase.Finishing;
        public bool IsEasingIntoPause => _easingIntoPause;
        public bool IsFullyPaused => WaitingForInput && !_easingIntoPause && Time.timeScale <= 0f;
        public bool PromptVisible => WaitingForInput || HasFailed;
        public float PromptAlpha => HasFailed ? 1f : !WaitingForInput ? 0f : PromptFadeSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _promptStarted) / PromptFadeSeconds));
        public float CurrentPauseElapsed => _pauseElapsed;
        public float CurrentPauseDuration => _pauseDuration;
        public bool InputBuffered => _hasBufferedInput;
        public bool RequiresHookHold => _holdRequired;
        public int ActionAcceptedCount
        {
            get;
            private set;
        }
        public int PhaseNumber => State == Phase.Walk ? 1 : State == Phase.JumpPrompt || State == Phase.Jumping ? 2 : State == Phase.FirstHookPrompt || State == Phase.FirstHookFlight || State == Phase.FirstSwing ? 3 : State == Phase.DPrompt || State == Phase.DriveSwing ? 4 : State == Phase.ReleasePrompt || State == Phase.Coast ? 5 : State == Phase.SecondHookPrompt || State == Phase.SecondHookFlight || State == Phase.SecondSwing ? 6 : State == Phase.Failed ? _failedPhaseNumber : 7;

        private float MinimumPromptPresentation => Mathf.Max(Mathf.Max(0f, MinimumPromptPresentationSeconds), Mathf.Max(0f, PromptFadeSeconds));
        private bool PromptReady => IsFullyPaused && Time.unscaledTime - _promptStarted >= MinimumPromptPresentation;

        private void Start()
        {
            if (AutoStart)
            {
                BeginTutorial();
            }
        }

        public void BeginTutorial()
        {
            if (_sessionOwned)
            {
                RestoreOwnedState();
            }

            if (!Player)
            {
                Player = FindAnyObjectByType<PlayerMotor>();
            }

            if (!SceneTransition)
            {
                SceneTransition = FindAnyObjectByType<SceneTransition>();
            }

            if (!FollowCamera)
            {
                FollowCamera = FindAnyObjectByType<FollowCamera2D>();
            }

            if (!PromptView)
            {
                PromptView = GetComponent<TutorialPromptView>();
            }

            if (!PromptView)
            {
                PromptView = gameObject.AddComponent<TutorialPromptView>();
            }

            if (!Player || !Player.Input || !Player.Body || !Player.Grapple || !Player.Settings || !Gate1 || !Gate2 || !Gate3 || !Gate4 || !Gate5 || !FirstAnchor || !SecondAnchor)
            {
                Debug.LogError("Stage One Tutorial is missing a connection", this);
                enabled = false;
                return;
            }

            _controls = Player.Input;
            _previousInputLock = _controls.GameplayLocked;
            _previousTimeScale = Time.timeScale;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            _resumeTimeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
            _sessionOwned = true;
            _controls.SetGameplayLocked(true);
            _controls.Clear();
            Player.OnJump.AddListener(RecordJump);
            _previousPosition = Player.Body.position;
            _previousVelocity = Player.Body.linearVelocity;
            _holdRequired = _jumpObserved = _completionObserved = _returnRequested = false;
            _easingIntoPause = _resuming = _hasBufferedInput = false;
            _latestRaw = _bufferedInput = default;
            ActionAcceptedCount = 0;
            _walkInstructionDismissed = false;
            EnterTravel(Phase.Walk, WalkPrompt, false);
            _physicsObserver = StartCoroutine(ObservePhysics());
        }

        private IEnumerator ObservePhysics()
        {
            var afterPhysics = new WaitForFixedUpdate();
            while (_sessionOwned)
            {
                yield return afterPhysics;
                if (Time.timeScale > 0f && !(SceneTransition && SceneTransition.IsTransitioning))
                {
                    EvaluateProgress();
                }
            }
        }

        private void Update()
        {
            if (!_sessionOwned || !Player || !_controls)
            {
                return;
            }

            if (SceneTransition && SceneTransition.IsTransitioning)
            {
                return;
            }

            UpdateSimulationTiming();
            if (_controls.ReadHardware)
            {
                SubmitInput(_controls.RawSnapshot);
            }
            else if (WaitingForInput)
            {
                TryAcceptBufferedInput();
            }

            if (!WaitingForInput && !HasFailed && !IsComplete && State != Phase.Walk && Time.time - _travelStarted > TravelTimeout)
            {
                Fail(InterruptedRouteMessage);
            }

            if (IsComplete && !_completionObserved && Time.time - _travelStarted > TravelTimeout)
            {
                Fail(InterruptedRouteMessage);
            }

            UpdateTerminalState();
        }
                public bool SubmitInput(InputFrame raw)
        {
            if (!_sessionOwned || HasFailed || (SceneTransition && SceneTransition.LastRequestedSceneIndex >= 0))
            {
                return false;
            }

            _latestRaw = raw;
            if (State == Phase.Walk && Mathf.Abs(raw.Move) > 0.01f)
            {
                _walkInstructionDismissed = true;
            }

            if (IsComplete)
            {
                _controls.Inject(default);
                return false;
            }

            bool requestedFinalRelease = State == Phase.SpacePrompt && (raw.JumpPressed || (_hasBufferedInput && _bufferedInput.JumpPressed));
            bool bufferedHookHold = _hasBufferedInput && _bufferedInput.HookPressed;
            if ((_holdRequired || bufferedHookHold) && State != Phase.ReleasePrompt && !requestedFinalRelease && (raw.HookReleased || !raw.HookHeld))
            {
                Fail(FailureMessage);
                return false;
            }

            if (!WaitingForInput)
            {
                _controls.Inject(FilterTravelInput(State, raw));
                return false;
            }

            if (IsValidPromptInput(raw))
            {
                BufferInput(raw);
            }

            if (TryAcceptBufferedInput())
            {
                return true;
            }

            _controls.Inject(_easingIntoPause ? FilterApproachInput(raw) : new InputFrame { HookHeld = _holdRequired && raw.HookHeld, AimWorld = raw.AimWorld });
            return false;
        }

        private InputFrame FilterTravelInput(Phase phase, InputFrame raw)
        {
            var permitted = new InputFrame
            {
                AimWorld = raw.AimWorld
            };
            if (phase == Phase.Walk)
            {
                permitted.Move = raw.Move;
            }

            if (phase == Phase.FirstHookFlight || phase == Phase.FirstSwing || phase == Phase.SecondHookFlight || phase == Phase.SecondSwing || phase == Phase.DriveSwing)
            {
                permitted.HookHeld = raw.HookHeld;
            }

            if (phase == Phase.DriveSwing)
            {
                permitted.Move = raw.Move > 0f ? 1f : 0f;
            }

            return permitted;
        }

        private InputFrame FilterApproachInput(InputFrame raw)
        {
            InputFrame permitted = FilterTravelInput(_approachFrom, raw);
            if (_holdRequired)
            {
                permitted.HookHeld = true;
            }

            return permitted;
        }

        private bool IsValidPromptInput(InputFrame raw)
        {
            switch (State)
            {
                case Phase.JumpPrompt:
                    return raw.JumpPressed && Player.IsSupported;
                case Phase.FirstHookPrompt:
                    return raw.HookPressed && raw.HookHeld && IsCorrectAim(raw.AimWorld, FirstAnchor);
                case Phase.SecondHookPrompt:
                    return raw.HookPressed && raw.HookHeld && IsCorrectAim(raw.AimWorld, SecondAnchor);
                case Phase.DPrompt:
                    return raw.Move > 0f && raw.HookHeld;
                case Phase.ReleasePrompt:
                    return raw.HookReleased || !raw.HookHeld;
                case Phase.SpacePrompt:
                    return raw.JumpPressed && Player.Grapple.IsAttached;
                default:
                    return false;
            }
        }

        private void BufferInput(InputFrame raw)
        {
            _bufferedInput = raw;
            _hasBufferedInput = true;
            float readyAt = Mathf.Max(_pauseStarted + _pauseDuration, _promptStarted + MinimumPromptPresentation);
            _bufferedUntil = Mathf.Max(Time.unscaledTime, readyAt) + Mathf.Max(0f, InputBufferSeconds);
        }

        private bool TryAcceptBufferedInput()
        {
            if (!WaitingForInput || !_hasBufferedInput || !PromptReady)
            {
                return false;
            }

            if (Time.unscaledTime > _bufferedUntil)
            {
                _hasBufferedInput = false;
                return false;
            }

            InputFrame requested = _bufferedInput;
            requested.HookHeld = _latestRaw.HookHeld;
            if ((requested.HookPressed || State == Phase.DPrompt) && !requested.HookHeld)
            {
                _hasBufferedInput = false;
                return false;
            }

            if (State == Phase.DPrompt && _latestRaw.Move <= 0f)
            {
                _hasBufferedInput = false;
                return false;
            }

            var permitted = new InputFrame
            {
                AimWorld = requested.AimWorld
            };
            switch (State)
            {
                case Phase.JumpPrompt:
                    if (!Player.IsSupported)
                    {
                        return false;
                    }

                    permitted.JumpPressed = true;
                    _jumpObserved = false;
                    if (NormalizeJumpHorizontalSpeed)
                    {
                        Player.Body.linearVelocity = new Vector2(GuidedJumpHorizontalSpeed, Player.Body.linearVelocity.y);
                    }

                    EnterTravel(Phase.Jumping, "Follow the jump.", true);
                    break;
                case Phase.FirstHookPrompt:
                case Phase.SecondHookPrompt:
                    bool first = State == Phase.FirstHookPrompt;
                    Collider2D expected = first ? FirstAnchor : SecondAnchor;
                    if (!IsCorrectAim(requested.AimWorld, expected))
                    {
                        _hasBufferedInput = false;
                        return false;
                    }

                    permitted.HookPressed = permitted.HookHeld = true;
                    permitted.AimWorld = expected.bounds.center;
                    _holdRequired = true;
                    EnterTravel(first ? Phase.FirstHookFlight : Phase.SecondHookFlight, first ? FirstSwingPrompt : SecondSwingPrompt, true);
                    break;
                case Phase.DPrompt:
                    permitted.HookHeld = true;
                    permitted.Move = 1f;
                    EnterTravel(Phase.DriveSwing, DriveSwingPrompt, true);
                    break;
                case Phase.ReleasePrompt:
                    permitted.HookReleased = true;
                    _holdRequired = false;
                    EnterTravel(Phase.Coast, CoastPrompt, true);
                    break;
                case Phase.SpacePrompt:
                    if (!Player.Grapple.IsAttached)
                    {
                        return false;
                    }

                    permitted.HookHeld = _latestRaw.HookHeld;
                    permitted.JumpPressed = true;
                    _holdRequired = false;
                    _completionObserved = false;
                    EnterTravel(Phase.Finishing, CompletionMessage, true);
                    break;
                default:
                    return false;
            }

            _controls.Inject(permitted);
            _controls.QueueFrame(permitted);
            ActionAcceptedCount++;
            return true;
        }

        public void EvaluateProgress()
        {
            if (!_sessionOwned || !Player || !Player.Body || WaitingForInput || HasFailed || IsComplete)
            {
                return;
            }

            Vector2 position = Player.Body.position;
            switch (State)
            {
                case Phase.Walk:
                    if (ShouldApproachGate(Gate1, position))
                    {
                        BeginPause(Phase.JumpPrompt, JumpPrompt);
                    }

                    break;
                case Phase.Jumping:
                    float gravity = Mathf.Max(0.01f, -Physics2D.gravity.y * Player.Body.gravityScale);
                    float approachSpeed = gravity * BrakingSimulationSeconds + ApexVelocityTolerance;
                    if (_jumpObserved && !Player.IsSupported && Player.Body.linearVelocity.y <= approachSpeed)
                    {
                        BeginPause(Phase.FirstHookPrompt, FirstHookPrompt);
                    }

                    break;
                case Phase.FirstHookFlight:
                    if (Player.Grapple.IsAttached)
                    {
                        EnterTravel(Phase.FirstSwing, FirstSwingPrompt, false);
                        if (ShouldApproachGate(Gate2, position))
                        {
                            BeginPause(Phase.DPrompt, DrivePrompt);
                        }
                    }

                    break;
                case Phase.FirstSwing:
                    if (ShouldApproachGate(Gate2, position))
                    {
                        BeginPause(Phase.DPrompt, DrivePrompt);
                    }

                    break;
                case Phase.DriveSwing:
                    if (ShouldApproachGate(Gate3, position))
                    {
                        BeginPause(Phase.ReleasePrompt, ReleasePrompt);
                    }

                    break;
                case Phase.Coast:
                    if (ShouldApproachGate(Gate4, position))
                    {
                        BeginPause(Phase.SecondHookPrompt, SecondHookPrompt);
                    }

                    break;
                case Phase.SecondHookFlight:
                    if (Player.Grapple.IsAttached)
                    {
                        EnterTravel(Phase.SecondSwing, SecondSwingPrompt, false);
                        if (ShouldApproachGate(Gate5, position))
                        {
                            BeginPause(Phase.SpacePrompt, SpacePrompt);
                        }
                    }

                    break;
                case Phase.SecondSwing:
                    if (ShouldApproachGate(Gate5, position))
                    {
                        BeginPause(Phase.SpacePrompt, SpacePrompt);
                    }

                    break;
            }

            _previousPosition = position;
            _previousVelocity = Player.Body.linearVelocity;
        }

        private float BrakingSimulationSeconds
        {
            get
            {
                const int Samples = 20;
                float integral = 0f;
                for (int i = 0; i < Samples; i++)
                {
                    integral += PauseCurveAt((i + 0.5f) / Samples);
                }

                return Mathf.Max(0f, PauseEaseInSeconds) * _resumeTimeScale * integral / Samples;
            }
        }

        private bool ShouldApproachGate(Transform gate, Vector2 position)
        {
            if (!gate || Mathf.Abs(position.y - gate.position.y) > GateVerticalTolerance)
            {
                return false;
            }

            if (ReachedGate(gate, _previousPosition, position))
            {
                return true;
            }

            float speed = Player.Body.linearVelocity.x;
            if (speed <= 0f)
            {
                return false;
            }

            float duration = BrakingSimulationSeconds;
            float acceleration = Mathf.Max(0f, (speed - _previousVelocity.x) / Mathf.Max(0.0001f, Time.fixedDeltaTime));
            float distance = speed * duration + 0.5f * acceleration * duration * duration;
            return gate.position.x - position.x <= distance;
        }

        private bool ReachedGate(Transform gate, Vector2 from, Vector2 to)
        {
            if (!gate || to.x < gate.position.x)
            {
                return false;
            }

            float crossingY = to.y;
            if (from.x < gate.position.x && to.x > from.x)
            {
                crossingY = Mathf.Lerp(from.y, to.y, (gate.position.x - from.x) / (to.x - from.x));
            }

            return Mathf.Abs(crossingY - gate.position.y) <= GateVerticalTolerance;
        }

        private bool IsCorrectAim(Vector2 aim, Collider2D expected)
        {
            if (!expected || !expected.enabled || !expected.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (Vector2.Distance(aim, expected.ClosestPoint(aim)) > HookAimTolerance)
            {
                return false;
            }

            return Player.Grapple.CanAimAt(aim, expected) && Player.Grapple.CanAimAt(expected.bounds.center, expected);
        }

        private static bool IsPrompt(Phase phase) => phase == Phase.JumpPrompt || phase == Phase.FirstHookPrompt || phase == Phase.DPrompt || phase == Phase.ReleasePrompt || phase == Phase.SecondHookPrompt || phase == Phase.SpacePrompt;
        private void BeginPause(Phase next, string message)
        {
            _approachFrom = State;
            State = next;
            Status = message;
            _resuming = false;
            _hasBufferedInput = false;
            _promptStarted = _pauseStarted = Time.unscaledTime;
            _pauseDuration = Mathf.Max(0f, PauseEaseInSeconds);
            _pauseElapsed = 0f;
            _easingIntoPause = _pauseDuration > 0f;
            _controls.Clear();
            _controls.Inject(FilterApproachInput(_latestRaw));
            if (FollowCamera)
            {
                FollowCamera.BeginTutorialSettle(Mathf.Min(Mathf.Max(0f, CameraSettleSeconds), _pauseDuration));
            }

            if (!_easingIntoPause)
            {
                ApplySimulationScale(0f);
                if (FollowCamera)
                {
                    FollowCamera.CancelTutorialSettle();
                }
            }
        }

        private void EnterTravel(Phase next, string message, bool resumeFromPause)
        {
            State = next;
            Status = message;
            _travelStarted = Time.time;
            _hasBufferedInput = false;
            _easingIntoPause = false;
            _previousPosition = Player.Body.position;
            _previousVelocity = Player.Body.linearVelocity;
            if (next != Phase.FirstSwing && next != Phase.SecondSwing)
            {
                _controls.Clear();
            }

            if (resumeFromPause)
            {
                if (FollowCamera)
                {
                    FollowCamera.CancelTutorialSettle();
                }

                _resuming = ResumeSeconds > 0f;
                _resumeStarted = Time.unscaledTime;
                ApplySimulationScale(_resuming ? _resumeTimeScale * 0.02f : _resumeTimeScale);
            }
            else if (!_resuming)
            {
                ApplySimulationScale(_resumeTimeScale);
            }
        }

        private float PauseCurveAt(float progress)
        {
            if (progress >= 1f)
            {
                return 0f;
            }

            if (progress <= 0f)
            {
                return 1f;
            }

            return PauseEaseCurve == null || PauseEaseCurve.length == 0 ? 1f - Mathf.SmoothStep(0f, 1f, progress) : Mathf.Clamp01(PauseEaseCurve.Evaluate(progress));
        }

        private void UpdateSimulationTiming()
        {
            if (_easingIntoPause)
            {
                _pauseElapsed = Mathf.Min(_pauseDuration, Time.unscaledTime - _pauseStarted);
                float progress = _pauseDuration > 0f ? _pauseElapsed / _pauseDuration : 1f;
                ApplySimulationScale(_resumeTimeScale * PauseCurveAt(progress));
                if (progress >= 1f)
                {
                    _easingIntoPause = false;
                    ApplySimulationScale(0f);
                    if (FollowCamera)
                    {
                        FollowCamera.CancelTutorialSettle();
                    }
                }
            }
            else if (_resuming)
            {
                float duration = Mathf.Clamp(ResumeSeconds, 0f, 0.05f);
                float progress = duration > 0f ? Mathf.Clamp01((Time.unscaledTime - _resumeStarted) / duration) : 1f;
                ApplySimulationScale(_resumeTimeScale * Mathf.Lerp(0.02f, 1f, Mathf.SmoothStep(0f, 1f, progress)));
                if (progress >= 1f)
                {
                    _resuming = false;
                }
            }
        }

        private void ApplySimulationScale(float scale)
        {
            Time.timeScale = Mathf.Max(0f, scale);
            Time.fixedDeltaTime = scale > 0f ? Mathf.Max(0.0001f, _previousFixedDeltaTime * scale / _resumeTimeScale) : _previousFixedDeltaTime;
        }

        private void RecordJump()
        {
            if (State == Phase.Jumping)
            {
                _jumpObserved = true;
            }

            if (State == Phase.Finishing && Player.LastReleaseAction == "Space release")
            {
                _completionObserved = true;
                _terminalStarted = Time.unscaledTime;
            }
        }

        private void Fail(string message)
        {
            _failedPhaseNumber = PhaseNumber;
            _holdRequired = false;
            State = Phase.Failed;
            Status = message;
            _easingIntoPause = _resuming = _hasBufferedInput = false;
            _controls.Clear();
            ApplySimulationScale(0f);
            if (FollowCamera)
            {
                FollowCamera.CancelTutorialSettle();
            }

            _terminalStarted = Time.unscaledTime;
        }

        private void UpdateTerminalState()
        {
            if (_returnRequested || (!HasFailed && !IsComplete))
            {
                return;
            }

            if (IsComplete && !_completionObserved)
            {
                return;
            }

            float delay = HasFailed ? FailDelay : CompletionDelay;
            if (Time.unscaledTime - _terminalStarted < delay)
            {
                return;
            }

            if (!HasFailed && Sequence)
            {
                _returnRequested = true;
                Time.fixedDeltaTime = _previousFixedDeltaTime;
                Sequence.EnterStageTwo();
                return;
            }

            int sceneIndex = HasFailed ? gameObject.scene.buildIndex : TitleSceneIndex;
            if (!SceneTransition || !SceneTransition.IsValidSceneIndex(sceneIndex))
            {
                Status = "Missing transition or Scene List index";
                return;
            }

            _returnRequested = true;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            SceneTransition.FadeToScene(sceneIndex, HasFailed);
        }

        private void RestoreOwnedState()
        {
            if (_physicsObserver != null)
            {
                StopCoroutine(_physicsObserver);
                _physicsObserver = null;
            }

            bool exiting = SceneTransition && SceneTransition.LastRequestedSceneIndex >= 0;
            if (!exiting)
            {
                Time.timeScale = _previousTimeScale;
            }

            Time.fixedDeltaTime = _previousFixedDeltaTime;
            if (FollowCamera)
            {
                FollowCamera.CancelTutorialSettle();
            }

            if (_controls)
            {
                _controls.Clear();
                if (!exiting)
                {
                    _controls.SetGameplayLocked(_previousInputLock);
                }
            }

            if (Player)
            {
                Player.OnJump.RemoveListener(RecordJump);
            }

            _sessionOwned = _easingIntoPause = _resuming = _hasBufferedInput = false;
        }

        private void OnDisable()
        {
            if (PromptView)
            {
                PromptView.Hide();
            }

            if (_sessionOwned)
            {
                RestoreOwnedState();
            }
        }

        private void LateUpdate()
        {
            if (!PromptView)
            {
                return;
            }

            PromptView.PresentationScale = InstructionScale;
            if (!_sessionOwned || (SceneTransition && SceneTransition.IsTransitioning))
            {
                PromptView.Hide();
                return;
            }

            if (HasFailed)
            {
                PromptView.Present(Status, FailureSupport);
                return;
            }

            if (State == Phase.Walk && !_walkInstructionDismissed)
            {
                PromptView.Present(WalkPrompt, "");
                return;
            }

            if (!WaitingForInput)
            {
                PromptView.Hide();
                return;
            }

            bool hookPrompt = State == Phase.FirstHookPrompt || State == Phase.SecondHookPrompt;
            Collider2D anchor = State == Phase.FirstHookPrompt ? FirstAnchor : State == Phase.SecondHookPrompt ? SecondAnchor : null;
            string support = hookPrompt ? HookSupport : State == Phase.DPrompt || State == Phase.SpacePrompt ? HeldMouseSupport : "";
            Camera camera = _controls && _controls.AimCamera ? _controls.AimCamera : Camera.main;
            PromptView.Present(Status, support, PromptAlpha, anchor, camera);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.8f);
            foreach (Transform gate in new[]
            {
                Gate1,
                Gate2,
                Gate3,
                Gate4,
                Gate5
            })
            {
                if (gate)
                {
                    Gizmos.DrawLine(gate.position + Vector3.up * GateVerticalTolerance, gate.position - Vector3.up * GateVerticalTolerance);
                }
            }

            Gizmos.color = Color.yellow;
            if (FirstAnchor)
            {
                Gizmos.DrawWireSphere(FirstAnchor.bounds.center, HookAimTolerance);
            }

            if (SecondAnchor)
            {
                Gizmos.DrawWireSphere(SecondAnchor.bounds.center, HookAimTolerance);
            }

            if (Gate1 && FirstAnchor)
            {
                Gizmos.DrawLine(Gate1.position, FirstAnchor.bounds.center);
            }

            if (Gate4 && SecondAnchor)
            {
                Gizmos.DrawLine(Gate4.position, SecondAnchor.bounds.center);
            }
        }
    }
}

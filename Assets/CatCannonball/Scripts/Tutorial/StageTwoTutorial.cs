using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary>
    /// Teaches the real low-speed S yank, then hands every movement ability back
    /// to the player for a short practice period. Never moves or teleports the body.
    /// </summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class StageTwoTutorial : MonoBehaviour
    {
        [Header("Scene references")]
        public PlayerMotor Player;
        public SceneTransition SceneTransition;
        public FollowCamera2D FollowCamera;
        public TutorialPromptView PromptView;
        public TutorialSequence Sequence;
        public Collider2D CenterAnchor;
        [Min(0)]
        public int TitleSceneIndex;
        public bool AutoStart = true;
        [Header("Pause and response feel (real seconds)")]
        [Min(0f)]
        public float PauseEaseInSeconds = 0.15f;
        public AnimationCurve PauseEaseCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [Min(0f)]
        public float PromptFadeSeconds = 0.10f;
        [Min(0f)]
        public float InputBufferSeconds = 0.12f;
        [Min(0f)]
        public float MinimumPromptPresentationSeconds = 0.10f;
        [Min(0f)]
        public float CameraSettleSeconds = 0.15f;
        [Min(0f)]
        public float ApexVelocityTolerance = 0.05f;
        [Min(0.01f)]
        public float HookAimTolerance = 0.5f;
        [Header("Practice and retry timing (real seconds)")]
        [Min(0f)]
        public float FreePlaySeconds = 10f;
        [Min(0f)]
        public float FreePlayHintSeconds = 1f;
        [Min(0f)]
        public float FailDelay = 2f;
        [Min(1f)]
        public float TravelTimeout = 15f;
        [Header("Focused instructions")]
        [TextArea]
        public string JumpPrompt = "Press SPACE to jump.";
        [TextArea]
        public string HookPrompt = "Aim at the target. Hold LEFT CLICK.";
        [TextArea]
        public string YankPrompt = "Press S to yank toward the target.";
        [TextArea]
        public string HoldReminder = "Keep LEFT CLICK held.";
        [TextArea]
        public string FreePlayPrompt = "Try it yourself.";
        [TextArea]
        public string FreePlaySupport = "Move, swing, release and yank.";
        [TextArea]
        public string FailureMessage = "Try that again.";
        [TextArea]
        public string FailureSupport = "Keep LEFT CLICK held until you press S.";
        [TextArea]
        public string InterruptedRouteSupport = "Let's try the jump and yank again.";
        private PlayerInputReader _controls;
        private Coroutine _physicsObserver;
        private bool _sessionOwned;
        private bool _previousInputLock;
        private bool _waitingForReveal;
        private bool _easingIntoPause;
        private bool _hasBufferedInput;
        private bool _holdRequired;
        private bool _jumpObserved;
        private bool _returnRequested;
        private float _previousTimeScale;
        private float _resumeTimeScale;
        private float _previousFixedDeltaTime;
        private float _promptStarted;
        private float _pauseStarted;
        private float _pauseElapsed;
        private float _pauseDuration;
        private float _bufferedUntil;
        private float _travelStarted;
        private float _terminalStarted;
        private InputFrame _latestRaw;
        private InputFrame _bufferedInput;
        private string _currentFailureSupport;
        public enum Phase
        {
            GroundJumpPrompt,
            Jumping,
            HookPrompt,
            HookFlight,
            YankPrompt,
            Yanking,
            FreePlay,
            Finishing,
            Failed
        }

        public Phase State
        {
            get;
            private set;
        } = Phase.GroundJumpPrompt;
        public string Status
        {
            get;
            private set;
        } = "";
        public bool WaitingForInput => IsPrompt(State);
        public bool HasFailed => State == Phase.Failed;
        public bool IsComplete => State == Phase.Finishing;
        public bool IsEasingIntoPause => _easingIntoPause;
        public bool IsFullyPaused => WaitingForInput && !_waitingForReveal && !_easingIntoPause && Time.timeScale <= 0f;
        public bool InputBuffered => _hasBufferedInput;
        public bool RequiresHookHold => _holdRequired;
        public int ActionAcceptedCount
        {
            get;
            private set;
        }
        public float CurrentPauseElapsed => _pauseElapsed;
        public float CurrentPauseDuration => _pauseDuration;
        public float FreePlayStartedAt
        {
            get;
            private set;
        } = -1f;
        public float RemainingFreePlayTime => FreePlayStartedAt < 0f ? Mathf.Max(0f, FreePlaySeconds) : Mathf.Max(0f, FreePlaySeconds - (Time.unscaledTime - FreePlayStartedAt));
        public float PromptAlpha => HasFailed ? 1f : !WaitingForInput || _waitingForReveal ? 0f : PromptFadeSeconds <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _promptStarted) / PromptFadeSeconds));
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

            if (!Player || !Player.Input || !Player.Body || !Player.Grapple || !Player.Settings || !CenterAnchor || !PromptView)
            {
                Debug.LogError("Missing connection", this);
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
            Player.OnYank.AddListener(RecordYank);
            _latestRaw = _bufferedInput = default;
            _easingIntoPause = _hasBufferedInput = _holdRequired = _jumpObserved = _returnRequested = false;
            ActionAcceptedCount = 0;
            FreePlayStartedAt = -1f;
            State = Phase.GroundJumpPrompt;
            Status = JumpPrompt;
            _travelStarted = Time.time;

            _waitingForReveal = true;
            ApplySimulationScale(_resumeTimeScale);
            PromptView.Hide();
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
                PromptView.Hide();
                return;
            }

            if (_waitingForReveal)
            {
                if (!Player.IsSupported)
                {
                    if (Time.time - _travelStarted > TravelTimeout)
                    {
                        _waitingForReveal = false;
                        Fail(InterruptedRouteSupport);
                    }

                    return;
                }

                _waitingForReveal = false;
                BeginPause(Phase.GroundJumpPrompt, JumpPrompt);
            }

            UpdateSimulationTiming();
            if (_controls.ReadHardware && State != Phase.FreePlay)
            {
                SubmitInput(_controls.RawSnapshot);
            }
            else if (WaitingForInput)
            {
                TryAcceptBufferedInput();
            }

            if (!WaitingForInput && !HasFailed && !IsComplete && State != Phase.FreePlay && Time.time - _travelStarted > TravelTimeout)
            {
                Fail(InterruptedRouteSupport);
            }

            if (State == Phase.FreePlay && RemainingFreePlayTime <= 0f)
            {
                Finish();
            }

            UpdateTerminalState();
            UpdatePresentation();
        }

        public bool SubmitInput(InputFrame raw)
        {
            if (!_sessionOwned || _waitingForReveal || HasFailed || IsComplete || (SceneTransition && SceneTransition.IsTransitioning))
            {
                return false;
            }

            if (State == Phase.FreePlay)
            {
                if (!_controls.ReadHardware)
                {
                    _controls.Inject(raw);
                }

                return false;
            }

            _latestRaw = raw;
            bool requestedYank = State == Phase.YankPrompt && Player.Grapple.IsAttached && (raw.DropPressed || (_hasBufferedInput && _bufferedInput.DropPressed));
            bool bufferedHookHold = _hasBufferedInput && _bufferedInput.HookPressed;
            if ((_holdRequired || bufferedHookHold) && !requestedYank && (raw.HookReleased || !raw.HookHeld))
            {
                Fail(FailureSupport);
                return false;
            }

            if (!WaitingForInput)
            {
                _controls.Inject(FilterTravelInput(raw));
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

            _controls.Inject(new InputFrame { HookHeld = _holdRequired, AimWorld = raw.AimWorld });
            return false;
        }

        private InputFrame FilterTravelInput(InputFrame raw)
        {
            return new InputFrame
            {
                AimWorld = raw.AimWorld,
                HookHeld = (State == Phase.HookFlight || State == Phase.Yanking) && raw.HookHeld
            };
        }

        private bool IsValidPromptInput(InputFrame raw)
        {
            switch (State)
            {
                case Phase.GroundJumpPrompt:
                    return raw.JumpPressed && Player.IsSupported;
                case Phase.HookPrompt:
                    return raw.HookPressed && raw.HookHeld && IsCorrectAim(raw.AimWorld);
                case Phase.YankPrompt:
                    return raw.DropPressed && Player.Grapple.IsAttached;
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
            if (requested.HookPressed && !_latestRaw.HookHeld)
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
                case Phase.GroundJumpPrompt:
                    if (!Player.IsSupported)
                    {
                        return false;
                    }

                    permitted.JumpPressed = true;
                    _jumpObserved = false;
                    EnterTravel(Phase.Jumping);
                    break;
                case Phase.HookPrompt:
                    if (!IsCorrectAim(requested.AimWorld))
                    {
                        _hasBufferedInput = false;
                        return false;
                    }

                    permitted.HookPressed = permitted.HookHeld = true;
                    permitted.AimWorld = CenterAnchor.bounds.center;
                    _holdRequired = true;
                    EnterTravel(Phase.HookFlight);
                    break;
                case Phase.YankPrompt:
                    if (!Player.Grapple.IsAttached)
                    {
                        return false;
                    }

                    permitted.DropPressed = true;
                    permitted.FastFall = requested.FastFall;
                    permitted.HookHeld = _latestRaw.HookHeld;
                    _holdRequired = false;
                    EnterTravel(Phase.Yanking);
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
            if (!_sessionOwned || _waitingForReveal || WaitingForInput || HasFailed || IsComplete)
            {
                return;
            }

            switch (State)
            {
                case Phase.Jumping:
                    float gravity = Mathf.Max(0.01f, -Physics2D.gravity.y * Player.Body.gravityScale);
                    float approachSpeed = gravity * BrakingSimulationSeconds + ApexVelocityTolerance;
                    if (_jumpObserved && !Player.IsSupported && Player.Body.linearVelocity.y <= approachSpeed)
                    {
                        BeginPause(Phase.HookPrompt, HookPrompt);
                    }

                    break;
                case Phase.HookFlight:
                    if (Player.Grapple.IsAttached)
                    {
                        if (Player.Grapple.TargetCollider != CenterAnchor)
                        {
                            Fail(InterruptedRouteSupport);
                            break;
                        }

                        BeginPause(Phase.YankPrompt, YankPrompt);
                    }

                    break;
            }
        }

        private bool IsCorrectAim(Vector2 aim)
        {
            return CenterAnchor && CenterAnchor.enabled && CenterAnchor.gameObject.activeInHierarchy && Vector2.Distance(aim, CenterAnchor.ClosestPoint(aim)) <= HookAimTolerance && Player.Grapple.CanAimAt(aim, CenterAnchor) && Player.Grapple.CanAimAt(CenterAnchor.bounds.center, CenterAnchor);
        }

        private static bool IsPrompt(Phase phase) => phase == Phase.GroundJumpPrompt || phase == Phase.HookPrompt || phase == Phase.YankPrompt;
        private void BeginPause(Phase next, string instruction)
        {
            State = next;
            Status = instruction;
            _hasBufferedInput = false;
            _promptStarted = _pauseStarted = Time.unscaledTime;
            _pauseDuration = Mathf.Max(0f, PauseEaseInSeconds);
            _pauseElapsed = 0f;
            _easingIntoPause = _pauseDuration > 0f;
            _controls.Clear();
            _controls.Inject(new InputFrame { HookHeld = _holdRequired, AimWorld = _latestRaw.AimWorld });
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

            UpdatePresentation();
        }

        private void EnterTravel(Phase next)
        {
            State = next;
            Status = "";
            _travelStarted = Time.time;
            _hasBufferedInput = _easingIntoPause = false;
            _controls.Clear();
            if (FollowCamera)
            {
                FollowCamera.CancelTutorialSettle();
            }

            ApplySimulationScale(_resumeTimeScale);
            PromptView.Hide();
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
            if (!_easingIntoPause)
            {
                return;
            }

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
        }

        private void RecordYank()
        {
            if (State != Phase.Yanking)
            {
                return;
            }

            if (Player.LastYankMode != "Low-speed yank")
            {
                Fail(InterruptedRouteSupport);
                return;
            }

            EnterTravel(Phase.FreePlay);
            _controls.SetGameplayLocked(false);
            FreePlayStartedAt = Time.unscaledTime;
            Status = FreePlayPrompt;
            UpdatePresentation();
        }

        private void Finish()
        {
            State = Phase.Finishing;
            Status = "";
            _controls.SetGameplayLocked(true);
            _controls.Clear();
            PromptView.Hide();
            _terminalStarted = Time.unscaledTime;
        }

        private void Fail(string support)
        {
            State = Phase.Failed;
            Status = FailureMessage;
            _controls.SetGameplayLocked(true);
            _currentFailureSupport = support;
            _holdRequired = _easingIntoPause = _hasBufferedInput = false;
            _controls.Clear();
            ApplySimulationScale(0f);
            if (FollowCamera)
            {
                FollowCamera.CancelTutorialSettle();
            }

            _terminalStarted = Time.unscaledTime;
            UpdatePresentation();
        }

        private void UpdateTerminalState()
        {
            if (_returnRequested || (!HasFailed && !IsComplete))
            {
                return;
            }

            if (HasFailed && Time.unscaledTime - _terminalStarted < Mathf.Max(0f, FailDelay))
            {
                return;
            }

            if (HasFailed && Sequence)
            {
                _returnRequested = true;
                Time.fixedDeltaTime = _previousFixedDeltaTime;
                Sequence.RestartStageTwo();
                return;
            }

            int sceneIndex = HasFailed ? gameObject.scene.buildIndex : TitleSceneIndex;
            if (!SceneTransition || !SceneTransition.IsValidSceneIndex(sceneIndex))
            {
                Debug.LogError("Missing transition or Scene List index", this);
                enabled = false;
                return;
            }

            _returnRequested = true;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            SceneTransition.FadeToScene(sceneIndex, HasFailed);
        }

        private void UpdatePresentation()
        {
            if (!PromptView)
            {
                return;
            }

            if (HasFailed)
            {
                PromptView.Present(FailureMessage, _currentFailureSupport);
                return;
            }

            if (WaitingForInput && !_waitingForReveal)
            {
                Collider2D target = State == Phase.HookPrompt ? CenterAnchor : null;
                Camera aimCamera = _controls && _controls.AimCamera ? _controls.AimCamera : Camera.main;
                PromptView.Present(Status, State == Phase.YankPrompt ? HoldReminder : "", PromptAlpha, target, aimCamera);
                return;
            }

            if (State == Phase.FreePlay && FreePlayHintSeconds > 0f)
            {
                float elapsed = Time.unscaledTime - FreePlayStartedAt;
                float duration = Mathf.Min(FreePlayHintSeconds, Mathf.Max(0f, FreePlaySeconds));
                if (elapsed < duration)
                {
                    float fade = Mathf.Min(0.15f, duration * 0.5f);
                    float alpha = fade > 0f ? Mathf.Min(Mathf.Clamp01(elapsed / fade), Mathf.Clamp01((duration - elapsed) / fade)) : 1f;
                    PromptView.Present(FreePlayPrompt, FreePlaySupport, alpha);
                    return;
                }
            }

            PromptView.Hide();
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

            if (PromptView)
            {
                PromptView.Hide();
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
                Player.OnYank.RemoveListener(RecordYank);
            }

            _sessionOwned = _waitingForReveal = _easingIntoPause = _hasBufferedInput = false;
        }

        private void OnDisable()
        {
            if (_sessionOwned)
            {
                RestoreOwnedState();
            }
        }
    }
}

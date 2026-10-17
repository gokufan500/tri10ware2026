using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    // Read buttons in Update
    public struct InputFrame
    {
        public float Move;
        public bool WallRun;
        public bool FastFall;
        public bool FastFallReleased;
        public bool JumpPressed;
        public bool DropPressed;
        public bool HookPressed;
        public bool HookHeld;
        public bool HookReleased;
        public bool RestartPressed;
        public Vector2 AimWorld;
    }

    [DefaultExecutionOrder(-300)]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [FormerlySerializedAs("aimCamera")]
        public Camera AimCamera;
        [FormerlySerializedAs("readHardware")]
        public bool ReadHardware = true;
        private InputFrame _frame;
        private InputFrame _queuedFrame;
        private InputFrame _rawSnapshot;
        private bool _hasQueuedFrame;
        public Vector2 AimWorld => _frame.AimWorld;
        public bool GameplayLocked
        {
            get;
            private set;
        }
        public int QueuedFramesConsumed
        {
            get;
            private set;
        }
        public InputFrame RawSnapshot => _rawSnapshot;

        private void Update()
        {
            if (!ReadHardware)
            {
                return;
            }

            if (!AimCamera)
            {
                AimCamera = Camera.main;
            }

            _rawSnapshot = new InputFrame
            {
                Move = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f),
                WallRun = Input.GetKey(KeyCode.W),
                FastFall = Input.GetKey(KeyCode.S),
                FastFallReleased = Input.GetKeyUp(KeyCode.S),
                JumpPressed = Input.GetKeyDown(KeyCode.Space),
                DropPressed = Input.GetKeyDown(KeyCode.S),
                HookPressed = Input.GetMouseButtonDown(0),
                HookHeld = Input.GetMouseButton(0),
                HookReleased = Input.GetMouseButtonUp(0),
                RestartPressed = Input.GetKeyDown(KeyCode.R)
            };
            if (AimCamera)
            {
                Vector3 mouse = Input.mousePosition;
                mouse.z = -AimCamera.transform.position.z;
                _rawSnapshot.AimWorld = AimCamera.ScreenToWorldPoint(mouse);
            }

            if (GameplayLocked)
            {
                return;
            }

            _frame.Move = _rawSnapshot.Move;
            _frame.WallRun = _rawSnapshot.WallRun;
            _frame.FastFall = _rawSnapshot.FastFall;
            _frame.FastFallReleased |= _rawSnapshot.FastFallReleased;
            _frame.JumpPressed |= _rawSnapshot.JumpPressed;
            _frame.DropPressed |= _rawSnapshot.DropPressed;
            _frame.HookPressed |= _rawSnapshot.HookPressed;
            _frame.HookHeld = _rawSnapshot.HookHeld;
            _frame.HookReleased |= _rawSnapshot.HookReleased;
            _frame.RestartPressed |= _rawSnapshot.RestartPressed;
            _frame.AimWorld = _rawSnapshot.AimWorld;
        }

        public InputFrame Consume()
        {
            if (_hasQueuedFrame)
            {
                InputFrame queued = _queuedFrame;
                _hasQueuedFrame = false;
                _queuedFrame = default;
                ClearEdges();
                QueuedFramesConsumed++;
                return queued;
            }

            InputFrame result = _frame;
            ClearEdges();
            return result;
        }

        private void ClearEdges()
        {
            _frame.JumpPressed = _frame.DropPressed = _frame.HookPressed = false;
            _frame.HookReleased = _frame.RestartPressed = false;
            _frame.FastFallReleased = false;
        }

        public void SetGameplayLocked(bool value)
        {
            if (GameplayLocked == value)
            {
                return;
            }

            GameplayLocked = value;
            Clear();
        }

        public void QueueFrame(InputFrame value)
        {
            _queuedFrame = value;
            _hasQueuedFrame = true;
        }

        public void Inject(InputFrame value)
        {
            _frame = value;
        }

        public void Clear()
        {
            _frame = _queuedFrame = default;
            _hasQueuedFrame = false;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused)
            {
                return;
            }

            Clear();
            _rawSnapshot = default;
        }
    }
}

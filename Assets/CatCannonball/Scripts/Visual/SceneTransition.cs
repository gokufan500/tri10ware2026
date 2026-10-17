using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace CatCannonball
{
    [DisallowMultipleComponent]
    public sealed class SceneTransition : MonoBehaviour
    {
        [Header("Fade tuning (real seconds)")]
        [Min(0f)]
        public float FadeOutSeconds = 0.4f;
        [Min(0f)]
        public float FadeInSeconds = 0.4f;
        public Color FadeColor = Color.black;
        private bool _outgoing;
        private bool _loadingScene;
        private bool _restoredWithinScene;
        private float _previousTimeScale;
        private readonly List<PlayerInputReader> _lockedInputs = new List<PlayerInputReader>();
        private readonly List<bool> _previousInputLocks = new List<bool>();
        public float CurrentAlpha
        {
            get;
            private set;
        }
        public bool IsTransitioning
        {
            get;
            private set;
        }
        public int LastRequestedSceneIndex
        {
            get;
            private set;
        } = -1;
        public bool PausesGameplayDuringTransition
        {
            get;
            private set;
        }

        private void OnEnable()
        {
            CurrentAlpha = FadeInSeconds > 0f ? 1f : 0f;
            StartCoroutine(FadeIn());
        }

        public bool IsValidSceneIndex(int buildIndex)
        {
            return buildIndex >= 0 && buildIndex < SceneManager.sceneCountInBuildSettings && Application.CanStreamedLevelBeLoaded(buildIndex);
        }

        public void FadeToScene(int buildIndex) => FadeToScene(buildIndex, true);
                public void FadeToScene(int buildIndex, bool pauseGameplay)
        {
            if (!IsValidSceneIndex(buildIndex))
            {
                Debug.LogWarning($"Scene index {buildIndex} is not enabled in Build Profiles > Scene List. Update the scene index in the Inspector.", this);
                return;
            }

            if (_outgoing || !isActiveAndEnabled)
            {
                return;
            }

            CombatFeedbackController.CancelSceneHitStops(gameObject.scene);
            LastRequestedSceneIndex = buildIndex;
            StopAllCoroutines();
            _outgoing = true;
            IsTransitioning = true;
            PausesGameplayDuringTransition = pauseGameplay;
            _previousTimeScale = Time.timeScale;
            LockSceneInput();
            if (pauseGameplay)
            {
                Time.timeScale = 0f;
            }

            StartCoroutine(FadeOutAndLoad(buildIndex));
        }
                public void FadeWithinScene(Action midpoint, bool pauseGameplay = true)
        {
            if (_outgoing || !isActiveAndEnabled || midpoint == null)
            {
                return;
            }

            CombatFeedbackController.CancelSceneHitStops(gameObject.scene);
            LastRequestedSceneIndex = -1;
            StopAllCoroutines();
            _outgoing = true;
            _restoredWithinScene = false;
            IsTransitioning = true;
            PausesGameplayDuringTransition = pauseGameplay;
            _previousTimeScale = Time.timeScale;
            LockSceneInput();
            if (pauseGameplay)
            {
                Time.timeScale = 0f;
            }

            StartCoroutine(FadeOutWithinScene(midpoint));
        }

        private IEnumerator FadeIn()
        {
            IsTransitioning = true;
            float duration = Mathf.Max(0f, FadeInSeconds);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                CurrentAlpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            CurrentAlpha = 0f;
            IsTransitioning = false;
        }

        private IEnumerator FadeOutAndLoad(int buildIndex)
        {
            float duration = Mathf.Max(0f, FadeOutSeconds);
            float elapsed = 0f;
            float initialAlpha = CurrentAlpha;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                CurrentAlpha = Mathf.Lerp(initialAlpha, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            CurrentAlpha = 1f;
            yield return null;
            SceneManager.sceneLoaded -= RestoreTimeAfterLoad;
            SceneManager.sceneLoaded += RestoreTimeAfterLoad;
            _loadingScene = true;
            SceneManager.LoadScene(buildIndex);
        }

        private IEnumerator FadeOutWithinScene(Action midpoint)
        {
            float duration = Mathf.Max(0f, FadeOutSeconds);
            float elapsed = 0f;
            float initialAlpha = CurrentAlpha;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                CurrentAlpha = Mathf.Lerp(initialAlpha, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            CurrentAlpha = 1f;
            yield return null;
            RestoreSceneInput();
            if (PausesGameplayDuringTransition)
            {
                Time.timeScale = _previousTimeScale;
            }

            _restoredWithinScene = true;
            try
            {
                midpoint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }

            yield return null;
            yield return FadeIn();
            _outgoing = false;
            _restoredWithinScene = false;
            PausesGameplayDuringTransition = false;
        }

        private static void RestoreTimeAfterLoad(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= RestoreTimeAfterLoad;
            Time.timeScale = 1f;
        }

        private void LockSceneInput()
        {
            foreach (PlayerInputReader reader in FindObjectsByType<PlayerInputReader>())
            {
                if (reader.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                _lockedInputs.Add(reader);
                _previousInputLocks.Add(reader.GameplayLocked);
                reader.SetGameplayLocked(true);
                reader.Clear();
            }
        }

        private void RestoreSceneInput()
        {
            for (int i = 0; i < _lockedInputs.Count; i++)
            {
                PlayerInputReader reader = _lockedInputs[i];
                if (!reader)
                {
                    continue;
                }

                reader.Clear();
                reader.SetGameplayLocked(_previousInputLocks[i]);
            }

            _lockedInputs.Clear();
            _previousInputLocks.Clear();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (_outgoing && !_restoredWithinScene)
            {
                RestoreSceneInput();
                if (!_loadingScene && PausesGameplayDuringTransition)
                {
                    Time.timeScale = _previousTimeScale;
                }
            }

            _outgoing = false;
            _restoredWithinScene = false;
            IsTransitioning = false;
            PausesGameplayDuringTransition = false;
            CurrentAlpha = 0f;
        }

        private void OnGUI()
        {
            if (CurrentAlpha <= 0f && !IsTransitioning)
            {
                return;
            }

            int previousDepth = GUI.depth;
            Color previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            GUI.depth = -10000;
            GUI.enabled = true;
            GUI.color = new Color(FadeColor.r, FadeColor.g, FadeColor.b, CurrentAlpha);
            Rect fullScreen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.DrawTexture(fullScreen, Texture2D.whiteTexture);
            GUI.color = Color.clear;
            GUI.Button(fullScreen, GUIContent.none, GUIStyle.none);
            if (Event.current.isMouse || Event.current.type == EventType.ScrollWheel)
            {
                Event.current.Use();
            }

            GUI.color = previousColor;
            GUI.enabled = previousEnabled;
            GUI.depth = previousDepth;
        }
    }
}

using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary>Plain two-button title screen with Inspector-configurable scene routing.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreen : MonoBehaviour
    {
        [Header("Scene routing")]
        public SceneTransition Transitions;
        [Min(0), Tooltip("Correlates to the build profiles. Go to Files > Build Profiles > Scene List.")]
        public int PlaySceneIndex = 1;
        [Min(0), Tooltip("Correlates to the build profiles. Go to Files > Build Profiles > Scene List.")]
        public int TutorialSceneIndex = 2;
        [Header("Plain menu layout")]
        public string Title = "CAT CANNONBALL";
        public Vector2 ButtonSize = new Vector2(280f, 64f);
        [Min(0f)]
        public float ButtonSpacing = 18f;
        [Min(12)]
        public int TitleFontSize = 40;
        [Min(12)]
        public int ButtonFontSize = 25;
        private GUIStyle _titleStyle;
        private GUIStyle _buttonStyle;
        private void Awake()
        {
            if (!Transitions)
            {
                Transitions = GetComponent<SceneTransition>();
            }
        }

        public void Play() => OpenScene(PlaySceneIndex);
        public void Tutorial() => OpenScene(TutorialSceneIndex);
        private void OpenScene(int buildIndex)
        {
            if (!Transitions)
            {
                Debug.LogWarning("Assign a Scene Transition component on the Title Screen.", this);
                return;
            }

            Transitions.FadeToScene(buildIndex);
        }

        private void OnGUI()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                _titleStyle.normal.textColor = Color.white;
                _buttonStyle = new GUIStyle(GUI.skin.button);
            }

            _titleStyle.fontSize = Mathf.Max(12, TitleFontSize);
            _buttonStyle.fontSize = Mathf.Max(12, ButtonFontSize);
            float scale = Mathf.Clamp(Screen.height / 900f, 0.55f, 2f);
            Matrix4x4 previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            float buttonWidth = Mathf.Clamp(ButtonSize.x, 80f, width - 32f);
            float buttonHeight = Mathf.Max(28f, ButtonSize.y);
            float top = height * 0.48f;
            GUI.Label(new Rect(16f, top - 135f, width - 32f, 95f), Title, _titleStyle);
            GUI.enabled = previousEnabled && Transitions && !Transitions.IsTransitioning;
            if (GUI.Button(new Rect((width - buttonWidth) / 2f, top, buttonWidth, buttonHeight), "Play", _buttonStyle))
            {
                Play();
            }

            if (GUI.Button(new Rect((width - buttonWidth) / 2f, top + buttonHeight + Mathf.Max(0f, ButtonSpacing), buttonWidth, buttonHeight), "Tutorial", _buttonStyle))
            {
                Tutorial();
            }

            GUI.enabled = previousEnabled;
            GUI.matrix = previousMatrix;
        }
    }
}

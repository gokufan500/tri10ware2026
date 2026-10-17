using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    // A deliberately plain diagnostics overlay. Replace freely when art/UI begins.
    public sealed class PrototypeHud : MonoBehaviour
    {
        [FormerlySerializedAs("motor")]
        public PlayerMotor Motor;
        [FormerlySerializedAs("input")]
        public PlayerInputReader Input;
        [FormerlySerializedAs("levelFlow")]
        public LevelFlow LevelFlow;
        [FormerlySerializedAs("viewCamera")]
        public Camera ViewCamera;
        [Tooltip("Scene transition used by Escape to return to the title screen.")]
        [FormerlySerializedAs("transitions")]
        public SceneTransition Transitions;
        [Min(0), Tooltip("Title index in Build Profiles > Scene List.")]
        [FormerlySerializedAs("titleSceneIndex")]
        public int TitleSceneIndex = 0;
        [Tooltip("Show the movement diagnostics overlay. Disable for guided tutorials with their own prompts.")]
        [FormerlySerializedAs("showDiagnostics")]
        public bool ShowDiagnostics = true;
        [FormerlySerializedAs("showHelp")]
        public bool ShowHelp = true;
        private GUIStyle _label;
        private GUIStyle _small;
        private void Start()
        {
            if (!Transitions)
            {
                Transitions = GetComponent<SceneTransition>();
            }
        }

        private void Update()
        {
            if (Input && Input.ReadHardware && UnityEngine.Input.GetKeyDown(KeyCode.H))
            {
                ShowHelp = !ShowHelp;
            }

            if (Transitions && !Transitions.IsTransitioning && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Transitions.FadeToScene(TitleSceneIndex);
            }
        }

        private void OnGUI()
        {
            if (!ShowDiagnostics || !Motor || !Motor.Body || !Motor.Grapple)
            {
                return;
            }

            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold
                };
                _label.normal.textColor = Color.white;
                _small = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    wordWrap = true
                };
                _small.normal.textColor = new Color(0.88f, 0.91f, 0.94f);
            }

            float scale = Mathf.Max(0.65f, Screen.height / 900f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale;
            float speed = Motor.Body.linearVelocity.magnitude;
            float threshold = Motor.Settings ? Motor.Settings.YankSpeedThreshold : 0f;
            string nextYank = Motor.Grapple.IsAttached ? speed < threshold ? "Low-speed yank" : "High-speed redirect" : "Attach to use S";
            GUI.Box(new Rect(12, 12, width - 24, 93), GUIContent.none);
            GUI.Label(new Rect(24, 19, width - 48, 27), "CAT CANNONBALL  /  MOVEMENT LAB", _label);
            GUI.Label(new Rect(24, 45, width - 48, 26), $"Speed {speed:0.0} units/s  |  Hook {Motor.Grapple.State}  |  Rope {Motor.Grapple.RopeLength:0.0}  |  {(Motor.IsSupported ? "Supported" : "Airborne")}  |  R reset  /  H help  /  Esc title", _small);
            GUI.Label(new Rect(24, 73, width - 48, 26), $"S ready: {nextYank} (threshold {threshold:0.0})  |  Last S: {Motor.LastYankMode}  |  Flow break: {Motor.LastFlowBreakReason}", _small);
            if (ShowHelp)
            {
                GUI.Box(new Rect(12, 115, 355, 220), GUIContent.none);
                GUI.Label(new Rect(24, 123, 330, 205), "A / D   Steer\nLeft mouse   Fire / hold hook; release to let go\nSpace   Jump; attached = release + upward bump\nS attached   Slow = yank; fast = redirect\nS free   Drop through / fast fall\nW   Run up wall / slow wall descent\nLandings and harsh impacts break flow.\nCyan = hooks; orange = speed targets\nH hides help; Escape returns to the title", _small);
            }

            GUI.matrix = previousMatrix;
        }
    }
}

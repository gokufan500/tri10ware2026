using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary>
    /// A single focused instruction shared by both tutorial scenes. Controllers
    /// decide when it is needed; this component only presents it and its target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialPromptView : MonoBehaviour
    {
        [Header("Readable instruction layout (at 720p)")]
        [Range(0.65f, 1.5f)]
        public float ViewScale = 1f;
        [Min(240f)]
        public float MaxPanelWidth = 900f;
        [Range(20, 48)]
        public int InstructionFontSize = 32;
        [Range(16, 32)]
        public int SupportFontSize = 21;
        [Min(8f)]
        public float Padding = 22f;
        [Min(8f)]
        public float TopMargin = 24f;
        [Min(0f)]
        public float LineSpacing = 8f;
        [Header("Target cue")]
        public Color TargetColor = new Color(1f, 0.84f, 0.3f);
        [Min(1f)]
        public float TargetLineWidth = 3f;
        [Min(2f)]
        public float TargetPadding = 12f;
        private Camera _aimCamera;
        public bool Visible
        {
            get;
            private set;
        }
        public string Instruction
        {
            get;
            private set;
        } = "";
        public string SupportLine
        {
            get;
            private set;
        } = "";
        public float Alpha
        {
            get;
            private set;
        }
        public Collider2D Target
        {
            get;
            private set;
        }
        public float PresentationScale
        {
            get;
            set;
        } = 1f;

        public void Present(string instruction, string supportLine, float alpha = 1f, Collider2D target = null, Camera aimCamera = null)
        {
            Instruction = instruction ?? "";
            SupportLine = supportLine ?? "";
            Alpha = Mathf.Clamp01(alpha);
            Target = target;
            _aimCamera = aimCamera;
            Visible = !string.IsNullOrWhiteSpace(Instruction) || !string.IsNullOrWhiteSpace(SupportLine);
        }

        public void Hide()
        {
            Visible = false;
            Instruction = SupportLine = "";
            Alpha = 0f;
            Target = null;
            _aimCamera = null;
        }

        private void OnDisable() => Hide();
        private void OnGUI()
        {
            if (!Visible || Alpha <= 0f)
            {
                return;
            }

            float scale = Mathf.Max(0.4f, Screen.height / 720f) * Mathf.Clamp(ViewScale, 0.65f, 1.5f) * Mathf.Clamp(PresentationScale, 0.65f, 1.5f);
            float canvasWidth = Screen.width / scale;
            float canvasHeight = Screen.height / scale;
            float edge = Mathf.Min(TopMargin, canvasWidth * 0.04f);
            float width = Mathf.Min(MaxPanelWidth, Mathf.Max(100f, canvasWidth - edge * 2f));
            float inset = Mathf.Min(Padding, width * 0.08f);
            float textWidth = width - inset * 2f;
            var instruction = new GUIStyle(GUI.skin.label)
            {
                fontSize = InstructionFontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = Color.white
                }
            };
            var support = new GUIStyle(GUI.skin.label)
            {
                fontSize = SupportFontSize,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = new Color(0.85f, 0.87f, 0.9f)
                }
            };
            float mainHeight = string.IsNullOrWhiteSpace(Instruction) ? 0f : instruction.CalcHeight(new GUIContent(Instruction), textWidth);
            float supportHeight = string.IsNullOrWhiteSpace(SupportLine) ? 0f : support.CalcHeight(new GUIContent(SupportLine), textWidth);
            float gap = mainHeight > 0f && supportHeight > 0f ? LineSpacing : 0f;
            float height = inset * 2f + mainHeight + gap + supportHeight;
            Rect panel = new Rect((canvasWidth - width) * 0.5f, edge, width, height);
            Camera camera = _aimCamera ? _aimCamera : Camera.main;
            bool showTarget = Target && camera;
            Vector2 targetPoint = default;
            float targetRadius = 24f;
            if (showTarget)
            {
                Vector3 center = camera.WorldToScreenPoint(Target.bounds.center);
                showTarget = center.z > 0f;
                targetPoint = new Vector2(center.x / scale, (Screen.height - center.y) / scale);
                Vector3 extent = camera.WorldToScreenPoint(Target.bounds.center + Vector3.up * Target.bounds.extents.y);
                targetRadius = Mathf.Clamp(Mathf.Abs(extent.y - center.y) / scale + TargetPadding, 18f, 48f);

                Rect cueArea = new Rect(targetPoint.x - targetRadius, targetPoint.y - targetRadius - 35f, targetRadius * 2f, targetRadius * 2f + 35f);
                if (panel.Overlaps(cueArea))
                {
                    panel.y = Mathf.Max(edge, canvasHeight - edge - height);
                }
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            GUI.depth = -100;
            GUI.matrix = oldMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.color = new Color(1f, 1f, 1f, Alpha);
            GUI.Box(panel, GUIContent.none);
            float y = panel.y + inset;
            if (mainHeight > 0f)
            {
                GUI.Label(new Rect(panel.x + inset, y, textWidth, mainHeight), Instruction, instruction);
                y += mainHeight + gap;
            }

            if (supportHeight > 0f)
            {
                GUI.Label(new Rect(panel.x + inset, y, textWidth, supportHeight), SupportLine, support);
            }

            if (showTarget)
            {
                GUI.color = new Color(TargetColor.r, TargetColor.g, TargetColor.b, Alpha);
                DrawTargetCorners(targetPoint, targetRadius);
                var arrow = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 28,
                    alignment = TextAnchor.MiddleCenter,
                    normal =
                    {
                        textColor = Color.white
                    }
                };
                GUI.Label(new Rect(targetPoint.x - 20f, targetPoint.y - targetRadius - 34f, 40f, 32f), "\u25BC", arrow);
            }

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
            GUI.depth = oldDepth;
        }

        private void DrawTargetCorners(Vector2 center, float radius)
        {
            float length = radius * 0.45f;
            float thickness = Mathf.Max(1f, TargetLineWidth);
            float left = center.x - radius, right = center.x + radius;
            float top = center.y - radius, bottom = center.y + radius;
            foreach (float x in new[]
            {
                left,
                right - length
            })
            {
                GUI.DrawTexture(new Rect(x, top, length, thickness), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x, bottom - thickness, length, thickness), Texture2D.whiteTexture);
            }

            foreach (float y in new[]
            {
                top,
                bottom - length
            })
            {
                GUI.DrawTexture(new Rect(left, y, thickness, length), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(right - thickness, y, thickness, length), Texture2D.whiteTexture);
            }
        }
    }
}

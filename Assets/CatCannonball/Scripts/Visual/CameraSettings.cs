using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary>One shared, designer-editable profile for the CameraRig prefab.</summary>
    [CreateAssetMenu(menuName = "Cat Cannonball/Camera Settings", fileName = "CameraSettings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [Header("Framing")]
        [Min(0.1f)]
        public float OrthographicSize = 8f;
        public Vector2 Offset = new Vector2(0f, 1f);
        [Header("Smooth following")]
        [Min(0.001f)]
        public float SmoothTimeX = 0.16f;
        [Min(0.001f)]
        public float SmoothTimeY = 0.20f;
        [Header("Fast-movement safety")]
        [Min(0.01f)]
        public float MaxLagX = 5f;
        [Min(0.01f)]
        public float MaxLagY = 3f;
        [Range(0f, 0.45f)]
        [FormerlySerializedAs("viewportMargin")]
        public float ViewportMargin = 0.10f;
        [Range(0f, 0.99f)]
        public float CatchUpStartFraction = 0.70f;
        [Min(0f)]
        public float CatchUpSharpness = 10f;
        private void OnValidate()
        {
            OrthographicSize = Mathf.Max(0.1f, OrthographicSize);
            SmoothTimeX = Mathf.Max(0.001f, SmoothTimeX);
            SmoothTimeY = Mathf.Max(0.001f, SmoothTimeY);
            MaxLagX = Mathf.Max(0.01f, MaxLagX);
            MaxLagY = Mathf.Max(0.01f, MaxLagY);
            ViewportMargin = Mathf.Clamp(ViewportMargin, 0f, 0.45f);
            CatchUpStartFraction = Mathf.Clamp(CatchUpStartFraction, 0f, 0.99f);
            CatchUpSharpness = Mathf.Max(0f, CatchUpSharpness);
        }
    }
}

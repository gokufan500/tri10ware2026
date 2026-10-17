using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    public sealed class CameraBounds2D : MonoBehaviour
    {
        public Vector2 Size = new Vector2(40f, 24f);
        public Vector2 Offset;
        public Bounds WorldBounds
        {
            get
            {
                Vector3 scale = transform.lossyScale;
                Vector3 center = transform.TransformPoint(Offset);
                return new Bounds(center, new Vector3(Mathf.Abs(Size.x * scale.x), Mathf.Abs(Size.y * scale.y), 0f));
            }
        }

        public Vector2 ClampCenter(Vector2 center, Vector2 viewportHalfSize)
        {
            Bounds region = WorldBounds;
            center.x = ClampAxis(center.x, region.min.x, region.max.x, Mathf.Abs(viewportHalfSize.x));
            center.y = ClampAxis(center.y, region.min.y, region.max.y, Mathf.Abs(viewportHalfSize.y));
            return center;
        }

        private static float ClampAxis(float value, float minimum, float maximum, float halfViewport)
        {
            if (maximum - minimum <= halfViewport * 2f)
            {
                return (minimum + maximum) * 0.5f;
            }

            return Mathf.Clamp(value, minimum + halfViewport, maximum - halfViewport);
        }

        private void OnValidate()
        {
            Size.x = Mathf.Max(0.01f, Size.x);
            Size.y = Mathf.Max(0.01f, Size.y);
        }

        private void OnDrawGizmos()
        {
            Bounds region = WorldBounds;
            Gizmos.color = new Color(0.3f, 0.9f, 0.65f, 0.65f);
            Gizmos.DrawWireCube(region.center, region.size);
        }
    }
}

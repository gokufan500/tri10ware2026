using UnityEngine;
using UnityEngine.Serialization;

namespace CatCannonball
{
    /// <summary>Temp UI for testing and visualization</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SpeedTarget))]
    public sealed class EnemyHealthDisplay : MonoBehaviour
    {
        public SpeedTarget Target;
        [Min(0f)]
        public float HeightAboveEnemy = 0.3f;
        [Min(0.01f)]
        public float CharacterSize = 0.13f;
        [Min(1)]
        public int FontSize = 64;
        public Color Color = Color.white;
        public int SortingOrder = 20;
        private Collider2D[] _colliders;
        private MeshRenderer _labelRenderer;
        public TextMesh Label
        {
            get;
            private set;
        }

        private void Awake() => EnsureLabel();
        private void Start() => Refresh();
        private void OnEnable()
        {
            EnsureLabel();
            Target.OnHealthChanged.AddListener(OnHealthChanged);
            Refresh();
        }

        private void OnDisable()
        {
            if (Target)
            {
                Target.OnHealthChanged.RemoveListener(OnHealthChanged);
            }

            if (Label)
            {
                Label.gameObject.SetActive(false);
            }
        }

        private void EnsureLabel()
        {
            if (!Target)
            {
                Target = GetComponent<SpeedTarget>();
            }

            if (Label)
            {
                return;
            }

            var labelObject = new GameObject("Health remaining");
            labelObject.transform.SetParent(transform, false);
            Label = labelObject.AddComponent<TextMesh>();
            Label.anchor = TextAnchor.LowerCenter;
            Label.alignment = TextAlignment.Center;
            Label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _labelRenderer = labelObject.GetComponent<MeshRenderer>();
            if (Label.font)
            {
                _labelRenderer.sharedMaterial = Label.font.material;
            }

            _colliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void OnHealthChanged(int health) => Refresh();
        public void Refresh()
        {
            EnsureLabel();
            Label.text = Mathf.Max(0, Target.CurrentHealth).ToString();
            Label.color = Color;
            Label.fontSize = Mathf.Max(1, FontSize);
            Label.characterSize = Mathf.Max(0.01f, CharacterSize);
            if (_labelRenderer)
            {
                _labelRenderer.sortingOrder = SortingOrder;
            }

            Label.gameObject.SetActive(isActiveAndEnabled && Target.IsAlive);
            UpdatePosition();
        }

        private void LateUpdate() => UpdatePosition();
        private void UpdatePosition()
        {
            if (!Label)
            {
                return;
            }

            Vector3 position = transform.position;
            float top = position.y;
            if (_colliders != null)
            {
                foreach (Collider2D collider in _colliders)
                {
                    if (collider && collider.enabled)
                    {
                        top = Mathf.Max(top, collider.bounds.max.y);
                    }
                }
            }

            Label.transform.position = new Vector3(position.x, top + Mathf.Max(0f, HeightAboveEnemy), position.z);
            Label.transform.rotation = Quaternion.identity;
            Vector3 scale = transform.lossyScale;
            Label.transform.localScale = new Vector3(InverseScale(scale.x), InverseScale(scale.y), InverseScale(scale.z));
        }

        private static float InverseScale(float value) => Mathf.Sign(value) / Mathf.Max(0.001f, Mathf.Abs(value));
    }
}

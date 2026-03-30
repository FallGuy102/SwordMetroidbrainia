using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    public sealed class SavePointMarker : MonoBehaviour
    {
        [SerializeField] private Color inactiveColor = new(0.62f, 0.78f, 0.96f, 1f);
        [SerializeField] private Color activeColor = new(0.25f, 1f, 0.72f, 1f);

        private SpriteRenderer _renderer;
        private bool _isActive;

        public Vector2 RespawnPosition => (Vector2)transform.position + Vector2.up * 0.6f;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            ApplyVisualState();
        }

        public void SetActiveState(bool active)
        {
            _isActive = active;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer != null)
            {
                _renderer.color = _isActive ? activeColor : inactiveColor;
            }
        }
    }
}

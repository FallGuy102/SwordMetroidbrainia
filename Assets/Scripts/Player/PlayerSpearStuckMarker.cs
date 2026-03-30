using SwordMetroidbrainia.Map;
using UnityEngine;

namespace SwordMetroidbrainia
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerSpearStuckMarker : MonoBehaviour
    {
        private PlayerSpear _owner;
        private OneWayPlatformMarker _oneWayPlatformMarker;
        private OneWayPlatformMarker.PlatformAxis _axis = OneWayPlatformMarker.PlatformAxis.Horizontal;

        public OneWayPlatformMarker.PlatformAxis Axis => _axis;

        public void Initialize(PlayerSpear owner)
        {
            _owner = owner;
        }

        public void SetActsAsOneWayPlatform(bool enabled, OneWayPlatformMarker.PlatformAxis axis = OneWayPlatformMarker.PlatformAxis.Horizontal)
        {
            _axis = axis;
            if (enabled)
            {
                _oneWayPlatformMarker ??= gameObject.GetComponent<OneWayPlatformMarker>() ?? gameObject.AddComponent<OneWayPlatformMarker>();
                _oneWayPlatformMarker.Axis = axis;
                return;
            }

            if (_oneWayPlatformMarker != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_oneWayPlatformMarker);
                }
                else
                {
                    DestroyImmediate(_oneWayPlatformMarker);
                }

                _oneWayPlatformMarker = null;
            }
        }

        public bool TryBreakBySword()
        {
            if (_owner == null)
            {
                return false;
            }

            return _owner.TryBreakFromSwordHit();
        }
    }
}

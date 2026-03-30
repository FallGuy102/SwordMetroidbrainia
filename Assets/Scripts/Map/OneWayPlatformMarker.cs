using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    public sealed class OneWayPlatformMarker : MonoBehaviour
    {
        public enum PlatformAxis
        {
            Horizontal,
            Vertical
        }

        [SerializeField] private PlatformAxis axis = PlatformAxis.Horizontal;

        public PlatformAxis Axis
        {
            get => axis;
            set => axis = value;
        }
    }
}

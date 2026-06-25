using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    public sealed class BreakableCellMarker : MonoBehaviour
    {
        private MapPreviewRenderer _owner;
        private MapRoomDefinition _room;
        private Transform _roomInstanceRoot;
        private Vector2Int _cell;

        public void Initialize(MapPreviewRenderer owner, MapRoomDefinition room, Transform roomInstanceRoot, Vector2Int cell)
        {
            _owner = owner;
            _room = room;
            _roomInstanceRoot = roomInstanceRoot;
            _cell = cell;
        }

        public bool TryBreakConnectedCells()
        {
            if (_owner == null || _room == null || _roomInstanceRoot == null)
            {
                return false;
            }

            _owner.BreakConnectedBreakableCells(_room, _roomInstanceRoot, _cell);
            return true;
        }
    }
}

using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    [RequireComponent(typeof(MapAuthoringRoot))]
    [RequireComponent(typeof(MapRoomViewportMask))]
    public sealed class MapRoomRuntimeController : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool centerCameraOnCurrentRoom = true;

        private MapAuthoringRoot _root;
        private MapRoomViewportMask _viewportMask;
        private Vector2Int _currentRoomGridPosition;
        private bool _hasCurrentRoom;

        private void Awake()
        {
            _root = GetComponent<MapAuthoringRoot>();
            _viewportMask = GetComponent<MapRoomViewportMask>();
        }

        private void LateUpdate()
        {
            if (_root == null || _root.Map == null || player == null)
            {
                return;
            }

            var localPlayerPosition = (Vector2)(player.position - transform.position);
            var roomGridPosition = MapLayoutUtility.GetRoomGridPosition(localPlayerPosition, _root.CellSize);
            if (!_root.Map.TryGetRoomIndexAt(roomGridPosition, out var roomIndex))
            {
                _hasCurrentRoom = false;
                return;
            }

            if (!_hasCurrentRoom || _currentRoomGridPosition != roomGridPosition)
            {
                _currentRoomGridPosition = roomGridPosition;
                _hasCurrentRoom = true;
                UpdateViewportMask(roomIndex);
            }

            if (centerCameraOnCurrentRoom && targetCamera != null)
            {
                CenterCameraOnRoom(roomIndex);
            }
        }

        private void CenterCameraOnRoom(int roomIndex)
        {
            var roomOrigin = _root.Map.GetRoomWorldOrigin(roomIndex, _root.CellSize);
            var roomSize = MapLayoutUtility.GetRoomSize(_root.CellSize);
            var cameraPosition = targetCamera.transform.position;
            targetCamera.transform.position = new Vector3(
                transform.position.x + roomOrigin.x + roomSize.x * 0.5f,
                transform.position.y + roomOrigin.y + roomSize.y * 0.5f,
                cameraPosition.z);
        }

        private void UpdateViewportMask(int roomIndex)
        {
            if (_viewportMask == null)
            {
                return;
            }

            var roomOrigin = _root.Map.GetRoomWorldOrigin(roomIndex, _root.CellSize);
            var roomSize = MapLayoutUtility.GetRoomSize(_root.CellSize);
            var roomBounds = new Rect(
                transform.position.x + roomOrigin.x,
                transform.position.y + roomOrigin.y,
                roomSize.x,
                roomSize.y);
            _viewportMask.SetRoomBounds(roomBounds);
        }
    }
}

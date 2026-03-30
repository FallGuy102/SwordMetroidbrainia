using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    // Scene anchor used by the custom editor to draw and edit the full map directly in Scene view.
    [RequireComponent(typeof(MapPreviewRenderer))]
    public sealed class MapAuthoringRoot : MonoBehaviour
    {
        [SerializeField] private MapDefinition map;
        [SerializeField, Min(0.1f)] private float cellSize = 1f;
        [SerializeField] private string roomAssetFolder = "Assets/MapRooms";
        [SerializeField] private MapRoomDefinition roomToPlace;

        public MapDefinition Map => map;
        public float CellSize => cellSize;
        public string RoomAssetFolder
        {
            get => roomAssetFolder;
            set => roomAssetFolder = value;
        }

        public MapRoomDefinition RoomToPlace
        {
            get => roomToPlace;
            set => roomToPlace = value;
        }

        public void RefreshPreview()
        {
            var previewRenderer = GetComponent<MapPreviewRenderer>();
            if (previewRenderer != null)
            {
                previewRenderer.Rebuild();
            }
        }
    }
}

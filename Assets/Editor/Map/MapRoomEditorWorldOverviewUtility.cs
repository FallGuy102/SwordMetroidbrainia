using SwordMetroidbrainia.Map;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapRoomEditorWorldOverviewUtility
    {
        public static Rect GetWorldContentRect(Rect overviewRect, Vector2Int minGrid, Vector2Int maxGrid, float previewCellSize)
        {
            var contentWidth = (maxGrid.x - minGrid.x + 1) * MapRoomDefinition.RoomWidth * previewCellSize;
            var contentHeight = (maxGrid.y - minGrid.y + 1) * MapRoomDefinition.RoomHeight * previewCellSize;
            return new Rect(
                overviewRect.x + (overviewRect.width - contentWidth) * 0.5f,
                overviewRect.y + (overviewRect.height - contentHeight) * 0.5f,
                contentWidth,
                contentHeight);
        }

        public static Rect GetWorldRoomRect(Vector2Int roomGridPosition, Vector2Int minGrid, Rect contentRect, float previewCellSize)
        {
            var offsetX = (roomGridPosition.x - minGrid.x) * MapRoomDefinition.RoomWidth * previewCellSize;
            var offsetY = (roomGridPosition.y - minGrid.y) * MapRoomDefinition.RoomHeight * previewCellSize;
            var displayY = contentRect.yMax - offsetY - MapRoomDefinition.RoomHeight * previewCellSize;
            return new Rect(
                contentRect.x + offsetX,
                displayY,
                MapRoomDefinition.RoomWidth * previewCellSize,
                MapRoomDefinition.RoomHeight * previewCellSize);
        }

        public static bool TryGetWorldRegionGridPosition(Rect contentRect, Vector2Int minGrid, float previewCellSize, Vector2 mousePosition, out Vector2Int roomGridPosition)
        {
            roomGridPosition = default;
            var roomWidth = MapRoomDefinition.RoomWidth * previewCellSize;
            var roomHeight = MapRoomDefinition.RoomHeight * previewCellSize;
            if (roomWidth <= 0f || roomHeight <= 0f || !contentRect.Contains(mousePosition))
            {
                return false;
            }

            var localX = mousePosition.x - contentRect.x;
            var localYFromBottom = contentRect.yMax - mousePosition.y;
            var gridX = Mathf.FloorToInt(localX / roomWidth) + minGrid.x;
            var gridY = Mathf.FloorToInt(localYFromBottom / roomHeight) + minGrid.y;
            roomGridPosition = new Vector2Int(gridX, gridY);
            return true;
        }

        public static bool TryGetWorldPreviewBounds(MapDefinition map, int roomPadding, out Vector2Int minGrid, out Vector2Int maxGrid)
        {
            minGrid = default;
            maxGrid = default;
            if (map.Rooms.Count == 0)
            {
                return false;
            }

            minGrid = map.GetRoom(0).gridPosition;
            maxGrid = minGrid;
            for (var i = 1; i < map.Rooms.Count; i++)
            {
                var grid = map.GetRoom(i).gridPosition;
                minGrid = Vector2Int.Min(minGrid, grid);
                maxGrid = Vector2Int.Max(maxGrid, grid);
            }

            minGrid -= Vector2Int.one * roomPadding;
            maxGrid += Vector2Int.one * roomPadding;
            return true;
        }

        public static float GetWorldPreviewCellSize(Rect overviewRect)
        {
            var cellSizeFromWidth = overviewRect.width / (MapRoomDefinition.RoomWidth * 4.4f);
            var cellSizeFromHeight = overviewRect.height / (MapRoomDefinition.RoomHeight * 3.4f);
            return Mathf.Clamp(Mathf.Min(cellSizeFromWidth, cellSizeFromHeight), 3f, 6f);
        }
    }
}

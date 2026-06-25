using SwordMetroidbrainia.Map;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapRoomEditorGridGeometry
    {
        public static Rect GetCurrentRoomRect(Rect contentRect, float cellSize, int contextMargin)
        {
            return new Rect(
                contentRect.x + contextMargin * cellSize,
                contentRect.y + contextMargin * cellSize,
                MapRoomDefinition.RoomWidth * cellSize,
                MapRoomDefinition.RoomHeight * cellSize);
        }

        public static Rect GetContextCellRect(Rect contentRect, int contextX, int contextY, float cellSize, int contextMargin, int totalRows)
        {
            var displayColumn = contextX + contextMargin;
            var displayRow = totalRows - 1 - (contextY + contextMargin);
            return new Rect(contentRect.x + displayColumn * cellSize, contentRect.y + displayRow * cellSize, cellSize, cellSize);
        }

        public static bool TryGetEditableGridCell(Rect contentRect, Vector2 mousePosition, float cellSize, int contextMargin, out Vector2Int cell)
        {
            var currentRoomRect = GetCurrentRoomRect(contentRect, cellSize, contextMargin);
            if (!currentRoomRect.Contains(mousePosition))
            {
                cell = default;
                return false;
            }

            var localX = Mathf.FloorToInt((mousePosition.x - currentRoomRect.x) / cellSize);
            var displayRow = Mathf.FloorToInt((mousePosition.y - currentRoomRect.y) / cellSize);
            var roomY = MapRoomDefinition.RoomHeight - 1 - displayRow;

            if (localX < 0 || localX >= MapRoomDefinition.RoomWidth || roomY < 0 || roomY >= MapRoomDefinition.RoomHeight)
            {
                cell = default;
                return false;
            }

            cell = new Vector2Int(localX, roomY);
            return true;
        }
    }
}

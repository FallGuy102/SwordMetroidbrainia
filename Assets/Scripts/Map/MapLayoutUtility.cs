using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    /// <summary>
    /// Shared math helpers for converting between room grid, cell grid, and world space.
    /// Keeping this separate avoids duplicating room-size arithmetic across runtime and editor code.
    /// </summary>
    public static class MapLayoutUtility
    {
        public static Vector2 GetRoomSize(float cellSize)
        {
            return new Vector2(MapRoomDefinition.RoomWidth * cellSize, MapRoomDefinition.RoomHeight * cellSize);
        }

        public static Vector2 GetRoomOrigin(Vector2 rootPosition, Vector2Int roomGridPosition, float cellSize)
        {
            var roomSize = GetRoomSize(cellSize);
            return rootPosition + Vector2.Scale((Vector2)roomGridPosition, roomSize);
        }

        public static Vector2 GetCellCenter(Vector2 roomOrigin, int cellX, int cellY, float cellSize)
        {
            return roomOrigin + new Vector2((cellX + 0.5f) * cellSize, (cellY + 0.5f) * cellSize);
        }

        public static Vector2Int GetRoomGridPosition(Vector2 localPoint, float cellSize)
        {
            var roomSize = GetRoomSize(cellSize);
            return new Vector2Int(
                Mathf.FloorToInt(localPoint.x / roomSize.x),
                Mathf.FloorToInt(localPoint.y / roomSize.y));
        }

        public static bool TryGetCellCoordinates(Vector2 localPoint, Vector2Int roomGridPosition, float cellSize, out Vector2Int cellCoordinates)
        {
            var roomOrigin = GetRoomOrigin(Vector2.zero, roomGridPosition, cellSize);
            var roomLocalPoint = localPoint - roomOrigin;
            var cellX = Mathf.FloorToInt(roomLocalPoint.x / cellSize);
            var cellY = Mathf.FloorToInt(roomLocalPoint.y / cellSize);

            if (cellX < 0 || cellX >= MapRoomDefinition.RoomWidth || cellY < 0 || cellY >= MapRoomDefinition.RoomHeight)
            {
                cellCoordinates = default;
                return false;
            }

            cellCoordinates = new Vector2Int(cellX, cellY);
            return true;
        }
    }
}

using SwordMetroidbrainia.Map;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal readonly struct MapRoomEditorGridContext
    {
        public MapRoomEditorGridContext(
            MapDefinition map,
            MapRoomDefinition room,
            Vector2Int activeRoomGridPosition,
            int contextMargin,
            bool hasWorldContext)
        {
            Map = map;
            Room = room;
            ActiveRoomGridPosition = activeRoomGridPosition;
            ContextMargin = contextMargin;
            HasWorldContext = hasWorldContext;
        }

        public MapDefinition Map { get; }
        public MapRoomDefinition Room { get; }
        public Vector2Int ActiveRoomGridPosition { get; }
        public int ContextMargin { get; }
        public bool HasWorldContext { get; }
        public int TotalColumns => MapRoomDefinition.RoomWidth + ContextMargin * 2;
        public int TotalRows => MapRoomDefinition.RoomHeight + ContextMargin * 2;

        public bool TryGetCellType(int contextX, int contextY, out RoomCellType type, out bool isCurrentRoom)
        {
            type = RoomCellType.Empty;
            isCurrentRoom = contextX >= 0
                && contextX < MapRoomDefinition.RoomWidth
                && contextY >= 0
                && contextY < MapRoomDefinition.RoomHeight;

            if (isCurrentRoom)
            {
                type = Room.GetCellType(contextX, contextY);
                return true;
            }

            if (!HasWorldContext || Map == null)
            {
                return false;
            }

            var neighborGridPosition = ActiveRoomGridPosition + new Vector2Int(
                FloorDiv(contextX, MapRoomDefinition.RoomWidth),
                FloorDiv(contextY, MapRoomDefinition.RoomHeight));

            if (!Map.TryGetRoomIndexAt(neighborGridPosition, out var roomIndex))
            {
                return false;
            }

            var placement = Map.GetRoom(roomIndex);
            if (placement.room == null)
            {
                return false;
            }

            type = placement.room.GetCellType(
                PositiveMod(contextX, MapRoomDefinition.RoomWidth),
                PositiveMod(contextY, MapRoomDefinition.RoomHeight));
            return true;
        }

        private static int FloorDiv(int value, int divisor)
        {
            return Mathf.FloorToInt((float)value / divisor);
        }

        private static int PositiveMod(int value, int divisor)
        {
            return (value % divisor + divisor) % divisor;
        }
    }
}

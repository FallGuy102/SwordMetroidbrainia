using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    [CreateAssetMenu(menuName = "SwordMetroidbrainia/Map/Map Definition", fileName = "MapDefinition")]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField] private List<MapRoomPlacement> rooms = new();

        public IReadOnlyList<MapRoomPlacement> Rooms => rooms;

        public bool TryGetRoomIndexAt(Vector2Int roomGridPosition, out int roomIndex)
        {
            for (var i = 0; i < rooms.Count; i++)
            {
                if (rooms[i].gridPosition == roomGridPosition)
                {
                    roomIndex = i;
                    return true;
                }
            }

            roomIndex = -1;
            return false;
        }

        public bool IsValidRoomIndex(int roomIndex)
        {
            return roomIndex >= 0 && roomIndex < rooms.Count;
        }

        public MapRoomPlacement GetRoom(int roomIndex)
        {
            return rooms[roomIndex];
        }

        public void SetRoom(int roomIndex, MapRoomPlacement room)
        {
            rooms[roomIndex] = room;
        }

        public int AddRoom(MapRoomDefinition roomDefinition, Vector2Int roomGridPosition)
        {
            if (roomDefinition == null)
            {
                return -1;
            }

            if (TryGetRoomIndexAt(roomGridPosition, out var existingIndex))
            {
                return existingIndex;
            }

            rooms.Add(new MapRoomPlacement
            {
                room = roomDefinition,
                gridPosition = roomGridPosition
            });

            return rooms.Count - 1;
        }

        public void RemoveRoomAt(Vector2Int roomGridPosition)
        {
            if (!TryGetRoomIndexAt(roomGridPosition, out var roomIndex))
            {
                return;
            }

            rooms.RemoveAt(roomIndex);
        }

        public Vector2 GetRoomWorldOrigin(int roomIndex, float cellSize)
        {
            var room = rooms[roomIndex];
            return MapLayoutUtility.GetRoomOrigin(Vector2.zero, room.gridPosition, cellSize);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var seenPositions = new HashSet<Vector2Int>();
            for (var i = rooms.Count - 1; i >= 0; i--)
            {
                var room = rooms[i];
                if (room.room == null || !seenPositions.Add(room.gridPosition))
                {
                    rooms.RemoveAt(i);
                    continue;
                }

                rooms[i] = room;
            }
        }
#endif
    }

    [Serializable]
    public struct MapRoomPlacement
    {
        public MapRoomDefinition room;
        public Vector2Int gridPosition;
    }
}

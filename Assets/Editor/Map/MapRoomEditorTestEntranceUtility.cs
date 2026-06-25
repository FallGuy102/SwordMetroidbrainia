using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapRoomEditorTestEntranceUtility
    {
        public static bool TryMovePlayerToRoom(
            MapAuthoringRoot mapRoot,
            MapRoomDefinition room,
            Vector2Int roomGridPosition,
            bool enterPlayMode)
        {
            if (!TryGetRoomTestPosition(mapRoot, room, roomGridPosition, out var testPosition))
            {
                EditorUtility.DisplayDialog("No Room Selected", "Select a placed room in the World Overview first.", "OK");
                return false;
            }

            var player = Object.FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            if (player == null)
            {
                EditorUtility.DisplayDialog("Player Not Found", "Could not find a PlayerController2D in the open scene.", "OK");
                return false;
            }

            MovePlayer(player, testPosition);

            if (enterPlayMode && !EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = true;
            }

            return true;
        }

        public static bool TryGetRoomTestPosition(
            MapAuthoringRoot mapRoot,
            MapRoomDefinition room,
            Vector2Int roomGridPosition,
            out Vector2 position)
        {
            position = default;
            if (mapRoot == null || room == null)
            {
                return false;
            }

            var roomOrigin = MapLayoutUtility.GetRoomOrigin(mapRoot.transform.position, roomGridPosition, mapRoot.CellSize);
            position = TryGetFirstSavePointPosition(room, roomOrigin, mapRoot.CellSize, out var savePointPosition)
                ? savePointPosition
                : roomOrigin + MapLayoutUtility.GetRoomSize(mapRoot.CellSize) * 0.5f;
            return true;
        }

        private static bool TryGetFirstSavePointPosition(
            MapRoomDefinition room,
            Vector2 roomOrigin,
            float cellSize,
            out Vector2 position)
        {
            position = default;
            for (var y = 0; y < MapRoomDefinition.RoomHeight; y++)
            {
                for (var x = 0; x < MapRoomDefinition.RoomWidth; x++)
                {
                    if (room.GetCellType(x, y) != RoomCellType.SavePoint)
                    {
                        continue;
                    }

                    position = MapLayoutUtility.GetCellCenter(roomOrigin, x, y, cellSize);
                    return true;
                }
            }

            return false;
        }

        private static void MovePlayer(PlayerController2D player, Vector2 position)
        {
            if (Application.isPlaying)
            {
                player.TeleportTo(position);
                return;
            }

            Undo.RecordObject(player.transform, "Move Player To Room");
            var previousPosition = player.transform.position;
            player.transform.position = new Vector3(position.x, position.y, previousPosition.z);
            EditorUtility.SetDirty(player.transform);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Selection.activeGameObject = player.gameObject;
        }
    }
}

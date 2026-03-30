using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapAuthoringSceneUtility
    {
        private static readonly Color WallCellColor = new(0.22f, 0.22f, 0.22f, 0.72f);
        private static readonly Color GroundCellColor = new(0.56f, 0.36f, 0.18f, 0.72f);
        private static readonly Color OneWayPlatformCellColor = new(0.82f, 0.52f, 0.2f, 0.72f);

        public static void CaptureSceneInput()
        {
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (Event.current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }
        }

        public static void DrawRooms(MapAuthoringRoot root, MapDefinition map, int selectedRoomIndex)
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

            for (var i = 0; i < map.Rooms.Count; i++)
            {
                var placement = map.GetRoom(i);
                if (placement.room == null)
                {
                    continue;
                }

                DrawRoom(root, placement, i == selectedRoomIndex);
            }
        }

        public static bool HandleSceneInput(
            MapAuthoringRoot root,
            MapDefinition map,
            MapAuthoringEditorState state,
            System.Action repaintInspector)
        {
            var currentEvent = Event.current;
            if (MapAuthoringBrushShortcutUtility.TryGetBrushShortcut(currentEvent, out var shortcutBrush))
            {
                state.Brush = shortcutBrush;
                repaintInspector?.Invoke();
                SceneView.RepaintAll();
                return true;
            }

            if (currentEvent.type == EventType.MouseUp)
            {
                state.StopPainting();
                return false;
            }

            if (currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseDrag)
            {
                return false;
            }

            if (currentEvent.button != 0)
            {
                return false;
            }

            var isEraseInput = currentEvent.control || currentEvent.command;
            if (!TryGetMapLocalPoint(root, currentEvent.mousePosition, out var localPoint))
            {
                return false;
            }

            var roomGridPosition = MapLayoutUtility.GetRoomGridPosition(localPoint, root.CellSize);
            if (!map.TryGetRoomIndexAt(roomGridPosition, out var roomIndex))
            {
                if (TryPlaceRoom(root, map, state, currentEvent, roomGridPosition, isEraseInput, out roomIndex))
                {
                    repaintInspector?.Invoke();
                }

                currentEvent.Use();
                return true;
            }

            if (TrySelectRoom(root, map, state, roomIndex, roomGridPosition))
            {
                currentEvent.Use();
                return true;
            }

            if (!state.EditCells || !map.IsValidRoomIndex(roomIndex))
            {
                currentEvent.Use();
                return true;
            }

            if (TryPaintCell(root, map, state, currentEvent, roomIndex, localPoint))
            {
                repaintInspector?.Invoke();
            }

            currentEvent.Use();
            return true;
        }

        public static void ClearSelectedRoomHighlight(MapAuthoringRoot root)
        {
            var previewRenderer = root.GetComponent<MapPreviewRenderer>();
            if (previewRenderer == null)
            {
                return;
            }

            previewRenderer.ClearEditorSelectedRoom();
        }

        private static bool TryPlaceRoom(
            MapAuthoringRoot root,
            MapDefinition map,
            MapAuthoringEditorState state,
            Event currentEvent,
            Vector2Int roomGridPosition,
            bool isEraseInput,
            out int roomIndex)
        {
            roomIndex = -1;
            if (isEraseInput || !currentEvent.shift || root.RoomToPlace == null || currentEvent.type != EventType.MouseDown)
            {
                return false;
            }

            Undo.RecordObject(map, "Place Room");
            roomIndex = map.AddRoom(root.RoomToPlace, roomGridPosition);
            if (roomIndex < 0)
            {
                return false;
            }

            EditorUtility.SetDirty(map);
            state.SelectedRoomIndex = roomIndex;
            state.StopPainting();
            SetSelectedRoomHighlight(root, roomGridPosition);
            RefreshEditor(root);
            return true;
        }

        private static bool TrySelectRoom(
            MapAuthoringRoot root,
            MapDefinition map,
            MapAuthoringEditorState state,
            int roomIndex,
            Vector2Int roomGridPosition)
        {
            var changedSelection = state.SelectedRoomIndex != roomIndex;
            state.SelectedRoomIndex = roomIndex;
            SyncActiveRoomDefinition(root, map, roomIndex);
            SetSelectedRoomHighlight(root, roomGridPosition);

            if (!changedSelection)
            {
                return false;
            }

            state.StopPainting();
            RefreshEditor(root);
            return true;
        }

        private static bool TryPaintCell(
            MapAuthoringRoot root,
            MapDefinition map,
            MapAuthoringEditorState state,
            Event currentEvent,
            int roomIndex,
            Vector2 localPoint)
        {
            var placement = map.GetRoom(roomIndex);
            if (placement.room == null)
            {
                return false;
            }

            if (!MapLayoutUtility.TryGetCellCoordinates(localPoint, placement.gridPosition, root.CellSize, out var cellCoordinates))
            {
                if (currentEvent.type == EventType.MouseDrag)
                {
                    state.StopPainting();
                }

                return false;
            }

            var isEraseInput = currentEvent.control || currentEvent.command;
            var isPaintInput = !currentEvent.shift || isEraseInput;
            if (currentEvent.type == EventType.MouseDown)
            {
                state.IsPaintingCells = isPaintInput;
                state.PaintingRoomIndex = isPaintInput ? roomIndex : -1;
                state.IsErasingCells = isEraseInput;
            }

            var canApplyPaint = currentEvent.type == EventType.MouseDown
                || (currentEvent.type == EventType.MouseDrag
                    && state.IsPaintingCells
                    && state.PaintingRoomIndex == roomIndex);

            if (!canApplyPaint || !isPaintInput)
            {
                return false;
            }

            var nextCellType = state.IsErasingCells ? RoomCellType.Empty : state.Brush;
            if (placement.room.GetCellType(cellCoordinates.x, cellCoordinates.y) == nextCellType)
            {
                return false;
            }

            Undo.RecordObject(placement.room, "Paint Room Cell");
            placement.room.SetCellType(cellCoordinates.x, cellCoordinates.y, nextCellType);
            EditorUtility.SetDirty(placement.room);
            RefreshEditor(root);
            return true;
        }

        private static void SyncActiveRoomDefinition(MapAuthoringRoot root, MapDefinition map, int roomIndex)
        {
            if (!map.IsValidRoomIndex(roomIndex))
            {
                return;
            }

            var roomDefinition = map.GetRoom(roomIndex).room;
            if (roomDefinition == null || root.RoomToPlace == roomDefinition)
            {
                return;
            }

            Undo.RecordObject(root, "Select Room Definition");
            root.RoomToPlace = roomDefinition;
            EditorUtility.SetDirty(root);
            EditorGUIUtility.PingObject(roomDefinition);
        }

        private static void SetSelectedRoomHighlight(MapAuthoringRoot root, Vector2Int roomGridPosition)
        {
            var previewRenderer = root.GetComponent<MapPreviewRenderer>();
            if (previewRenderer == null)
            {
                return;
            }

            previewRenderer.SetEditorSelectedRoom(roomGridPosition);
        }

        private static void DrawRoom(MapAuthoringRoot root, MapRoomPlacement placement, bool isSelected)
        {
            var roomOrigin = GetRoomOrigin(root, placement.gridPosition);
            var roomSize = MapLayoutUtility.GetRoomSize(root.CellSize);
            var fillColor = placement.room.ShowOnMapWhenDiscovered
                ? new Color(0.2f, 0.6f, 0.4f, 0.14f)
                : new Color(0.5f, 0.5f, 0.5f, 0.12f);
            var outlineColor = isSelected ? new Color(1f, 0.92f, 0.2f, 1f) : new Color(0f, 0f, 0f, 0.85f);

            Handles.DrawSolidRectangleWithOutline(
                new Vector3[]
                {
                    roomOrigin,
                    roomOrigin + new Vector2(roomSize.x, 0f),
                    roomOrigin + roomSize,
                    roomOrigin + new Vector2(0f, roomSize.y)
                },
                fillColor,
                outlineColor);

            DrawRoomOutline(roomOrigin, roomSize, outlineColor, isSelected ? 4f : 3f);
            Handles.Label(roomOrigin + new Vector2(0.2f, roomSize.y - 0.5f), placement.room.DisplayName);
            DrawRoomGrid(root, placement.gridPosition, isSelected);
            DrawRoomCells(root, placement);
        }

        private static void DrawRoomGrid(MapAuthoringRoot root, Vector2Int roomGridPosition, bool isSelected)
        {
            var origin = GetRoomOrigin(root, roomGridPosition);
            var majorColor = isSelected ? new Color(1f, 1f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0.22f);
            var minorColor = isSelected ? new Color(0.92f, 0.95f, 1f, 0.28f) : new Color(0.92f, 0.95f, 1f, 0.14f);
            var roomSize = MapLayoutUtility.GetRoomSize(root.CellSize);

            for (var x = 0; x <= MapRoomDefinition.RoomWidth; x++)
            {
                var start = origin + new Vector2(x * root.CellSize, 0f);
                var end = origin + new Vector2(x * root.CellSize, roomSize.y);
                Handles.color = x == 0 || x == MapRoomDefinition.RoomWidth ? majorColor : minorColor;
                Handles.DrawLine(start, end);
            }

            for (var y = 0; y <= MapRoomDefinition.RoomHeight; y++)
            {
                var start = origin + new Vector2(0f, y * root.CellSize);
                var end = origin + new Vector2(roomSize.x, y * root.CellSize);
                Handles.color = y == 0 || y == MapRoomDefinition.RoomHeight ? majorColor : minorColor;
                Handles.DrawLine(start, end);
            }
        }

        private static void DrawRoomCells(MapAuthoringRoot root, MapRoomPlacement placement)
        {
            var origin = GetRoomOrigin(root, placement.gridPosition);

            for (var y = 0; y < MapRoomDefinition.RoomHeight; y++)
            {
                for (var x = 0; x < MapRoomDefinition.RoomWidth; x++)
                {
                    var type = placement.room.GetCellType(x, y);
                    if (type == RoomCellType.Empty)
                    {
                        continue;
                    }

                    var min = origin + new Vector2(x * root.CellSize, y * root.CellSize);
                    var max = min + Vector2.one * root.CellSize;
                    Handles.DrawSolidRectangleWithOutline(
                        new Vector3[]
                        {
                            min,
                            new Vector2(max.x, min.y),
                            max,
                            new Vector2(min.x, max.y)
                        },
                        GetCellColor(type),
                        new Color(0f, 0f, 0f, 0.15f));
                }
            }
        }

        private static bool TryGetMapLocalPoint(MapAuthoringRoot root, Vector2 mousePosition, out Vector2 localPoint)
        {
            var plane = new Plane(Vector3.forward, root.transform.position);
            var ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            if (!plane.Raycast(ray, out var enter))
            {
                localPoint = default;
                return false;
            }

            var worldPoint = ray.GetPoint(enter);
            localPoint = (Vector2)(worldPoint - root.transform.position);
            return true;
        }

        private static Vector2 GetRoomOrigin(MapAuthoringRoot root, Vector2Int roomGridPosition)
        {
            return MapLayoutUtility.GetRoomOrigin(root.transform.position, roomGridPosition, root.CellSize);
        }

        private static Color GetCellColor(RoomCellType type)
        {
            return type switch
            {
                RoomCellType.Wall => WallCellColor,
                RoomCellType.Ground => GroundCellColor,
                RoomCellType.OneWayPlatform => OneWayPlatformCellColor,
                _ => new Color(0.8f, 0.8f, 0.8f, 0.25f)
            };
        }

        private static void RefreshEditor(MapAuthoringRoot root)
        {
            root.RefreshPreview();
            SceneView.RepaintAll();
        }

        private static void DrawRoomOutline(Vector2 origin, Vector2 size, Color color, float thickness)
        {
            var bottomLeft = origin;
            var bottomRight = origin + new Vector2(size.x, 0f);
            var topRight = origin + size;
            var topLeft = origin + new Vector2(0f, size.y);

            Handles.color = color;
            Handles.DrawAAPolyLine(thickness, bottomLeft, bottomRight, topRight, topLeft, bottomLeft);
        }
    }
}

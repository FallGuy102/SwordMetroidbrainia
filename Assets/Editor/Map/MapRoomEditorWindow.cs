using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    public sealed class MapRoomEditorWindow : EditorWindow
    {
        private const float GridPadding = 12f;
        private const float GridPanelRatio = 0.58f;
        private const float MinGridPanelWidth = 420f;
        private const float MinWorldPanelWidth = 220f;
        private const float WorldPreviewPadding = 16f;
        private const int WorldOverviewRoomPadding = 2;

        private enum WorldRegionTool
        {
            Select,
            CreateNewRoom,
            ReplaceWithActiveRoom,
            DeleteRoom
        }

        private MapRoomDefinition _room;
        private MapAuthoringRoot _mapRoot;
        private RoomCellType _brush = RoomCellType.Wall;
        private WorldRegionTool _worldRegionTool = WorldRegionTool.Select;
        private string _roomNameDraft = string.Empty;

        private bool _isPainting;
        private bool _isErasing;
        private Vector2Int _lastPaintedCell = new(-1, -1);

        private int _selectedPlacementIndex = -1;
        private bool _hasSelectedRegion;
        private Vector2Int _selectedRegionGridPosition;
        private Vector2 _worldPan;
        private bool _worldPanInitialized;
        private bool _isPanningWorld;
        private Vector2 _lastWorldPanMousePosition;

        [MenuItem("SwordMetroidbrainia/Map/Room Editor")]
        public static void OpenWindow()
        {
            var window = GetWindow<MapRoomEditorWindow>("Room Editor");
            window.minSize = new Vector2(720f, 680f);
            window.TrySyncRoomFromSelection();
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            TrySyncRoomFromSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            if (TrySyncRoomFromSelection())
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            TrySyncRoomFromSelection();

            HandleBrushShortcuts();
            DrawToolbar();

            if (_room == null)
            {
                EditorGUILayout.HelpBox("Select a room from the world overview, or select a map room asset in the Project view.", MessageType.Info);
                return;
            }

            DrawBody();
        }

        private void HandleBrushShortcuts()
        {
            if (MapAuthoringBrushShortcutUtility.TryGetBrushShortcut(Event.current, out var shortcutBrush))
            {
                _brush = shortcutBrush;
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            ResolveMapRootIfNeeded();

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _brush = (RoomCellType)EditorGUILayout.EnumPopup("Brush", _brush);
                _worldRegionTool = (WorldRegionTool)GUILayout.Toolbar(
                    (int)_worldRegionTool,
                    new[] { "Select", "New Room", "Replace", "Delete" });
            }
        }

        private void DrawBody()
        {
            EditorGUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                var gridPanelWidth = Mathf.Clamp(position.width * GridPanelRatio, MinGridPanelWidth, position.width - MinWorldPanelWidth);
                DrawGridPanel(gridPanelWidth);
                GUILayout.Space(10f);
                DrawWorldOverviewPanel();
            }
        }

        private void DrawGridPanel(float panelWidth)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(panelWidth)))
            {
                DrawRoomNameField();
                DrawGrid(panelWidth);
            }
        }

        private void DrawRoomNameField()
        {
            if (_room == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_roomNameDraft))
            {
                _roomNameDraft = _room.DisplayName;
            }

            var nextDraft = EditorGUILayout.TextField("Room Name", _roomNameDraft);
            if (nextDraft == _roomNameDraft)
            {
                return;
            }

            _roomNameDraft = nextDraft;

            var serializedRoom = new SerializedObject(_room);
            serializedRoom.Update();
            serializedRoom.FindProperty("displayName").stringValue = _roomNameDraft;
            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(_room);
        }

        private void DrawGrid(float panelWidth)
        {
            var availableWidth = Mathf.Max(200f, panelWidth - GridPadding * 2f - 8f);
            var cellSize = Mathf.Clamp(
                Mathf.Min(
                    availableWidth / MapRoomDefinition.RoomWidth,
                    (position.height - 220f) / MapRoomDefinition.RoomHeight),
                20f,
                42f);

            var gridWidth = MapRoomDefinition.RoomWidth * cellSize;
            var gridHeight = MapRoomDefinition.RoomHeight * cellSize;
            var gridRect = GUILayoutUtility.GetRect(gridWidth + GridPadding * 2f, gridHeight + GridPadding * 2f, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
            var contentRect = new Rect(gridRect.x + GridPadding, gridRect.y + GridPadding, gridWidth, gridHeight);

            EditorGUI.DrawRect(gridRect, new Color(0.1f, 0.12f, 0.12f, 1f));
            EditorGUI.DrawRect(contentRect, MapRoomEditorTheme.GridBackgroundColor);

            DrawGridCells(contentRect, cellSize);
            DrawGridLines(contentRect, cellSize);
            HandleGridInput(contentRect, cellSize);
        }

        private void DrawGridCells(Rect contentRect, float cellSize)
        {
            for (var displayRow = 0; displayRow < MapRoomDefinition.RoomHeight; displayRow++)
            {
                for (var x = 0; x < MapRoomDefinition.RoomWidth; x++)
                {
                    var roomY = MapRoomDefinition.RoomHeight - 1 - displayRow;
                    var type = _room.GetCellType(x, roomY);
                    if (type == RoomCellType.Empty)
                    {
                        continue;
                    }

                    EditorGUI.DrawRect(GetGridCellRect(contentRect, x, displayRow, cellSize), MapRoomEditorTheme.GetCellColor(type));
                }
            }

            if (TryGetGridCell(contentRect, Event.current.mousePosition, cellSize, out var hovered))
            {
                var displayRow = MapRoomDefinition.RoomHeight - 1 - hovered.y;
                EditorGUI.DrawRect(GetGridCellRect(contentRect, hovered.x, displayRow, cellSize), MapRoomEditorTheme.HoverColor);
            }
        }

        private static void DrawGridLines(Rect contentRect, float cellSize)
        {
            Handles.BeginGUI();
            Handles.color = MapRoomEditorTheme.GridLineColor;

            for (var x = 0; x <= MapRoomDefinition.RoomWidth; x++)
            {
                var lineX = contentRect.x + x * cellSize;
                Handles.DrawLine(new Vector3(lineX, contentRect.y), new Vector3(lineX, contentRect.yMax));
            }

            for (var y = 0; y <= MapRoomDefinition.RoomHeight; y++)
            {
                var lineY = contentRect.y + y * cellSize;
                Handles.DrawLine(new Vector3(contentRect.x, lineY), new Vector3(contentRect.xMax, lineY));
            }

            Handles.color = MapRoomEditorTheme.MajorGridLineColor;
            for (var x = 0; x <= MapRoomDefinition.RoomWidth; x += 5)
            {
                var lineX = contentRect.x + x * cellSize;
                Handles.DrawAAPolyLine(1.6f, new Vector3(lineX, contentRect.y), new Vector3(lineX, contentRect.yMax));
            }

            for (var y = 0; y <= MapRoomDefinition.RoomHeight; y += 5)
            {
                var lineY = contentRect.y + y * cellSize;
                Handles.DrawAAPolyLine(1.6f, new Vector3(contentRect.x, lineY), new Vector3(contentRect.xMax, lineY));
            }

            Handles.color = MapRoomEditorTheme.BorderLineColor;
            Handles.DrawAAPolyLine(
                2f,
                new Vector3(contentRect.x, contentRect.y),
                new Vector3(contentRect.xMax, contentRect.y),
                new Vector3(contentRect.xMax, contentRect.yMax),
                new Vector3(contentRect.x, contentRect.yMax),
                new Vector3(contentRect.x, contentRect.y));
            Handles.EndGUI();
        }

        private void HandleGridInput(Rect contentRect, float cellSize)
        {
            var currentEvent = Event.current;
            if (_room == null)
            {
                return;
            }

            if (currentEvent.type == EventType.MouseUp)
            {
                StopGridPainting();
                return;
            }

            if ((currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseDrag) || !contentRect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.button != 0 && currentEvent.button != 1)
            {
                return;
            }

            if (!TryGetGridCell(contentRect, currentEvent.mousePosition, cellSize, out var cell))
            {
                return;
            }

            if (currentEvent.type == EventType.MouseDown)
            {
                _isPainting = true;
                _isErasing = currentEvent.button == 1;
                _lastPaintedCell = new Vector2Int(-1, -1);
            }

            if (!_isPainting || _lastPaintedCell == cell)
            {
                currentEvent.Use();
                return;
            }

            var nextType = _isErasing ? RoomCellType.Empty : _brush;
            if (_room.GetCellType(cell.x, cell.y) != nextType)
            {
                Undo.RecordObject(_room, _isErasing ? "Erase Room Cell" : "Paint Room Cell");
                _room.SetCellType(cell.x, cell.y, nextType);
                EditorUtility.SetDirty(_room);
                RefreshAllMapPreviews();
            }

            _lastPaintedCell = cell;
            currentEvent.Use();
            Repaint();
        }

        private void StopGridPainting()
        {
            _isPainting = false;
            _lastPaintedCell = new Vector2Int(-1, -1);
        }

        private void DrawWorldOverviewPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                EditorGUILayout.LabelField("World Overview", EditorStyles.boldLabel);
                var overviewRect = GUILayoutUtility.GetRect(200f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawWorldOverview(overviewRect);
            }
        }

        private void DrawWorldOverview(Rect overviewRect)
        {
            EditorGUI.DrawRect(overviewRect, new Color(0.09f, 0.1f, 0.12f, 1f));
            DrawOutline(overviewRect, new Color(1f, 1f, 1f, 0.16f));

            if (_mapRoot == null || _mapRoot.Map == null || _mapRoot.Map.Rooms.Count == 0)
            {
                return;
            }

            var map = _mapRoot.Map;
            _selectedPlacementIndex = FindSelectedPlacementIndex(map, _room, _selectedPlacementIndex);
            if (!MapRoomEditorWorldOverviewUtility.TryGetWorldPreviewBounds(map, WorldOverviewRoomPadding, out var minGrid, out var maxGrid))
            {
                return;
            }

            var worldPreviewCellSize = MapRoomEditorWorldOverviewUtility.GetWorldPreviewCellSize(overviewRect);
            var contentRect = MapRoomEditorWorldOverviewUtility.GetWorldContentRect(overviewRect, minGrid, maxGrid, worldPreviewCellSize);

            InitializeWorldPanIfNeeded(overviewRect, contentRect, minGrid, worldPreviewCellSize);
            HandleWorldOverviewInput(overviewRect);

            var absoluteContentRect = new Rect(
                contentRect.x + _worldPan.x,
                contentRect.y + _worldPan.y,
                contentRect.width,
                contentRect.height);

            DrawWorldOverviewRooms(overviewRect, absoluteContentRect, map, minGrid, worldPreviewCellSize);
            HandleWorldRegionAction(map, minGrid, absoluteContentRect, worldPreviewCellSize);
        }

        private void DrawWorldOverviewRooms(Rect overviewRect, Rect absoluteContentRect, MapDefinition map, Vector2Int minGrid, float worldPreviewCellSize)
        {
            GUI.BeginClip(overviewRect);

            var localContentRect = new Rect(
                absoluteContentRect.x - overviewRect.x,
                absoluteContentRect.y - overviewRect.y,
                absoluteContentRect.width,
                absoluteContentRect.height);

            DrawOutline(
                new Rect(
                    localContentRect.x - WorldPreviewPadding * 0.5f,
                    localContentRect.y - WorldPreviewPadding * 0.5f,
                    localContentRect.width + WorldPreviewPadding,
                    localContentRect.height + WorldPreviewPadding),
                new Color(1f, 1f, 1f, 0.08f));

            for (var i = 0; i < map.Rooms.Count; i++)
            {
                var placement = map.GetRoom(i);
                if (placement.room == null)
                {
                    continue;
                }

                var localRoomRect = MapRoomEditorWorldOverviewUtility.GetWorldRoomRect(placement.gridPosition, minGrid, localContentRect, worldPreviewCellSize);
                DrawRoomMiniPreview(localRoomRect, placement.room, worldPreviewCellSize);

                var outlineColor = i == _selectedPlacementIndex
                    ? new Color(1f, 0.95f, 0.4f, 1f)
                    : new Color(1f, 1f, 1f, 0.14f);
                DrawOutline(localRoomRect, outlineColor);
            }

            if (_hasSelectedRegion)
            {
                var selectedRegionRect = MapRoomEditorWorldOverviewUtility.GetWorldRoomRect(_selectedRegionGridPosition, minGrid, localContentRect, worldPreviewCellSize);
                DrawOutline(selectedRegionRect, new Color(0.5f, 0.85f, 1f, 1f));
            }

            GUI.EndClip();
        }

        private void HandleWorldOverviewInput(Rect overviewRect)
        {
            var currentEvent = Event.current;
            if (!overviewRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.type == EventType.MouseUp && currentEvent.button == 1)
                {
                    _isPanningWorld = false;
                }

                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            {
                _isPanningWorld = true;
                _lastWorldPanMousePosition = currentEvent.mousePosition;
                currentEvent.Use();
                return;
            }

            if (currentEvent.type == EventType.MouseDrag && _isPanningWorld)
            {
                _worldPan += currentEvent.mousePosition - _lastWorldPanMousePosition;
                _lastWorldPanMousePosition = currentEvent.mousePosition;
                currentEvent.Use();
                Repaint();
                return;
            }

            if (currentEvent.type == EventType.MouseUp && currentEvent.button == 1)
            {
                _isPanningWorld = false;
                currentEvent.Use();
            }
        }

        private void HandleWorldRegionAction(MapDefinition map, Vector2Int minGrid, Rect absoluteContentRect, float worldPreviewCellSize)
        {
            var currentEvent = Event.current;
            if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
            {
                return;
            }

            if (!MapRoomEditorWorldOverviewUtility.TryGetWorldRegionGridPosition(absoluteContentRect, minGrid, worldPreviewCellSize, currentEvent.mousePosition, out var roomGridPosition))
            {
                return;
            }

            _hasSelectedRegion = true;
            _selectedRegionGridPosition = roomGridPosition;

            switch (_worldRegionTool)
            {
                case WorldRegionTool.Select:
                    SelectWorldRegion(map, roomGridPosition);
                    break;
                case WorldRegionTool.CreateNewRoom:
                    CreateRoomInRegion(map, roomGridPosition);
                    break;
                case WorldRegionTool.ReplaceWithActiveRoom:
                    ReplaceRoomInRegion(map, roomGridPosition);
                    break;
                case WorldRegionTool.DeleteRoom:
                    DeleteRoomInRegion(map, roomGridPosition);
                    break;
            }

            currentEvent.Use();
            Repaint();
        }

        private void SelectWorldRoom(int roomIndex, MapRoomDefinition room)
        {
            _selectedPlacementIndex = roomIndex;
            _room = room;
            _roomNameDraft = room != null ? room.DisplayName : string.Empty;

            if (_mapRoot != null)
            {
                _mapRoot.RoomToPlace = room;
                _mapRoot.RefreshPreview();
                Selection.activeGameObject = _mapRoot.gameObject;
            }
        }

        private void SelectWorldRegion(MapDefinition map, Vector2Int roomGridPosition)
        {
            _selectedPlacementIndex = -1;
            if (!map.TryGetRoomIndexAt(roomGridPosition, out var roomIndex))
            {
                return;
            }

            var placement = map.GetRoom(roomIndex);
            if (placement.room == null)
            {
                return;
            }

            SelectWorldRoom(roomIndex, placement.room);
        }

        private void CreateRoomInRegion(MapDefinition map, Vector2Int roomGridPosition)
        {
            if (_mapRoot == null)
            {
                return;
            }

            if (map.TryGetRoomIndexAt(roomGridPosition, out var existingIndex))
            {
                var existingPlacement = map.GetRoom(existingIndex);
                if (existingPlacement.room != null)
                {
                    SelectWorldRoom(existingIndex, existingPlacement.room);
                }

                return;
            }

            var createdRoom = MapAuthoringAssetUtility.CreateRoomAsset(_mapRoot, $"Room_{roomGridPosition.x}_{roomGridPosition.y}");
            if (createdRoom == null)
            {
                return;
            }

            Undo.RecordObject(map, "Create Room In Region");
            var roomIndex = map.AddRoom(createdRoom, roomGridPosition);
            EditorUtility.SetDirty(map);
            SelectWorldRoom(roomIndex, createdRoom);
        }

        private void ReplaceRoomInRegion(MapDefinition map, Vector2Int roomGridPosition)
        {
            if (_room == null)
            {
                return;
            }

            Undo.RecordObject(map, "Replace Room In Region");
            if (map.TryGetRoomIndexAt(roomGridPosition, out var roomIndex))
            {
                map.SetRoom(roomIndex, new MapRoomPlacement
                {
                    room = _room,
                    gridPosition = roomGridPosition
                });
                EditorUtility.SetDirty(map);
                SelectWorldRoom(roomIndex, _room);
                return;
            }

            var newIndex = map.AddRoom(_room, roomGridPosition);
            EditorUtility.SetDirty(map);
            SelectWorldRoom(newIndex, _room);
        }

        private void DeleteRoomInRegion(MapDefinition map, Vector2Int roomGridPosition)
        {
            if (!map.TryGetRoomIndexAt(roomGridPosition, out var roomIndex))
            {
                return;
            }

            Undo.RecordObject(map, "Delete Room In Region");
            map.RemoveRoomAt(roomGridPosition);
            EditorUtility.SetDirty(map);

            if (_selectedPlacementIndex == roomIndex)
            {
                _selectedPlacementIndex = -1;
            }

            if (_mapRoot != null)
            {
                _mapRoot.RefreshPreview();
            }
        }

        private bool TrySyncRoomFromSelection()
        {
            ResolveMapRootIfNeeded();

            if (Selection.activeObject is MapRoomDefinition selectedRoom)
            {
                if (_room != selectedRoom)
                {
                    _room = selectedRoom;
                    _roomNameDraft = selectedRoom.DisplayName;
                    _worldPanInitialized = false;
                }

                return true;
            }

            if (Selection.activeGameObject == null)
            {
                return false;
            }

            var root = Selection.activeGameObject.GetComponent<MapAuthoringRoot>();
            if (root == null || root.RoomToPlace == null)
            {
                return false;
            }

            _mapRoot = root;
            if (_room != root.RoomToPlace)
            {
                _room = root.RoomToPlace;
                _roomNameDraft = root.RoomToPlace.DisplayName;
                _worldPanInitialized = false;
            }

            return true;
        }

        private void ResolveMapRootIfNeeded()
        {
            if (_mapRoot != null)
            {
                return;
            }

            var roots = Object.FindObjectsByType<MapAuthoringRoot>(FindObjectsSortMode.None);
            if (roots.Length == 1)
            {
                _mapRoot = roots[0];
            }
        }

        private void InitializeWorldPanIfNeeded(Rect overviewRect, Rect contentRect, Vector2Int minGrid, float worldPreviewCellSize)
        {
            if (_worldPanInitialized)
            {
                return;
            }

            if (_mapRoot != null && _mapRoot.Map != null && _mapRoot.Map.IsValidRoomIndex(_selectedPlacementIndex))
            {
                var placement = _mapRoot.Map.GetRoom(_selectedPlacementIndex);
                var selectedRoomRect = MapRoomEditorWorldOverviewUtility.GetWorldRoomRect(placement.gridPosition, minGrid, contentRect, worldPreviewCellSize);
                _worldPan = overviewRect.center - selectedRoomRect.center;
            }
            else
            {
                _worldPan = overviewRect.center - contentRect.center;
            }

            _worldPanInitialized = true;
        }

        private static void DrawRoomMiniPreview(Rect rect, MapRoomDefinition room, float worldPreviewCellSize)
        {
            EditorGUI.DrawRect(rect, MapRoomEditorTheme.GridBackgroundColor);

            for (var displayRow = 0; displayRow < MapRoomDefinition.RoomHeight; displayRow++)
            {
                for (var x = 0; x < MapRoomDefinition.RoomWidth; x++)
                {
                    var roomY = MapRoomDefinition.RoomHeight - 1 - displayRow;
                    var type = room.GetCellType(x, roomY);
                    if (type == RoomCellType.Empty)
                    {
                        continue;
                    }

                    var cellRect = new Rect(
                        rect.x + x * worldPreviewCellSize,
                        rect.y + displayRow * worldPreviewCellSize,
                        worldPreviewCellSize,
                        worldPreviewCellSize);
                    EditorGUI.DrawRect(cellRect, MapRoomEditorTheme.GetCellColor(type));
                }
            }
        }

        private static int FindSelectedPlacementIndex(MapDefinition map, MapRoomDefinition room, int fallbackIndex)
        {
            if (room != null)
            {
                for (var i = 0; i < map.Rooms.Count; i++)
                {
                    if (map.GetRoom(i).room == room)
                    {
                        return i;
                    }
                }
            }

            return map.IsValidRoomIndex(fallbackIndex) ? fallbackIndex : -1;
        }

        private static Rect GetGridCellRect(Rect contentRect, int x, int displayRow, float cellSize)
        {
            return new Rect(contentRect.x + x * cellSize, contentRect.y + displayRow * cellSize, cellSize, cellSize);
        }

        private static bool TryGetGridCell(Rect contentRect, Vector2 mousePosition, float cellSize, out Vector2Int cell)
        {
            if (!contentRect.Contains(mousePosition))
            {
                cell = default;
                return false;
            }

            var localX = Mathf.FloorToInt((mousePosition.x - contentRect.x) / cellSize);
            var displayRow = Mathf.FloorToInt((mousePosition.y - contentRect.y) / cellSize);
            var roomY = MapRoomDefinition.RoomHeight - 1 - displayRow;

            if (localX < 0 || localX >= MapRoomDefinition.RoomWidth || roomY < 0 || roomY >= MapRoomDefinition.RoomHeight)
            {
                cell = default;
                return false;
            }

            cell = new Vector2Int(localX, roomY);
            return true;
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAPolyLine(
                1.5f,
                new Vector3(rect.x, rect.y),
                new Vector3(rect.xMax, rect.y),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.x, rect.yMax),
                new Vector3(rect.x, rect.y));
            Handles.EndGUI();
        }

        private static void RefreshAllMapPreviews()
        {
            var roots = Object.FindObjectsByType<MapAuthoringRoot>(FindObjectsSortMode.None);
            foreach (var root in roots)
            {
                root.RefreshPreview();
            }

            SceneView.RepaintAll();
        }
    }
}

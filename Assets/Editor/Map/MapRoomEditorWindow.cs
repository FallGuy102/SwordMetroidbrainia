using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    public sealed class MapRoomEditorWindow : EditorWindow
    {
        private const float GridPadding = 12f;
        private const float MinGridPanelWidth = 420f;
        private const float MinWorldPanelWidth = 220f;
        private const float WorldPreviewPadding = 16f;
        private const int WorldOverviewRoomPadding = 2;
        private const int NeighborContextCells = 6;
        private const float MinWorldPanelHeight = 180f;

        private MapRoomDefinition _room;
        private MapAuthoringRoot _mapRoot;
        private RoomCellType _brush = RoomCellType.Wall;
        private MapRoomEditorWorldRegionTool _worldRegionTool = MapRoomEditorWorldRegionTool.Select;
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
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            TrySyncRoomFromSelection();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnUndoRedoPerformed()
        {
            RefreshAllMapPreviews();
            Repaint();
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

            HandleBrushInput();
            DrawToolbar();

            if (_room == null)
            {
                EditorGUILayout.HelpBox("Select a room from the world overview, or select a map room asset in the Project view.", MessageType.Info);
                return;
            }

            DrawBody();
        }

        private void HandleBrushInput()
        {
            if (MapAuthoringBrushShortcutUtility.TryHandleBrushInput(Event.current, _brush, out var nextBrush))
            {
                _brush = nextBrush;
                Repaint();
            }
        }

        private void DrawToolbar()
        {
            ResolveMapRootIfNeeded();

            var action = MapRoomEditorToolbarUtility.Draw(
                _brush,
                _worldRegionTool,
                CanUseSelectedRoomAsTestEntrance(),
                out _brush,
                out _worldRegionTool);
            HandleToolbarAction(action);
        }

        private void HandleToolbarAction(MapRoomEditorToolbarAction action)
        {
            switch (action)
            {
                case MapRoomEditorToolbarAction.MovePlayerHere:
                    MovePlayerToSelectedRoom(false);
                    break;
                case MapRoomEditorToolbarAction.PlayHere:
                    MovePlayerToSelectedRoom(true);
                    break;
            }
        }

        private void DrawBody()
        {
            var bodyRect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (!MapRoomEditorLayoutUtility.TryGetMainLayout(bodyRect, MinGridPanelWidth, MinWorldPanelWidth, MinWorldPanelHeight, out var layout))
            {
                return;
            }

            DrawGridPanel(layout.GridRect);
            DrawWorldOverviewPanel(layout.WorldRect);
            DrawStatusBar(layout.StatusRect);
        }

        private void DrawGridPanel(Rect panelRect)
        {
            GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);
            var contentRect = MapRoomEditorLayoutUtility.InsetRect(panelRect, MapRoomEditorLayoutUtility.PanelInset);
            var headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, MapRoomEditorLayoutUtility.HeaderHeight);
            var gridRect = new Rect(contentRect.x, headerRect.yMax + MapRoomEditorLayoutUtility.PanelGap, contentRect.width, Mathf.Max(1f, contentRect.yMax - headerRect.yMax - MapRoomEditorLayoutUtility.PanelGap));

            DrawRoomHeader(headerRect);
            DrawGrid(gridRect);
        }

        private void DrawRoomHeader(Rect headerRect)
        {
            if (_room == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_roomNameDraft))
            {
                _roomNameDraft = _room.DisplayName;
            }

            var labelRect = new Rect(headerRect.x, headerRect.y + 2f, 42f, EditorGUIUtility.singleLineHeight);
            var fieldWidth = Mathf.Clamp(headerRect.width * 0.34f, 120f, 360f);
            var fieldRect = new Rect(labelRect.xMax + 6f, headerRect.y + 1f, Mathf.Min(fieldWidth, Mathf.Max(80f, headerRect.width - labelRect.width - 18f)), EditorGUIUtility.singleLineHeight);
            var infoRect = new Rect(fieldRect.xMax + 10f, headerRect.y + 2f, Mathf.Max(1f, headerRect.xMax - fieldRect.xMax - 10f), EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(labelRect, "Room", EditorStyles.boldLabel);
            var nextDraft = EditorGUI.TextField(fieldRect, _roomNameDraft);
            EditorGUI.LabelField(infoRect, "center room is editable, neighbors are reference only", EditorStyles.miniLabel);
            if (nextDraft == _roomNameDraft)
            {
                return;
            }

            _roomNameDraft = nextDraft;

            Undo.RecordObject(_room, "Rename Room");
            var serializedRoom = new SerializedObject(_room);
            serializedRoom.Update();
            serializedRoom.FindProperty("displayName").stringValue = _roomNameDraft;
            serializedRoom.ApplyModifiedProperties();
            EditorUtility.SetDirty(_room);
        }

        private void DrawGrid(Rect availableRect)
        {
            var gridContext = CreateGridContext();
            var cellSize = Mathf.Clamp(
                Mathf.Min(
                    (availableRect.width - GridPadding * 2f) / gridContext.TotalColumns,
                    (availableRect.height - GridPadding * 2f) / gridContext.TotalRows),
                6f,
                42f);

            var gridWidth = gridContext.TotalColumns * cellSize;
            var gridHeight = gridContext.TotalRows * cellSize;
            var gridRect = new Rect(
                availableRect.x + (availableRect.width - gridWidth) * 0.5f,
                availableRect.y + (availableRect.height - gridHeight) * 0.5f,
                gridWidth,
                gridHeight);
            var frameRect = new Rect(gridRect.x - GridPadding, gridRect.y - GridPadding, gridRect.width + GridPadding * 2f, gridRect.height + GridPadding * 2f);

            EditorGUI.DrawRect(frameRect, new Color(0.1f, 0.12f, 0.12f, 1f));
            EditorGUI.DrawRect(gridRect, MapRoomEditorTheme.GridBackgroundColor);

            DrawGridCells(gridRect, cellSize, gridContext);
            DrawGridLines(gridRect, cellSize, gridContext);
            HandleGridInput(gridRect, cellSize, gridContext.ContextMargin);
        }

        private void DrawGridCells(Rect contentRect, float cellSize, MapRoomEditorGridContext gridContext)
        {
            var currentRoomRect = MapRoomEditorGridGeometry.GetCurrentRoomRect(contentRect, cellSize, gridContext.ContextMargin);
            EditorGUI.DrawRect(currentRoomRect, new Color(0.18f, 0.23f, 0.24f, 0.42f));

            for (var contextY = -gridContext.ContextMargin; contextY < MapRoomDefinition.RoomHeight + gridContext.ContextMargin; contextY++)
            {
                for (var contextX = -gridContext.ContextMargin; contextX < MapRoomDefinition.RoomWidth + gridContext.ContextMargin; contextX++)
                {
                    if (!gridContext.TryGetCellType(contextX, contextY, out var type, out var isCurrentRoom))
                    {
                        continue;
                    }

                    if (type == RoomCellType.Empty)
                    {
                        continue;
                    }

                    var color = MapRoomEditorTheme.GetCellColor(type);
                    if (!isCurrentRoom)
                    {
                        color = Color.Lerp(MapRoomEditorTheme.GridBackgroundColor, color, 0.45f);
                    }

                    EditorGUI.DrawRect(MapRoomEditorGridGeometry.GetContextCellRect(contentRect, contextX, contextY, cellSize, gridContext.ContextMargin, gridContext.TotalRows), color);
                }
            }

            if (MapRoomEditorGridGeometry.TryGetEditableGridCell(contentRect, Event.current.mousePosition, cellSize, gridContext.ContextMargin, out var hovered))
            {
                EditorGUI.DrawRect(MapRoomEditorGridGeometry.GetContextCellRect(contentRect, hovered.x, hovered.y, cellSize, gridContext.ContextMargin, gridContext.TotalRows), MapRoomEditorTheme.HoverColor);
            }
        }

        private static void DrawGridLines(Rect contentRect, float cellSize, MapRoomEditorGridContext gridContext)
        {
            Handles.BeginGUI();
            Handles.color = MapRoomEditorTheme.GridLineColor;

            for (var x = 0; x <= gridContext.TotalColumns; x++)
            {
                var lineX = contentRect.x + x * cellSize;
                Handles.DrawLine(new Vector3(lineX, contentRect.y), new Vector3(lineX, contentRect.yMax));
            }

            for (var y = 0; y <= gridContext.TotalRows; y++)
            {
                var lineY = contentRect.y + y * cellSize;
                Handles.DrawLine(new Vector3(contentRect.x, lineY), new Vector3(contentRect.xMax, lineY));
            }

            Handles.color = MapRoomEditorTheme.MajorGridLineColor;
            for (var x = 0; x <= gridContext.TotalColumns; x += 5)
            {
                var lineX = contentRect.x + x * cellSize;
                Handles.DrawAAPolyLine(1.6f, new Vector3(lineX, contentRect.y), new Vector3(lineX, contentRect.yMax));
            }

            for (var y = 0; y <= gridContext.TotalRows; y += 5)
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

            var currentRoomRect = MapRoomEditorGridGeometry.GetCurrentRoomRect(contentRect, cellSize, gridContext.ContextMargin);
            Handles.color = new Color(1f, 0.98f, 0.76f, 0.95f);
            Handles.DrawAAPolyLine(
                2.5f,
                new Vector3(currentRoomRect.x, currentRoomRect.y),
                new Vector3(currentRoomRect.xMax, currentRoomRect.y),
                new Vector3(currentRoomRect.xMax, currentRoomRect.yMax),
                new Vector3(currentRoomRect.x, currentRoomRect.yMax),
                new Vector3(currentRoomRect.x, currentRoomRect.y));
            Handles.EndGUI();
        }

        private void HandleGridInput(Rect contentRect, float cellSize, int contextMargin)
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

            if ((currentEvent.type != EventType.MouseDown && currentEvent.type != EventType.MouseDrag) || !MapRoomEditorGridGeometry.GetCurrentRoomRect(contentRect, cellSize, contextMargin).Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.button != 0 && currentEvent.button != 1)
            {
                return;
            }

            if (!MapRoomEditorGridGeometry.TryGetEditableGridCell(contentRect, currentEvent.mousePosition, cellSize, contextMargin, out var cell))
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

        private void DrawWorldOverviewPanel(Rect panelRect)
        {
            GUI.Box(panelRect, GUIContent.none, EditorStyles.helpBox);
            var contentRect = MapRoomEditorLayoutUtility.InsetRect(panelRect, MapRoomEditorLayoutUtility.PanelInset);
            var headerRect = new Rect(contentRect.x, contentRect.y + 2f, contentRect.width, EditorGUIUtility.singleLineHeight);
            var overviewRect = new Rect(contentRect.x, headerRect.yMax + MapRoomEditorLayoutUtility.PanelGap, contentRect.width, Mathf.Max(1f, contentRect.yMax - headerRect.yMax - MapRoomEditorLayoutUtility.PanelGap));

            EditorGUI.LabelField(headerRect, "World Overview", EditorStyles.boldLabel);
            DrawWorldOverview(overviewRect);
        }

        private void DrawStatusBar(Rect statusRect)
        {
            EditorGUI.DrawRect(statusRect, new Color(0.11f, 0.12f, 0.13f, 1f));
            DrawOutline(statusRect, new Color(1f, 1f, 1f, 0.12f));

            var roomName = _room != null ? _room.DisplayName : "No Room";
            var status = $"Room: {roomName}    Brush: {_brush}    World Tool: {_worldRegionTool}    1-7 brush shortcuts, left paint, right erase";
            var labelRect = new Rect(statusRect.x + 8f, statusRect.y + 3f, statusRect.width - 16f, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(labelRect, status, EditorStyles.miniLabel);
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
                case MapRoomEditorWorldRegionTool.Select:
                    SelectWorldRegion(map, roomGridPosition);
                    break;
                case MapRoomEditorWorldRegionTool.CreateNewRoom:
                    CreateRoomInRegion(map, roomGridPosition);
                    break;
                case MapRoomEditorWorldRegionTool.ReplaceWithActiveRoom:
                    ReplaceRoomInRegion(map, roomGridPosition);
                    break;
                case MapRoomEditorWorldRegionTool.DeleteRoom:
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

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Room In Region");

            var createdRoom = MapAuthoringAssetUtility.CreateRoomAsset(_mapRoot, $"Room_{roomGridPosition.x}_{roomGridPosition.y}");
            if (createdRoom == null)
            {
                Undo.CollapseUndoOperations(undoGroup);
                return;
            }

            Undo.RecordObject(map, "Create Room In Region");
            var roomIndex = map.AddRoom(createdRoom, roomGridPosition);
            EditorUtility.SetDirty(map);
            SelectWorldRoom(roomIndex, createdRoom);
            Undo.CollapseUndoOperations(undoGroup);
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

        private bool CanUseSelectedRoomAsTestEntrance()
        {
            return _mapRoot != null
                && _mapRoot.Map != null
                && _room != null
                && TryGetActiveRoomGridPosition(out _);
        }

        private void MovePlayerToSelectedRoom(bool enterPlayMode)
        {
            if (!TryGetActiveRoomGridPosition(out var roomGridPosition))
            {
                return;
            }

            MapRoomEditorTestEntranceUtility.TryMovePlayerToRoom(_mapRoot, _room, roomGridPosition, enterPlayMode);
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

        private MapRoomEditorGridContext CreateGridContext()
        {
            var hasWorldContext = TryGetActiveRoomGridPosition(out var activeRoomGridPosition);
            return new MapRoomEditorGridContext(
                _mapRoot != null ? _mapRoot.Map : null,
                _room,
                activeRoomGridPosition,
                hasWorldContext ? NeighborContextCells : 0,
                hasWorldContext);
        }

        private bool TryGetActiveRoomGridPosition(out Vector2Int gridPosition)
        {
            gridPosition = default;
            var map = _mapRoot != null ? _mapRoot.Map : null;
            if (map == null || _room == null)
            {
                return false;
            }

            if (map.IsValidRoomIndex(_selectedPlacementIndex))
            {
                var selectedPlacement = map.GetRoom(_selectedPlacementIndex);
                if (selectedPlacement.room == _room)
                {
                    gridPosition = selectedPlacement.gridPosition;
                    return true;
                }
            }

            for (var i = 0; i < map.Rooms.Count; i++)
            {
                var placement = map.GetRoom(i);
                if (placement.room != _room)
                {
                    continue;
                }

                _selectedPlacementIndex = i;
                gridPosition = placement.gridPosition;
                return true;
            }

            return false;
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

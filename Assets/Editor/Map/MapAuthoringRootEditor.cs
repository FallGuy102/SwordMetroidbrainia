using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    [CustomEditor(typeof(MapAuthoringRoot))]
    public sealed class MapAuthoringRootEditor : UnityEditor.Editor
    {
        private readonly MapAuthoringEditorState _state = new();
        private SerializedProperty _mapProperty;
        private SerializedProperty _cellSizeProperty;
        private SerializedProperty _roomAssetFolderProperty;

        private void OnEnable()
        {
            _mapProperty = serializedObject.FindProperty("map");
            _cellSizeProperty = serializedObject.FindProperty("cellSize");
            _roomAssetFolderProperty = serializedObject.FindProperty("roomAssetFolder");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawBaseInspector();
            serializedObject.ApplyModifiedProperties();

            var root = (MapAuthoringRoot)target;
            var map = root.Map;
            if (map == null)
            {
                EditorGUILayout.HelpBox("Assign a MapDefinition asset. Then use Scene view to place and paint rooms.", MessageType.Info);
                return;
            }

            DrawSceneEditingInspector(root, map);
            DrawSelectedRoomInspector(root, map);
            DrawSceneHelpBox();
        }

        private void OnSceneGUI()
        {
            var root = (MapAuthoringRoot)target;
            var map = root.Map;
            if (map == null)
            {
                return;
            }

            MapAuthoringSceneUtility.CaptureSceneInput();
            MapAuthoringSceneUtility.DrawRooms(root, map, _state.SelectedRoomIndex);
            MapAuthoringSceneUtility.HandleSceneInput(root, map, _state, Repaint);
        }

        private void DrawSceneEditingInspector(MapAuthoringRoot root, MapDefinition map)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Editing", EditorStyles.boldLabel);
            _state.EditCells = EditorGUILayout.Toggle("Edit Cells", _state.EditCells);
            _state.Brush = (RoomCellType)EditorGUILayout.EnumPopup("Brush", _state.Brush);
            DrawActiveRoomInspector(root, map);

            DrawRoomCreationInspector(root);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _state.SelectedRoomIndex >= 0 && map.IsValidRoomIndex(_state.SelectedRoomIndex);
                if (GUILayout.Button("Remove Selected Placement"))
                {
                    var placement = map.GetRoom(_state.SelectedRoomIndex);
                    Undo.RecordObject(map, "Remove Room Placement");
                    map.RemoveRoomAt(placement.gridPosition);
                    _state.SelectedRoomIndex = -1;
                    EditorUtility.SetDirty(map);
                    MapAuthoringSceneUtility.ClearSelectedRoomHighlight(root);
                    RefreshEditor(root);
                }

                GUI.enabled = true;
            }
        }

        private void DrawActiveRoomInspector(MapAuthoringRoot root, MapDefinition map)
        {
            var activeRoom = GetActiveRoomDefinition(root, map);
            var nextActiveRoom = (MapRoomDefinition)EditorGUILayout.ObjectField("Active Room", activeRoom, typeof(MapRoomDefinition), false);
            if (nextActiveRoom != root.RoomToPlace)
            {
                Undo.RecordObject(root, "Change Active Room");
                root.RoomToPlace = nextActiveRoom;
                EditorUtility.SetDirty(root);
                RefreshEditor(root);
            }

            if (activeRoom == null)
            {
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Ping Definition"))
                {
                    EditorGUIUtility.PingObject(activeRoom);
                }

                if (GUILayout.Button("Select Definition"))
                {
                    Selection.activeObject = activeRoom;
                }
            }
        }

        private void DrawRoomCreationInspector(MapAuthoringRoot root)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Room Asset Creation", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pick Folder", GUILayout.Width(100f)))
                {
                    MapAuthoringAssetUtility.PickRoomFolder(root);
                }

                if (GUILayout.Button("Open Folder", GUILayout.Width(100f)))
                {
                    MapAuthoringAssetUtility.OpenRoomFolder(root);
                }
            }

            _state.NewRoomName = EditorGUILayout.TextField("New Room Name", _state.NewRoomName);

            if (!GUILayout.Button("Create Room Asset"))
            {
                return;
            }

            var createdRoom = MapAuthoringAssetUtility.CreateRoomAsset(root, _state.NewRoomName);
            if (createdRoom == null)
            {
                return;
            }

            Undo.RecordObject(root, "Assign Active Room");
            root.RoomToPlace = createdRoom;
            EditorUtility.SetDirty(root);
            RefreshEditor(root);
            Selection.activeObject = createdRoom;
            EditorGUIUtility.PingObject(createdRoom);
            Repaint();
        }

        private void DrawSelectedRoomInspector(MapAuthoringRoot root, MapDefinition map)
        {
            if (_state.SelectedRoomIndex < 0 || !map.IsValidRoomIndex(_state.SelectedRoomIndex))
            {
                return;
            }

            var placement = map.GetRoom(_state.SelectedRoomIndex);
            var room = placement.room;
            EditorGUILayout.LabelField("Selected Room", room != null ? room.DisplayName : "Missing");
            EditorGUILayout.LabelField("Room Grid", placement.gridPosition.ToString());
            if (room == null)
            {
                return;
            }

            var toggled = EditorGUILayout.Toggle("Reveal On Map", room.ShowOnMapWhenDiscovered);
            if (toggled == room.ShowOnMapWhenDiscovered)
            {
                return;
            }

            Undo.RecordObject(room, "Toggle Room Map Visibility");
            room.ShowOnMapWhenDiscovered = toggled;
            EditorUtility.SetDirty(room);
            RefreshEditor(root);
        }

        private void DrawBaseInspector()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((MapAuthoringRoot)target), typeof(MapAuthoringRoot), false);
            }

            EditorGUILayout.PropertyField(_mapProperty, new GUIContent("Map"));
            EditorGUILayout.PropertyField(_cellSizeProperty, new GUIContent("Cell Size"));
            EditorGUILayout.PropertyField(_roomAssetFolderProperty, new GUIContent("Room Asset Folder"));
        }

        private static void DrawSceneHelpBox()
        {
            EditorGUILayout.HelpBox(
                "Scene controls:\n" +
                "Left click placed room: select room placement and sync its definition\n" +
                "Shift + Left click empty room space: place the active room asset\n" +
                "Left click inside the selected room: paint cells\n" +
                "Ctrl + Left click inside the selected room: erase cells\n" +
                "1 Empty, 2 Wall, 3 Ground, 4 OneWayPlatform\n" +
                "Remove placements from the inspector button",
                MessageType.None);
        }

        private static void RefreshEditor(MapAuthoringRoot root)
        {
            root.RefreshPreview();
            SceneView.RepaintAll();
        }

        private MapRoomDefinition GetActiveRoomDefinition(MapAuthoringRoot root, MapDefinition map)
        {
            if (_state.SelectedRoomIndex >= 0 && map.IsValidRoomIndex(_state.SelectedRoomIndex))
            {
                return map.GetRoom(_state.SelectedRoomIndex).room;
            }

            return root.RoomToPlace;
        }
    }

    internal sealed class MapAuthoringEditorState
    {
        private const string DefaultNewRoomName = "NewRoom";

        public RoomCellType Brush { get; set; } = RoomCellType.Wall;
        public int SelectedRoomIndex { get; set; } = -1;
        public bool EditCells { get; set; } = true;
        public string NewRoomName { get; set; } = DefaultNewRoomName;
        public bool IsPaintingCells { get; set; }
        public int PaintingRoomIndex { get; set; } = -1;
        public bool IsErasingCells { get; set; }

        public void StopPainting()
        {
            IsPaintingCells = false;
            PaintingRoomIndex = -1;
            IsErasingCells = false;
        }
    }
}

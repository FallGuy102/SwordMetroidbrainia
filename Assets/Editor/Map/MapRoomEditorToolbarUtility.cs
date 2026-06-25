using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapRoomEditorToolbarUtility
    {
        private static readonly string[] WorldToolLabels = { "Select", "New Room", "Replace", "Delete" };

        public static MapRoomEditorToolbarAction Draw(
            RoomCellType currentBrush,
            MapRoomEditorWorldRegionTool currentWorldTool,
            bool canUseTestEntrance,
            out RoomCellType nextBrush,
            out MapRoomEditorWorldRegionTool nextWorldTool)
        {
            var action = MapRoomEditorToolbarAction.None;
            nextBrush = currentBrush;
            nextWorldTool = currentWorldTool;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Brush", GUILayout.Width(42f));
                nextBrush = (RoomCellType)EditorGUILayout.EnumPopup(currentBrush, GUILayout.Width(180f));

                GUILayout.Space(12f);
                GUILayout.Label("World", GUILayout.Width(42f));
                nextWorldTool = (MapRoomEditorWorldRegionTool)GUILayout.Toolbar(
                    (int)currentWorldTool,
                    WorldToolLabels,
                    EditorStyles.toolbarButton,
                    GUILayout.MinWidth(300f));

                GUILayout.Space(12f);
                using (new EditorGUI.DisabledScope(!canUseTestEntrance))
                {
                    if (GUILayout.Button("Move Player Here", EditorStyles.toolbarButton, GUILayout.Width(112f)))
                    {
                        action = MapRoomEditorToolbarAction.MovePlayerHere;
                    }

                    if (GUILayout.Button("Play Here", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                    {
                        action = MapRoomEditorToolbarAction.PlayHere;
                    }
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label("1-7 / Wheel: Brush  |  RMB drag overview", EditorStyles.miniLabel);
            }

            return action;
        }
    }
}

using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapAuthoringBrushShortcutUtility
    {
        public static bool TryGetBrushShortcut(Event currentEvent, out RoomCellType brush)
        {
            brush = default;
            if (currentEvent.type != EventType.KeyDown || EditorGUIUtility.editingTextField)
            {
                return false;
            }

            var handled = currentEvent.keyCode switch
            {
                KeyCode.Alpha1 => TryAssignBrush(RoomCellType.Empty, out brush),
                KeyCode.Alpha2 => TryAssignBrush(RoomCellType.Wall, out brush),
                KeyCode.Alpha3 => TryAssignBrush(RoomCellType.Ground, out brush),
                KeyCode.Alpha4 => TryAssignBrush(RoomCellType.OneWayPlatform, out brush),
                _ => false
            };

            if (handled)
            {
                currentEvent.Use();
            }

            return handled;
        }

        private static bool TryAssignBrush(RoomCellType nextBrush, out RoomCellType brush)
        {
            brush = nextBrush;
            return true;
        }
    }
}

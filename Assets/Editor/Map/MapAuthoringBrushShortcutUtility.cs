using SwordMetroidbrainia.Map;
using UnityEditor;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapAuthoringBrushShortcutUtility
    {
        public static bool TryHandleBrushInput(Event currentEvent, RoomCellType currentBrush, out RoomCellType brush)
        {
            if (TryGetBrushShortcut(currentEvent, out brush))
            {
                return true;
            }

            if (TryGetScrollBrush(currentEvent, currentBrush, out brush))
            {
                return true;
            }

            brush = currentBrush;
            return false;
        }

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
                KeyCode.Alpha5 => TryAssignBrush(RoomCellType.Death, out brush),
                KeyCode.Alpha6 => TryAssignBrush(RoomCellType.SavePoint, out brush),
                KeyCode.Alpha7 => TryAssignBrush(RoomCellType.Breakable, out brush),
                _ => false
            };

            if (handled)
            {
                currentEvent.Use();
            }

            return handled;
        }

        private static bool TryGetScrollBrush(Event currentEvent, RoomCellType currentBrush, out RoomCellType brush)
        {
            brush = currentBrush;
            if (currentEvent.type != EventType.ScrollWheel || EditorGUIUtility.editingTextField)
            {
                return false;
            }

            brush = CycleBrush(currentBrush, currentEvent.delta.y > 0f ? 1 : -1);
            currentEvent.Use();
            return true;
        }

        private static RoomCellType CycleBrush(RoomCellType currentBrush, int direction)
        {
            var brushes = (RoomCellType[])System.Enum.GetValues(typeof(RoomCellType));
            var currentIndex = System.Array.IndexOf(brushes, currentBrush);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            var nextIndex = (currentIndex + direction) % brushes.Length;
            if (nextIndex < 0)
            {
                nextIndex += brushes.Length;
            }

            return brushes[nextIndex];
        }

        private static bool TryAssignBrush(RoomCellType nextBrush, out RoomCellType brush)
        {
            brush = nextBrush;
            return true;
        }
    }
}

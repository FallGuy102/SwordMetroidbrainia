using SwordMetroidbrainia.Map;
using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapRoomEditorTheme
    {
        public static readonly Color GridBackgroundColor = new(0.16f, 0.2f, 0.21f, 1f);
        public static readonly Color GridLineColor = new(0.86f, 0.91f, 0.92f, 0.18f);
        public static readonly Color BorderLineColor = new(1f, 1f, 1f, 0.7f);
        public static readonly Color MajorGridLineColor = new(0.92f, 0.97f, 0.98f, 0.09f);
        public static readonly Color WallColor = new(0.36f, 0.37f, 0.46f, 1f);
        public static readonly Color GroundColor = new(0.7f, 0.46f, 0.22f, 1f);
        public static readonly Color OneWayPlatformColor = new(0.95f, 0.76f, 0.28f, 1f);
        public static readonly Color DeathColor = new(0.82f, 0.18f, 0.24f, 1f);
        public static readonly Color SavePointColor = new(0.6f, 0.8f, 0.96f, 1f);
        public static readonly Color HoverColor = new(1f, 1f, 1f, 0.1f);

        public static Color GetCellColor(RoomCellType type)
        {
            return type switch
            {
                RoomCellType.Wall => WallColor,
                RoomCellType.Ground => GroundColor,
                RoomCellType.OneWayPlatform => OneWayPlatformColor,
                RoomCellType.Death => DeathColor,
                RoomCellType.SavePoint => SavePointColor,
                _ => Color.clear
            };
        }
    }
}

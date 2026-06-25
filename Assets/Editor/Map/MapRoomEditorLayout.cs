using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal readonly struct MapRoomEditorLayout
    {
        public MapRoomEditorLayout(Rect gridRect, Rect worldRect, Rect statusRect)
        {
            GridRect = gridRect;
            WorldRect = worldRect;
            StatusRect = statusRect;
        }

        public Rect GridRect { get; }
        public Rect WorldRect { get; }
        public Rect StatusRect { get; }
    }
}

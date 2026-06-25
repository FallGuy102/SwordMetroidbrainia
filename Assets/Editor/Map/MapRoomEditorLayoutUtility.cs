using UnityEngine;

namespace SwordMetroidbrainia.Editor.Map
{
    internal static class MapRoomEditorLayoutUtility
    {
        public const float PanelGap = 8f;
        public const float PanelInset = 8f;
        public const float HeaderHeight = 24f;
        public const float StatusBarHeight = 22f;

        private const float StackedLayoutWidth = 980f;
        private const float StackedWorldHeightRatio = 0.34f;
        private const float MaxStackedWorldHeightRatio = 0.48f;

        public static bool TryGetMainLayout(
            Rect bodyRect,
            float minGridPanelWidth,
            float minWorldPanelWidth,
            float minWorldPanelHeight,
            out MapRoomEditorLayout layout)
        {
            layout = default;
            if (bodyRect.width <= 1f || bodyRect.height <= StatusBarHeight + PanelGap)
            {
                return false;
            }

            var statusRect = new Rect(bodyRect.x, bodyRect.yMax - StatusBarHeight, bodyRect.width, StatusBarHeight);
            var contentRect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, bodyRect.height - StatusBarHeight - PanelGap);

            if (ShouldUseStackedLayout(contentRect))
            {
                var worldHeight = Mathf.Clamp(contentRect.height * StackedWorldHeightRatio, minWorldPanelHeight, contentRect.height * MaxStackedWorldHeightRatio);
                var gridRect = new Rect(contentRect.x, contentRect.y, contentRect.width, contentRect.height - worldHeight - PanelGap);
                var worldRect = new Rect(contentRect.x, gridRect.yMax + PanelGap, contentRect.width, worldHeight);
                layout = new MapRoomEditorLayout(gridRect, worldRect, statusRect);
                return true;
            }

            var worldWidth = Mathf.Clamp(contentRect.width * 0.38f, minWorldPanelWidth, contentRect.width - minGridPanelWidth - PanelGap);
            var horizontalGridRect = new Rect(contentRect.x, contentRect.y, contentRect.width - worldWidth - PanelGap, contentRect.height);
            var horizontalWorldRect = new Rect(horizontalGridRect.xMax + PanelGap, contentRect.y, worldWidth, contentRect.height);
            layout = new MapRoomEditorLayout(horizontalGridRect, horizontalWorldRect, statusRect);
            return true;
        }

        public static Rect InsetRect(Rect rect, float inset)
        {
            return new Rect(rect.x + inset, rect.y + inset, Mathf.Max(1f, rect.width - inset * 2f), Mathf.Max(1f, rect.height - inset * 2f));
        }

        private static bool ShouldUseStackedLayout(Rect layoutRect)
        {
            return layoutRect.width < StackedLayoutWidth || layoutRect.height > layoutRect.width * 0.78f;
        }
    }
}

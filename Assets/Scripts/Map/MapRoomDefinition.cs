using System;
using System.Collections.Generic;
using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    [CreateAssetMenu(menuName = "SwordMetroidbrainia/Map/Room Definition", fileName = "MapRoomDefinition")]
    public sealed class MapRoomDefinition : ScriptableObject
    {
        public const int RoomWidth = 20;
        public const int RoomHeight = 15;
        public const int MinimapPixelsPerCell = 4;
        public const int MinimapWidth = RoomWidth * MinimapPixelsPerCell;
        public const int MinimapHeight = RoomHeight * MinimapPixelsPerCell;

        [SerializeField] private string roomId;
        [SerializeField] private string displayName;
        [SerializeField] private bool showOnMapWhenDiscovered;
        [SerializeField] private RoomCellData[] cells = Array.Empty<RoomCellData>();
        [SerializeField] private Color32[] minimapPixels = Array.Empty<Color32>();

        public string RoomId => roomId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public bool ShowOnMapWhenDiscovered
        {
            get => showOnMapWhenDiscovered;
            set => showOnMapWhenDiscovered = value;
        }
        public IReadOnlyList<Color32> MinimapPixels => minimapPixels;

        public RoomCellType GetCellType(int x, int y)
        {
            EnsureCells();
            if (x < 0 || x >= RoomWidth || y < 0 || y >= RoomHeight)
            {
                return RoomCellType.Empty;
            }

            return cells[y * RoomWidth + x].type;
        }

        public void SetCellType(int x, int y, RoomCellType type)
        {
            EnsureCells();
            if (x < 0 || x >= RoomWidth || y < 0 || y >= RoomHeight)
            {
                return;
            }

            cells[y * RoomWidth + x].type = type;
            RebuildMinimapCache();
        }

        public void Fill(RoomCellType type)
        {
            EnsureCells();
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].type = type;
            }

            RebuildMinimapCache();
        }

        public void Clear()
        {
            Fill(RoomCellType.Empty);
        }

        public void EnsureCells()
        {
            var expectedLength = RoomWidth * RoomHeight;
            if (cells != null && cells.Length == expectedLength)
            {
                return;
            }

            var resized = new RoomCellData[expectedLength];
            if (cells != null)
            {
                Array.Copy(cells, resized, Mathf.Min(cells.Length, resized.Length));
            }

            cells = resized;
        }

        public void RebuildMinimapCache()
        {
            EnsureCells();
            var expectedLength = MinimapWidth * MinimapHeight;
            if (minimapPixels == null || minimapPixels.Length != expectedLength)
            {
                minimapPixels = new Color32[expectedLength];
            }

            for (var y = 0; y < RoomHeight; y++)
            {
                for (var x = 0; x < RoomWidth; x++)
                {
                    DrawMinimapCell(x, y, cells[y * RoomWidth + x].type);
                }
            }
        }

        public Texture2D CreateMinimapTexture(FilterMode filterMode = FilterMode.Point)
        {
            EnsureMinimapCache();
            var texture = new Texture2D(MinimapWidth, MinimapHeight, TextureFormat.RGBA32, false)
            {
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(minimapPixels);
            texture.Apply(false, false);
            return texture;
        }

        private void EnsureMinimapCache()
        {
            if (minimapPixels == null || minimapPixels.Length != MinimapWidth * MinimapHeight)
            {
                RebuildMinimapCache();
            }
        }

        private void DrawMinimapCell(int cellX, int cellY, RoomCellType type)
        {
            var baseX = cellX * MinimapPixelsPerCell;
            var baseY = cellY * MinimapPixelsPerCell;
            FillMinimapBlock(baseX, baseY, MinimapPixelsPerCell, MinimapPixelsPerCell, GetMinimapColor(RoomCellType.Empty));

            switch (type)
            {
                case RoomCellType.OneWayPlatform:
                    FillMinimapBlock(baseX, baseY + MinimapPixelsPerCell / 2, MinimapPixelsPerCell, MinimapPixelsPerCell / 2, GetMinimapColor(type));
                    break;
                case RoomCellType.SavePoint:
                    FillMinimapBlock(baseX + 1, baseY + 1, MinimapPixelsPerCell - 2, MinimapPixelsPerCell - 2, GetMinimapColor(type));
                    break;
                case RoomCellType.Empty:
                    break;
                default:
                    FillMinimapBlock(baseX, baseY, MinimapPixelsPerCell, MinimapPixelsPerCell, GetMinimapColor(type));
                    break;
            }
        }

        private void FillMinimapBlock(int startX, int startY, int width, int height, Color32 color)
        {
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    minimapPixels[(startY + y) * MinimapWidth + startX + x] = color;
                }
            }
        }

        private static Color32 GetMinimapColor(RoomCellType type)
        {
            return type switch
            {
                RoomCellType.Wall => new Color32(74, 76, 88, 255),
                RoomCellType.Ground => new Color32(166, 104, 45, 255),
                RoomCellType.OneWayPlatform => new Color32(224, 174, 54, 255),
                RoomCellType.Death => new Color32(208, 38, 58, 255),
                RoomCellType.SavePoint => new Color32(94, 198, 245, 255),
                _ => new Color32(28, 43, 43, 255)
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                roomId = name.Replace(" ", "_").ToLowerInvariant();
            }

            EnsureCells();
            RebuildMinimapCache();
        }
#endif
    }
}

using System;
using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    [CreateAssetMenu(menuName = "SwordMetroidbrainia/Map/Room Definition", fileName = "MapRoomDefinition")]
    public sealed class MapRoomDefinition : ScriptableObject
    {
        public const int RoomWidth = 20;
        public const int RoomHeight = 15;

        [SerializeField] private string roomId;
        [SerializeField] private string displayName;
        [SerializeField] private bool showOnMapWhenDiscovered;
        [SerializeField] private RoomCellData[] cells = Array.Empty<RoomCellData>();

        public string RoomId => roomId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public bool ShowOnMapWhenDiscovered
        {
            get => showOnMapWhenDiscovered;
            set => showOnMapWhenDiscovered = value;
        }

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
        }

        public void Fill(RoomCellType type)
        {
            EnsureCells();
            for (var i = 0; i < cells.Length; i++)
            {
                cells[i].type = type;
            }
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

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                roomId = name.Replace(" ", "_").ToLowerInvariant();
            }

            EnsureCells();
        }
#endif
    }
}

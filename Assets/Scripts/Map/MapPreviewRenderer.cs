using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SwordMetroidbrainia.Map
{
    [ExecuteAlways]
    [RequireComponent(typeof(MapAuthoringRoot))]
    public sealed class MapPreviewRenderer : MonoBehaviour
    {
        private const string PreviewRootName = "__MapPreview";

        [SerializeField] private bool showInEditor = true;
        [SerializeField] private bool showInGame = true;
        [SerializeField] private Color roomTint = new(0.2f, 0.45f, 0.35f, 0.18f);
        [SerializeField] private Color wallColor = new(0.22f, 0.22f, 0.22f, 1f);
        [SerializeField] private Color groundColor = new(0.56f, 0.36f, 0.18f, 1f);
        [SerializeField] private Color oneWayPlatformColor = new(0.82f, 0.52f, 0.2f, 1f);
        [SerializeField] private Color deathCellColor = new(0.82f, 0.16f, 0.24f, 1f);
        [SerializeField] private Color savePointColor = new(0.62f, 0.78f, 0.96f, 1f);
        [SerializeField] private Color editorBreakableColor = new(0.42f, 0.24f, 0.13f, 1f);
        [SerializeField] private Color gameBreakableColor = new(0.72f, 0.5f, 0.32f, 1f);
        [SerializeField] private int sortingOrder = -20;
        [SerializeField, Range(0f, 0.45f)] private float cellVisualInset = 0.06f;
        [SerializeField] private bool generateSolidColliders = true;
        [SerializeField] private string solidSortingLayerName = "Default";

        private readonly Dictionary<Transform, Dictionary<Vector2Int, BreakableCellMarker>> _breakableCells = new();

        private MapAuthoringRoot _root;
        private Transform _previewRoot;
        private Sprite _whiteSprite;
        private Sprite _roundedSquareSprite;
        private bool _queuedEditorRebuild;
#if UNITY_EDITOR
        private bool _hasEditorSelectedRoom;
        private Vector2Int _editorSelectedRoomGridPosition;
#endif

        private void Awake()
        {
            _root = GetComponent<MapAuthoringRoot>();
        }

        private void OnEnable()
        {
            EnsureDependencies();
            Rebuild();
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        private void OnValidate()
        {
            EnsureDependencies();
#if UNITY_EDITOR
            QueueEditorRebuild();
#else
            Rebuild();
#endif
        }

        public void Rebuild()
        {
            EnsureDependencies();
            ClearPreview();

            if (_root == null || _root.Map == null)
            {
                return;
            }

            var shouldShow = Application.isPlaying ? showInGame : showInEditor;
            if (!shouldShow)
            {
                return;
            }

            EnsurePreviewRoot();

            for (var i = 0; i < _root.Map.Rooms.Count; i++)
            {
                var placement = _root.Map.GetRoom(i);
                if (placement.room == null)
                {
                    continue;
                }

                DrawRoomPreview(placement);
            }
        }

        private void DrawRoomPreview(MapRoomPlacement placement)
        {
            var roomOrigin = MapLayoutUtility.GetRoomOrigin(Vector2.zero, placement.gridPosition, _root.CellSize);
            var roomSize = MapLayoutUtility.GetRoomSize(_root.CellSize);
            var roomPreviewRoot = CreateRoomPreviewRoot(placement);
            if (ShouldDrawRoomTint(placement.gridPosition))
            {
                CreatePreviewQuad(
                    roomPreviewRoot,
                    "Tint",
                    roomOrigin + roomSize * 0.5f,
                    roomSize,
                    roomTint);
            }

            for (var y = 0; y < MapRoomDefinition.RoomHeight; y++)
            {
                for (var x = 0; x < MapRoomDefinition.RoomWidth; x++)
                {
                    var type = placement.room.GetCellType(x, y);
                    if (type == RoomCellType.Empty)
                    {
                        continue;
                    }

                    if (type == RoomCellType.OneWayPlatform)
                    {
                        CreateOneWayPlatform(roomPreviewRoot, roomOrigin, x, y);
                        continue;
                    }

                    if (type == RoomCellType.Death)
                    {
                        CreateDeathCell(roomPreviewRoot, roomOrigin, x, y);
                        continue;
                    }

                    if (type == RoomCellType.SavePoint)
                    {
                        CreateSavePointCell(roomPreviewRoot, roomOrigin, x, y);
                        continue;
                    }

                    if (type == RoomCellType.Breakable)
                    {
                        CreateBreakableCell(roomPreviewRoot, placement.room, roomOrigin, x, y);
                        continue;
                    }

                    var cellCenter = MapLayoutUtility.GetCellCenter(roomOrigin, x, y, _root.CellSize);
                    var visualCellSize = GetFullCellVisualSize();
                    CreatePreviewQuad(
                        roomPreviewRoot,
                        $"Cell_{x}_{y}",
                        cellCenter,
                        visualCellSize,
                        GetCellColor(type),
                        IsSolid(type),
                        Vector2.one * _root.CellSize);
                }
            }
        }

        public void BreakConnectedBreakableCells(MapRoomDefinition room, Transform roomInstanceRoot, Vector2Int startCell)
        {
            if (room == null || roomInstanceRoot == null || room.GetCellType(startCell.x, startCell.y) != RoomCellType.Breakable)
            {
                return;
            }

            var cellsToBreak = FindConnectedBreakableCells(room, startCell);
            for (var i = 0; i < cellsToBreak.Count; i++)
            {
                DestroyBreakableCell(roomInstanceRoot, cellsToBreak[i]);
            }
        }

        private void CreateOneWayPlatform(Transform parent, Vector2 roomOrigin, int cellX, int cellY)
        {
            var cellCenter = MapLayoutUtility.GetCellCenter(roomOrigin, cellX, cellY, _root.CellSize);

            var cellObject = new GameObject($"Cell_{cellX}_{cellY}");
            cellObject.hideFlags = HideFlags.DontSave;
            cellObject.transform.SetParent(parent, false);
            cellObject.transform.localPosition = new Vector3(cellCenter.x, cellCenter.y + _root.CellSize * 0.25f, 0f);
            var visualSize = new Vector2(GetFullCellVisualSize().x, Mathf.Max(0.01f, _root.CellSize * 0.5f * GetVisualScale()));
            cellObject.transform.localScale = new Vector3(visualSize.x, visualSize.y, 1f);

            var renderer = cellObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = oneWayPlatformColor;
            renderer.sortingOrder = sortingOrder;
            renderer.sortingLayerName = solidSortingLayerName;

            if (!generateSolidColliders)
            {
                return;
            }

            var collider = cellObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
            var marker = cellObject.AddComponent<OneWayPlatformMarker>();
            marker.Axis = OneWayPlatformMarker.PlatformAxis.Horizontal;
        }

        private void CreateDeathCell(Transform parent, Vector2 roomOrigin, int cellX, int cellY)
        {
            var cellCenter = MapLayoutUtility.GetCellCenter(roomOrigin, cellX, cellY, _root.CellSize);
            var visualCellSize = GetFullCellVisualSize();
            var cellObject = CreatePreviewQuad(
                parent,
                $"Cell_{cellX}_{cellY}",
                cellCenter,
                visualCellSize,
                deathCellColor);

            var collider = cellObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
            cellObject.AddComponent<DeathCellMarker>();
        }

        private void CreateSavePointCell(Transform parent, Vector2 roomOrigin, int cellX, int cellY)
        {
            var cellCenter = MapLayoutUtility.GetCellCenter(roomOrigin, cellX, cellY, _root.CellSize);
            var visualSize = Vector2.one * Mathf.Max(0.18f, _root.CellSize * 0.45f * GetVisualScale());
            var cellObject = CreatePreviewQuad(
                parent,
                $"Cell_{cellX}_{cellY}",
                cellCenter,
                visualSize,
                savePointColor);

            var trigger = cellObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(
                Mathf.Approximately(visualSize.x, 0f) ? 1f : (_root.CellSize * 0.55f) / visualSize.x,
                Mathf.Approximately(visualSize.y, 0f) ? 1f : (_root.CellSize * 0.55f) / visualSize.y);
            trigger.offset = Vector2.zero;
            cellObject.AddComponent<SavePointMarker>();
        }

        private void CreateBreakableCell(Transform parent, MapRoomDefinition room, Vector2 roomOrigin, int cellX, int cellY)
        {
            var cellCenter = MapLayoutUtility.GetCellCenter(roomOrigin, cellX, cellY, _root.CellSize);
            var visualCellSize = GetFullCellVisualSize();
            var cellObject = CreatePreviewQuad(
                parent,
                $"Cell_{cellX}_{cellY}",
                cellCenter,
                visualCellSize,
                GetBreakableColor(),
                true,
                Vector2.one * _root.CellSize,
                sprite: GetRoundedSquareSprite());

            var marker = cellObject.AddComponent<BreakableCellMarker>();
            var cell = new Vector2Int(cellX, cellY);
            marker.Initialize(this, room, parent, cell);
            RegisterBreakableCell(parent, cell, marker);
        }

        private Transform CreateRoomPreviewRoot(MapRoomPlacement placement)
        {
            var roomRoot = new GameObject($"Room_{placement.gridPosition.x}_{placement.gridPosition.y}_{placement.room.DisplayName}");
            roomRoot.hideFlags = HideFlags.DontSave;
            roomRoot.transform.SetParent(_previewRoot, false);
            return roomRoot.transform;
        }

        private GameObject CreatePreviewQuad(
            Transform parent,
            string quadName,
            Vector2 localPosition,
            Vector2 visualSize,
            Color color,
            bool solid = false,
            Vector2? colliderWorldSize = null,
            Vector2? colliderLocalOffset = null,
            Sprite sprite = null)
        {
            var previewObject = new GameObject(quadName);
            previewObject.hideFlags = HideFlags.DontSave;
            previewObject.transform.SetParent(parent, false);
            previewObject.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            previewObject.transform.localScale = new Vector3(visualSize.x, visualSize.y, 1f);

            var renderer = previewObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : GetWhiteSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            renderer.sortingLayerName = solidSortingLayerName;

            if (generateSolidColliders && solid)
            {
                var collider = previewObject.AddComponent<BoxCollider2D>();
                var targetWorldSize = colliderWorldSize ?? visualSize;
                collider.size = new Vector2(
                    Mathf.Approximately(visualSize.x, 0f) ? 1f : targetWorldSize.x / visualSize.x,
                    Mathf.Approximately(visualSize.y, 0f) ? 1f : targetWorldSize.y / visualSize.y);
                collider.offset = colliderLocalOffset ?? Vector2.zero;

                if (quadName.StartsWith("Cell_") && targetWorldSize.y < _root.CellSize)
                {
                    previewObject.AddComponent<OneWayPlatformMarker>();
                }
            }

            return previewObject;
        }

        private Color GetCellColor(RoomCellType type)
        {
            return type switch
            {
                RoomCellType.Wall => wallColor,
                RoomCellType.Ground => groundColor,
                RoomCellType.OneWayPlatform => oneWayPlatformColor,
                RoomCellType.Death => deathCellColor,
                RoomCellType.SavePoint => savePointColor,
                RoomCellType.Breakable => GetBreakableColor(),
                _ => Color.clear
            };
        }

        private static bool IsSolid(RoomCellType type)
        {
            return type == RoomCellType.Wall
                || type == RoomCellType.Ground
                || type == RoomCellType.OneWayPlatform
                || type == RoomCellType.Breakable;
        }

        private Color GetBreakableColor()
        {
            return Application.isPlaying ? gameBreakableColor : editorBreakableColor;
        }

        private List<Vector2Int> FindConnectedBreakableCells(MapRoomDefinition room, Vector2Int startCell)
        {
            var result = new List<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var frontier = new Queue<Vector2Int>();
            frontier.Enqueue(startCell);
            visited.Add(startCell);

            while (frontier.Count > 0)
            {
                var current = frontier.Dequeue();
                result.Add(current);
                TryQueueBreakableNeighbor(room, current + Vector2Int.up, visited, frontier);
                TryQueueBreakableNeighbor(room, current + Vector2Int.down, visited, frontier);
                TryQueueBreakableNeighbor(room, current + Vector2Int.left, visited, frontier);
                TryQueueBreakableNeighbor(room, current + Vector2Int.right, visited, frontier);
            }

            return result;
        }

        private static void TryQueueBreakableNeighbor(MapRoomDefinition room, Vector2Int cell, HashSet<Vector2Int> visited, Queue<Vector2Int> frontier)
        {
            if (visited.Contains(cell) || room.GetCellType(cell.x, cell.y) != RoomCellType.Breakable)
            {
                return;
            }

            visited.Add(cell);
            frontier.Enqueue(cell);
        }

        private void RegisterBreakableCell(Transform roomInstanceRoot, Vector2Int cell, BreakableCellMarker marker)
        {
            if (!_breakableCells.TryGetValue(roomInstanceRoot, out var roomCells))
            {
                roomCells = new Dictionary<Vector2Int, BreakableCellMarker>();
                _breakableCells.Add(roomInstanceRoot, roomCells);
            }

            roomCells[cell] = marker;
        }

        private void DestroyBreakableCell(Transform roomInstanceRoot, Vector2Int cell)
        {
            if (!_breakableCells.TryGetValue(roomInstanceRoot, out var roomCells) || !roomCells.TryGetValue(cell, out var marker))
            {
                return;
            }

            roomCells.Remove(cell);
            if (roomCells.Count == 0)
            {
                _breakableCells.Remove(roomInstanceRoot);
            }

            if (marker == null)
            {
                return;
            }

            DestroyPreviewObject(marker.gameObject);
        }

        private Vector2 GetFullCellVisualSize()
        {
            var fullSize = _root.CellSize * GetVisualScale();
            return Vector2.one * Mathf.Max(0.01f, fullSize);
        }

        private float GetVisualScale()
        {
            return Mathf.Clamp01(1f - cellVisualInset * 2f);
        }

        private void EnsureDependencies()
        {
            if (_root == null)
            {
                _root = GetComponent<MapAuthoringRoot>();
            }
        }

        private bool ShouldDrawRoomTint(Vector2Int roomGridPosition)
        {
            if (Application.isPlaying)
            {
                return false;
            }

#if UNITY_EDITOR
            return _hasEditorSelectedRoom && _editorSelectedRoomGridPosition == roomGridPosition;
#else
            return false;
#endif
        }

#if UNITY_EDITOR
        public void SetEditorSelectedRoom(Vector2Int roomGridPosition)
        {
            _hasEditorSelectedRoom = true;
            _editorSelectedRoomGridPosition = roomGridPosition;
        }

        public void ClearEditorSelectedRoom()
        {
            _hasEditorSelectedRoom = false;
        }

        private void QueueEditorRebuild()
        {
            if (_queuedEditorRebuild)
            {
                return;
            }

            _queuedEditorRebuild = true;
            EditorApplication.delayCall += RebuildFromEditorDelay;
        }

        private void RebuildFromEditorDelay()
        {
            EditorApplication.delayCall -= RebuildFromEditorDelay;
            _queuedEditorRebuild = false;

            if (this == null)
            {
                return;
            }

            Rebuild();
        }
#endif

        private void EnsurePreviewRoot()
        {
            if (_previewRoot != null)
            {
                return;
            }

            var existing = transform.Find(PreviewRootName);
            if (existing != null)
            {
                _previewRoot = existing;
                return;
            }

            var previewRoot = new GameObject(PreviewRootName);
            previewRoot.hideFlags = HideFlags.DontSave;
            previewRoot.transform.SetParent(transform, false);
            _previewRoot = previewRoot.transform;
        }

        private void ClearPreview()
        {
            _breakableCells.Clear();

            if (_previewRoot == null)
            {
                var existing = transform.Find(PreviewRootName);
                if (existing != null)
                {
                    _previewRoot = existing;
                }
            }

            if (_previewRoot == null)
            {
                return;
            }

            for (var i = _previewRoot.childCount - 1; i >= 0; i--)
            {
                var child = _previewRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private Sprite GetWhiteSprite()
        {
            if (_whiteSprite != null)
            {
                return _whiteSprite;
            }

            _whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _whiteSprite.hideFlags = HideFlags.DontSave;
            return _whiteSprite;
        }

        private Sprite GetRoundedSquareSprite()
        {
            if (_roundedSquareSprite != null)
            {
                return _roundedSquareSprite;
            }

            const int size = 32;
            const float radius = 7f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] = IsInsideRoundedSquare(x, y, size, radius)
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            _roundedSquareSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            _roundedSquareSprite.hideFlags = HideFlags.DontSave;
            return _roundedSquareSprite;
        }

        private static bool IsInsideRoundedSquare(int x, int y, int size, float radius)
        {
            var inset = radius;
            var px = x + 0.5f;
            var py = y + 0.5f;
            var nearestX = Mathf.Clamp(px, inset, size - inset);
            var nearestY = Mathf.Clamp(py, inset, size - inset);
            var distance = new Vector2(px - nearestX, py - nearestY).magnitude;
            return distance <= radius;
        }

        private static void DestroyPreviewObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}

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
        [SerializeField] private int sortingOrder = -20;
        [SerializeField, Range(0f, 0.45f)] private float cellVisualInset = 0.06f;
        [SerializeField] private bool generateSolidColliders = true;
        [SerializeField] private string solidSortingLayerName = "Default";

        private MapAuthoringRoot _root;
        private Transform _previewRoot;
        private Sprite _whiteSprite;
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
            cellObject.AddComponent<OneWayPlatformMarker>();
        }

        private Transform CreateRoomPreviewRoot(MapRoomPlacement placement)
        {
            var roomRoot = new GameObject($"Room_{placement.gridPosition.x}_{placement.gridPosition.y}_{placement.room.DisplayName}");
            roomRoot.hideFlags = HideFlags.DontSave;
            roomRoot.transform.SetParent(_previewRoot, false);
            return roomRoot.transform;
        }

        private void CreatePreviewQuad(
            Transform parent,
            string quadName,
            Vector2 localPosition,
            Vector2 visualSize,
            Color color,
            bool solid = false,
            Vector2? colliderWorldSize = null,
            Vector2? colliderLocalOffset = null)
        {
            var previewObject = new GameObject(quadName);
            previewObject.hideFlags = HideFlags.DontSave;
            previewObject.transform.SetParent(parent, false);
            previewObject.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
            previewObject.transform.localScale = new Vector3(visualSize.x, visualSize.y, 1f);

            var renderer = previewObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
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
        }

        private Color GetCellColor(RoomCellType type)
        {
            return type switch
            {
                RoomCellType.Wall => wallColor,
                RoomCellType.Ground => groundColor,
                RoomCellType.OneWayPlatform => oneWayPlatformColor,
                _ => Color.clear
            };
        }

        private static bool IsSolid(RoomCellType type)
        {
            return type == RoomCellType.Wall || type == RoomCellType.Ground || type == RoomCellType.OneWayPlatform;
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
    }
}

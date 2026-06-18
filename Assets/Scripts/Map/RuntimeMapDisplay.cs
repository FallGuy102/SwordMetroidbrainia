using System.Collections.Generic;
using SwordMetroidbrainia;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SwordMetroidbrainia.Map
{
    [DisallowMultipleComponent]
    public sealed class RuntimeMapDisplay : MonoBehaviour
    {
        private const string RuntimeCanvasName = "__RuntimeMapCanvas";

        [Header("Data")]
        [SerializeField] private MapDefinition map;
        [SerializeField] private Transform player;
        [SerializeField] private float cellSize = 1f;

        [Header("Input")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private bool useFallbackInput = true;

        [Header("Display")]
        [SerializeField] private bool startsVisible;
        [SerializeField] private bool showOnlyDiscoveredRooms = true;
        [SerializeField] private bool freezeGameWhileVisible = true;
        [SerializeField, Range(0.45f, 0.95f)] private float screenFill = 0.82f;
        [SerializeField, Range(0.05f, 1f)] private float roomScaleMultiplier = 0.25f;
        [SerializeField, Min(0f)] private float roomGap = 2f;
        [SerializeField, Min(0f)] private float panelPadding = 40f;

        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new(0.02f, 0.025f, 0.04f, 0.94f);
        [SerializeField] private Color roomOutlineColor = new(0.25f, 0.4f, 0.45f, 0.45f);
        [SerializeField] private Color currentRoomOutlineColor = new(0.95f, 0.9f, 0.45f, 1f);

        private readonly Dictionary<Vector2Int, RuntimeRoomView> _roomViews = new();
        private readonly HashSet<Vector2Int> _discoveredRooms = new();

        private Canvas _canvas;
        private InputAction _fallbackOpenMapAction;
        private RectTransform _roomRoot;
        private Vector2Int _currentRoomGrid;
        private bool _hasCurrentRoom;
        private bool _isVisible;
        private bool _isBuilt;
        private bool _visibilityDirty = true;
        private bool _hasFrozenGame;
        private float _timeScaleBeforeFreeze = 1f;
        private PlayerInputReader _blockedInputReader;

        private sealed class RuntimeRoomView
        {
            public RawImage RoomImage;
            public Texture2D Texture;
            public readonly List<Image> Outlines = new();

            public void SetVisible(bool visible, bool isCurrent, Color roomOutlineColor, Color currentRoomOutlineColor)
            {
                if (RoomImage != null)
                {
                    RoomImage.enabled = visible;
                }

                foreach (var outline in Outlines)
                {
                    if (outline == null)
                    {
                        continue;
                    }

                    outline.enabled = visible;
                    outline.color = isCurrent ? currentRoomOutlineColor : roomOutlineColor;
                }
            }
        }

        private readonly struct RuntimeMapLayout
        {
            public RuntimeMapLayout(Vector2Int minGrid, Vector2Int maxGrid, Vector2 roomPixelSize, Vector2 mapPixelSize)
            {
                MinGrid = minGrid;
                MaxGrid = maxGrid;
                RoomPixelSize = roomPixelSize;
                MapPixelSize = mapPixelSize;
            }

            public Vector2Int MinGrid { get; }
            public Vector2Int MaxGrid { get; }
            public Vector2 RoomPixelSize { get; }
            public Vector2 MapPixelSize { get; }
        }

        private void Awake()
        {
            ResolveReferences();
            CreateFallbackInput();
            EnsureUi();
            SetVisible(startsVisible);
        }

        private void OnEnable()
        {
            _fallbackOpenMapAction?.Enable();
        }

        private void OnDisable()
        {
            _fallbackOpenMapAction?.Disable();
            SetGameplayInputBlocked(false);
            SetGameFrozen(false);
        }

        private void OnDestroy()
        {
            SetGameplayInputBlocked(false);
            SetGameFrozen(false);
            _fallbackOpenMapAction?.Dispose();
            ClearRooms();
        }

        private void Update()
        {
            ResolveReferences();
            if (_isVisible)
            {
                SetGameplayInputBlocked(true);
            }

            if (IsOpenMapTriggered())
            {
                Toggle();
            }

            UpdateCurrentRoom();
            if (_visibilityDirty)
            {
                RefreshRoomVisibility();
            }
        }

        public void Toggle()
        {
            SetVisible(!_isVisible);
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(visible);
            }

            SetGameFrozen(visible);
            SetGameplayInputBlocked(visible);

            if (!visible)
            {
                return;
            }

            if (!_isBuilt)
            {
                Rebuild();
            }

            _visibilityDirty = true;
            RefreshRoomVisibility();
        }

        public void DiscoverRoom(Vector2Int roomGridPosition)
        {
            if (_discoveredRooms.Add(roomGridPosition))
            {
                _visibilityDirty = true;
            }
        }

        public void Rebuild()
        {
            EnsureUi();
            ClearRooms();

            if (map == null || map.Rooms.Count == 0)
            {
                return;
            }

            var layout = BuildLayout();
            _roomRoot.sizeDelta = layout.MapPixelSize;
            BuildRoomViews(layout);
            _isBuilt = true;
            _visibilityDirty = true;
        }

        private void ResolveReferences()
        {
            if (map == null && TryGetComponent<MapAuthoringRoot>(out var root))
            {
                map = root.Map;
                cellSize = root.CellSize;
            }

            if (inputReader == null)
            {
                inputReader = FindObjectOfType<PlayerInputReader>();
            }

            if (player == null && inputReader != null)
            {
                player = inputReader.transform;
            }
        }

        private void CreateFallbackInput()
        {
            if (!useFallbackInput || _fallbackOpenMapAction != null)
            {
                return;
            }

            _fallbackOpenMapAction = new InputAction(name: "RuntimeMapOpen", type: InputActionType.Button);
            _fallbackOpenMapAction.AddBinding("<Keyboard>/tab");
            _fallbackOpenMapAction.AddBinding("<Gamepad>/leftShoulder");
            _fallbackOpenMapAction.Enable();
        }

        private bool IsOpenMapTriggered()
        {
            if (inputReader != null && inputReader.OpenMapTriggered)
            {
                return true;
            }

            return useFallbackInput
                && _fallbackOpenMapAction != null
                && _fallbackOpenMapAction.WasPressedThisFrame();
        }

        private void EnsureUi()
        {
            if (_canvas != null)
            {
                return;
            }

            CreateCanvas();
            CreateFullscreenBackground();
            CreateRoomRoot();
        }

        private void CreateCanvas()
        {
            var canvasObject = new GameObject(RuntimeCanvasName);
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void CreateFullscreenBackground()
        {
            var panelObject = new GameObject("FullscreenMapPanel");
            panelObject.transform.SetParent(_canvas.transform, false);

            var background = panelObject.AddComponent<RectTransform>();
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.pivot = new Vector2(0.5f, 0.5f);
            background.offsetMin = Vector2.zero;
            background.offsetMax = Vector2.zero;

            var panelImage = panelObject.AddComponent<Image>();
            panelImage.color = backgroundColor;
            panelImage.raycastTarget = false;
        }

        private void CreateRoomRoot()
        {
            var roomRootObject = new GameObject("Rooms");
            roomRootObject.transform.SetParent(_canvas.transform, false);
            _roomRoot = roomRootObject.AddComponent<RectTransform>();
            _roomRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _roomRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _roomRoot.pivot = new Vector2(0.5f, 0.5f);
            _roomRoot.anchoredPosition = Vector2.zero;
        }

        private RuntimeMapLayout BuildLayout()
        {
            GetMapBounds(out var minGrid, out var maxGrid);
            var roomCount = new Vector2Int(maxGrid.x - minGrid.x + 1, maxGrid.y - minGrid.y + 1);
            var roomPixelSize = GetRoomPixelSize(GetAvailableMapSize(), roomCount);
            var mapPixelSize = new Vector2(
                roomCount.x * roomPixelSize.x + Mathf.Max(0, roomCount.x - 1) * roomGap,
                roomCount.y * roomPixelSize.y + Mathf.Max(0, roomCount.y - 1) * roomGap);

            return new RuntimeMapLayout(minGrid, maxGrid, roomPixelSize, mapPixelSize);
        }

        private void BuildRoomViews(RuntimeMapLayout layout)
        {
            for (var i = 0; i < map.Rooms.Count; i++)
            {
                var placement = map.GetRoom(i);
                if (placement.room == null)
                {
                    continue;
                }

                _roomViews[placement.gridPosition] = CreateRoomView(placement, layout);
            }
        }

        private RuntimeRoomView CreateRoomView(MapRoomPlacement placement, RuntimeMapLayout layout)
        {
            var roomObject = new GameObject($"Room_{placement.gridPosition.x}_{placement.gridPosition.y}");
            roomObject.transform.SetParent(_roomRoot, false);

            var roomRect = roomObject.AddComponent<RectTransform>();
            roomRect.sizeDelta = layout.RoomPixelSize;
            roomRect.anchorMin = new Vector2(0.5f, 0.5f);
            roomRect.anchorMax = new Vector2(0.5f, 0.5f);
            roomRect.pivot = new Vector2(0.5f, 0.5f);
            roomRect.anchoredPosition = GetRoomAnchoredPosition(placement.gridPosition, layout);

            var roomImage = roomObject.AddComponent<RawImage>();
            roomImage.texture = placement.room.CreateMinimapTexture();
            roomImage.color = Color.white;
            roomImage.raycastTarget = false;

            var view = new RuntimeRoomView
            {
                RoomImage = roomImage,
                Texture = roomImage.texture as Texture2D
            };

            CreateOutline(roomObject.transform, view);
            return view;
        }

        private void CreateOutline(Transform parent, RuntimeRoomView view)
        {
            view.Outlines.Add(CreateOutlineEdge(parent, "OutlineTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -2f), new Vector2(0f, 0f)));
            view.Outlines.Add(CreateOutlineEdge(parent, "OutlineBottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 2f)));
            view.Outlines.Add(CreateOutlineEdge(parent, "OutlineLeft", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f)));
            view.Outlines.Add(CreateOutlineEdge(parent, "OutlineRight", new Vector2(1f, 0f), Vector2.one, new Vector2(-2f, 0f), Vector2.zero));
        }

        private Image CreateOutlineEdge(Transform parent, string edgeName, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var edgeObject = new GameObject(edgeName);
            edgeObject.transform.SetParent(parent, false);
            var edgeRect = edgeObject.AddComponent<RectTransform>();
            edgeRect.anchorMin = anchorMin;
            edgeRect.anchorMax = anchorMax;
            edgeRect.offsetMin = offsetMin;
            edgeRect.offsetMax = offsetMax;

            var edge = edgeObject.AddComponent<Image>();
            edge.color = roomOutlineColor;
            edge.raycastTarget = false;
            return edge;
        }

        private Vector2 GetAvailableMapSize()
        {
            var width = Mathf.Max(640f, Screen.width * screenFill);
            var height = Mathf.Max(360f, Screen.height * screenFill);
            return new Vector2(width, height);
        }

        private Vector2 GetRoomPixelSize(Vector2 availableSize, Vector2Int roomCount)
        {
            var usableWidth = Mathf.Max(1f, availableSize.x - panelPadding * 2f - Mathf.Max(0, roomCount.x - 1) * roomGap);
            var usableHeight = Mathf.Max(1f, availableSize.y - panelPadding * 2f - Mathf.Max(0, roomCount.y - 1) * roomGap);
            var roomWidth = usableWidth / Mathf.Max(1, roomCount.x);
            var roomHeight = usableHeight / Mathf.Max(1, roomCount.y);
            var roomScale = Mathf.Min(roomWidth / MapRoomDefinition.RoomWidth, roomHeight / MapRoomDefinition.RoomHeight);
            roomScale *= roomScaleMultiplier;
            return new Vector2(MapRoomDefinition.RoomWidth * roomScale, MapRoomDefinition.RoomHeight * roomScale);
        }

        private Vector2 GetRoomAnchoredPosition(Vector2Int roomGridPosition, RuntimeMapLayout layout)
        {
            var roomStep = layout.RoomPixelSize + Vector2.one * roomGap;
            var x = (roomGridPosition.x - layout.MinGrid.x) * roomStep.x;
            var y = (roomGridPosition.y - layout.MinGrid.y) * roomStep.y;
            var width = (layout.MaxGrid.x - layout.MinGrid.x) * roomStep.x;
            var height = (layout.MaxGrid.y - layout.MinGrid.y) * roomStep.y;
            return new Vector2(x - width * 0.5f, y - height * 0.5f);
        }

        private void UpdateCurrentRoom()
        {
            if (map == null || player == null)
            {
                return;
            }

            var playerPosition = (Vector2)player.position;
            for (var i = 0; i < map.Rooms.Count; i++)
            {
                var placement = map.GetRoom(i);
                var roomOrigin = MapLayoutUtility.GetRoomOrigin(transform.position, placement.gridPosition, cellSize);
                var roomSize = MapLayoutUtility.GetRoomSize(cellSize);
                var roomRect = new Rect(roomOrigin, roomSize);
                if (!roomRect.Contains(playerPosition))
                {
                    continue;
                }

                if (!_hasCurrentRoom || _currentRoomGrid != placement.gridPosition)
                {
                    _currentRoomGrid = placement.gridPosition;
                    _hasCurrentRoom = true;
                    _visibilityDirty = true;
                }

                DiscoverRoom(_currentRoomGrid);
                return;
            }
        }

        private void RefreshRoomVisibility()
        {
            _visibilityDirty = false;
            foreach (var pair in _roomViews)
            {
                var roomGridPosition = pair.Key;
                var view = pair.Value;
                var isCurrent = _hasCurrentRoom && roomGridPosition == _currentRoomGrid;
                var isDiscovered = _discoveredRooms.Contains(roomGridPosition);
                var shouldShow = !showOnlyDiscoveredRooms || isDiscovered || isCurrent;
                view.SetVisible(shouldShow, isCurrent, roomOutlineColor, currentRoomOutlineColor);
            }
        }

        private void ClearRooms()
        {
            foreach (var view in _roomViews.Values)
            {
                if (view?.Texture != null)
                {
                    DestroyObject(view.Texture);
                }
            }

            _roomViews.Clear();
            _isBuilt = false;
            if (_roomRoot == null)
            {
                return;
            }

            for (var i = _roomRoot.childCount - 1; i >= 0; i--)
            {
                DestroyObject(_roomRoot.GetChild(i).gameObject);
            }
        }

        private void GetMapBounds(out Vector2Int minGrid, out Vector2Int maxGrid)
        {
            minGrid = map.GetRoom(0).gridPosition;
            maxGrid = minGrid;

            for (var i = 1; i < map.Rooms.Count; i++)
            {
                var grid = map.GetRoom(i).gridPosition;
                minGrid = Vector2Int.Min(minGrid, grid);
                maxGrid = Vector2Int.Max(maxGrid, grid);
            }
        }

        private static void DestroyObject(Object target)
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

        private void SetGameFrozen(bool frozen)
        {
            if (!freezeGameWhileVisible && frozen)
            {
                return;
            }

            if (frozen)
            {
                if (_hasFrozenGame)
                {
                    return;
                }

                _timeScaleBeforeFreeze = Time.timeScale;
                Time.timeScale = 0f;
                _hasFrozenGame = true;
                return;
            }

            if (!_hasFrozenGame)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforeFreeze;
            _hasFrozenGame = false;
        }

        private void SetGameplayInputBlocked(bool blocked)
        {
            if (blocked)
            {
                if (inputReader == null)
                {
                    return;
                }

                if (_blockedInputReader == inputReader)
                {
                    return;
                }

                SetGameplayInputBlocked(false);
                inputReader.SetGameplayInputEnabled(false);
                _blockedInputReader = inputReader;
                return;
            }

            if (_blockedInputReader == null)
            {
                return;
            }

            _blockedInputReader.SetGameplayInputEnabled(true);
            _blockedInputReader = null;
        }
    }
}

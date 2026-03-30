using UnityEngine;

namespace SwordMetroidbrainia.Map
{
    [ExecuteAlways]
    public sealed class MapRoomViewportMask : MonoBehaviour
    {
        private const string MaskRootName = "__RoomViewportMask";
        private const float MaskExtent = 200f;

        [SerializeField] private Color maskColor = Color.black;
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = 500;

        private Transform _maskRoot;
        private SpriteRenderer _leftMask;
        private SpriteRenderer _rightMask;
        private SpriteRenderer _topMask;
        private SpriteRenderer _bottomMask;
        private Sprite _whiteSprite;

        private void OnEnable()
        {
            EnsureMaskObjects();
        }

        private void OnDisable()
        {
            ClearMaskObjects();
        }

        public void SetRoomBounds(Rect roomBounds)
        {
            EnsureMaskObjects();

            var roomCenter = roomBounds.center;
            var roomWidth = roomBounds.width;
            var roomHeight = roomBounds.height;

            ConfigureMask(
                _leftMask,
                new Vector2(roomBounds.xMin - MaskExtent * 0.5f, roomCenter.y),
                new Vector2(MaskExtent, roomHeight + MaskExtent * 2f));
            ConfigureMask(
                _rightMask,
                new Vector2(roomBounds.xMax + MaskExtent * 0.5f, roomCenter.y),
                new Vector2(MaskExtent, roomHeight + MaskExtent * 2f));
            ConfigureMask(
                _topMask,
                new Vector2(roomCenter.x, roomBounds.yMax + MaskExtent * 0.5f),
                new Vector2(roomWidth, MaskExtent));
            ConfigureMask(
                _bottomMask,
                new Vector2(roomCenter.x, roomBounds.yMin - MaskExtent * 0.5f),
                new Vector2(roomWidth, MaskExtent));
        }

        private void EnsureMaskObjects()
        {
            if (_maskRoot == null)
            {
                var existing = transform.Find(MaskRootName);
                if (existing != null)
                {
                    _maskRoot = existing;
                }
            }

            if (_maskRoot == null)
            {
                var maskRoot = new GameObject(MaskRootName);
                maskRoot.hideFlags = HideFlags.DontSave;
                maskRoot.transform.SetParent(transform, false);
                _maskRoot = maskRoot.transform;
            }

            _leftMask ??= CreateMaskSprite("LeftMask");
            _rightMask ??= CreateMaskSprite("RightMask");
            _topMask ??= CreateMaskSprite("TopMask");
            _bottomMask ??= CreateMaskSprite("BottomMask");
        }

        private SpriteRenderer CreateMaskSprite(string objectName)
        {
            var maskObject = new GameObject(objectName);
            maskObject.hideFlags = HideFlags.DontSave;
            maskObject.transform.SetParent(_maskRoot, false);

            var renderer = maskObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetWhiteSprite();
            renderer.color = maskColor;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void ConfigureMask(SpriteRenderer renderer, Vector2 position, Vector2 size)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.color = maskColor;
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
            renderer.transform.position = new Vector3(position.x, position.y, 0f);
            renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
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

        private void ClearMaskObjects()
        {
            if (_maskRoot == null)
            {
                var existing = transform.Find(MaskRootName);
                if (existing != null)
                {
                    _maskRoot = existing;
                }
            }

            if (_maskRoot == null)
            {
                return;
            }

            for (var i = _maskRoot.childCount - 1; i >= 0; i--)
            {
                var child = _maskRoot.GetChild(i).gameObject;
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
    }
}

using UnityEngine;

namespace SwordMetroidbrainia
{
    // Handles sword input, attack direction resolution, hit detection, and attack debug visualization.
    [RequireComponent(typeof(PlayerController2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerSword : MonoBehaviour
    {
        private const int AttackBufferSize = 8;

        [Header("Attack")]
        [SerializeField] private float attackRange = 0.5f;
        [SerializeField] private Vector2 attackBoxSize = new(0.45f, 0.45f);
        [SerializeField] private float attackCooldown = 0.12f;

        [Header("Debug")]
        [SerializeField] private bool showAttackGizmo = true;
        [SerializeField] private Color idleAttackBoxColor = new(1f, 0.6f, 0.6f, 0.35f);
        [SerializeField] private Color activeAttackBoxColor = new(1f, 0.2f, 0.2f, 0.9f);
        [SerializeField] private bool showAttackBoxInGame = true;
        [SerializeField] private float debugLineWidth = 0.03f;
        [SerializeField] private float activeDebugDuration = 0.08f;

        private readonly Collider2D[] _attackHits = new Collider2D[AttackBufferSize];

        private PlayerController2D _controller;
        private PlayerInputReader _inputReader;
        private float _attackCooldownTimer;
        private float _activeDebugTimer;
        private LineRenderer _debugLineRenderer;

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _inputReader = GetComponent<PlayerInputReader>();
            EnsureDebugLineRenderer();
        }

        private void Update()
        {
            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= Time.deltaTime;
            }

            if (_activeDebugTimer > 0f)
            {
                _activeDebugTimer -= Time.deltaTime;
            }

            if (_inputReader.PrimaryAbilityTriggered)
            {
                TryAttack();
            }
        }

        private void LateUpdate()
        {
            UpdateDebugLine();
        }

        public Vector2 GetAttackDirection()
        {
            var moveInput = _inputReader.DigitalMove;
            var upPressed = moveInput.y > 0f;
            var downPressed = moveInput.y < 0f;

            if (upPressed && !downPressed)
            {
                return Vector2.up;
            }

            if (downPressed && !upPressed)
            {
                return Vector2.down;
            }

            // Neutral attacks inherit the last horizontal facing instead of relying on mouse position.
            return _controller.FacingDirection >= 0 ? Vector2.right : Vector2.left;
        }

        public Vector2 GetAttackOrigin()
        {
            return _controller.CurrentPosition + GetAttackDirection() * attackRange;
        }

        private void TryAttack()
        {
            if (_attackCooldownTimer > 0f)
            {
                return;
            }

            _attackCooldownTimer = attackCooldown;
            _activeDebugTimer = activeDebugDuration;

            var attackDirection = GetAttackDirection();
            var attackOrigin = _controller.CurrentPosition + attackDirection * attackRange;
            var hitCount = Physics2D.OverlapBoxNonAlloc(attackOrigin, attackBoxSize, 0f, _attackHits);
            if (hitCount <= 0)
            {
                return;
            }

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _attackHits[i];
                if (hit == null || hit == _controller.BodyCollider)
                {
                    continue;
                }

                // Sword hits are decoupled from recoil so the weapon can gain additional effects later.
                _controller.ApplySwordRecoil(-attackDirection);
                return;
            }
        }

        private void EnsureDebugLineRenderer()
        {
            if (_debugLineRenderer != null)
            {
                return;
            }

            var existing = GetComponent<LineRenderer>();
            if (existing != null)
            {
                _debugLineRenderer = existing;
            }
            else
            {
                _debugLineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            _debugLineRenderer.useWorldSpace = true;
            _debugLineRenderer.loop = false;
            _debugLineRenderer.positionCount = 5;
            _debugLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _debugLineRenderer.receiveShadows = false;
            _debugLineRenderer.alignment = LineAlignment.View;
            _debugLineRenderer.textureMode = LineTextureMode.Stretch;

            var material = new Material(Shader.Find("Sprites/Default"));
            _debugLineRenderer.material = material;
        }

        private void UpdateDebugLine()
        {
            if (_debugLineRenderer == null)
            {
                return;
            }

            var shouldShow = Application.isPlaying && showAttackBoxInGame;
            _debugLineRenderer.enabled = shouldShow;
            if (!shouldShow)
            {
                return;
            }

            var origin = GetAttackOrigin();
            var halfSize = attackBoxSize * 0.5f;

            _debugLineRenderer.startWidth = debugLineWidth;
            _debugLineRenderer.endWidth = debugLineWidth;
            var debugColor = _activeDebugTimer > 0f ? activeAttackBoxColor : idleAttackBoxColor;
            _debugLineRenderer.startColor = debugColor;
            _debugLineRenderer.endColor = debugColor;

            var z = transform.position.z - 0.1f;
            _debugLineRenderer.SetPosition(0, new Vector3(origin.x - halfSize.x, origin.y - halfSize.y, z));
            _debugLineRenderer.SetPosition(1, new Vector3(origin.x - halfSize.x, origin.y + halfSize.y, z));
            _debugLineRenderer.SetPosition(2, new Vector3(origin.x + halfSize.x, origin.y + halfSize.y, z));
            _debugLineRenderer.SetPosition(3, new Vector3(origin.x + halfSize.x, origin.y - halfSize.y, z));
            _debugLineRenderer.SetPosition(4, new Vector3(origin.x - halfSize.x, origin.y - halfSize.y, z));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            attackRange = Mathf.Max(0f, attackRange);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            debugLineWidth = Mathf.Max(0.001f, debugLineWidth);
            activeDebugDuration = Mathf.Max(0.01f, activeDebugDuration);
        }

        private void OnDrawGizmos()
        {
            if (!showAttackGizmo)
            {
                return;
            }

            var controller = Application.isPlaying ? _controller : GetComponent<PlayerController2D>();
            if (controller == null)
            {
                return;
            }

            var direction = Application.isPlaying ? GetAttackDirection() : (controller.FacingDirection >= 0 ? Vector2.right : Vector2.left);
            var origin = Application.isPlaying ? GetAttackOrigin() : (Vector2)transform.position + direction * attackRange;

            Gizmos.color = activeAttackBoxColor;
            Gizmos.DrawWireCube(origin, attackBoxSize);
        }
#endif
    }
}

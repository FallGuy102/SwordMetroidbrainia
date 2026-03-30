using SwordMetroidbrainia.Map;
using UnityEngine;

namespace SwordMetroidbrainia
{
    [RequireComponent(typeof(PlayerController2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerSpear : MonoBehaviour, IPlayerUnlockableAbility
    {
        private enum SpearState
        {
            InHand,
            Charging,
            Flying,
            Stuck,
            Pulling,
            Recovering
        }

        private static class PhysicsCache
        {
            public static readonly RaycastHit2D[] RaycastHits = new RaycastHit2D[12];
        }

        [Header("Availability")]
        [SerializeField] private bool startsUnlocked;

        [Header("Throw")]
        [SerializeField] private float spawnOffset = 0.45f;
        [SerializeField] private float spearSpeed = 14f;
        [SerializeField] private float spearLength = 0.7f;
        [SerializeField] private float spearThickness = 0.14f;
        [SerializeField] private float throwCooldown = 0.08f;
        [SerializeField] private float maxFlightDuration = 3f;
        [SerializeField] private float exposedStuckLength = 1f;

        [Header("Aim")]
        [SerializeField] private float holdThreshold = 0.18f;
        [SerializeField, Range(0.02f, 1f)] private float bulletTimeScale = 0.15f;

        [Header("Pull")]
        [SerializeField] private float pullAcceleration = 80f;
        [SerializeField] private float pullMaxSpeed = 18f;
        [SerializeField] private float pullStopDistance = 0.2f;
        [SerializeField] private float pullStartCooldown = 0.08f;
        [SerializeField] private float pullCancelCooldown = 0.08f;

        [Header("Recovery")]
        [SerializeField] private float brokenReturnDelay = 0.2f;
        [SerializeField] private float reachReturnDelay = 0.05f;

        [Header("Visual")]
        [SerializeField] private Color spearColor = new(0.82f, 0.86f, 0.95f, 1f);

        private PlayerController2D _controller;
        private PlayerInputReader _inputReader;
        private SpearState _state = SpearState.InHand;
        private float _defaultFixedDeltaTime;

        private float _actionCooldownTimer;
        private float _recoverTimer;
        private float _holdTimer;
        private float _flightTimer;
        private bool _secondaryHeldSequenceActive;

        private Vector2 _spearPosition;
        private Vector2 _spearDirection = Vector2.right;

        private Transform _visualRoot;
        private SpriteRenderer _visualRenderer;
        private BoxCollider2D _stuckCollider;
        private PlayerSpearStuckMarker _stuckMarker;
        private Sprite _whiteSprite;
        private bool _isUnlocked;

        public bool IsUnlocked => _isUnlocked;

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _inputReader = GetComponent<PlayerInputReader>();
            _defaultFixedDeltaTime = Time.fixedDeltaTime;
            _isUnlocked = startsUnlocked;
            EnsureVisual();
            SetVisualVisible(false);
        }

        private void OnDisable()
        {
            ExitBulletTime();
        }

        private void Update()
        {
            TickTimers();

            if (!_isUnlocked)
            {
                return;
            }

            UpdateState();
            UpdateVisualTransform();
        }

        public void Unlock()
        {
            SetUnlocked(true);
        }

        public void Lock()
        {
            SetUnlocked(false);
        }

        public void SetUnlocked(bool unlocked)
        {
            if (_isUnlocked == unlocked)
            {
                return;
            }

            _isUnlocked = unlocked;
            if (unlocked)
            {
                return;
            }

            ForceResetToLockedState();
        }

        public bool TryBreakFromSwordHit()
        {
            if (_state != SpearState.Stuck)
            {
                return false;
            }

            BreakSpear();
            return true;
        }

        public void BreakFromOwnerDeath()
        {
            if (_state == SpearState.InHand)
            {
                return;
            }

            BreakSpear();
        }

        private void TickTimers()
        {
            if (_actionCooldownTimer > 0f)
            {
                _actionCooldownTimer = Mathf.Max(0f, _actionCooldownTimer - Time.unscaledDeltaTime);
            }

            if (_recoverTimer > 0f)
            {
                _recoverTimer = Mathf.Max(0f, _recoverTimer - Time.unscaledDeltaTime);
            }
        }

        private void UpdateState()
        {
            switch (_state)
            {
                case SpearState.InHand:
                case SpearState.Charging:
                    UpdateReadyState();
                    break;
                case SpearState.Flying:
                    UpdateFlyingState();
                    break;
                case SpearState.Stuck:
                    UpdateStuckState();
                    break;
                case SpearState.Pulling:
                    UpdatePullingState();
                    break;
                case SpearState.Recovering:
                    UpdateRecoveryState();
                    break;
            }
        }

        private void UpdateReadyState()
        {
            if (_actionCooldownTimer > 0f)
            {
                return;
            }

            if (_inputReader.SecondaryAbilityTriggered)
            {
                _secondaryHeldSequenceActive = true;
                _holdTimer = 0f;
            }

            if (!_secondaryHeldSequenceActive)
            {
                return;
            }

            if (_inputReader.SecondaryAbilityHeld)
            {
                _holdTimer += Time.unscaledDeltaTime;
                if (_state == SpearState.InHand && _holdTimer >= holdThreshold)
                {
                    BeginCharge();
                }
            }

            if (!_inputReader.SecondaryAbilityReleased)
            {
                return;
            }

            FireChargedOrQuickThrow();
        }

        private void BeginCharge()
        {
            _state = SpearState.Charging;
            EnterBulletTime();
        }

        private void FireChargedOrQuickThrow()
        {
            _secondaryHeldSequenceActive = false;
            ExitBulletTime();
            BeginThrow(ResolveAbilityDirection());
        }

        private void BeginThrow(Vector2 direction)
        {
            _state = SpearState.Flying;
            _actionCooldownTimer = throwCooldown;
            _flightTimer = 0f;
            _spearDirection = direction;
            _spearPosition = _controller.CurrentPosition + direction * spawnOffset;
            ConfigureStuckCollider(false);
            SetVisualVisible(true);
        }

        private void UpdateFlyingState()
        {
            _flightTimer += Time.unscaledDeltaTime;
            if (_flightTimer >= maxFlightDuration)
            {
                BreakSpear();
                return;
            }

            var travelDistance = spearSpeed * Time.deltaTime;
            if (travelDistance <= 0f)
            {
                return;
            }

            if (TryGetThrowHit(travelDistance, out var hitPoint, out var shouldBreak))
            {
                if (shouldBreak)
                {
                    BreakSpear();
                    return;
                }

                StickIntoSurface(hitPoint);
                return;
            }

            _spearPosition += _spearDirection * travelDistance;
        }

        private bool TryGetThrowHit(float travelDistance, out Vector2 hitPoint, out bool shouldBreak)
        {
            hitPoint = default;
            shouldBreak = false;
            var hitCount = Physics2D.RaycastNonAlloc(_spearPosition, _spearDirection, PhysicsCache.RaycastHits, travelDistance);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = PhysicsCache.RaycastHits[i];
                if (hit.collider == null || hit.collider == _controller.BodyCollider || hit.collider.isTrigger)
                {
                    continue;
                }

                if (hit.collider.TryGetComponent<DeathCellMarker>(out _))
                {
                    hitPoint = hit.point;
                    shouldBreak = true;
                    return true;
                }

                hitPoint = hit.point;
                return true;
            }

            return false;
        }

        private void StickIntoSurface(Vector2 hitPoint)
        {
            _state = SpearState.Stuck;
            _spearPosition = hitPoint - _spearDirection * (exposedStuckLength * 0.5f);
            ConfigureStuckCollider(true);
        }

        private void UpdateStuckState()
        {
            if (_actionCooldownTimer > 0f || !_inputReader.SecondaryAbilityTriggered)
            {
                return;
            }

            _controller.BeginPull(_spearPosition, pullAcceleration, pullMaxSpeed, pullStopDistance);
            _state = SpearState.Pulling;
            _actionCooldownTimer = pullStartCooldown;
        }

        private void UpdatePullingState()
        {
            if (_actionCooldownTimer <= 0f && _inputReader.SecondaryAbilityTriggered)
            {
                _controller.CancelPull(true);
                BeginRecovery(brokenReturnDelay, pullCancelCooldown);
                return;
            }

            if (!_controller.ConsumePullEndReason(out var reason))
            {
                return;
            }

            switch (reason)
            {
                case PlayerController2D.PullEndReason.Blocked:
                case PlayerController2D.PullEndReason.Canceled:
                    BeginRecovery(brokenReturnDelay, pullCancelCooldown);
                    break;
                case PlayerController2D.PullEndReason.ReachedTarget:
                    BeginRecovery(reachReturnDelay, pullStartCooldown);
                    break;
            }
        }

        private void UpdateRecoveryState()
        {
            if (_recoverTimer > 0f)
            {
                return;
            }

            _state = SpearState.InHand;
        }

        private void BeginRecovery(float returnDelay, float nextActionCooldown)
        {
            ExitBulletTime();
            _state = SpearState.Recovering;
            _recoverTimer = returnDelay;
            _actionCooldownTimer = nextActionCooldown;
            ConfigureStuckCollider(false);
            SetVisualVisible(false);
        }

        private void BreakSpear()
        {
            if (_state == SpearState.Recovering || _state == SpearState.InHand)
            {
                return;
            }

            BeginRecovery(brokenReturnDelay, pullCancelCooldown);
        }

        private void ForceResetToLockedState()
        {
            ExitBulletTime();
            _secondaryHeldSequenceActive = false;
            _holdTimer = 0f;
            _flightTimer = 0f;
            _recoverTimer = 0f;
            _actionCooldownTimer = 0f;
            _controller.CancelPull(false);
            ConfigureStuckCollider(false);
            SetVisualVisible(false);
            _state = SpearState.InHand;
        }

        private Vector2 ResolveAbilityDirection()
        {
            var moveInput = _inputReader.DigitalMove;
            if (moveInput.y > 0f)
            {
                return Vector2.up;
            }

            if (moveInput.y < 0f)
            {
                return Vector2.down;
            }

            return _controller.FacingDirection >= 0 ? Vector2.right : Vector2.left;
        }

        private void UpdateVisualTransform()
        {
            if (_visualRoot == null || !_visualRoot.gameObject.activeSelf)
            {
                return;
            }

            _visualRoot.position = new Vector3(_spearPosition.x, _spearPosition.y, transform.position.z - 0.15f);
            var angle = Mathf.Atan2(_spearDirection.y, _spearDirection.x) * Mathf.Rad2Deg;
            _visualRoot.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void EnsureVisual()
        {
            if (_visualRoot != null)
            {
                return;
            }

            var visualObject = new GameObject("SpearVisual");
            visualObject.hideFlags = HideFlags.DontSave;
            visualObject.transform.SetParent(null, false);
            visualObject.transform.localScale = new Vector3(spearLength, spearThickness, 1f);
            _visualRoot = visualObject.transform;

            _visualRenderer = visualObject.AddComponent<SpriteRenderer>();
            _visualRenderer.sprite = GetWhiteSprite();
            _visualRenderer.color = spearColor;
            _visualRenderer.sortingOrder = 30;

            _stuckCollider = visualObject.AddComponent<BoxCollider2D>();
            _stuckCollider.enabled = false;
            _stuckCollider.isTrigger = true;

            _stuckMarker = visualObject.AddComponent<PlayerSpearStuckMarker>();
            _stuckMarker.Initialize(this);
        }

        private void SetVisualVisible(bool visible)
        {
            if (_visualRoot == null)
            {
                return;
            }

            _visualRoot.gameObject.SetActive(visible);
        }

        private void ConfigureStuckCollider(bool enabled)
        {
            if (_stuckCollider == null || _stuckMarker == null || _visualRoot == null)
            {
                return;
            }

            _stuckCollider.enabled = enabled;
            if (!enabled)
            {
                _stuckCollider.isTrigger = true;
                _stuckCollider.size = Vector2.one;
                _stuckCollider.offset = Vector2.zero;
                _stuckMarker.SetActsAsOneWayPlatform(false);
                _visualRoot.localScale = new Vector3(spearLength, spearThickness, 1f);
                return;
            }

            var actsAsPlatform = Mathf.Abs(_spearDirection.x) > 0.01f;
            _visualRoot.localScale = new Vector3(exposedStuckLength, spearThickness, 1f);
            _stuckCollider.size = Vector2.one;
            _stuckCollider.offset = Vector2.zero;
            _stuckCollider.isTrigger = !actsAsPlatform;
            _stuckMarker.SetActsAsOneWayPlatform(
                actsAsPlatform,
                GetPlatformAxisFromDirection(_spearDirection));
        }

        private static OneWayPlatformMarker.PlatformAxis GetPlatformAxisFromDirection(Vector2 direction)
        {
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? OneWayPlatformMarker.PlatformAxis.Horizontal
                : OneWayPlatformMarker.PlatformAxis.Vertical;
        }

        private void EnterBulletTime()
        {
            Time.timeScale = bulletTimeScale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * bulletTimeScale;
        }

        private void ExitBulletTime()
        {
            if (Mathf.Approximately(Time.timeScale, 1f) && Mathf.Approximately(Time.fixedDeltaTime, _defaultFixedDeltaTime))
            {
                return;
            }

            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDeltaTime;
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

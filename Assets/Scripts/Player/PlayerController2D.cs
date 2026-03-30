using UnityEngine;
using SwordMetroidbrainia.Map;

namespace SwordMetroidbrainia
{
    // Handles player locomotion, collision resolution, grounding, and sword-triggered recoil.
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        public enum PullEndReason
        {
            None,
            ReachedTarget,
            Blocked,
            Canceled
        }

        private struct PullState
        {
            public bool IsActive;
            public Vector2 Target;
            public float Acceleration;
            public float MaxSpeed;
            public float StopDistance;
            public float CurrentSpeed;
            public Vector2 LastDirection;
        }

        private const float SkinWidth = 0.01f;
        private const int CastBufferSize = 8;
        private const int OverlapBufferSize = 16;

        [Header("Movement")]
        [SerializeField] private PlayerMovementSettings movement = new()
        {
            moveSpeed = 4f,
            airControlMultiplier = 0.85f,
            gravity = 18f,
            maxFallSpeed = 12f,
            minimumGroundMoveDistance = 0.35f,
            groundAlignmentStep = 0.1f
        };

        [Header("Ground Check")]
        [SerializeField] private PlayerGroundingSettings grounding = new()
        {
            solidLayers = ~0,
            groundCheckDistance = 0.08f,
            groundSnapDistance = 0.12f
        };

        [Header("Collision")]
        [SerializeField] private bool fitBoxColliderToOneCell = true;
        [SerializeField] private Vector2 collisionBoxSize = new(0.9f, 0.9f);
        [SerializeField] private Vector2 collisionBoxOffset = Vector2.zero;

        [Header("Recoil")]
        [SerializeField] private PlayerRecoilSettings recoil = new()
        {
            horizontal = new RecoilProfile
            {
                distance = 1f,
                duration = 0.16f,
                curve = null,
                exitSpeed = 4f
            },
            verticalUpward = new RecoilProfile
            {
                distance = 1f,
                duration = 0.16f,
                curve = null,
                exitSpeed = 4f
            },
            verticalDownward = new RecoilProfile
            {
                distance = 1f,
                duration = 0.16f,
                curve = null,
                exitSpeed = 4f
            },
            allowHorizontalMoveDuringVerticalRecoil = true,
            ignoreGravityDuringHorizontalRecoil = true,
            ignoreGravityDuringVerticalRecoil = true
        };

        [Header("One Way Platform")]
        [SerializeField, Min(0f)] private float oneWayPassThroughDuration = 0.08f;

        [Header("Pull Motion")]
        [SerializeField, Min(0f)] private float horizontalMomentumDecay = 16f;

        private readonly RaycastHit2D[] _castBuffer = new RaycastHit2D[CastBufferSize];
        private readonly Collider2D[] _overlapBuffer = new Collider2D[OverlapBufferSize];

        private Collider2D _collider2D;
        private Rigidbody2D _rigidbody2D;
        private PlayerInputReader _inputReader;
        private PlayerRespawnController _respawnController;
        private ContactFilter2D _solidFilter;
        private ContactFilter2D _overlapFilter;
        private ContactFilter2D _triggerOverlapFilter;
        private float _moveInput;
        private float _verticalVelocity;
        private bool _isGrounded;
        private int _facingDirection = 1;
        private Vector2 _currentPosition;
        private PlayerRecoilState _recoilState;
        private float _previousMoveInput;
        private float _minimumMoveRemaining;
        private float _minimumMoveDirection;
        private float _oneWayPassThroughTimer;
        private float _horizontalMomentumVelocity;
        private PullState _pullState;
        private PullEndReason _lastPullEndReason;

        public bool IsGrounded => _isGrounded;
        public int FacingDirection => _facingDirection;
        public Vector2 CurrentPosition => _currentPosition;
        public Collider2D BodyCollider => _collider2D;
        public bool IsPulling => _pullState.IsActive;

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _inputReader = GetComponent<PlayerInputReader>();
            _respawnController = GetComponent<PlayerRespawnController>();
            ApplyColliderShape();
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.simulated = true;
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.freezeRotation = true;

            _solidFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = grounding.solidLayers,
                useTriggers = false
            };

            _overlapFilter = _solidFilter;
            _triggerOverlapFilter = new ContactFilter2D
            {
                useLayerMask = false,
                useTriggers = true
            };
            _currentPosition = _rigidbody2D.position;
            EnsureDefaultCurves();
        }

        private void Update()
        {
            var moveVector = _inputReader.DigitalMove;
            var nextMoveInput = moveVector.x;
            if (_isGrounded && movement.minimumGroundMoveDistance > 0f)
            {
                var startedMove = Mathf.Approximately(_previousMoveInput, 0f) && !Mathf.Approximately(nextMoveInput, 0f);
                var reversedMove = !Mathf.Approximately(_previousMoveInput, 0f)
                    && !Mathf.Approximately(nextMoveInput, 0f)
                    && Mathf.Sign(_previousMoveInput) != Mathf.Sign(nextMoveInput);

                if (startedMove || reversedMove)
                {
                    _minimumMoveRemaining = GetAlignedGroundMoveDistance();
                    _minimumMoveDirection = Mathf.Sign(nextMoveInput);
                }
            }

            _moveInput = nextMoveInput;
            _previousMoveInput = nextMoveInput;

            if (_moveInput > 0f)
            {
                _facingDirection = 1;
            }
            else if (_moveInput < 0f)
            {
                _facingDirection = -1;
            }
        }

        private void FixedUpdate()
        {
            if (_oneWayPassThroughTimer > 0f)
            {
                _oneWayPassThroughTimer = Mathf.Max(0f, _oneWayPassThroughTimer - Time.fixedDeltaTime);
            }

            _currentPosition = _rigidbody2D.position;
            SyncBodyPosition();
            ResolveOverlaps();
            CheckSpecialOverlaps();
            UpdateGroundedState();

            var deltaTime = Time.fixedDeltaTime;
            var motion = Vector2.zero;

            if (_pullState.IsActive)
            {
                ProcessPullMotion(deltaTime);
                SyncBodyPosition();
                _rigidbody2D.MovePosition(_currentPosition);
                CheckSpecialOverlaps();
                UpdateGroundedState();
                return;
            }

            var horizontalMotion = GetHorizontalMoveDelta(deltaTime);
            if (horizontalMotion != 0f)
            {
                motion.x += horizontalMotion;
            }

            if (_recoilState.IsActive)
            {
                motion += GetRecoilDelta(deltaTime);
            }

            ApplyHorizontalMomentum(deltaTime, ref motion);
            ApplyGravity(deltaTime, ref motion);
            MoveCharacter(motion);
            SnapToGround();
            SnapHorizontalPositionToAlignment();
            ResolveOverlaps();
            SyncBodyPosition();
            _rigidbody2D.MovePosition(_currentPosition);
            CheckSpecialOverlaps();
            UpdateGroundedState();
        }

        public void ApplySwordRecoil(Vector2 direction)
        {
            ForceStopPull(false, PullEndReason.Canceled);
            _recoilState.Begin(direction);
            _verticalVelocity = 0f;

            if (Mathf.Abs(direction.x) < 0.01f && direction.y < -0.01f)
            {
                _oneWayPassThroughTimer = oneWayPassThroughDuration;
                DropThroughOneWayPlatformIfStanding();
            }
        }

        public void BeginPull(Vector2 target, float acceleration, float maxSpeed, float stopDistance)
        {
            _pullState = new PullState
            {
                IsActive = true,
                Target = target,
                Acceleration = Mathf.Max(0f, acceleration),
                MaxSpeed = Mathf.Max(0f, maxSpeed),
                StopDistance = Mathf.Max(0.01f, stopDistance),
                CurrentSpeed = 0f,
                LastDirection = (target - _currentPosition).sqrMagnitude > 0.0001f
                    ? (target - _currentPosition).normalized
                    : Vector2.zero
            };

            _lastPullEndReason = PullEndReason.None;
            _verticalVelocity = 0f;
            _horizontalMomentumVelocity = 0f;
            _recoilState.Stop();
            _minimumMoveRemaining = 0f;
        }

        public void CancelPull(bool preserveVelocity)
        {
            ForceStopPull(preserveVelocity, PullEndReason.Canceled);
        }

        public bool ConsumePullEndReason(out PullEndReason reason)
        {
            reason = _lastPullEndReason;
            if (reason == PullEndReason.None)
            {
                return false;
            }

            _lastPullEndReason = PullEndReason.None;
            return true;
        }

        public void TeleportTo(Vector2 worldPosition)
        {
            _pullState = default;
            _lastPullEndReason = PullEndReason.None;
            _recoilState.Stop();
            _verticalVelocity = 0f;
            _horizontalMomentumVelocity = 0f;
            _minimumMoveRemaining = 0f;
            _oneWayPassThroughTimer = 0f;
            _currentPosition = worldPosition;
            SyncBodyPosition();
            _rigidbody2D.MovePosition(_currentPosition);
            UpdateGroundedState();
        }

        private float GetHorizontalMoveDelta(float deltaTime)
        {
            if (_recoilState.IsActive && recoil.LocksHorizontalMovement(_recoilState.Direction))
            {
                _minimumMoveRemaining = 0f;
                return 0f;
            }

            // Horizontal movement is intentionally digital: pressing a direction goes straight to full speed.
            var control = _isGrounded ? 1f : movement.airControlMultiplier;
            var requestedDirection = _moveInput;
            if (Mathf.Approximately(requestedDirection, 0f) && _isGrounded && _minimumMoveRemaining > 0f)
            {
                requestedDirection = _minimumMoveDirection;
            }

            var frameMove = requestedDirection * movement.moveSpeed * control * deltaTime;
            if (!_isGrounded || _minimumMoveRemaining <= 0f || Mathf.Approximately(requestedDirection, 0f))
            {
                return frameMove;
            }

            var minimumStep = Mathf.Min(Mathf.Abs(frameMove), _minimumMoveRemaining);
            _minimumMoveRemaining = Mathf.Max(0f, _minimumMoveRemaining - minimumStep);
            return Mathf.Sign(requestedDirection) * minimumStep;
        }

        private Vector2 GetRecoilDelta(float deltaTime)
        {
            var profile = recoil.GetProfile(_recoilState.Direction);
            _recoilState.Elapsed += deltaTime;
            var normalizedTime = Mathf.Clamp01(_recoilState.Elapsed / Mathf.Max(profile.duration, 0.0001f));
            var curveValue = profile.curve.Evaluate(normalizedTime);
            var deltaProgress = curveValue - _recoilState.LastCurveValue;
            _recoilState.LastCurveValue = curveValue;

            // Recoil curves store cumulative travel progress, so each frame uses the delta from the previous sample.
            var recoilDelta = _recoilState.Direction * (profile.distance * deltaProgress);

            if (normalizedTime >= 1f)
            {
                ApplyRecoilExitSpeed(profile.exitSpeed);
                _recoilState.Stop();
            }

            return recoilDelta;
        }

        private void ApplyRecoilExitSpeed(float exitSpeed)
        {
            if (Mathf.Abs(_recoilState.Direction.y) > 0.01f && Mathf.Abs(_recoilState.Direction.x) < 0.01f)
            {
                _verticalVelocity = _recoilState.Direction.y * exitSpeed;
                return;
            }

            _verticalVelocity = 0f;
        }

        private void ApplyGravity(float deltaTime, ref Vector2 motion)
        {
            if (_isGrounded && _verticalVelocity <= 0f)
            {
                _verticalVelocity = 0f;
                return;
            }

            if (_recoilState.IsActive)
            {
                if (recoil.IgnoresGravity(_recoilState.Direction))
                {
                    _verticalVelocity = 0f;
                    return;
                }
            }

            // Downward speed accelerates until it hits the configured terminal fall speed.
            _verticalVelocity = Mathf.Max(_verticalVelocity - movement.gravity * deltaTime, -movement.maxFallSpeed);
            motion.y += _verticalVelocity * deltaTime;
        }

        private void ApplyHorizontalMomentum(float deltaTime, ref Vector2 motion)
        {
            if (Mathf.Abs(_horizontalMomentumVelocity) <= 0.001f)
            {
                _horizontalMomentumVelocity = 0f;
                return;
            }

            motion.x += _horizontalMomentumVelocity * deltaTime;
            _horizontalMomentumVelocity = Mathf.MoveTowards(_horizontalMomentumVelocity, 0f, horizontalMomentumDecay * deltaTime);
        }

        private void ProcessPullMotion(float deltaTime)
        {
            var toTarget = _pullState.Target - _currentPosition;
            var distanceToTarget = toTarget.magnitude;
            if (distanceToTarget <= _pullState.StopDistance)
            {
                ForceStopPull(false, PullEndReason.ReachedTarget);
                return;
            }

            var direction = toTarget / Mathf.Max(distanceToTarget, 0.0001f);
            _pullState.LastDirection = direction;
            _pullState.CurrentSpeed = Mathf.Min(_pullState.MaxSpeed, _pullState.CurrentSpeed + _pullState.Acceleration * deltaTime);
            var motion = direction * Mathf.Min(distanceToTarget, _pullState.CurrentSpeed * deltaTime);

            MoveCharacter(motion, out var blockedX, out var blockedY);
            if (blockedX || blockedY)
            {
                ForceStopPull(false, PullEndReason.Blocked);
                return;
            }

            SnapToGround();
            ResolveOverlaps();
        }

        private void ForceStopPull(bool preserveVelocity, PullEndReason reason)
        {
            if (!_pullState.IsActive)
            {
                return;
            }

            if (preserveVelocity)
            {
                _horizontalMomentumVelocity = _pullState.LastDirection.x * _pullState.CurrentSpeed;
                _verticalVelocity = _pullState.LastDirection.y * _pullState.CurrentSpeed;
            }
            else
            {
                _horizontalMomentumVelocity = 0f;
                _verticalVelocity = 0f;
            }

            _pullState = default;
            _lastPullEndReason = reason;
        }

        private void MoveCharacter(Vector2 motion)
        {
            MoveCharacter(motion, out _, out _);
        }

        private void MoveCharacter(Vector2 motion, out bool blockedX, out bool blockedY)
        {
            blockedX = false;
            blockedY = false;

            if (motion.x != 0f)
            {
                MoveAlongAxis(Vector2.right, motion.x, out blockedX);
            }

            if (motion.y != 0f)
            {
                MoveAlongAxis(Vector2.up, motion.y, out blockedY);
            }
        }

        private void MoveAlongAxis(Vector2 axis, float amount, out bool blocked)
        {
            blocked = false;
            var distance = Mathf.Abs(amount);
            if (distance <= 0f)
            {
                return;
            }

            var direction = axis * Mathf.Sign(amount);
            var hitCount = _collider2D.Cast(direction, _solidFilter, _castBuffer, distance + SkinWidth);
            var allowedDistance = distance;

            for (var i = 0; i < hitCount; i++)
            {
                if (ShouldIgnoreCollision(_castBuffer[i].collider, direction))
                {
                    continue;
                }

                if (_castBuffer[i].collider.TryGetComponent<DeathCellMarker>(out _))
                {
                    KillPlayer();
                    blocked = true;
                    return;
                }

                var hitDistance = _castBuffer[i].distance - SkinWidth;
                if (hitDistance < allowedDistance)
                {
                    allowedDistance = Mathf.Max(hitDistance, 0f);
                }
            }

            // Movement is solved one axis at a time so we can clamp travel cleanly against tile collisions.
            _currentPosition += direction * allowedDistance;
            SyncBodyPosition();

            if (allowedDistance + SkinWidth < distance)
            {
                blocked = true;
                if (axis == Vector2.up)
                {
                    _verticalVelocity = 0f;
                    if (amount > 0f)
                    {
                        _recoilState.Stop();
                    }
                }

                if (axis == Vector2.right
                    && _recoilState.IsActive
                    && Mathf.Abs(_recoilState.Direction.x) > 0.01f)
                {
                    _recoilState.Stop();
                }
            }
        }

        private void UpdateGroundedState()
        {
            var hitCount = _collider2D.Cast(Vector2.down, _solidFilter, _castBuffer, grounding.groundCheckDistance + SkinWidth);
            _isGrounded = false;
            for (var i = 0; i < hitCount; i++)
            {
                if (ShouldIgnoreCollision(_castBuffer[i].collider, Vector2.down))
                {
                    continue;
                }

                _isGrounded = true;
                break;
            }

            if (_isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = 0f;
            }
        }

        private void SnapToGround()
        {
            if (_verticalVelocity > 0f)
            {
                return;
            }

            if (_recoilState.IsActive && Mathf.Abs(_recoilState.Direction.y) > 0.01f)
            {
                return;
            }

            var hitCount = _collider2D.Cast(Vector2.down, _solidFilter, _castBuffer, grounding.groundSnapDistance + SkinWidth);
            if (hitCount <= 0)
            {
                return;
            }

            var closestDistance = float.MaxValue;
            for (var i = 0; i < hitCount; i++)
            {
                if (ShouldIgnoreCollision(_castBuffer[i].collider, Vector2.down))
                {
                    continue;
                }

                var hitDistance = _castBuffer[i].distance;
                if (hitDistance < closestDistance)
                {
                    closestDistance = hitDistance;
                }
            }

            if (closestDistance == float.MaxValue || closestDistance <= SkinWidth)
            {
                return;
            }

            var snapDistance = Mathf.Min(closestDistance - SkinWidth, grounding.groundSnapDistance);
            if (snapDistance <= 0f)
            {
                return;
            }

            // Ground snapping prevents the controller from visually hovering above flat ground after movement resolution.
            _currentPosition += Vector2.down * snapDistance;
            SyncBodyPosition();
        }

        private void ResolveOverlaps()
        {
            var overlapCount = _collider2D.OverlapCollider(_overlapFilter, _overlapBuffer);
            for (var i = 0; i < overlapCount; i++)
            {
                var other = _overlapBuffer[i];
                if (other == null || other == _collider2D)
                {
                    continue;
                }

                if (other.TryGetComponent<DeathCellMarker>(out _))
                {
                    KillPlayer();
                    return;
                }

                if (other.TryGetComponent<OneWayPlatformMarker>(out _))
                {
                    continue;
                }

                var distance = Physics2D.Distance(_collider2D, other);
                if (distance.isOverlapped || distance.distance < SkinWidth)
                {
                    var pushDistance = SkinWidth - distance.distance;
                    if (pushDistance > 0f)
                    {
                        // Push the controller back out of any accidental penetration before the next cast step.
                        _currentPosition -= distance.normal * pushDistance;
                        SyncBodyPosition();
                    }
                }
            }
        }

        private void CheckSpecialOverlaps()
        {
            var overlapCount = _collider2D.OverlapCollider(_triggerOverlapFilter, _overlapBuffer);
            for (var i = 0; i < overlapCount; i++)
            {
                var other = _overlapBuffer[i];
                if (other == null || other == _collider2D)
                {
                    continue;
                }

                if (other.TryGetComponent<SavePointMarker>(out var savePointMarker) && _respawnController != null)
                {
                    _respawnController.ActivateSavePoint(savePointMarker);
                }

                if (other.TryGetComponent<DeathCellMarker>(out _))
                {
                    KillPlayer();
                    return;
                }
            }

            overlapCount = _collider2D.OverlapCollider(_overlapFilter, _overlapBuffer);
            for (var i = 0; i < overlapCount; i++)
            {
                var other = _overlapBuffer[i];
                if (other == null || other == _collider2D)
                {
                    continue;
                }

                if (other.TryGetComponent<DeathCellMarker>(out _))
                {
                    KillPlayer();
                    return;
                }
            }
        }

        private void KillPlayer()
        {
            if (_respawnController == null)
            {
                return;
            }

            _respawnController.KillPlayer();
        }

        private void SyncBodyPosition()
        {
            transform.position = _currentPosition;
            Physics2D.SyncTransforms();
        }

        private void SnapHorizontalPositionToAlignment()
        {
            if (!_isGrounded)
            {
                return;
            }

            if (_recoilState.IsActive && Mathf.Abs(_recoilState.Direction.x) > 0.01f)
            {
                return;
            }

            if (!Mathf.Approximately(_moveInput, 0f))
            {
                return;
            }

            if (_minimumMoveRemaining > 0f)
            {
                return;
            }

            var alignmentStep = Mathf.Max(0.01f, movement.groundAlignmentStep);
            var alignedX = Mathf.Round(_currentPosition.x / alignmentStep) * alignmentStep;
            if (Mathf.Abs(alignedX - _currentPosition.x) <= 0.0001f)
            {
                return;
            }

            _currentPosition.x = alignedX;
            SyncBodyPosition();
        }

        private void EnsureDefaultCurves()
        {
            if (recoil.horizontal.curve == null)
            {
                // Horizontal recoil should feel snappy: front-loaded speed with a short tail.
                recoil.horizontal.curve = new AnimationCurve(
                    new Keyframe(0f, 0f, 5f, 5f),
                    new Keyframe(0.1f, 0.34f, 2.8f, 2.8f),
                    new Keyframe(0.34f, 0.7f, 1.1f, 1.1f),
                    new Keyframe(0.72f, 0.93f, 0.45f, 0.45f),
                    new Keyframe(1f, 1f, 0f, 0f));
            }

            if (recoil.verticalUpward.curve == null)
            {
                recoil.verticalUpward.curve = new AnimationCurve(
                    new Keyframe(0f, 0f, 5f, 5f),
                    new Keyframe(0.1f, 0.34f, 2.8f, 2.8f),
                    new Keyframe(0.34f, 0.7f, 1.1f, 1.1f),
                    new Keyframe(0.72f, 0.93f, 0.45f, 0.45f),
                    new Keyframe(1f, 1f, 0f, 0f));
            }

            if (recoil.verticalDownward.curve == null)
            {
                recoil.verticalDownward.curve = new AnimationCurve(
                    new Keyframe(0f, 0f, 5f, 5f),
                    new Keyframe(0.1f, 0.34f, 2.8f, 2.8f),
                    new Keyframe(0.34f, 0.7f, 1.1f, 1.1f),
                    new Keyframe(0.72f, 0.93f, 0.45f, 0.45f),
                    new Keyframe(1f, 1f, 0f, 0f));
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureDefaultCurves();
            collisionBoxSize.x = Mathf.Max(0.1f, collisionBoxSize.x);
            collisionBoxSize.y = Mathf.Max(0.1f, collisionBoxSize.y);
            movement.Validate();
            grounding.Validate();
            recoil.Validate();

            if (!Application.isPlaying)
            {
                _collider2D = GetComponent<Collider2D>();
                ApplyColliderShape();
            }
        }
#endif

        private void ApplyColliderShape()
        {
            if (!fitBoxColliderToOneCell)
            {
                return;
            }

            if (_collider2D is not BoxCollider2D boxCollider)
            {
                return;
            }

            // Keep the visible character tile-sized, but make the collision body slightly smaller
            // so a one-cell-wide passage is traversable with the kinematic controller.
            boxCollider.size = collisionBoxSize;
            boxCollider.offset = collisionBoxOffset;
        }

        private float GetAlignedGroundMoveDistance()
        {
            if (movement.minimumGroundMoveDistance <= 0f)
            {
                return 0f;
            }

            var alignmentStep = Mathf.Max(0.01f, movement.groundAlignmentStep);
            return Mathf.Ceil(movement.minimumGroundMoveDistance / alignmentStep) * alignmentStep;
        }

        private bool ShouldIgnoreCollision(Collider2D otherCollider, Vector2 castDirection)
        {
            if (otherCollider == null)
            {
                return false;
            }

            if (otherCollider.TryGetComponent<DeathCellMarker>(out _))
            {
                // Death zones should kill on touch, but they must never behave like solid ground or walls.
                return true;
            }

            if (!otherCollider.TryGetComponent<OneWayPlatformMarker>(out _))
            {
                return false;
            }

            if (_recoilState.IsActive
                && Mathf.Abs(_recoilState.Direction.x) < 0.01f
                && _recoilState.Direction.y < -0.01f
                && _oneWayPassThroughTimer > 0f)
            {
                // Upward sword slashes recoil the player downward. During that recoil window,
                // one-way platforms should not catch the player from above.
                return true;
            }

            if (castDirection.y > 0.01f)
            {
                return true;
            }

            if (Mathf.Abs(castDirection.x) > 0.01f)
            {
                return true;
            }

            var playerBottom = _collider2D.bounds.min.y;
            var platformTop = otherCollider.bounds.max.y;
            return playerBottom < platformTop - 0.02f;
        }

        private void DropThroughOneWayPlatformIfStanding()
        {
            var hitCount = _collider2D.Cast(Vector2.down, _solidFilter, _castBuffer, grounding.groundCheckDistance + SkinWidth);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = _castBuffer[i];
                var collider = hit.collider;
                if (collider == null || !collider.TryGetComponent<OneWayPlatformMarker>(out _))
                {
                    continue;
                }

                _currentPosition += Vector2.down * (grounding.groundCheckDistance + SkinWidth + 0.02f);
                _isGrounded = false;
                SyncBodyPosition();
                return;
            }
        }
    }
}

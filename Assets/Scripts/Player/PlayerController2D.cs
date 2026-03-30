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
            vertical = new RecoilProfile
            {
                distance = 1f,
                duration = 0.22f,
                curve = null,
                exitSpeed = 0f
            },
            allowHorizontalMoveDuringVerticalRecoil = true,
            ignoreGravityDuringHorizontalRecoil = true,
            ignoreGravityDuringVerticalRecoil = true
        };

        [Header("One Way Platform")]
        [SerializeField, Min(0f)] private float oneWayPassThroughDuration = 0.08f;

        private readonly RaycastHit2D[] _castBuffer = new RaycastHit2D[CastBufferSize];
        private readonly Collider2D[] _overlapBuffer = new Collider2D[OverlapBufferSize];

        private Collider2D _collider2D;
        private Rigidbody2D _rigidbody2D;
        private PlayerInputReader _inputReader;
        private ContactFilter2D _solidFilter;
        private ContactFilter2D _overlapFilter;
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

        public bool IsGrounded => _isGrounded;
        public int FacingDirection => _facingDirection;
        public Vector2 CurrentPosition => _currentPosition;
        public Collider2D BodyCollider => _collider2D;

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _inputReader = GetComponent<PlayerInputReader>();
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
            UpdateGroundedState();

            var deltaTime = Time.fixedDeltaTime;
            var motion = Vector2.zero;

            var horizontalMotion = GetHorizontalMoveDelta(deltaTime);
            if (horizontalMotion != 0f)
            {
                motion.x += horizontalMotion;
            }

            if (_recoilState.IsActive)
            {
                motion += GetRecoilDelta(deltaTime);
            }

            ApplyGravity(deltaTime, ref motion);
            MoveCharacter(motion);
            SnapToGround();
            SnapHorizontalPositionToAlignment();
            ResolveOverlaps();
            SyncBodyPosition();
            _rigidbody2D.MovePosition(_currentPosition);
            UpdateGroundedState();
        }

        public void ApplySwordRecoil(Vector2 direction)
        {
            _recoilState.Begin(direction);
            _verticalVelocity = 0f;

            if (Mathf.Abs(direction.x) < 0.01f && direction.y < -0.01f)
            {
                _oneWayPassThroughTimer = oneWayPassThroughDuration;
                DropThroughOneWayPlatformIfStanding();
            }
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

        private void MoveCharacter(Vector2 motion)
        {
            if (motion.x != 0f)
            {
                MoveAlongAxis(Vector2.right, motion.x);
            }

            if (motion.y != 0f)
            {
                MoveAlongAxis(Vector2.up, motion.y);
            }
        }

        private void MoveAlongAxis(Vector2 axis, float amount)
        {
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

                var hitDistance = _castBuffer[i].distance - SkinWidth;
                if (hitDistance < allowedDistance)
                {
                    allowedDistance = Mathf.Max(hitDistance, 0f);
                }
            }

            // Movement is solved one axis at a time so we can clamp travel cleanly against tile collisions.
            _currentPosition += direction * allowedDistance;
            SyncBodyPosition();

            if (allowedDistance + SkinWidth < distance && axis == Vector2.up)
            {
                _verticalVelocity = 0f;
                if (amount > 0f)
                {
                    _recoilState.Stop();
                }
            }

            if (allowedDistance + SkinWidth < distance
                && axis == Vector2.right
                && _recoilState.IsActive
                && Mathf.Abs(_recoilState.Direction.x) > 0.01f)
            {
                _recoilState.Stop();
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

            if (recoil.vertical.curve == null)
            {
                // Vertical recoil uses a longer profile so the player can hang a little longer in the air.
                recoil.vertical.curve = new AnimationCurve(
                    new Keyframe(0f, 0f, 4f, 4f),
                    new Keyframe(0.12f, 0.28f, 2.2f, 2.2f),
                    new Keyframe(0.42f, 0.68f, 0.9f, 0.9f),
                    new Keyframe(0.78f, 0.92f, 0.3f, 0.3f),
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
            if (otherCollider == null || !otherCollider.TryGetComponent<OneWayPlatformMarker>(out _))
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

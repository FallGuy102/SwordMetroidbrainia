using System;
using UnityEngine;

namespace SwordMetroidbrainia
{
    [Serializable]
    public struct PlayerMovementSettings
    {
        [Min(0f)] public float moveSpeed;
        [Min(0f)] public float airControlMultiplier;
        [Min(0f)] public float gravity;
        [Min(0f)] public float maxFallSpeed;
        [Min(0f)] public float minimumGroundMoveDistance;
        [Min(0.01f)] public float groundAlignmentStep;

        public void Validate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            airControlMultiplier = Mathf.Max(0f, airControlMultiplier);
            gravity = Mathf.Max(0f, gravity);
            maxFallSpeed = Mathf.Max(0f, maxFallSpeed);
            minimumGroundMoveDistance = Mathf.Max(0f, minimumGroundMoveDistance);
            groundAlignmentStep = Mathf.Max(0.01f, groundAlignmentStep);
        }
    }

    [Serializable]
    public struct PlayerGroundingSettings
    {
        public LayerMask solidLayers;
        [Min(0.01f)] public float groundCheckDistance;
        [Min(0.01f)] public float groundSnapDistance;

        public void Validate()
        {
            groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
            groundSnapDistance = Mathf.Max(groundCheckDistance, groundSnapDistance);
        }
    }

    [Serializable]
    public struct RecoilProfile
    {
        [Min(0f)] public float distance;
        [Min(0.01f)] public float duration;
        // Curves represent cumulative travel progress from 0..1 across the recoil lifetime.
        public AnimationCurve curve;
        // Exit speed is handed off to the movement system once recoil finishes.
        [Min(0f)] public float exitSpeed;

        public void Validate()
        {
            distance = Mathf.Max(0f, distance);
            duration = Mathf.Max(0.01f, duration);
            exitSpeed = Mathf.Max(0f, exitSpeed);
        }
    }

    [Serializable]
    public struct PlayerRecoilSettings
    {
        public RecoilProfile horizontal;
        public RecoilProfile vertical;
        public bool allowHorizontalMoveDuringVerticalRecoil;
        public bool ignoreGravityDuringHorizontalRecoil;
        public bool ignoreGravityDuringVerticalRecoil;

        public RecoilProfile GetProfile(Vector2 recoilDirection)
        {
            // Pure vertical recoil is tuned separately from horizontal recoil to match the intended game feel.
            var isVerticalRecoil = Mathf.Abs(recoilDirection.y) > 0.01f && Mathf.Abs(recoilDirection.x) < 0.01f;
            return isVerticalRecoil ? vertical : horizontal;
        }

        public bool IgnoresGravity(Vector2 recoilDirection)
        {
            return Mathf.Abs(recoilDirection.x) > 0.01f
                ? ignoreGravityDuringHorizontalRecoil
                : ignoreGravityDuringVerticalRecoil;
        }

        public bool LocksHorizontalMovement(Vector2 recoilDirection)
        {
            if (Mathf.Abs(recoilDirection.x) > 0.01f)
            {
                return true;
            }

            return !allowHorizontalMoveDuringVerticalRecoil;
        }

        public void Validate()
        {
            horizontal.Validate();
            vertical.Validate();
        }
    }

    public struct PlayerRecoilState
    {
        public bool IsActive;
        public Vector2 Direction;
        public float Elapsed;
        // Stores the last sampled cumulative curve value so we can extract per-frame travel deltas.
        public float LastCurveValue;

        public void Begin(Vector2 direction)
        {
            IsActive = true;
            Direction = direction.normalized;
            Elapsed = 0f;
            LastCurveValue = 0f;
        }

        public void Stop()
        {
            IsActive = false;
            Elapsed = 0f;
            LastCurveValue = 0f;
        }
    }
}

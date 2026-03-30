using SwordMetroidbrainia.Map;
using UnityEngine;

namespace SwordMetroidbrainia
{
    [RequireComponent(typeof(PlayerController2D))]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        private PlayerController2D _controller;
        private Vector2 _initialSpawnPosition;
        private SavePointMarker _activeSavePoint;

        private void Awake()
        {
            _controller = GetComponent<PlayerController2D>();
            _initialSpawnPosition = transform.position;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandleTrigger(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandleTrigger(other);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            HandleCollider(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            HandleCollider(collision.collider);
        }

        private void HandleTrigger(Collider2D other)
        {
            HandleCollider(other);
        }

        private void HandleCollider(Collider2D other)
        {
            if (other == null)
            {
                return;
            }

            if (other.TryGetComponent<SavePointMarker>(out var savePoint))
            {
                ActivateSavePoint(savePoint);
            }

            if (other.TryGetComponent<DeathCellMarker>(out _))
            {
                Respawn();
            }
        }

        public void ActivateSavePoint(SavePointMarker savePoint)
        {
            if (_activeSavePoint == savePoint)
            {
                return;
            }

            if (_activeSavePoint != null)
            {
                _activeSavePoint.SetActiveState(false);
            }

            _activeSavePoint = savePoint;
            _activeSavePoint.SetActiveState(true);
        }

        private void Respawn()
        {
            var respawnPosition = _activeSavePoint != null
                ? _activeSavePoint.RespawnPosition
                : _initialSpawnPosition;
            var spear = GetComponent<PlayerSpear>();
            if (spear != null)
            {
                spear.BreakFromOwnerDeath();
            }

            _controller.TeleportTo(respawnPosition);
        }

        public void KillPlayer()
        {
            Respawn();
        }
    }
}

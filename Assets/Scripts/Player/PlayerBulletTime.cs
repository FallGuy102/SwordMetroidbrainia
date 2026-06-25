using UnityEngine;

namespace SwordMetroidbrainia
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerBulletTime : MonoBehaviour
    {
        [SerializeField, Range(0.02f, 1f)] private float bulletTimeScale = 0.15f;

        private PlayerInputReader _inputReader;
        private float _defaultFixedDeltaTime;
        private bool _isBulletTimeActive;

        private void Awake()
        {
            _inputReader = GetComponent<PlayerInputReader>();
            _defaultFixedDeltaTime = Time.fixedDeltaTime;
        }

        private void OnDisable()
        {
            ExitBulletTime(force: true);
        }

        private void Update()
        {
            if (ShouldEnterBulletTime())
            {
                EnterBulletTime();
                return;
            }

            ExitBulletTime(force: false);
        }

        private bool ShouldEnterBulletTime()
        {
            return _inputReader != null && _inputReader.BulletTimeHeld;
        }

        private void EnterBulletTime()
        {
            if (_isBulletTimeActive)
            {
                return;
            }

            _isBulletTimeActive = true;
            ApplyTimeScale(bulletTimeScale);
        }

        private void ExitBulletTime(bool force)
        {
            if (!_isBulletTimeActive)
            {
                return;
            }

            // Do not fight systems that fully pause the game, such as the full-screen map.
            if (!force && Mathf.Approximately(Time.timeScale, 0f))
            {
                return;
            }

            _isBulletTimeActive = false;
            ApplyTimeScale(1f);
        }

        private void ApplyTimeScale(float timeScale)
        {
            Time.timeScale = timeScale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * timeScale;
        }

        private void OnValidate()
        {
            bulletTimeScale = Mathf.Clamp(bulletTimeScale, 0.02f, 1f);
        }
    }
}

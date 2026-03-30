using UnityEngine;

namespace SwordMetroidbrainia
{
    [DisallowMultipleComponent]
    public sealed class PlayerAbilityLoadout : MonoBehaviour
    {
        [SerializeField] private PlayerSword sword;
        [SerializeField] private PlayerSpear spear;

        public bool HasSword => sword != null && sword.IsUnlocked;
        public bool HasSpear => spear != null && spear.IsUnlocked;

        private void Awake()
        {
            if (sword == null)
            {
                sword = GetComponent<PlayerSword>();
            }

            if (spear == null)
            {
                spear = GetComponent<PlayerSpear>();
            }
        }

        public void UnlockSword()
        {
            SetSwordUnlocked(true);
        }

        public void LockSword()
        {
            SetSwordUnlocked(false);
        }

        public void UnlockSpear()
        {
            SetSpearUnlocked(true);
        }

        public void LockSpear()
        {
            SetSpearUnlocked(false);
        }

        public void SetSwordUnlocked(bool unlocked)
        {
            SetAbilityUnlocked(sword, unlocked);
        }

        public void SetSpearUnlocked(bool unlocked)
        {
            SetAbilityUnlocked(spear, unlocked);
        }

        public void SetCoreAbilitiesUnlocked(bool swordUnlocked, bool spearUnlocked)
        {
            SetSwordUnlocked(swordUnlocked);
            SetSpearUnlocked(spearUnlocked);
        }

        private static void SetAbilityUnlocked(IPlayerUnlockableAbility ability, bool unlocked)
        {
            if (ability == null)
            {
                return;
            }

            ability.SetUnlocked(unlocked);
        }
    }
}

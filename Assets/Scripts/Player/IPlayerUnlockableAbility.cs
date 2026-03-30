namespace SwordMetroidbrainia
{
    public interface IPlayerUnlockableAbility
    {
        bool IsUnlocked { get; }
        void Unlock();
        void Lock();
        void SetUnlocked(bool unlocked);
    }
}

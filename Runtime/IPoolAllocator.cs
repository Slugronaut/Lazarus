using UnityEngine;

namespace Peg.Lazarus
{
    /// <summary>
    /// 
    /// </summary>
    public interface IPoolAllocator
    {
        int MaxPoolSize { get; }
        int CountActive { get; }
        int CountInactive { get; }
        int CountAll { get; }
        int PoolIdentifier { get; }

        GameObject Summon();
        GameObject Recycle();
        void Relenquish(GameObject inst);
        void RelenquishAll();
        void Drain();
        void Dispose();
    }
}

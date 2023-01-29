using UnityEngine;

namespace Toolbox.Lazarus
{
    /// <summary>
    /// 
    /// </summary>
    public interface IPoolSystem
    {
        IPoolAllocator GetAssociatedPool(GameObject blueprint);
        GameObject Summon(GameObject blueprint, bool activate = true);
        GameObject Summon(GameObject blueprint, Vector3 position, bool activate = true);
        GameObject RecycleSummon(GameObject blueprint, Vector3 position, bool activate = true);
        void RelenquishToPool(GameObject gameObject);
        void RelenquishToPool(GameObject gameObject, float time);
        void RelenquishAll();
        void DrainAllPools();
        void DrainPool(IPoolAllocator pool);
        void ForceRecreatePool(GameObject blueprint, int chunkSize, int capacity, int maxPoolSize);
        int InactivePoolCount(GameObject blueprint);
        bool IsVirginPool(GameObject blueprint);
    }
}
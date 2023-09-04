using System;

namespace Peg.Lazarus
{
    /// <summary>
    /// A group of values that represent the pool alloc sizes associated with a specfic pool.
    /// </summary>
    [Serializable]
    public struct PoolSizeGroup
    {
        public int ChunkSize;
        public int Capacity;
        public int MaxPoolSize;
    }
}

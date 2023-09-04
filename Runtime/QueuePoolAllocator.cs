using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Peg.Lazarus
{
    /// <summary>
    /// Custom pool allocator using a Queue<> as a backing datatype.
    /// </summary>
    public class QueuePoolAllocator : IPoolAllocator
    {
        public int MaxPoolSize { get; private set; }
        public int CountActive => ActiveRefs.Count;
        public int CountInactive => PooledRefs.Count;
        public int CountAll => ActiveRefs.Count + PooledRefs.Count;
        public int PoolIdentifier => _PoolId;

        readonly public int _PoolId;
        readonly int ChunkSize;
        readonly static string OnRelenquishHandler = "OnRelenquish";
        readonly GameObject Blueprint;
        readonly Queue<GameObject> PooledRefs;
        readonly LinkedList<GameObject> ActiveRefs;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="blueprint">The prefab link to use when instantiating objects for this pool.</param>
        /// <param name="chunkSize">The number of objects to pre-allocate at a time when the pool is currently dry.</param>
        /// <param name="capacity">The pre-allocated number of elements at startup.</param>
        /// <param name="maxPoolSize">The max number of elements a pool can have pre-allocated. Requests beyond this range will still happen but released objects will be destroyed instead of being returned to the pool.</param>
        public QueuePoolAllocator(GameObject blueprint, int chunkSize, int maxPoolSize)
        {
            _PoolId = PoolId.PoolIdValue(blueprint);
            ChunkSize = chunkSize;
            MaxPoolSize = maxPoolSize;
            Blueprint = blueprint;
            PooledRefs = new();
            ActiveRefs = new();

            SceneManager.sceneUnloaded += HandleSceneUnload;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        void HandleSceneUnload(Scene scene)
        {
            Drain();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public GameObject Summon()
        {
            if (PooledRefs.Count == 0)
            {
                //our pool is dry, allocate a chunksize number of elements if we are still under the max total
                int allocCount = Mathf.Min(ChunkSize, MaxPoolSize - CountAll);
                if (allocCount < 1) allocCount = 1; //need a minimum of 1 just for this request at least

                for(int i = 0; i < allocCount; i++)
                {
                    var temp = GameObject.Instantiate(Blueprint);
                    if (temp.activeSelf)
                        temp.SetActive(false);
                    PooledRefs.Enqueue(temp);
                }
            }

            var obj = PooledRefs.Dequeue();
            ActiveRefs.AddLast(obj);
            return obj;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public GameObject Recycle()
        {
            if (CountInactive > 0 || CountAll < MaxPoolSize)
                return Summon();

            var inst = ActiveRefs.First.Value;
            ActiveRefs.RemoveFirst();
            ActiveRefs.AddLast(inst);

            inst.BroadcastMessage(OnRelenquishHandler, SendMessageOptions.DontRequireReceiver);
            if (inst.activeSelf) inst.SetActive(false);
            return inst;
        }

        /// <summary>
        /// It is assumed to already have been confirmed that this object came from this pool for sure.
        /// </summary>
        /// <param name="inst">An object previously obtained from this pool.</param>
        public void Relenquish(GameObject inst)
        {
            inst.BroadcastMessage(OnRelenquishHandler, SendMessageOptions.DontRequireReceiver);
            if (inst.activeSelf) 
                inst.SetActive(false);

            ActiveRefs.Remove(inst);
            if(PooledRefs.Count < MaxPoolSize)
                PooledRefs.Enqueue(inst);
            else GameObject.Destroy(inst);
        }

        /// <summary>
        /// Relenquishes all GameObjects associated with this pool.
        /// </summary>
        public void RelenquishAll()
        {
            while(ActiveRefs.Count > 0)
                Relenquish(ActiveRefs.First.Value);
        }

        /// <summary>
        /// Relenquishes all active objects associated with this pool and then destroys all inactive pooled objects.
        /// </summary>
        public void Drain()
        {
            RelenquishAll();
            while (PooledRefs.TryPeek(out var next))
            {
                GameObject.Destroy(next);
                PooledRefs.Dequeue();
            }
            PooledRefs.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            Drain();
        }

    }
}

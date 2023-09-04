using System.Collections.Generic;
using Peg.Util;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;


namespace Peg.Lazarus
{
    /// <summary>
    /// Custom pool allocator using Unity's built-in ObjectPool<> as a backing datatype.
    /// </summary>
    public class UnityPoolAllocator : IPoolAllocator
    {
        public int MaxPoolSize { get; private set; }
        public int CountActive => ObjPool.CountActive;
        public int CountInactive => ObjPool.CountInactive;
        public int CountAll => ObjPool.CountAll;
        public int PoolIdentifier => _PoolId;

        readonly public int _PoolId;
        readonly int ChunkSize;
        readonly static string OnRelenquishHandler = "OnRelenquish";
        readonly GameObject Blueprint;
        readonly ObjectPool<GameObject> ObjPool;
        readonly LinkedList<GameObject> ActiveRefs;
        public int ActiveCount;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="blueprint">The prefab link to use when instantiating objects for this pool.</param>
        /// <param name="chunkSize">The number of objects to pre-allocate at a time when the pool is currently dry.</param>
        /// <param name="capacity">The pre-allocated number of elements at startup.</param>
        /// <param name="maxPoolSize">The max number of elements a pool can have pre-allocated. Requests beyond this range will still happen but released objects will be destroyed instead of being returned to the pool.</param>
        public UnityPoolAllocator(GameObject blueprint, int chunkSize, int capacity, int maxPoolSize)
        {
            _PoolId = PoolIdValue(blueprint);
            ChunkSize = chunkSize;
            MaxPoolSize = maxPoolSize;
            Blueprint = blueprint;
            ActiveRefs = new();
            ObjPool = new ObjectPool<GameObject>(() =>
                    GameObject.Instantiate<GameObject>(Blueprint),
                    null, null, HandleDestroy, false, capacity, maxPoolSize
                    );
            SceneManager.sceneUnloaded += HandleSceneUnload;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inst"></param>
        static void HandleDestroy(GameObject inst)
        {
            GameObject.Destroy(inst);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scene"></param>
        void HandleSceneUnload(Scene scene)
        {
            RelenquishAll();
            ObjPool.Clear();
        }

        /// <summary>
        /// Helper for retreiving the number used to identify
        /// to what pool an object should belong.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public static int PoolIdValue(GameObject gameObject)
        {
            return gameObject.TryGetComponent<PoolId>(out var poolId) ? poolId.Id : gameObject.GetInstanceID();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public GameObject Summon()
        {
            var obj = ObjPool.Get();
            if (obj.activeSelf) obj.SetActive(false);
            ActiveRefs.AddLast(obj);

            //pre-allocate a 'ChunkSize' number of objects if we don't have
            //them in the pool and the pool isn't yet full
            int spaceLeft = MaxPoolSize - (ObjPool.CountInactive + ObjPool.CountActive);
            if (ObjPool.CountInactive < 1 && spaceLeft > 0)
            {
                int chunk = Mathf.Min(ChunkSize - 1, spaceLeft); //subtract 1 from chunksize due to the one we created above that is part of this chunk
                var list = SharedArrayFactory.RequestTempList<GameObject>();
                for (int i = 0; i < chunk; i++)
                {
                    var temp = ObjPool.Get();
                    if (temp.activeSelf) temp.SetActive(false);
                    list.Add(temp);
                }

                for (int i = 0; i < list.Count; i++)
                    ObjPool.Release(list[i]);
            }


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
        /// 
        /// </summary>
        /// <param name="inst">An object previously obtained from this pool.</param>
        public void Relenquish(GameObject inst)
        {
            inst.BroadcastMessage(OnRelenquishHandler, SendMessageOptions.DontRequireReceiver);
            if (inst.activeSelf)
                inst.SetActive(false);

            ActiveRefs.Remove(inst);
            ObjPool.Release(inst);
        }

        /// <summary>
        /// Relenquishes all GameObjects associated with this pool.
        /// </summary>
        public void RelenquishAll()
        {
            while (ActiveRefs.Count > 0)
                Relenquish(ActiveRefs.First.Value);
        }

        /// <summary>
        /// Relenquishes all active objects associated with this pool and then destroys all inactive pooled objects.
        /// </summary>
        public void Drain()
        {
            RelenquishAll();
            ObjPool.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            Drain();
            ObjPool.Dispose();
        }
    }
}
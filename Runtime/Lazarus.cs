/*
 * Ancient Craft Games
 * Copyright 2016-2023 James Clark
 */
using System.Collections.Generic;
using UnityEngine;
using System;
using Peg.AutoCreate;
using UnityEngine.Assertions;
using System.Runtime.Serialization;
using System.Linq;
using Sirenix.OdinInspector;

namespace Peg.Lazarus
{
    /// <summary>
    /// This is a general-purpose utility for instantiating 
    /// GameObjects at runtime using a pooling mechanism. This
    /// allows for very fast instantiations at the cost of
    /// using more memory. All allocation and pool management
    /// is handled automatically behind the scenes. Simply use
    /// 'Summon(somePrefab)' to get an object and
    /// 'RelenquishToPool(somePreviouslySummonedGameObject)' to return it.
    /// </summary>
    [AutoCreate(CreationActions.DeserializeSingletonData, typeof(IPoolSystem))]
    public sealed class Lazarus : IPoolSystem
    {
        public static IPoolSystem Instance { get; private set; }
        static PoolSizeGroup ReusablePair = new();
        public enum PoolAllocatorTypes
        {
            QueuePool,
            UnityObjectPool,
        }

        [Tooltip("The pre-allocated chunksize when nothing is specified for a given blueprint.")]
        public int DefaultChunkSize;
        [Tooltip("The capcity to use for pools when nothing is specified for a given blueprint.")]
        public int DefaultCapacity;
        [Tooltip("The max pool size to use for pools when nothing is specified for a given blueprint.")]
        public int DefaultMaxPoolSize;
        [Tooltip("The type of allocator used for managing the prefab pools.")]
        [InfoBox("Unity's built-in allocator ObjectPool<> currently has a bug that disallows it from properly tracking objects correctly. Do not use this allocator.")]
        public PoolAllocatorTypes PoolAllocatorType;
        [Tooltip("The number of preallocated and max allocated spaces for the pool for a given blueprint.")]
        public Peg.Util.HashMap<GameObject, PoolSizeGroup> PoolAllocations = new();
        

        Dictionary<int, IPoolAllocator> Pools;


        /// <summary>
        /// This is required because Odin's deserializer does NOT invoke any constructors and thus default values are never set.
        /// </summary>
        [OnDeserializing]
        void OnDeserializing()
        {
            DefaultChunkSize = 1;
            DefaultCapacity = 10;
            DefaultMaxPoolSize = 100;
            PoolAllocatorType = PoolAllocatorTypes.QueuePool;
            Pools = new();
        }

        /// <summary>
        /// Invoked by AutoCreator when this instance is auto-instantiated.
        /// </summary>
        void AutoAwake()
        {
            Instance = this;
        }

        /// <summary>
        /// Completely empties all pools
        /// </summary>
        public void DrainAllPools()
        {
            foreach (var pool in Pools.Values.ToList())
                pool.Drain();
        }

        /// <summary>
        /// Relenquishes all active objects associated with the given pool and then empties that pool of all inactive references, destroying them in the process.
        /// </summary>
        /// <param name="pool"></param>
        public void DrainPool(IPoolAllocator pool)
        {
            pool.Drain();
        }

        /// <summary>
        /// Returns a pool that is appropriate for storing identical copies of an object.
        /// </summary>
        public IPoolAllocator GetAssociatedPool(GameObject blueprint)
        {
            if(!Pools.TryGetValue(PoolId.PoolIdValue(blueprint), out var pool))
            {
                GetCapacity(blueprint, ref ReusablePair);
                pool = PoolAllocatorType switch
                {
                    PoolAllocatorTypes.QueuePool => new QueuePoolAllocator(blueprint, ReusablePair.ChunkSize, ReusablePair.MaxPoolSize),
                    PoolAllocatorTypes.UnityObjectPool => new UnityPoolAllocator(blueprint, ReusablePair.ChunkSize, ReusablePair.Capacity, ReusablePair.MaxPoolSize),
                    _ => throw new UnityException("A pool allocator type must be specificed in Lazarus before it can allocate any."),
                };
                Pools[pool.PoolIdentifier] = pool;
            }

            return pool;
        }

        /// <summary>
        /// Forces the recreation of a pool. In the process it drains the pool causing all associated objects
        /// to be relenquished and destroyed. If a pool doesn't already exist for the given blueprint then a
        /// new one will be created.
        /// </summary>
        public void ForceRecreatePool(GameObject blueprint, int chunkSize, int capacity, int maxPoolSize)
        {
            var poolId = PoolId.PoolIdValue(blueprint);
            if (Pools.TryGetValue(poolId, out var pool))
                pool.Dispose();

            pool = PoolAllocatorType switch
            {
                PoolAllocatorTypes.QueuePool => new QueuePoolAllocator(blueprint, chunkSize, maxPoolSize),
                PoolAllocatorTypes.UnityObjectPool => new UnityPoolAllocator(blueprint, chunkSize, capacity, maxPoolSize),
                _ => throw new UnityException("A pool allocator type must be specificed in Lazarus before it can allocate any."),
            };
            Pools[pool.PoolIdentifier] = pool;

        }

        /// <summary>
        /// Gets the capcity and max pool size specified for this blueprint or the defauls if none was specified.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        void GetCapacity(GameObject blueprint, ref PoolSizeGroup output)
        {
            if (!PoolAllocations.TryGetValue(blueprint, out output))
            {
                output.ChunkSize = DefaultChunkSize;
                output.Capacity = DefaultCapacity;
                output.MaxPoolSize = DefaultMaxPoolSize;
            }
        }

        /// <summary>
        /// Instantiates an copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and may
        /// be relenquished by calling this object's static 'Relenquish' method.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        public GameObject Summon(GameObject blueprint, bool activate = true)
        {
            return Summon(blueprint, blueprint.transform.position, activate);
        }

        /// <summary>
        /// Instantiates a copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and can
        /// be relenquished by calling this object's static 'Relenquish' method.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        public GameObject Summon(GameObject blueprint, Vector3 position, bool activate = true)
        {
            if (blueprint == null) throw new ArgumentNullException("blueprint");

            //don't use pools during editmode, it fucks everything
            #if UNITY_EDITOR
            if (!Application.isPlaying) return GameObject.Instantiate<GameObject>(blueprint);
            #endif

            var pool = GetAssociatedPool(blueprint);
            GameObject obj = pool.Summon();
            if (!obj.TryGetComponent<PoolId>(out var id))
                id = obj.AddComponent<PoolId>();
            id.Id = pool.PoolIdentifier;
            id.InPool = false;

            if (activate) obj.SetActive(activate);
            obj.transform.position = position;
            if (obj.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent))
                agent.Warp(position);
            
            return obj;
        }

        /// <summary>
        /// Instantiates a copied instance of the given GameObject. The
        /// returned object will be tracked internally by the pool and can
        /// be relenquished by calling the 'Relenquish' method.
        /// If the pool is dry a currently active object will be recycled and returned instead.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException" />
        public GameObject RecycleSummon(GameObject blueprint, Vector3 position, bool activate = true)
        {
            if (blueprint == null) throw new ArgumentNullException("blueprint");

            //don't use pools during editmode, it fucks everything
            #if UNITY_EDITOR
            if (!Application.isPlaying) return GameObject.Instantiate<GameObject>(blueprint);
            #endif

            var pool = GetAssociatedPool(blueprint);
            GameObject obj = pool.Recycle();
            if (!obj.TryGetComponent<PoolId>(out var id))
                id = obj.AddComponent<PoolId>();
            id.Id = pool.PoolIdentifier;
            id.InPool = false;

            if (activate) obj.SetActive(activate);
            obj.transform.position = position;
            if (obj.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent))
                agent.Warp(position);

            return obj;
        }

        /// <summary>
        /// Relenquishes an object to the internal pooling mechanism. For all intents
        /// and purposes, this should be treated the same as calling Destroy on the GameObject
        /// and it should no longer be accessed. If the internal pool is at maximum capacity 
        /// the GameObject will be destroyed.
        /// 
        /// Note that even if an object was not instantiated using a pool, it can still be
        /// relenquished to one, however, the pool it is placed in will not be the same one
        /// as any other copies that were instantiated using Summon().
        /// </summary>
        /// <param name="gameObject"></param>
        public void RelenquishToPool(GameObject gameObject)
        {
            Assert.IsNotNull(gameObject);

            //don't use pools during editmode, it fucks everything
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (UnityEditor.PrefabUtility.GetPrefabAssetType(gameObject) == UnityEditor.PrefabAssetType.NotAPrefab)
                    GameObject.DestroyImmediate(gameObject);
                else Debug.LogWarning("Could not destroy prefab: " + gameObject.name);
                return;
            }
            #endif

            var poolId = gameObject.GetComponent<PoolId>();
            Assert.IsNotNull(poolId, $"The GameObject '{gameObject.name}' does not have a PoolId component. All objects relenqished to the pool must have a PoolId component or have originated from a pool summon.");


            var pool = Pools[poolId.Id];
            if (pool != null)
                pool.Relenquish(gameObject);
            else
            {
                Debug.LogError($"The GameObject '{gameObject.name}' cannot be relenquished to any known pool and will be destroyed instead.");
                GameObject.Destroy(gameObject);
            }
        }

        /// <summary>
        /// Relenquishes an object to the internal pooling mechanism. For all intents
        /// and purposes, this should be treated the same as calling Destroy on the GameObject
        /// and it should no longer be accessed. If the internal pool is at maximum capacity 
        /// the GameObject will be destroyed.
        /// 
        /// Note that even if an object was not instantiated using a pool, it can still be
        /// relenquished to one, however, the pool it is placed in will not be the same one
        /// as any other copies that were instantiated using Summon().
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="time"></param>
        public void RelenquishToPool(GameObject gameObject, float time)
        {
            MEC.Timing.RunCoroutine(DelayedRelenquish(gameObject, time));
        }

        /// <summary>
        /// Helper for creating a deleayed relenquish function.
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="time"></param>
        IEnumerator<float> DelayedRelenquish(GameObject gameObject, float time)
        {
            yield return MEC.Timing.WaitForSeconds(time);
            RelenquishToPool(gameObject);
        }

        /// <summary>
        /// This forces all entities that came from any pool to immediately
        /// relenquish back to it. Note that this may cause some entities
        /// to become destroyed if its pool is at full capcity.
        /// </summary>
        public void RelenquishAll()
        {
            //there are no pooled resources when in edit mode
            #if UNITY_EDITOR
            if (!Application.isPlaying) return;
            #endif


            Debug.LogError("Relenquishing pooled objects...");
            foreach (var pool in Pools.Values)
                pool.RelenquishAll();
            Debug.LogError("... Relenquishing complete.");
        }

        /// <summary>
        /// Checks the number of inactive (pooled) objects for the given pool.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns></returns>
        public int InactivePoolCount(GameObject blueprint) => GetAssociatedPool(blueprint).CountInactive;

        /// <summary>
        /// Checks the total number of allocations for a pool. Both active, and inactive.
        /// </summary>
        /// <param name="blueprint"></param>
        /// <returns><c>true</c> if no active or inactive allocations exist. <c>false</c> otherwise.</returns>
        public bool IsVirginPool(GameObject blueprint) => GetAssociatedPool(blueprint).CountAll == 0;

    }
}



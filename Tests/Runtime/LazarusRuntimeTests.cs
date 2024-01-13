#if UNITY_EDITOR
#define UNITYOBJECTPOOLISBROKEN
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Peg.AutoCreate;
using UnityEngine.TestTools;
using System.Collections;
using System.Collections.Generic;


namespace Peg.Lazarus.Editor.Tests
{
    /// <summary>
    /// Base class for implementing tests. Derived classes are used to change allocator types.
    /// </summary>
    public abstract class LazarusRuntimeTests
    {
        public const string RootDir = "Packages/com.postegames.lazarus/Tests/Editor/";
        public const string Prefab001Path = RootDir + "Lazarus Test Prefab 001.prefab";
        public const string Prefab002Path = RootDir + "Lazarus Test Prefab 002.prefab";

        [AutoResolve]
        protected Lazarus Laz;

        protected int DefaultCap;
        protected int DefaultChunk;
        protected int DefaultMax;


        #region Helpers
        protected virtual void SetupTest()
        {
            StaticGlobalToTest.Flag1 = false;
            AutoCreator.Reset();
            AutoCreator.Initialize();
            AutoCreator.Resolve(this);
            DefaultCap = Laz.DefaultCapacity;
            DefaultChunk = Laz.DefaultChunkSize;
            DefaultMax = Laz.DefaultMaxPoolSize;

            Laz.DefaultCapacity = 1;
            Laz.DefaultChunkSize = 1;
            Laz.DefaultMaxPoolSize = 10;
        }

        protected void ShutdownTest()
        {
            Laz.DrainAllPools();
            Laz.DefaultCapacity = DefaultCap;
            Laz.DefaultChunkSize = DefaultChunk;
            Laz.DefaultMaxPoolSize = DefaultMax;
            AutoCreator.Reset();
            Laz = null;
        }

        /// <summary>
        /// Helper method for comparing references and ensuring all within the list are unique.
        /// </summary>
        /// <param name="list"></param>
        protected void AssertRefsAreUnique(List<GameObject> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = 0; j < list.Count; j++)
                {
                    if (i != j)
                        Assert.AreNotSame(list[i], list[j]);
                }
            }
        }
        #endregion


        [Test]
        public void FindPrefabTestAssets()
        {
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            Assert.NotNull(prefab1);

            if (UnityEditor.PrefabUtility.GetPrefabAssetType(prefab1) == UnityEditor.PrefabAssetType.NotAPrefab)
                Assert.Fail($"The prefab asset at '{Prefab001Path}' was not loaded as a prefab.");
        }

        [Test]
        public void AutoResolveLazarusSingleton()
        {
            AutoCreator.Initialize();
            AutoCreator.Resolve(this);
            Assert.NotNull(Laz);
            Assert.AreSame(Laz, AutoCreator.AsSingleton(typeof(Lazarus)));
            AutoCreator.Reset();
            Laz = null;
        }

        [Test]
        public void SummonOnePrefab()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);

            var inst1 = Laz.Summon(prefab1);
            Assert.NotNull(inst1);
            Assert.False(PrefabUtility.GetPrefabAssetType(prefab1) == PrefabAssetType.NotAPrefab);
            Assert.IsTrue(PrefabUtility.GetPrefabAssetType(inst1) == PrefabAssetType.NotAPrefab);
            Assert.AreNotSame(prefab1, inst1);

            ShutdownTest();
        }

        [Test]
        public void PoolTracksActiveAndInactiveSummons()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);

            //confirm pool is actually empty
            var pool = Laz.GetAssociatedPool(prefab1);
            Assert.NotNull(pool);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive); //also zero cause we have yet to instantiate a single thing

            //confirm pool created and tracking properly
            var inst1 = Laz.Summon(prefab1);
            var pool2 = Laz.GetAssociatedPool(prefab1);
            Assert.AreSame(pool, pool2);
            Assert.NotNull(pool);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            //confirm pool tracks count after relenquishing
            Laz.RelenquishToPool(inst1);
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);

            Laz.DrainAllPools();
            Assert.AreEqual(0, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            ShutdownTest();
        }

        [Test]
        public void SamePrefabAndInstanceRefenceSamePool()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            var inst1_1 = Laz.Summon(prefab1);
            Assert.AreNotSame(prefab1, inst1_1);

            var prefabPool = Laz.GetAssociatedPool(prefab1);
            var inst1_1Pool = Laz.GetAssociatedPool(inst1_1);
            Assert.AreSame(prefabPool, inst1_1Pool);

            var inst1_2 = Laz.Summon(prefab1);
            var inst1_2Pool = Laz.GetAssociatedPool(inst1_2);
            Assert.AreNotSame(inst1_2, inst1_1);
            Assert.AreSame(inst1_2Pool, inst1_1Pool);

            ShutdownTest();
        }

        [UnityTest]
        public IEnumerator ObjectsDestroyedWhenPoolIsDrained()
        {
            SetupTest();
            var prefab2 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab002Path);

            Assert.AreEqual(false, StaticGlobalToTest.Flag1);
            var inst2 = Laz.Summon(prefab2);
            Assert.AreEqual(true, StaticGlobalToTest.Flag1);
            Laz.DrainAllPools();

            //need to wait a frame for GameObject.Destroy to process
            yield return new WaitForEndOfFrame();
            Assert.AreEqual(false, StaticGlobalToTest.Flag1);

            ShutdownTest();
        }

        /// <summary>
        /// Ensures the pooling system properly pre-allocates chunks of objects when the pool is dry.
        /// </summary>
        [Test]
        public void PreAllocChunkObeyed()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            Laz.DefaultChunkSize = 5;
            Laz.DefaultCapacity = 5;
            Laz.DefaultMaxPoolSize = 10;

            Laz.Summon(prefab1);
            var pool = Laz.GetAssociatedPool(prefab1);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(4, pool.CountInactive);

            ShutdownTest();
        }

        [Test]
        public void PoolCountsAreCorrectWhenOverdrawn()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            Laz.DefaultChunkSize = 1;
            Laz.DefaultCapacity = 1;
            Laz.DefaultMaxPoolSize = 10;

            var pool = Laz.GetAssociatedPool(prefab1);
            for (int i = 0; i < 15; i++)
                Laz.Summon(prefab1);
            Assert.AreEqual(15, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            ShutdownTest();
        }

        [Test]
        public void UnderdrawnPoolSizeRemainsAfterRelenquish()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            Laz.DefaultChunkSize = 1;
            Laz.DefaultCapacity = 1;
            Laz.DefaultMaxPoolSize = 10;

            var pool = Laz.GetAssociatedPool(prefab1);
            List<GameObject> list = new(10);
            for (int i = 0; i < 10; i++)
                list.Add(Laz.Summon(prefab1));
            Assert.AreEqual(10, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            Laz.RelenquishToPool(list[9]);
            Assert.AreEqual(9, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);


            ShutdownTest();
        }

        [Test]
        public void AllSummonedRefsAreUnique()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);

            List<GameObject> list = new(15);
            for (int i = 0; i < 15; i++)
                list.Add(Laz.Summon(prefab1));
            AssertRefsAreUnique(list);

            ShutdownTest();
        }

        /// <summary>
        /// BUG: Currently fails when using UnityPoolAllocator due to a bug in Unity's ObjectPool<>
        /// which does not properly track the active count after returning items to a pool that
        /// is already full.
        /// </summary>
        [Test]
        public void OverdrawnPoolReturnsToMaxSizeAfterRelenquish()
        {
            SetupTest();
            /*
#if SKIP_UNITYOBJECTPOOL
            if (Laz.PoolAllocatorType == Lazarus.PoolAllocatorTypes.UnityObjectPool)
            {
                Assert.Inconclusive("Currently fails when using UnityPoolAllocator due to a bug in Unity's ObjectPool<> which does not properly track the active count after returning items to a pool that is already full.");
                ShutdownTest();
                return;
            }
#endif
            */
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            Laz.DefaultChunkSize = 1;
            Laz.DefaultCapacity = 1;
            Laz.DefaultMaxPoolSize = 10;

            var pool = Laz.GetAssociatedPool(prefab1);
            List<GameObject> list = new(15);
            for(int i = 0; i < 15; i++)
                list.Add(Laz.Summon(prefab1));
            Assert.AreEqual(15, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            //confirm all returned refs are to unique instances
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    if (i != j)
                        Assert.AreNotSame(list[i], list[j]);
                }
            }

            Laz.RelenquishToPool(list[14]);
            Assert.AreEqual(14, pool.CountActive);
            Assert.AreEqual(1, pool.CountInactive);

            Laz.RelenquishToPool(list[13]);
            Laz.RelenquishToPool(list[12]);
            Laz.RelenquishToPool(list[11]);
            Laz.RelenquishToPool(list[10]);
            Laz.RelenquishToPool(list[9]);
            Laz.RelenquishToPool(list[8]);
            Assert.AreEqual(8, pool.CountActive);
            Assert.AreEqual(7, pool.CountInactive);

            Laz.RelenquishToPool(list[7]);
            Laz.RelenquishToPool(list[6]);
            Laz.RelenquishToPool(list[5]);
            Laz.RelenquishToPool(list[4]);
            Laz.RelenquishToPool(list[3]);
#if UNITYOBJECTPOOLISBROKEN
            if (Laz.PoolAllocatorType == Lazarus.PoolAllocatorTypes.UnityObjectPool)
            {
                //we should be overdrawn by 2 here so if this fails it means Unity finally fixed their fucking shit.
                Assert.AreEqual(3, pool.CountActive-2);
                Assert.AreEqual(10, pool.CountInactive);
                Assert.Inconclusive("Currently fails when using UnityPoolAllocator due to a bug in Unity's ObjectPool<> which does not properly track the active count after returning items to a pool that is already full.");
                ShutdownTest();
                return;
            }
#else
            Assert.AreEqual(3, pool.CountActive);
            Assert.AreEqual(10, pool.CountInactive);
#endif

            ShutdownTest();
        }

        [Test]
        public void DifferentPrefabsUseDifferentPools()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            var prefab2 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab002Path);

            var inst1 = Laz.Summon(prefab1);
            var inst2 = Laz.Summon(prefab2);
            Assert.AreNotSame(inst1, inst2);

            var instPool1 = Laz.GetAssociatedPool(inst1);
            var instPool2 = Laz.GetAssociatedPool(inst2);
            Assert.AreNotSame(instPool1, instPool2);

            var prefabPool1 = Laz.GetAssociatedPool(prefab1);
            var prefabPool2 = Laz.GetAssociatedPool(prefab2);
            Assert.AreNotSame(prefabPool1, prefabPool2);
            Assert.AreSame(prefabPool1, instPool1);
            Assert.AreSame(prefabPool2, instPool2);


            ShutdownTest();
        }

        [Test]
        public void SummonWorksWhenPoolIsOverdrawn()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            var pool = Laz.GetAssociatedPool(prefab1);

            List<GameObject> list = new(10);
            for (int i = 0; i < 12; i++)
            {
                var item = Laz.Summon(prefab1, Vector3.zero);
                Assert.NotNull(item);
                list.Add(item);
            }
            AssertRefsAreUnique(list);

            Assert.AreEqual(12, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            ShutdownTest();
        }

        /// <summary>
        /// BUG: Currently fails when using UnityPoolAllocator due to a bug in Unity's ObjectPool<>
        /// which does not properly track the active count after returning items to a pool that
        /// is already full.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator RelenquishDestroysItemsWhenPoolIsFull()
        {
            SetupTest();
#if SKIP_UNITYOBJECTPOOL
            if(Laz.PoolAllocatorType == Lazarus.PoolAllocatorTypes.UnityObjectPool)
            {
                Assert.Inconclusive("Currently fails when using UnityPoolAllocator due to a bug in Unity's ObjectPool<> which does not properly track the active count after returning items to a pool that is already full.");
                ShutdownTest();
                yield break;
            }
#endif
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            var pool = Laz.GetAssociatedPool(prefab1);

            List<GameObject> list = new(10);
            for (int i = 0; i < 12; i++)
                list.Add(Laz.Summon(prefab1, Vector3.zero));
            Assert.AreEqual(12, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            for (int i = 0; i < 11; i++)
                Laz.RelenquishToPool(list[i]);
            Assert.AreEqual(1, pool.CountActive);
            Assert.AreEqual(10, pool.CountInactive);

            yield return null; //need to wait a frame for Destroy to complete on any objects that need it
            Assert.IsTrue(list[10] == null); //this one should be been destroyed since it couldn't go back into the pool

            ShutdownTest();
        }

        [Test]
        public void RecycleCreatesNewInstancesWhenMaxNotReached()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            var pool = Laz.GetAssociatedPool(prefab1);

            List<GameObject> list = new(10);
            for (int i = 0; i < 10; i++)
                list.Add(Laz.RecycleSummon(prefab1, Vector3.zero));
            AssertRefsAreUnique(list);
            Assert.AreEqual(10, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            ShutdownTest();
        }

        [Test]
        public void RecycleDrawsFromPoolFirst()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            var pool = Laz.GetAssociatedPool(prefab1);

            List<GameObject> list = new(10);
            for (int i = 0; i < 10; i++)
                list.Add(Laz.RecycleSummon(prefab1, Vector3.zero));

            Laz.RelenquishToPool(list[9]);
            var last = Laz.RecycleSummon(prefab1, Vector3.zero);
            Assert.AreSame(list[9], last);
            Assert.AreEqual(10, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);
            AssertRefsAreUnique(list);

            ShutdownTest();
        }

        [Test]
        public void RecycleDrawsOldestActiveWhenMaxReached()
        {
            SetupTest();
            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab001Path);
            var pool = Laz.GetAssociatedPool(prefab1);

            List<GameObject> list = new(10);
            for (int i = 0; i < 10; i++)
                list.Add(Laz.RecycleSummon(prefab1, Vector3.zero));

            var last = Laz.RecycleSummon(prefab1, Vector3.zero);
            Assert.AreSame(last, list[0]);
            Assert.AreEqual(10, pool.CountActive);
            Assert.AreEqual(0, pool.CountInactive);

            ShutdownTest();
        }
    }


    /// <summary>
    /// Runs all tests while using the Unity ObjectPool<> allocator.
    /// </summary>
    public class LazarusRuntimeTests_UnityAllocator : LazarusRuntimeTests
    {
        protected override void SetupTest()
        {
            base.SetupTest();
            Laz.PoolAllocatorType = Lazarus.PoolAllocatorTypes.UnityObjectPool;
        }
    }


    /// <summary>
    /// Runs all tests while using the Queue<> allocator.
    /// </summary>
    public class LazarusRuntimeTests_QueueAllocator : LazarusRuntimeTests
    {
        protected override void SetupTest()
        {
            base.SetupTest();
            Laz.PoolAllocatorType = Lazarus.PoolAllocatorTypes.QueuePool;
        }
    }
}
#endif
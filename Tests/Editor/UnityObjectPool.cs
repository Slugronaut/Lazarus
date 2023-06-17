using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;

namespace Toolbox.Lazarus.Editor.Tests
{
    /// <summary>
    /// 
    /// </summary>
    public class UnityObjectPool
    {
        const int DefCap = 10;
        const int MaxCap = 21;

        int GetCounter = 0;
        int ReleaseCounter = 0;

        ObjectPool<GameObject> TestPool;


        #region Utility
        [SetUp]
        public void Setup()
        {
            GetCounter = 0;
            ReleaseCounter = 0;
            TestPool = GetPool();
        }

        [TearDown]
        public void Teardown()
        {
            TestPool.Clear();
            TestPool = null;
        }

        ObjectPool<GameObject> GetPool()
        {
            return new ObjectPool<GameObject>(
                HandleCreate,
                HandleGetObj,
                HandleReleaseObj,
                HandleDestroy,
                true,
                DefCap,
                MaxCap
                );
        }

        GameObject HandleCreate()
        {
            var go = new GameObject("Test")
            {
                hideFlags = HideFlags.DontSave
            };
            return go;
        }

        void HandleGetObj(GameObject go)
        {

        }

        void HandleReleaseObj(GameObject go)
        {

        }

        void HandleDestroy(GameObject go)
        {
            GameObject.DestroyImmediate(go);
        }
        #endregion


        #region Inactive Counts
        [Test]
        public void InactiveCountOnFirstRequest()
        {
            Assert.AreEqual(0, TestPool.CountInactive);
            var go = TestPool.Get();
            Assert.AreEqual(0, TestPool.CountInactive);
        }

        [Test]
        public void InactiveCountOnFirstRelease()
        {
            Assert.AreEqual(0, TestPool.CountInactive);
            var go = TestPool.Get();
            Assert.AreEqual(0, TestPool.CountInactive);
            TestPool.Release(go);
            Assert.AreEqual(1, TestPool.CountInactive);
        }

        [Test]
        public void InactiveCountOnReleaseAfterDefault()
        {
            int cap = DefCap;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountInactive);
            for(int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            //should still be zero at this point
            Assert.AreEqual(0, TestPool.CountInactive);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
                Assert.AreEqual(i+1, TestPool.CountInactive);
            }
            Assert.AreEqual(cap, TestPool.CountInactive);
        }

        [Test]
        public void InactiveCountOnReleaseAfterMax()
        {
            int cap = MaxCap;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountInactive);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            //should still be zero at this point
            Assert.AreEqual(0, TestPool.CountInactive);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
                Assert.AreEqual(i + 1, TestPool.CountInactive);
            }
            Assert.AreEqual(cap, TestPool.CountInactive);
        }

        [Test]
        public void InactiveCountOnReleaseAfterExceedingMax()
        {
            int cap = MaxCap + 3;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountInactive);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            //should still be zero at this point
            Assert.AreEqual(0, TestPool.CountInactive);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
                //Assert.AreEqual(i + 1, TestPool.CountInactive);
            }
            Assert.AreEqual(MaxCap, TestPool.CountInactive);
        }
        #endregion


        #region Active Counts
        [Test]
        public void ActiveCountOnFirstRequest()
        {
            Assert.AreEqual(0, TestPool.CountActive);
            var go = TestPool.Get();
            Assert.AreEqual(1, TestPool.CountActive);
        }

        [Test]
        public void ActiveCountOnFirstRelease()
        {
            Assert.AreEqual(0, TestPool.CountActive);
            var go = TestPool.Get();
            Assert.AreEqual(1, TestPool.CountActive);
            TestPool.Release(go);
            Assert.AreEqual(0, TestPool.CountActive);
        }

        [Test]
        public void ActiveCountOnReleaseAfterDefault()
        {
            int cap = DefCap;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountActive);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            Assert.AreEqual(cap, TestPool.CountActive);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
            }
            Assert.AreEqual(0, TestPool.CountActive);
        }

        [Test]
        public void ActiveCountOnReleaseAfterMax()
        {
            int cap = MaxCap;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountActive);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            Assert.AreEqual(cap, TestPool.CountActive);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
            }
            Assert.AreEqual(0, TestPool.CountActive);
        }

        [Test]
        public void ActiveCountOnReleaseAfterExceedingMax()
        {
            int cap = MaxCap + 3;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountActive);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            Assert.AreEqual(cap, TestPool.CountActive);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
            }
            Assert.AreEqual(0, TestPool.CountActive);
        }
        #endregion


        #region Active Counts
        [Test]
        public void TotalCountOnFirstRequest()
        {
            Assert.AreEqual(0, TestPool.CountAll);
            var go = TestPool.Get();
            Assert.AreEqual(1, TestPool.CountAll);
        }

        [Test]
        public void TotalCountOnFirstRelease()
        {
            Assert.AreEqual(0, TestPool.CountAll);
            var go = TestPool.Get();
            Assert.AreEqual(1, TestPool.CountAll);
            TestPool.Release(go);
            Assert.AreEqual(1, TestPool.CountAll);
        }

        [Test]
        public void TotalCountOnReleaseAfterDefault()
        {
            int cap = DefCap;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountAll);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            Assert.AreEqual(cap, TestPool.CountAll);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
            }
            Assert.AreEqual(cap, TestPool.CountAll);
        }

        [Test]
        public void TotalCountOnReleaseAfterMax()
        {
            int cap = MaxCap;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountAll);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            Assert.AreEqual(cap, TestPool.CountAll);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
            }
            Assert.AreEqual(cap, TestPool.CountAll);
        }

        [Test]
        public void TotalCountOnReleaseAfterExceedingMax()
        {
            int cap = MaxCap + 3;
            List<GameObject> list = new(cap);
            Assert.AreEqual(0, TestPool.CountAll);
            for (int i = 0; i < cap; i++)
            {
                list.Add(TestPool.Get());
            }

            Assert.AreEqual(cap, TestPool.CountAll);

            for (int i = 0; i < cap; i++)
            {
                TestPool.Release(list[i]);
            }
            Assert.AreEqual(MaxCap, TestPool.CountAll);
        }
        #endregion
    }
}

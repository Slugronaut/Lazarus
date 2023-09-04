#if UNITY_EDITOR
using UnityEngine;

namespace Peg.Lazarus.Editor.Tests
{
    /// <summary>
    /// Used by Lazarus unit tests to determine if objects are being destroyed at the correct time.
    /// </summary>
    public class PoolDrainEventTester : MonoBehaviour
    {
        private void Awake()
        {
            StaticGlobalToTest.Flag1 = true;
        }

        private void OnDestroy()
        {
            StaticGlobalToTest.Flag1 = false;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public static class StaticGlobalToTest
    {
        public static bool Flag1;
    }
}
#endif
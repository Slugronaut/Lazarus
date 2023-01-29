using UnityEngine;
using System.Collections;


namespace Toolbox
{
    /// <summary>
    /// Somtimes the PoolPreloader has to allow objects
    /// to be active and enabled for at least one frame to ensure
    /// all systems are fully activated. It will attach this
    /// tempoaryary component to ensure it deactivated after updating.
    /// 
    /// This component will self destruct... *KABOOM*
    /// </summary>
    public class PreloadWarmup : MonoBehaviour
    {
        public static int ActiveFrames = 1;

        int Counter = 0;

        void Update()
        {
            if (Counter < ActiveFrames) Counter++;
            else
            {
                Destroy(this);
                gameObject.SetActive(false);
            }
        }
        
    }
}

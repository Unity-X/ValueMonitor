using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityX.ValueMonitor
{
    public class ValueMonitorDefaultClocks : MonoBehaviour
    {
#if UNITY_X_VALUE_MONITOR
        private Monitor.Clock _updateClock;
        private Monitor.Clock _fixedUpdateClock;

        private void Awake()
        {
            _updateClock = Monitor.GetOrCreateClock("Time.time Clock");
            _fixedUpdateClock = Monitor.GetOrCreateClock("Time.fixedTime Clock");
        }

        void Update()
        {
            _updateClock.Tick(Time.timeAsDouble);
        }

        private void FixedUpdate()
        {
            _fixedUpdateClock.Tick(Time.fixedTimeAsDouble);
        }
#endif
    }
}

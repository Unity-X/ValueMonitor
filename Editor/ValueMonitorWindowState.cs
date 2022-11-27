using UnityEditor;
using System.Collections.Generic;

namespace UnityX.ValueMonitor.Editor
{
    [FilePath("ValueMonitor/WindowState.txt", FilePathAttribute.Location.PreferencesFolder)]
    public class ValueMonitorWindowState : ScriptableSingleton<ValueMonitorWindowState>
    {
        [System.Serializable]
        public class StreamSetting
        {
            public string Id;
            public bool Visible = true;
        }

        [System.Serializable]
        public class ClockSetting
        {
            public string Id;
        }

        public List<StreamSetting> StreamSettings = new List<StreamSetting>();
        public List<ClockSetting> ClockSettings = new List<ClockSetting>();
        public string PreferredClockId = "";
        public bool InterpolateValues = false;
        public bool FollowLatestValuesInGraph = true;

        public void Save()
        {
            Save(saveAsText: true);
        }
    }
}
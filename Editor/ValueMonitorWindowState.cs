using UnityEditor;
using System.Collections.Generic;

namespace UnityX.ValueMonitor.Editor
{
    [FilePath("ValueMonitor/WindowState.txt", FilePathAttribute.Location.PreferencesFolder)]
    public class ValueMonitorWindowState : ScriptableSingleton<ValueMonitorWindowState>
    {
        [System.Serializable]
        public class DisplayedStream
        {
            public string Id;
            public bool Visible = true;
        }

        public List<DisplayedStream> StreamSettings = new List<DisplayedStream>();
    }
}
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityX.ValueMonitor.Editor
{
    internal static class EditorResources
    {
        internal static string ValueMonitorWindowStreamVisualTreeGUID => "faf729e26421f55448de42086e0972a4";
        internal static string ValueMonitorWindowVisualTreeGUID => "75f8a3adffc9ce140968f840f8c7217d";
        internal static string ValueMonitorWindowStyleSheetGUID => "0ec473a16bdb20d40ba4fb31a94715c1";
        internal static string GLGraphShaderGUID => "6935768a2297d814fad8054fff1f69ad";

        internal static VisualTreeAsset LoadValueMonitorWindowStreamVisualTreeAsset()
            => Load<VisualTreeAsset>(ValueMonitorWindowStreamVisualTreeGUID);

        internal static VisualTreeAsset LoadValueMonitorWindowVisualTreeAsset()
            => Load<VisualTreeAsset>(ValueMonitorWindowVisualTreeGUID);

        internal static StyleSheet LoadValueMonitorWindowStyleSheetAsset()
            => Load<StyleSheet>(ValueMonitorWindowStyleSheetGUID);

        internal static Shader LoadGLGraphShaderAsset()
            => Load<Shader>(GLGraphShaderGUID);

        internal static T Load<T>(string guid) where T : UnityEngine.Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}

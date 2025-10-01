using UnityEditor;

[InitializeOnLoad]
public class WebViewDefines
{
    static WebViewDefines()
    {
        var target = EditorUserBuildSettings.selectedBuildTargetGroup;
        if (target == BuildTargetGroup.Android)
        {
            // Unity 버전 호환성을 위해 pragma warning으로 deprecated 경고 억제
#pragma warning disable CS0618
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);

            if (!defines.Contains("UNITYWEBVIEW_ANDROID_USES_CLEARTEXT_TRAFFIC"))
            {
                defines += ";UNITYWEBVIEW_ANDROID_USES_CLEARTEXT_TRAFFIC";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
                UnityEngine.Debug.Log("Added UNITYWEBVIEW_ANDROID_USES_CLEARTEXT_TRAFFIC define");
            }
#pragma warning restore CS0618
        }
    }
}
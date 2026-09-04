using UnityEngine;
using UnityEditor;

public static class SetSdk
{
    public static void Run()
    {
        string ndk = "/Users/a1-6/Library/Android/sdk/ndk/23.1.7779620";
        string jdk = "/Applications/Tuanjie/Hub/Editor/2022.3.62t12/PlaybackEngines/AndroidPlayer/OpenJDK";
        // 正确的EditorPrefs keys (从反射获得)
        EditorPrefs.SetString("AndroidSdkRoot", "/Users/a1-6/Library/Android/sdk");
        EditorPrefs.SetString("AndroidNdkRootR23B", ndk);   // 关键: R23B后缀key!
        EditorPrefs.SetString("JdkPath", jdk);
        Debug.Log("AndroidNdkRootR23B=" + EditorPrefs.GetString("AndroidNdkRootR23B"));
        EditorApplication.Exit(0);
    }
}

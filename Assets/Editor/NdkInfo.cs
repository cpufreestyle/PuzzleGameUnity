using UnityEngine;
using UnityEditor;
using System.Reflection;

public static class NdkInfo
{
    static void DumpInstance(object inst, string label)
    {
        if (inst == null) { Debug.Log($"{label}: null"); return; }
        var t = inst.GetType();
        Debug.Log($"{label} [{t.Name}]");
        foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            try { Debug.Log($"  ip: {p.Name} = {p.GetValue(inst)}"); } catch {}
        }
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            try { Debug.Log($"  if: {f.Name} = {f.GetValue(inst)}"); } catch {}
        }
        foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (m.GetParameters().Length == 0 && m.ReturnType == typeof(string))
            {
                try { Debug.Log($"  im: {m.Name}() = {m.Invoke(inst, null)}"); } catch {}
            }
        }
    }

    public static void Run()
    {
        var asm = System.AppDomain.CurrentDomain.GetAssemblies();
        System.Type ndkRoot = null;
        foreach (var a in asm)
        {
            if (a.GetName().Name != "UnityEditor.Android.Extensions") continue;
            ndkRoot = a.GetType("UnityEditor.Android.AndroidNDKRoot");
        }
        if (ndkRoot == null) { Debug.Log("type missing"); EditorApplication.Exit(1); return; }

        // GetInstance
        var getInst = ndkRoot.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        object inst = getInst != null ? getInst.Invoke(null, null) : null;
        DumpInstance(inst, "Instance");

        // AndroidNDKTools
        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            if (a.GetName().Name != "UnityEditor.Android.Extensions") continue;
            var tools = a.GetType("UnityEditor.Android.AndroidNDKTools");
            if (tools != null)
            {
                foreach (var p in tools.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    try { Debug.Log($"NDKTools sp: {p.Name} = {p.GetValue(null)}"); } catch (System.Exception e) { Debug.Log($"NDKTools sp: {p.Name} ERR {e.Message.Substring(0,60)}"); }
                }
            }
        }
        EditorApplication.Exit(0);
    }
}

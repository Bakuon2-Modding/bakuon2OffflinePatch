using HarmonyLib;
using System;
using UnityEngine.SceneManagement;

namespace BakuonOfflinePatch
{
    [HarmonyPatch]
    public static class SceneLoadPatches
    {
        [HarmonyPatch(typeof(SceneManager), "LoadSceneAsync", new Type[] { typeof(string) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadSceneAsync_String_Prefix(string sceneName)
        {
            HitchMonitor.MarkEvent($"LoadSceneAsync:{sceneName}:Single");
        }

        [HarmonyPatch(typeof(SceneManager), "LoadSceneAsync", new Type[] { typeof(string), typeof(LoadSceneMode) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadSceneAsync_StringMode_Prefix(string sceneName, LoadSceneMode mode)
        {
            HitchMonitor.MarkEvent($"LoadSceneAsync:{sceneName}:{mode}");
        }

        [HarmonyPatch(typeof(SceneManager), "LoadSceneAsync", new Type[] { typeof(int) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadSceneAsync_Int_Prefix(int sceneBuildIndex)
        {
            HitchMonitor.MarkEvent($"LoadSceneAsync:#{sceneBuildIndex}:Single");
        }

        [HarmonyPatch(typeof(SceneManager), "LoadSceneAsync", new Type[] { typeof(int), typeof(LoadSceneMode) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadSceneAsync_IntMode_Prefix(int sceneBuildIndex, LoadSceneMode mode)
        {
            HitchMonitor.MarkEvent($"LoadSceneAsync:#{sceneBuildIndex}:{mode}");
        }

        [HarmonyPatch(typeof(SceneManager), "LoadScene", new Type[] { typeof(string) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadScene_String_Prefix(string sceneName)
        {
            HitchMonitor.MarkEvent($"LoadScene:{sceneName}:Single");
        }

        [HarmonyPatch(typeof(SceneManager), "LoadScene", new Type[] { typeof(string), typeof(LoadSceneMode) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadScene_StringMode_Prefix(string sceneName, LoadSceneMode mode)
        {
            HitchMonitor.MarkEvent($"LoadScene:{sceneName}:{mode}");
        }

        [HarmonyPatch(typeof(SceneManager), "LoadScene", new Type[] { typeof(int) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadScene_Int_Prefix(int sceneBuildIndex)
        {
            HitchMonitor.MarkEvent($"LoadScene:#{sceneBuildIndex}:Single");
        }

        [HarmonyPatch(typeof(SceneManager), "LoadScene", new Type[] { typeof(int), typeof(LoadSceneMode) })]
        [HarmonyPrefix]
        public static void SceneManager_LoadScene_IntMode_Prefix(int sceneBuildIndex, LoadSceneMode mode)
        {
            HitchMonitor.MarkEvent($"LoadScene:#{sceneBuildIndex}:{mode}");
        }
    }
}

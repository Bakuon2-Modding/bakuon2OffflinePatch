using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace BakuonOfflinePatch
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class OfflinePatchPlugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        private Harmony harmony;

        private void Awake()
        {
            Logger = base.Logger;
            // Harmonyパッチを適用
            harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();

            // ES2のセーブ/ロード操作をsaveData_offlineにリダイレクト
            // （オンライン版のセーブデータへの書き込みを防止）
            ES2RedirectPatches.ApplyPatches(harmony);

            // Unity AssetBundle キャッシュ（AppData\LocalLow\Unity）の誤削除を防止
            // （Caching.ClearCache 等は extern のため手動 Patch）
            CachingGuard.Apply(harmony);

            // Hitch monitor (frame spike logging + deferred heavy ops)
            HitchMonitor.Initialize();

            Logger.LogInfo($"Offline Patch v{PluginInfo.PLUGIN_VERSION} loaded");
        }

        private void OnDestroy()
        {
            if (harmony != null)
            {
                harmony.UnpatchSelf();
            }
        }
    }
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.bakuon.offlinepatch";
        public const string PLUGIN_NAME = "BakuonOfflinePatch";
        public const string PLUGIN_VERSION = "1.0.6";
    }

    /// <summary>
    /// 他のMODからオンラインモードを有効化するためのフック。
    /// OnlinePatch等のBepInDependency(offlinepatch)を持つMODからセットする。
    /// </summary>
    public static class OnlineMode
    {
        public static bool IsActive { get; set; } = false;
    }
    // ロガーヘルパークラス（時間付きログ）
    public static class LogHelper
    {
        public static void LogInfo(string message)
        {
            OfflinePatchPlugin.Logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void LogWarning(string message)
        {
            OfflinePatchPlugin.Logger.LogWarning($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }

        public static void LogError(string message)
        {
            OfflinePatchPlugin.Logger.LogError($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        }
    }
    // コルーチンを実行するためのヘルパー
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("OfflinePatch_CoroutineRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _instance = go.AddComponent<CoroutineRunner>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
            // AssetBundle関連の初期化
            LocalAssetBundleLoader.Initialize();
            AssetBundleSceneLoader.Initialize();
        }
    }
    // プラグイン起動時にCoroutineRunnerを初期化するパッチ
    [HarmonyPatch(typeof(GameManager), "Awake")]
    public static class GameManager_Awake_InitFloorGen_Patch
    {
        static void Postfix()
        {
            try
            {
                var runner = CoroutineRunner.Instance;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[FloorGenInit] 初期化エラー: {ex}");
            }
        }
    }
}

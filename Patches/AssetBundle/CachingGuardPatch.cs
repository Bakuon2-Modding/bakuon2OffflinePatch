using System;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BakuonOfflinePatch
{
    // ==========================================
    // Unity AssetBundle キャッシュ削除ガード
    // ==========================================
    // C:\Users\<user>\AppData\LocalLow\Unity\<company>_<product> に保存される
    // Unity の AssetBundle ダウンロードキャッシュが、オフライン起動時に
    // 誤って削除される現象への対策。
    //
    // ゲーム本体でキャッシュを削除するのは以下の4箇所のみ（すべて Caching 経由）:
    //   - AssetBundleDownloadSceneController.StartDownload  : Caching.ClearCache()           (isCleanCache 時)
    //   - AssetBundleManager.Download (coroutine)           : Caching.ClearAllCachedVersions(name) (CRC不一致時)
    //   - OAuthSceneManager.ClearChache                     : Caching.ClearCache()           (手動ボタン)
    //   - Utage WrapperUnityVersion.CleanCache              : Caching.ClearCache()           (Utage再DL時)
    //
    // よって Caching.ClearCache / ClearAllCachedVersions をガードすれば、
    // どの経路から呼ばれても確実に捕捉できる。
    //
    // 方針:
    //   - オフライン時 (!OnlineMode.IsActive): 削除をブロックし、呼び出し元をスタックトレースで記録する。
    //     オフラインでは本来キャッシュを再ダウンロードしないため、削除する必要が無い。
    //   - オンライン時 (OnlineMode.IsActive): 本来の動作を許可する（再DLが正当に行われるため）。
    //
    // Caching の各メソッドは extern (internal call) のため、属性ベースの PatchAll ではなく
    // Core.cs から手動で Patch する。失敗しても他パッチに影響しないよう個別に try/catch する。
    public static class CachingGuard
    {
        public static void Apply(Harmony harmony)
        {
            var prefix = new HarmonyMethod(
                typeof(CachingGuard).GetMethod(nameof(BlockPrefix), BindingFlags.NonPublic | BindingFlags.Static));

            TryGuard(harmony, prefix, "ClearCache", Type.EmptyTypes);
            TryGuard(harmony, prefix, "ClearCache", new[] { typeof(int) });
            TryGuard(harmony, prefix, "ClearAllCachedVersions", new[] { typeof(string) });
            TryGuard(harmony, prefix, "ClearCachedVersion", new[] { typeof(string), typeof(Hash128) });
        }

        private static void TryGuard(Harmony harmony, HarmonyMethod prefix, string methodName, Type[] args)
        {
            try
            {
                MethodInfo target = AccessTools.Method(typeof(Caching), methodName, args);
                if (target == null)
                {
                    // このオーバーロードは当該 Unity バージョンに存在しない
                    return;
                }

                harmony.Patch(target, prefix);
                LogHelper.LogInfo($"[CachingGuard] Caching.{methodName} をガードしました");
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"[CachingGuard] Caching.{methodName} のパッチに失敗しました（このバージョンでは未対応の可能性）: {ex.Message}");
            }
        }

        // Prefix: false を返して元メソッド（実際の削除）をスキップする
        private static bool BlockPrefix(MethodBase __originalMethod)
        {
            // オンラインモードでは本来の削除動作を許可する
            if (OnlineMode.IsActive)
            {
                return true;
            }

            // オフライン時はキャッシュ削除をブロックし、呼び出し元を記録する
            LogHelper.LogWarning(
                $"[CachingGuard] オフライン中に Caching.{__originalMethod.Name} が呼ばれました。" +
                "AppData の AssetBundle キャッシュ削除をブロックします。\n" +
                "呼び出し元:\n" + new StackTrace(1, true));

            return false;
        }
    }
}

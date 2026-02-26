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

    // ==========================================
    // ゲームバージョンチェックをバイパス
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "CheckGameVersion")]
    public static class NCMBManager_CheckGameVersion_Patch
    {
        static bool Prefix(NCMBManager __instance, string _gameVersion)
        {
            try
            {
                // バージョンチェックを成功として処理
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    SingletonMonoBehaviour<GameManager>.Instance.OnFinishedNetworkProcess_CheckGameVersion(true);
                }

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"CheckGameVersion パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // ゲストログインのパスワードチェックをスキップ
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "CheckGuestPassword")]
    public static class NCMBManager_CheckGuestPassword_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _password)
        {
            try
            {
                // ゲストログインを成功として処理
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    SingletonMonoBehaviour<GameManager>.Instance.userID = _userID;
                }

                if (SingletonMonoBehaviour<OAuthSceneManager>.Instance != null)
                {
                    SingletonMonoBehaviour<OAuthSceneManager>.Instance.PressedSkipOAuthButton();
                }

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"CheckGuestPassword パッチエラー: {ex}");
                return true;
            }
        }
    }


    // マスターパスワードチェックもスキップ
    [HarmonyPatch(typeof(NCMBManager), "CheckMasterPassword")]
    public static class NCMBManager_CheckMasterPassword_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _password)
        {
            try
            {
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    SingletonMonoBehaviour<GameManager>.Instance.userID = _userID;
                }

                if (SingletonMonoBehaviour<OAuthSceneManager>.Instance != null)
                {
                    SingletonMonoBehaviour<OAuthSceneManager>.Instance.PressedSkipOAuthButton();
                }

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"CheckMasterPassword パッチエラー: {ex}");
                return true;
            }
        }
    }


    // AssetBundleManagerのロードもスキップ（複数のオーバーロードに対応）

    // ==========================================
    // OAuthSceneManagerのAwakeでUIを即座に非表示にする（起動時のパッチ適用遅延防止）
    // ==========================================
    [HarmonyPatch(typeof(OAuthSceneManager), "Awake")]
    public static class OAuthSceneManager_Awake_Patch
    {
        static void Postfix(OAuthSceneManager __instance)
        {
            try
            {
                // OAuthシーンの全Canvasを無効化して描画を防止
                var canvases = __instance.GetComponentsInChildren<Canvas>(true);
                foreach (var canvas in canvases)
                {
                    canvas.enabled = false;
                }

                // シーン内のカメラも無効化（3D要素の描画防止）
                var cameras = __instance.GetComponentsInChildren<Camera>(true);
                foreach (var cam in cameras)
                {
                    cam.enabled = false;
                }

            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"[Auth] OAuth Awake UIhide error: {ex.Message}");
            }
        }
    }

    // ==========================================
    // OAuthSceneManagerの自動遷移を無効化
    // ==========================================
    [HarmonyPatch(typeof(OAuthSceneManager), "Start")]
    public static class OAuthSceneManager_Start_Patch
    {
        static bool Prefix()
        {
            try
            {
                // GameManagerの初期化
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;

                    // サーバーモード・ボットモードを無効化
                    gm.isServerMode = false;
                    gm.isBotMode = false;

                    // ゲストログインではなく本ログインとして扱う
                    gm.isGuestLogined = false;

                    // ユーザーIDを設定（未設定の場合）
                    if (string.IsNullOrEmpty(gm.userID))
                    {
                        gm.userID = "OfflineUser";
                    }

                    // AssetBundleロード済みフラグを立てる
                    gm.isLoadedAssetBundle = true;

                    // タイトルシーンに直接移動（OAuth画面をスキップ）
                    gm.LoadTitleScene();
                }

                return false; // 元のStartメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"OAuthSceneManager.Start パッチエラー: {ex}");
                return true; // エラー時は元のメソッドを実行
            }
        }
    }


    // PressedSkipOAuthButtonをパッチしてAssetBundleDownloadをスキップ
    [HarmonyPatch(typeof(OAuthSceneManager), "PressedSkipOAuthButton")]
    public static class OAuthSceneManager_PressedSkipOAuthButton_Patch
    {
        static bool Prefix()
        {
            try
            {
                // ユーザーIDを設定（SkipOAuthButtonが押された場合）
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;

                    // ゲストログインではなく本ログインとして扱う
                    gm.isGuestLogined = false;

                    if (string.IsNullOrEmpty(gm.userID))
                    {
                        gm.userID = "OfflineUser";
                    }

                    // AssetBundleロード済みフラグを立てる
                    gm.isLoadedAssetBundle = true;

                    // タイトルシーンに直接移動
                    gm.LoadTitleScene();
                }

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"OAuthSceneManager.PressedSkipOAuthButton パッチエラー: {ex}");
                return true;
            }
        }
    }


    // GameManagerのLoadOAuthSceneをパッチしてタイトルに戻す
    [HarmonyPatch(typeof(GameManager), "LoadOAuthScene")]
    public static class GameManager_LoadOAuthScene_Patch
    {
        static bool Prefix(GameManager __instance)
        {
            try
            {
                // AssetBundleロード済みフラグを立てる
                __instance.isLoadedAssetBundle = true;

                // タイトルシーンに移動
                __instance.LoadTitleScene();

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GameManager.LoadOAuthScene パッチエラー: {ex}");
                return true;
            }
        }
    }


    // GuestLoginの完了後にAssetBundleDownloadをスキップ
    [HarmonyPatch(typeof(OAuthSceneManager), "LoadAssetBundleDownloadScene")]
    public static class OAuthSceneManager_LoadAssetBundleDownloadScene_Patch
    {
        static bool Prefix()
        {
            try
            {
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    // ゲストログインフラグを解除（全機能を開放）
                    SingletonMonoBehaviour<GameManager>.Instance.isGuestLogined = false;

                    // AssetBundleロード済みフラグを立てる
                    SingletonMonoBehaviour<GameManager>.Instance.isLoadedAssetBundle = true;

                    // フェードイン
                    SingletonMonoBehaviour<GameManager>.Instance.FadeInEffect();

                    // タイトルシーンに直接移動
                    SingletonMonoBehaviour<GameManager>.Instance.LoadTitleScene();
                }

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"OAuthSceneManager.LoadAssetBundleDownloadScene パッチエラー: {ex}");
                return true;
            }
        }
    }


    // SuccessOAuthもパッチ
    [HarmonyPatch(typeof(OAuthSceneManager), "SuccessOAuth")]
    public static class OAuthSceneManager_SuccessOAuth_Patch
    {
        static bool Prefix()
        {
            try
            {
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("ログイン成功！");
                    SingletonMonoBehaviour<GameManager>.Instance.isGuestLogined = false;
                    SingletonMonoBehaviour<GameManager>.Instance.LoadCommonSaveData();

                    // AssetBundleロード済みフラグを立てる
                    SingletonMonoBehaviour<GameManager>.Instance.isLoadedAssetBundle = true;

                    // タイトルシーンに直接移動
                    SingletonMonoBehaviour<GameManager>.Instance.LoadTitleScene();
                }

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"OAuthSceneManager.SuccessOAuth パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // AssetBundleDownloadのスキップ
    // ==========================================

    // AssetBundleDownloadSceneControllerのStartメソッドをパッチ
    [HarmonyPatch(typeof(AssetBundleDownloadSceneController), "Start")]
    public static class AssetBundleDownloadSceneController_Start_Patch
    {
        static bool Prefix()
        {
            try
            {
                // AssetBundleを既にロード済みとしてマーク
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    SingletonMonoBehaviour<GameManager>.Instance.isLoadedAssetBundle = true;

                    // タイトル画面に移動
                    SingletonMonoBehaviour<GameManager>.Instance.LoadTitleScene();
                }

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"AssetBundleDownloadSceneController.Start パッチエラー: {ex}");
                return true;
            }
        }
    }


    // DownloadErrorメソッドもスキップ
    [HarmonyPatch(typeof(AssetBundleDownloadSceneController), "DownloadError")]
    public static class AssetBundleDownloadSceneController_DownloadError_Patch
    {
        static bool Prefix()
        {
            try
            {
                // AssetBundleを既にロード済みとしてマーク
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    SingletonMonoBehaviour<GameManager>.Instance.isLoadedAssetBundle = true;
                    SingletonMonoBehaviour<GameManager>.Instance.LoadTitleScene();
                }

                return false; // エラーダイアログを表示しない
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"AssetBundleDownloadSceneController.DownloadError パッチエラー: {ex}");
                return true;
            }
        }
    }

}

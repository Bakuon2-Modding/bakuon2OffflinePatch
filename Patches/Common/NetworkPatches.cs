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
    // PhotonNetworkのオフラインモードを有効化
    // ==========================================

    // StartConnectToMasterServerをバイパスしてオフラインモードに
    [HarmonyPatch(typeof(PUNController), "StartConnectToMasterServer")]
    public static class PUNController_StartConnectToMasterServer_Patch
    {
        static bool Prefix()
        {
            try
            {
                if (OnlineMode.IsActive) return true; // OnlinePatchに委ねる

                // オフラインモードを有効化
                PhotonNetwork.offlineMode = true;

                // OnConnectedToMasterを模擬的に呼び出す必要はない
                // 代わりに直接ルーム作成処理へ進む
                if (SingletonMonoBehaviour<PUNController>.Instance != null)
                {
                    SingletonMonoBehaviour<PUNController>.Instance.StartJoinOrCreateRoom();
                }

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"StartConnectToMasterServer パッチエラー: {ex}");
                return true;
            }
        }
    }


    // OnConnectedToMasterもスキップ（オフラインモードでは呼ばれないが、念のため）
    [HarmonyPatch(typeof(PUNController), "OnConnectedToMaster")]
    public static class PUNController_OnConnectedToMaster_Patch
    {
        static bool Prefix()
        {
            try
            {
                if (OnlineMode.IsActive) return true; // OnlinePatchに委ねる

                // オフラインモードでは既に接続済みなので、直接ルーム作成へ
                if (SingletonMonoBehaviour<PUNController>.Instance != null)
                {
                    SingletonMonoBehaviour<PUNController>.Instance.StartJoinOrCreateRoom();
                }

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"OnConnectedToMaster パッチエラー: {ex}");
                return true;
            }
        }
    }


    // StartPhotonProcess_OnFinishedFadeOutパッチ - オフラインモードでは直接LoadSceneを呼び出す
    [HarmonyPatch(typeof(PUNController), "StartPhotonProcess_OnFinishedFadeOut")]
    public static class PUNController_StartPhotonProcess_OnFinishedFadeOut_Patch
    {
        static bool Prefix(PUNController __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode)
                {
                    return true; // オフラインモードでない場合は元のメソッドを実行
                }

                // loadSceneNameを取得
                var loadSceneNameField = typeof(PUNController).GetField("loadSceneName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var loadSceneName = loadSceneNameField?.GetValue(__instance) as string;

                if (string.IsNullOrEmpty(loadSceneName))
                {
                    loadSceneName = "Home";
                    loadSceneNameField?.SetValue(__instance, loadSceneName);
                }

                // オフラインモードでルームを作成/参加
                var joinRoomNameField = typeof(PUNController).GetField("joinRoomName",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var joinRoomName = joinRoomNameField?.GetValue(__instance) as string;

                if (!string.IsNullOrEmpty(joinRoomName))
                {
                    // オフラインモードでルームを作成
                    PhotonNetwork.CreateRoom(joinRoomName, new RoomOptions { maxPlayers = 30 }, null);
                }

                // 直接LoadSceneを呼び出す
                __instance.LoadScene();

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"StartPhotonProcess_OnFinishedFadeOut パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // PhotonChatManagerの接続をバイパス
    // ==========================================
    [HarmonyPatch(typeof(PhotonChatManager), "Connect")]
    public static class PhotonChatManager_Connect_Patch
    {
        static bool Prefix()
        {
            try
            {
                if (OnlineMode.IsActive) return true; // OnlinePatchに委ねる

                return false; // 接続処理をスキップ
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"PhotonChatManager.Connect パッチエラー: {ex}");
                return true;
            }
        }
    }


    // SendOnlineStatus - オンラインステータス送信をスキップ
    [HarmonyPatch(typeof(PhotonChatManager), "SendOnlineStatus")]
    public static class PhotonChatManager_SendOnlineStatus_Patch
    {
        static bool Prefix()
        {
            try
            {
                if (OnlineMode.IsActive) return true; // OnlinePatchに委ねる

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"PhotonChatManager.SendOnlineStatus パッチエラー: {ex}");
                return true;
            }
        }
    }


    // PUNController.LoadSceneのオフライン対応
    // オリジナルの処理をそのまま実行（loadSceneNameが空の場合のみ補完）
    [HarmonyPatch(typeof(PUNController), "LoadScene")]
    public static class PUNController_LoadScene_Patch
    {
        static bool Prefix(PUNController __instance)
        {
            try
            {
                string loadSceneName = __instance.loadSceneName;

                // オフラインモードで loadSceneName が空の場合のみ、Homeシーンをデフォルトとして設定
                if (string.IsNullOrEmpty(loadSceneName) && PhotonNetwork.offlineMode)
                {
                    loadSceneName = "Home";
                    __instance.loadSceneName = loadSceneName;
                }

                // オリジナルの処理を実行
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"LoadScene パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // RejoinCurrentRoom - オフラインモードで現在のシーンをリロード
    // ==========================================
    // キャラクター変更時等に呼ばれるが、オフラインモードではAreaMovePatches経由で
    // シーン移動するため currentRoomName/currentSceneName/joinedRoomType が未設定。
    // 結果、常にホームに遷移してしまうので、現在のアクティブシーンをリロードする。
    [HarmonyPatch(typeof(PUNController), "RejoinCurrentRoom")]
    public static class PUNController_RejoinCurrentRoom_Patch
    {
        static bool Prefix()
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // 現在のアクティブシーン名を取得
                string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

                // _System サフィックスを除去してベースシーン名を取得
                string baseSceneName = activeSceneName;
                if (activeSceneName.EndsWith("_System"))
                {
                    baseSceneName = activeSceneName.Replace("_System", "");
                }

                SingletonMonoBehaviour<GameManager>.Instance.FadeOutEffect(delegate
                {
                    try
                    {
                        if (baseSceneName.StartsWith("City_") || baseSceneName.StartsWith("MMOField") ||
                            baseSceneName.StartsWith("Boss") || baseSceneName.StartsWith("TeamBattle") ||
                            baseSceneName == "Home")
                        {
                            // _Systemシーンをメインロード
                            string systemSceneName = baseSceneName + "_System";
                            UnityEngine.SceneManagement.SceneManager.LoadScene(systemSceneName);

                            // マップシーンを追加ロード
                            string[] mapSceneNames = new string[] {
                                baseSceneName,
                                baseSceneName.Replace("_Fixed", ""),
                            };

                            foreach (var mapName in mapSceneNames)
                            {
                                try
                                {
                                    UnityEngine.SceneManagement.SceneManager.LoadScene(mapName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(mapName);
                                    if (scene.IsValid() && scene.isLoaded)
                                    {
                                        break;
                                    }
                                }
                                catch { }
                            }

                            // BaseSystemSceneも追加ロード
                            UnityEngine.SceneManagement.SceneManager.LoadScene("BaseSystemScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
                        }
                        else
                        {
                            // その他のシーンはアクティブシーンをそのままリロード
                            UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
                            UnityEngine.SceneManagement.SceneManager.LoadScene("BaseSystemScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
                        }
                    }
                    catch (Exception sceneEx)
                    {
                        LogHelper.LogError($"[RejoinCurrentRoom] シーンリロードエラー: {sceneEx}");
                        // フォールバック: ホームに戻る
                        SingletonMonoBehaviour<PUNController>.Instance.loadSceneName = "Home";
                        SingletonMonoBehaviour<PUNController>.Instance.LoadScene();
                    }
                });

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[RejoinCurrentRoom] パッチエラー: {ex}");
                return true;
            }
        }
    }

}

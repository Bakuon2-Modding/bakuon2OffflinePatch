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
    // オフラインモードでの「現在の部屋名」を保持する
    // ==========================================
    // Photonオフラインモードでは LeaveRoom せずに CreateRoom しても
    // 「既に部屋にいる」ため no-op になり、PhotonNetwork.room.name は最初に
    // 作成した部屋(通常はHome)のまま固定されてしまう。
    // そのため room.name は信頼できない。PUNController が組み立てた joinRoomName
    // (原作と同じ "[識別子]:[userID]:[timestamp]:[gameVersion]" 形式) を遷移ごとに
    // 記録し、左下のフィールド名表示などの真値として使う。
    public static class OfflineRoomState
    {
        public static string CurrentRoomName;
    }

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
                    // 現在の部屋名として記録（CreateRoomが no-op でも左下表示の真値として使う）
                    OfflineRoomState.CurrentRoomName = joinRoomName;

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
                            // 待合室(MatchingRoom)・遺跡(Suteage_*)・アトラクション(CoopGame/DefenseGame 等)も
                            // _System シーン(インフラ=StartPosition) と 本体マップシーン(床コライダー) の
                            // デュアル構成。_System だけリロードすると床が生成されず、キャラ変更後に
                            // プレイヤーが床を抜けて落下し続けるため、if 側と同様に本体マップも追加ロードする。
                            UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);

                            // アクティブシーンが "_System" 付きなら、対応する本体マップ(床)を追加ロード
                            if (activeSceneName.EndsWith("_System"))
                            {
                                try
                                {
                                    UnityEngine.SceneManagement.SceneManager.LoadScene(baseSceneName, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                                }
                                catch (Exception mapEx)
                                {
                                    LogHelper.LogWarning($"[RejoinCurrentRoom] 本体マップ {baseSceneName} の追加ロード失敗: {mapEx.Message}");
                                }
                            }

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

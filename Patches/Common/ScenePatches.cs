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
    // GameManagerの初期化処理をパッチ
    // ==========================================
    [HarmonyPatch(typeof(GameManager), "Awake")]
    public static class GameManager_Awake_Patch
    {
        static void Postfix(GameManager __instance)
        {
            try
            {
                // デバッグモードの自動ログインを無効化
                if (__instance.debugSupportProperty != null)
                {
                    __instance.debugSupportProperty.isSkipTitle = false;
                    __instance.debugSupportProperty.isServerModeLogin = false;
                    __instance.debugSupportProperty.isBOTModeLogin = false;
                }

                // サーバーモード・ボットモードを強制的に無効化
                __instance.isServerMode = false;
                __instance.isBotMode = false;

                // AssetBundleを既にロード済みとしてマーク
                __instance.isLoadedAssetBundle = true;

                // 共通設定を saveData_offline から読み込む（LoadCommonSaveData はパッチ済み）
                __instance.LoadCommonSaveData();
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GameManager.Awake パッチエラー: {ex}");
            }
        }
    }


    // OnlineRoomController.Startでオフライン用の初期化を行う
    [HarmonyPatch(typeof(OnlineRoomController), "Start")]
    public static class OnlineRoomController_Start_Patch
    {
        // シーン名からルーム名を取得
        private static string GetRoomNameFromScene()
        {
            // 現在ロードされているシーンをチェック
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                string sceneName = scene.name;

                // シティシーンの判定
                if (sceneName.StartsWith("City_"))
                {
                    if (sceneName.Contains("Bakumatsu")) return "シティ:幕末志士";
                    if (sceneName.Contains("Nostal")) return "シティ:ノスタル志士";
                    if (sceneName.Contains("SDshishi")) return "シティ:SD志士";
                    if (sceneName.Contains("Kousoku")) return "シティ:光速の西郷";
                    if (sceneName.Contains("Kirizakikun")) return "シティ:キリザキ君は。";
                    if (sceneName.Contains("Tabentantei")) return "シティ:多弁探偵SAKA";
                    if (sceneName.Contains("RO")) return "シティ:シルバーナイト";
                    if (sceneName.Contains("EXLove")) return "シティ:獲威";
                    if (sceneName.Contains("Ketsuraku")) return "シティ:ケツラク";
                    if (sceneName.Contains("Mumi")) return "シティ:無味";
                    if (sceneName.Contains("FEZ")) return "シティ:パニ山パニ夫";
                    if (sceneName.Contains("LiveStadium")) return "シティ:ライブ会場";
                    if (sceneName.Contains("Last")) return "シティ:フィナーレ";
                    if (sceneName.Contains("Syakusaka")) return "シティ:尺坂の森";
                    if (sceneName.Contains("Syougatu")) return "シティ:幕末神社";
                    return "シティ";
                }

                // MMOFieldシーンの判定（元の形式: "{フィールド名}(CH{n})"）
                if (sceneName.StartsWith("MMOField"))
                {
                    if (sceneName.Contains("MMOField_1")) return "バクマツ平原(CH1)";
                    if (sceneName.Contains("MMOField_2")) return "ゲラゴッチ溶岩洞(CH1)";
                    if (sceneName.Contains("MMOField_3")) return "シロマリモ氷海域(CH1)";
                    if (sceneName.Contains("MMOField_4")) return "ビワオウ浮遊回廊(CH1)";
                    return "MMOフィールド";
                }

                // ステアゲ遺跡シーンの判定
                if (sceneName.StartsWith("Suteage_"))
                {
                    return "ステアゲイル遺跡";
                }

                // 待合室
                if (sceneName == "MatchingRoom")
                {
                    // matchingRoomDataからゲーム名を取得
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;
                    if (gm != null && gm.matchingRoomData != null && !string.IsNullOrEmpty(gm.matchingRoomData.gameName))
                    {
                        return gm.matchingRoomData.gameName + " 待合室";
                    }
                    return "待合室";
                }

                // アトラクション・バトル系シーン（待合室経由で matchingRoomData.gameName に名前が設定される）
                if (sceneName.StartsWith("CoopGame_") || sceneName == "DefenseGame" ||
                    sceneName.StartsWith("TeamBattle") || sceneName.StartsWith("SoloBattle") ||
                    sceneName.StartsWith("WarMain") || sceneName.StartsWith("WarSub") ||
                    sceneName.StartsWith("WarCarry") || sceneName.StartsWith("Boss") ||
                    sceneName.StartsWith("CharacterEpisode"))
                {
                    var gm2 = SingletonMonoBehaviour<GameManager>.Instance;
                    if (gm2 != null && gm2.matchingRoomData != null && !string.IsNullOrEmpty(gm2.matchingRoomData.gameName))
                    {
                        return gm2.matchingRoomData.gameName;
                    }
                }

                // Homeシーン（元の形式: "{プレイヤー名}のマイホーム"）
                if (sceneName == "Home")
                {
                    string playerName = SingletonMonoBehaviour<GameManager>.Instance != null
                        ? SingletonMonoBehaviour<GameManager>.Instance.playerName : "";
                    if (!string.IsNullOrEmpty(playerName))
                        return playerName + "のマイホーム";
                    return "マイホーム";
                }
            }

            return "オフラインルーム";
        }

        static bool Prefix(OnlineRoomController __instance)
        {
            try
            {
                // ユーザーコンテンツデータを先読みしてフィールド入場中にES2 I/Oを済ませる
                OfflineUserContentsStore.EnsureLoaded();

                SingletonMonoBehaviour<GameManager>.Instance.CheckLostItem();

                if (SingletonMonoBehaviour<MenuScreenManager>.Instance)
                {
                    SingletonMonoBehaviour<MenuScreenManager>.Instance.gameObject.SetActive(true);
                    SingletonMonoBehaviour<MenuScreenManager>.Instance.Initialize();
                }

                if (SingletonMonoBehaviour<ChatInputManager>.Instance)
                {
                    SingletonMonoBehaviour<ChatInputManager>.Instance.ResetChatInterface();
                }

                // ゲストログイン時の処理
                if (SingletonMonoBehaviour<GameManager>.Instance.isGuestLogined)
                {
                    if (__instance.myRoomPortal) __instance.myRoomPortal.SetActive(false);
                    if (__instance.nameChangeNPC) __instance.nameChangeNPC.SetActive(false);
                }

                PhotonNetwork.isMessageQueueRunning = true;

                // StartPositionを取得
                var startPositionObject = GameObject.Find("StartPosition");
                if (startPositionObject != null)
                {
                    var field = __instance.GetType().GetField("startPositionObject", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(__instance, startPositionObject);
                    }
                }
                else
                {
                    LogHelper.LogWarning("StartPositionが見つかりません");
                }

                // ルーム名を設定（シーン名から判定）
                string roomName = GetRoomNameFromScene();
                if (SingletonMonoBehaviour<MenuScreenManager>.Instance)
                {
                    SingletonMonoBehaviour<MenuScreenManager>.Instance.DisplayFieldName(roomName);
                }

                // プレイヤーを生成
                if (!SingletonMonoBehaviour<GameManager>.Instance.isServerMode)
                {
                    __instance.CreatePlayerPrefab();

                    try
                    {
                        if (SingletonMonoBehaviour<MenuScreenManager>.Instance)
                        {
                            SingletonMonoBehaviour<MenuScreenManager>.Instance.RenewCharacterFaceImage();
                        }
                    }
                    catch (Exception faceEx)
                    {
                        LogHelper.LogWarning($"RenewCharacterFaceImageでエラー（スキップして続行）: {faceEx.Message}");
                    }
                }

                // フェードイン効果
                SingletonMonoBehaviour<GameManager>.Instance.FadeInEffect();
                SingletonMonoBehaviour<GameManager>.Instance.ResizeSafeArea();

                // チュートリアル表示
                if (SingletonMonoBehaviour<TipsManager>.Instance && !SingletonMonoBehaviour<TipsManager>.Instance.isFinishedTutorial)
                {
                    SingletonMonoBehaviour<TipsManager>.Instance.ShowTutorial();
                }

                // Home床生成は Home/HomePatches.cs で処理

                // 元のメソッドをスキップ
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"OnlineRoomController.Start パッチエラー: {ex}");

                // エラーが起きても元のメソッドをスキップ（オフラインモードでは元のメソッドは動作しない）
                // フェードインだけは試みる
                try
                {
                    if (SingletonMonoBehaviour<GameManager>.Instance != null)
                    {
                        SingletonMonoBehaviour<GameManager>.Instance.FadeInEffect();
                        SingletonMonoBehaviour<GameManager>.Instance.ResizeSafeArea();
                    }
                }
                catch { }

                return false;
            }
        }
    }


    // ==========================================
    // MatchingRoom（待合室）スキップ処理 - 無効化
    // ==========================================
    // AssetBundleにMatchingRoomシーンが含まれているため、待合室を使用可能。
    // このパッチは無効化されています。
    //
    // 有効化方法: 下記のコメントを外すと待合室がスキップされます。
    /*
    [HarmonyPatch(typeof(MatchingRoomController), "Start")]
    public static class MatchingRoomController_Start_Patch
    {
        static bool Prefix(MatchingRoomController __instance)
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                string nextScene = gm.matchingRoomNextLoadSceneName;

                if (string.IsNullOrEmpty(nextScene))
                {
                    LogHelper.LogWarning("[MatchingRoom] matchingRoomNextLoadSceneNameが未設定 - スキップ不可");
                    return true; // 元のメソッドを実行
                }

                // 元のStartで行われる初期化を再現
                // 重要: currentSuteagePlayType を設定（元コードの line 43）
                gm.currentSuteagePlayType = SuteagePlayerDataProperty.enumPlayType.MULTIPLAY;
                gm.appendSuteageFloor = 0;

                // PUNControllerにロード先シーンを設定して遷移
                SingletonMonoBehaviour<PUNController>.Instance.loadSceneName = nextScene;
                gm.FadeOutEffect(new GameManager.DelegateOnFinishedFadeOut(() =>
                {
                    SingletonMonoBehaviour<PUNController>.Instance.LoadScene();
                }));

                return false; // 元のStartをスキップ
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[MatchingRoom] スキップ処理エラー: {ex}");
                return true; // エラー時は元のメソッドにフォールバック
            }
        }
    }
    */


    // GetPlayerInstantiatePositionのnullチェック
    [HarmonyPatch(typeof(OnlineRoomController), "GetPlayerInstantiatePosition")]
    public static class OnlineRoomController_GetPlayerInstantiatePosition_Patch
    {
        static void Prefix(OnlineRoomController __instance)
        {
            try
            {
                // startPositionObjectがnullの場合、デフォルトのStartPositionを探して設定
                var field = __instance.GetType().GetField("startPositionObject", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var startPos = field.GetValue(__instance) as GameObject;
                    if (startPos == null)
                    {
                        // StartPositionを探す
                        startPos = GameObject.Find("StartPosition");
                        if (startPos == null)
                        {
                            // それでもnullなら、ダミーのGameObjectを作成
                            startPos = new GameObject("DummyStartPosition");
                            startPos.transform.position = Vector3.zero;
                            OfflinePatchPlugin.Logger.LogWarning("StartPositionが見つからないため、ダミーを作成しました");
                        }
                        field.SetValue(__instance, startPos);
                    }
                }
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GetPlayerInstantiatePosition パッチエラー: {ex}");
            }
        }
    }


    // ==========================================
    // CheckLostItemをスキップ（オフラインモード用）
    // ==========================================
    [HarmonyPatch(typeof(GameManager), "CheckLostItem")]
    public static class GameManager_CheckLostItem_Patch
    {
        static bool Prefix(GameManager __instance)
        {
            try
            {
                // サーバーモードでなければチェックをスキップ
                if (__instance.isServerMode)
                {
                    return true;
                }

                // stockItemDataListを初期化（null対策）
                if (__instance.stockItemDataList == null)
                {
                    __instance.stockItemDataList = new System.Collections.Generic.List<ItemData>();
                }

                // 空の場合はダミーアイテムを追加
                if (__instance.stockItemDataList.Count == 0)
                {
                    ItemData dummyItem = new ItemData();
                    dummyItem.Initialize(ItemData.enumItemID.Item_Gold);
                    __instance.stockItemDataList.Add(dummyItem);
                }

                // チェックをスキップ（エラーを出さない）
                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"CheckLostItem パッチエラー: {ex}");
                return true; // エラー時は元のメソッドを実行
            }
        }
    }

}

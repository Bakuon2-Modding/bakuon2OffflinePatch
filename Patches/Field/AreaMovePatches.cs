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
    // フィールド移動時に選択したチャンネルを保持する
    // （シーン名だけでは CH 番号を復元できないため。
    //  MMOFieldは複数CHが同一シーンを共有する）
    // ==========================================
    public static class OfflineFieldSelection
    {
        public static string LoadSceneName;   // 例: "MMOField_1"
        public static int Channel;            // 選択したCH番号
        public static string FieldDisplayName; // 例: "バクマツ平原"（"フィールド:"接頭辞なし）

        public static void Set(ChannelButtonController controller)
        {
            if (controller == null || controller.myRoomData == null) return;
            LoadSceneName = controller.myRoomData.loadSceneName;
            Channel = controller.myRoomData.channel;
            string rn = controller.myRoomData.roomName ?? "";
            FieldDisplayName = rn.Contains(":")
                ? rn.Substring(rn.IndexOf(':') + 1)
                : rn;
        }
    }

    // ==========================================
    // AreaMoveWindowController パッチ（エリア移動）
    // ==========================================

    // GuestLimitをスキップ（オフラインモードでは全チャンネル開放）
    [HarmonyPatch(typeof(AreaMoveWindowController), "GuestLimit")]
    public static class AreaMoveWindowController_GuestLimit_Patch
    {
        static bool Prefix()
        {
            // オフラインモードではゲスト制限をスキップ
            if (PhotonNetwork.offlineMode)
            {
                // オフラインモードのためスキップ
                return false;
            }
            return true;
        }
    }


    // GuestLimit_NoInstantiateもスキップ
    [HarmonyPatch(typeof(AreaMoveWindowController), "GuestLimit_NoInstantiate")]
    public static class AreaMoveWindowController_GuestLimit_NoInstantiate_Patch
    {
        static bool Prefix()
        {
            // オフラインモードではゲスト制限をスキップ
            if (PhotonNetwork.offlineMode)
            {
                return false;
            }
            return true;
        }
    }


    // Initializeをパッチしてゲストメッセージを削除
    [HarmonyPatch(typeof(AreaMoveWindowController), "Initialize")]
    public static class AreaMoveWindowController_Initialize_Patch
    {
        static void Postfix(AreaMoveWindowController __instance)
        {
            if (!PhotonNetwork.offlineMode && !OnlineMode.IsActive) return;
            try
            {
                // カテゴリに応じた初期テキスト（CH未選択状態）
                // _joinRoomName は "シティ:幕末志士" や "" で直接使えないため、カテゴリから生成
                string defaultText;
                switch (__instance.myEnumAreaCategorly)
                {
                    case AreaMoveWindowController.enumAreaCategoly.City:
                        defaultText = "シティへ移動しますか？\n<size=24>（CHを選択してください）</size>";
                        break;
                    case AreaMoveWindowController.enumAreaCategoly.MMOField:
                        defaultText = "フィールドへ移動しますか？\n<size=24>（CHを選択してください）</size>";
                        break;
                    default:
                        defaultText = __instance.areaMoveText.text; // 元のテキストをそのまま使用
                        break;
                }
                __instance.areaMoveText.text = defaultText;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"AreaMoveWindowController_Initialize_Patch エラー: {ex}");
            }
        }
    }


    // Startをパッチしてオフラインモードでチャンネルボタンを有効化
    [HarmonyPatch(typeof(AreaMoveWindowController), "Start")]
    public static class AreaMoveWindowController_Start_Patch
    {
        // CH番号(1-15)に対応するシティシーン名と表示名（WorldData.cs準拠）
        private static readonly string[] CitySceneNames = new string[]
        {
            "",                          // index 0: 未使用（CHは1始まり）
            "City_Bakumatsu_Fixed",      // CH1: 幕末志士
            "City_Nostal_Fixed",         // CH2: ノスタル志士
            "City_SDshishi_Fixed",       // CH3: SD志士
            "City_Kousoku_Fixed",        // CH4: 光速の西郷
            "City_Kirizakikun_Fixed",    // CH5: キリザキ君は。
            "City_Tabentantei_Fixed",    // CH6: 多弁探偵SAKA
            "City_RO_Fixed",             // CH7: シルバーナイト
            "City_EXLove_Fixed",         // CH8: 獲威
            "City_Ketsuraku_Fixed",      // CH9: ケツラク
            "City_Mumi_Fixed",           // CH10: 無味
            "City_FEZ_Fixed",            // CH11: パニ山パニ夫
            "City_LiveStadium_Fixed",    // CH12: ライブ会場
            "City_Last",                 // CH13: フィナーレ
            "City_Syakusaka_Fixed",      // CH14: 尺坂の森
            "City_Syougatu_Fixed",       // CH15: 幕末神社
        };

        private static readonly string[] CityDisplayNames = new string[]
        {
            "",                // index 0: 未使用
            "幕末志士",        // CH1
            "ノスタル志士",    // CH2
            "SD志士",          // CH3
            "光速の西郷",      // CH4
            "キリザキ君は。",  // CH5
            "多弁探偵SAKA",    // CH6
            "シルバーナイト",  // CH7
            "獲威",            // CH8
            "ケツラク",        // CH9
            "無味",            // CH10
            "パニ山パニ夫",    // CH11
            "ライブ会場",      // CH12
            "フィナーレ",      // CH13
            "尺坂の森",        // CH14
            "幕末神社",        // CH15
        };

        static void Postfix(AreaMoveWindowController __instance)
        {
            try
            {
                if (PhotonNetwork.offlineMode || OnlineMode.IsActive)
                {
                    if (__instance.myEnumAreaCategorly == AreaMoveWindowController.enumAreaCategoly.City)
                    {
                        __instance.channelButtonGroup.SetActive(true);

                        // CH1-CH15に各シティシーンを割り当て
                        for (int i = 1; i <= 15 && i < __instance.channelButtonList.Count; i++)
                        {
                            var button = __instance.channelButtonList[i];
                            if (button == null) continue;

                            button.enabled = true;
                            button.interactable = true;
                            button.image.color = Color.white;

                            // テキストにシティ名を表示（CH13-15の赤色テキストも白に統一）
                            var textComponent = button.transform.Find("Text")?.GetComponent<TMPro.TextMeshProUGUI>();
                            if (textComponent != null)
                            {
                                textComponent.text = $"CH{i}\n{CityDisplayNames[i]}";
                                textComponent.color = Color.black;
                            }

                            // ChannelButtonControllerにシーンデータを設定
                            var channelController = button.GetComponent<ChannelButtonController>();
                            if (channelController != null)
                            {
                                channelController.myRoomData.Initialize(
                                    $"シティ:{CityDisplayNames[i]}",  // roomName
                                    1,                                 // currentPlayerCount
                                    20,                                // maxPlayerCount
                                    "",                                // password
                                    CitySceneNames[i],                 // loadSceneName
                                    "City",                            // gameName
                                    "Open",                            // roomStatus
                                    i,                                 // channel
                                    "",                                // existGMPlayerName
                                    ""                                 // existBossName
                                );
                            }
                        }

                        // CH16を非表示にする
                        if (__instance.channelButtonList.Count > 16 && __instance.channelButtonList[16] != null)
                        {
                            __instance.channelButtonList[16].gameObject.SetActive(false);
                        }

                        // CH13-16用の警告テキスト（「攻撃可能な戦エリアです」等）を非表示にする
                        // プレハブ内のTextMeshProUGUIを検索して非表示
                        foreach (Transform child in __instance.channelButtonGroup.transform)
                        {
                            var tmp = child.GetComponent<TMPro.TextMeshProUGUI>();
                            if (tmp != null && (tmp.text.Contains("ご注意") || tmp.text.Contains("攻撃可能")))
                            {
                                child.gameObject.SetActive(false);
                            }
                        }
                    }
                    else if (__instance.myEnumAreaCategorly == AreaMoveWindowController.enumAreaCategoly.MMOField)
                    {
                        __instance.mmoFieldChannelButtonGroup.SetActive(true);

                        // MMOFieldの全CHを有効化（3フィールドをCH順に割り当て）
                        for (int i = 1; i < __instance.mmoFieldChannelButtonList.Count; i++)
                        {
                            var button = __instance.mmoFieldChannelButtonList[i];
                            if (button == null) continue;

                            button.enabled = true;
                            button.interactable = true;
                            button.image.color = Color.white;

                            // 3フィールドを順番に割り当て
                            int fieldIndex = ((i - 1) % 3); // 0, 1, 2, 0, 1, 2, ...
                            string fieldName;
                            string loadSceneName;
                            switch (fieldIndex)
                            {
                                case 0:
                                    fieldName = "バクマツ平原";
                                    loadSceneName = "MMOField_1";
                                    break;
                                case 1:
                                    fieldName = "ゲラゴッチ溶岩洞";
                                    loadSceneName = "MMOField_2";
                                    break;
                                default:
                                    fieldName = "ビワオウ浮遊回廊";
                                    loadSceneName = "MMOField_4";
                                    break;
                            }

                            var textComponent = button.transform.Find("Text")?.GetComponent<TMPro.TextMeshProUGUI>();
                            if (textComponent != null)
                            {
                                textComponent.text = $"CH{i}\n{fieldName}";
                                textComponent.color = Color.black;
                            }

                            var channelController = button.GetComponent<ChannelButtonController>();
                            if (channelController != null)
                            {
                                channelController.myRoomData.Initialize(
                                    $"フィールド:{fieldName}",
                                    1, 20, "",
                                    loadSceneName,
                                    "MMOField",
                                    "Open",
                                    i, "", ""
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"AreaMoveWindowController_Start_Patch エラー: {ex}");
            }
        }
    }


    // RenewRoomInformation_Cityをスキップ（毎秒呼ばれてボタンを「情報取得中...」にリセットしてしまうため）
    [HarmonyPatch(typeof(AreaMoveWindowController), "RenewRoomInformation_City")]
    public static class AreaMoveWindowController_RenewRoomInformation_City_Patch
    {
        static bool Prefix()
        {
            if (PhotonNetwork.offlineMode || OnlineMode.IsActive)
            {
                return false; // オフライン/オンラインモードともにStart Postfixで設定済みなのでスキップ
            }
            return true;
        }
    }


    // RenewRoomInformation_MMOFieldをスキップ（毎秒呼ばれてボタンを「情報取得中...」にリセットしてしまうため）
    [HarmonyPatch(typeof(AreaMoveWindowController), "RenewRoomInformation_MMOField")]
    public static class AreaMoveWindowController_RenewRoomInformation_MMOField_Patch
    {
        static bool Prefix()
        {
            if (PhotonNetwork.offlineMode || OnlineMode.IsActive)
            {
                return false;
            }
            return true;
        }
    }


    // SelectChannelButton Postfix: CH選択時に areaMoveText を選択CH名で更新
    [HarmonyPatch(typeof(AreaMoveWindowController), "SelectChannelButton")]
    public static class AreaMoveWindowController_SelectChannelButton_Patch
    {
        static void Postfix(AreaMoveWindowController __instance)
        {
            if (!PhotonNetwork.offlineMode && !OnlineMode.IsActive) return;
            if (__instance.selectedChannelButton == null) return;

            try
            {
                var channelController = __instance.selectedChannelButton.GetComponent<ChannelButtonController>();
                if (channelController == null) return;

                // roomName 形式: "シティ:幕末志士" or "フィールド:バクマツ平原"
                string roomName = channelController.myRoomData.roomName;
                string displayName = roomName.Contains(":")
                    ? roomName.Substring(roomName.IndexOf(':') + 1)
                    : roomName;

                __instance.areaMoveText.text = displayName + "へ移動しますか？";
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"AreaMoveWindowController_SelectChannelButton_Patch エラー: {ex}");
            }
        }
    }


    // MoveAreaをパッチしてオフラインモードで直接シーン移動
    [HarmonyPatch(typeof(AreaMoveWindowController), "MoveArea")]
    public static class AreaMoveWindowController_MoveArea_Patch
    {
        static bool Prefix(AreaMoveWindowController __instance)
        {
            try
            {
                // オフラインモードの場合、直接シーン移動
                if (PhotonNetwork.offlineMode)
                {
                    if (__instance.selectedChannelButton == null)
                    {
                        SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("チャンネルを選択してください");
                        return false;
                    }

                    var channelController = __instance.selectedChannelButton.GetComponent<ChannelButtonController>();
                    if (channelController == null)
                    {
                        OfflinePatchPlugin.Logger.LogError("ChannelButtonControllerが見つかりません");
                        return false;
                    }

                    string loadSceneName = channelController.myRoomData.loadSceneName;

                    // 選択したチャンネルを保持（フィールド名表示でCH番号を正しく出すため）
                    OfflineFieldSelection.Set(channelController);

                    // 経路A(直接LoadScene)はPUNcontrollerの部屋を作らないため、
                    // 前回の遺跡/アトラクション部屋名が残らないようクリアする。
                    OfflineRoomState.CurrentRoomName = null;

                    // ウィンドウを閉じる
                    __instance.PressedCancelButton();

                    // フェードアウトしてシーン移動
                    SingletonMonoBehaviour<GameManager>.Instance.FadeOutEffect(delegate
                    {
                        try
                        {

                            // シティやMMOFieldなどの場合は _System シーンと本体シーンの両方をロード
                            if (loadSceneName.StartsWith("City_") || loadSceneName.StartsWith("MMOField") ||
                                loadSceneName.StartsWith("Boss") || loadSceneName.StartsWith("TeamBattle") ||
                                loadSceneName == "Home")
                            {
                                // まず _System シーンをロード
                                string systemSceneName = loadSceneName + "_System";
                                UnityEngine.SceneManagement.SceneManager.LoadScene(systemSceneName);

                                // 本体シーンを追加ロード（複数のシーン名バリエーションを試行）
                                string[] mapSceneNames = new string[] {
                                    loadSceneName,                           // City_Bakumatsu_Fixed
                                    loadSceneName.Replace("_Fixed", ""),     // City_Bakumatsu
                                };

                                bool mapLoaded = false;
                                foreach (var mapName in mapSceneNames)
                                {
                                    try
                                    {
                                        UnityEngine.SceneManagement.SceneManager.LoadScene(mapName, UnityEngine.SceneManagement.LoadSceneMode.Additive);

                                        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(mapName);
                                        if (scene.IsValid() && scene.isLoaded)
                                        {
                                            mapLoaded = true;
                                            break;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        OfflinePatchPlugin.Logger.LogWarning($"シーン {mapName} ロード失敗: {ex.Message}");
                                    }
                                }

                                if (!mapLoaded)
                                {
                                    OfflinePatchPlugin.Logger.LogWarning($"マップシーンが見つかりません: {loadSceneName}");
                                }

                                // BaseSystemSceneも追加ロード
                                UnityEngine.SceneManagement.SceneManager.LoadScene("BaseSystemScene", UnityEngine.SceneManagement.LoadSceneMode.Additive);
                            }
                            else
                            {
                                // その他のシーンは単体でロード
                                UnityEngine.SceneManagement.SceneManager.LoadScene(loadSceneName);
                            }

                        }
                        catch (Exception sceneEx)
                        {
                            OfflinePatchPlugin.Logger.LogError($"シーンロードエラー: {sceneEx}");
                            // エラー時はPUNControllerを使用
                            SingletonMonoBehaviour<PUNController>.Instance.loadSceneName = loadSceneName;
                            SingletonMonoBehaviour<PUNController>.Instance.LoadScene();
                        }
                    });

                    return false;
                }

                // オンラインモードの場合、JOIN_OR_CREATEで入室（StartJoinRoomはJOINのみで部屋がないと失敗する）
                if (OnlineMode.IsActive)
                {
                    if (__instance.selectedChannelButton == null)
                    {
                        SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("チャンネルを選択してください");
                        return false;
                    }

                    var channelController = __instance.selectedChannelButton.GetComponent<ChannelButtonController>();
                    if (channelController == null) return true;

                    string roomName = channelController.myRoomData.roomName;
                    string loadScene = channelController.myRoomData.loadSceneName;

                    // 選択したチャンネルを保持（フィールド名表示でCH番号を正しく出すため）
                    OfflineFieldSelection.Set(channelController);

                    SingletonMonoBehaviour<GameManager>.Instance.matchingRoomData.gameName = channelController.myRoomData.gameName;
                    LogHelper.LogInfo($"[AreaMove] JOIN_OR_CREATE: {roomName} / {loadScene}");
                    SingletonMonoBehaviour<PUNController>.Instance.StartJoinOrCreateRoom(
                        roomName, loadScene, 30, PUNController.roomType.MMO);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"AreaMoveWindowController_MoveArea_Patch エラー: {ex}");
                return true;
            }
        }
    }

}

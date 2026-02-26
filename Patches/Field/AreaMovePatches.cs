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
        static void Postfix(AreaMoveWindowController __instance, string _joinRoomName)
        {
            try
            {
                // オフラインモードの場合、ゲストメッセージを削除し、説明文を変更
                if (PhotonNetwork.offlineMode)
                {
                    __instance.areaMoveText.text = _joinRoomName + "へ移動しますか？\n<size=24>（オフラインモード：CHを選択してください）</size>";
                }
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
                if (PhotonNetwork.offlineMode)
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
            if (PhotonNetwork.offlineMode)
            {
                return false; // オフラインモードではStart Postfixで設定済みなのでスキップ
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
            if (PhotonNetwork.offlineMode)
            {
                return false;
            }
            return true;
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

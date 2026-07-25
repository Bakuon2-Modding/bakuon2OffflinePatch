using BepInEx;
using BepInEx.Logging;
using DG.Tweening;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BakuonOfflinePatch
{
    // MenuScreenManager.PressedInterfaceVisibleSwitchButtonパッチ - オフラインモード用にnullチェック追加
    [HarmonyPatch(typeof(MenuScreenManager), "PressedInterfaceVisibleSwitchButton")]
    public static class MenuScreenManager_PressedInterfaceVisibleSwitchButton_Patch
    {
        static bool Prefix(MenuScreenManager __instance)
        {
            try
            {
                // interfaceVisibleLevelフィールドを取得
                var interfaceVisibleLevelField = typeof(MenuScreenManager).GetField("interfaceVisibleLevel",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (interfaceVisibleLevelField == null) return true;

                int interfaceVisibleLevel = (int)interfaceVisibleLevelField.GetValue(__instance);

                if (interfaceVisibleLevel == 0)
                {
                    // UI表示をオフ
                    SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("UI表示をオフにしました");
                    interfaceVisibleLevelField.SetValue(__instance, 1);

                    // MenuScreenManager
                    if (__instance.mainCanvas != null)
                        __instance.mainCanvas.SetActive(false);
                    if (__instance.unitInformationCanvas != null)
                        __instance.unitInformationCanvas.SetActive(false);

                    // ChatInputManager（nullチェック）
                    var chatInputManager = SingletonMonoBehaviour<ChatInputManager>.Instance;
                    if (chatInputManager != null)
                    {
                        if (chatInputManager.chatInputCanvas != null)
                            chatInputManager.chatInputCanvas.gameObject.SetActive(true);
                        if (chatInputManager.textLogCanvas != null)
                            chatInputManager.textLogCanvas.gameObject.SetActive(false);
                    }

                    // FieldCameraManager（nullチェック）
                    var fieldCameraManager = SingletonMonoBehaviour<FieldCameraManager>.Instance;
                    if (fieldCameraManager != null && fieldCameraManager.miniMapCanvas != null)
                    {
                        fieldCameraManager.miniMapCanvas.gameObject.SetActive(false);
                    }

                    // InputManagerController（nullチェック）
                    if (InputManagerController.Instance != null && InputManagerController.Instance.inputButtonCanvas != null)
                    {
                        InputManagerController.Instance.inputButtonCanvas.gameObject.SetActive(false);
                    }

                    // GoodsPresentManager（nullチェック）
                    var goodsPresentManager = SingletonMonoBehaviour<GoodsPresentManager>.Instance;
                    if (goodsPresentManager != null && goodsPresentManager.canvas != null)
                    {
                        goodsPresentManager.canvas.SetActive(false);
                    }

                    // screenModeButtonの位置変更（アニメーションなし）
                    if (__instance.screenModeButton != null)
                    {
                        var rectTransform = __instance.screenModeButton.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            rectTransform.anchoredPosition = new Vector2(-50f, -50f);
                        }
                    }
                }
                else if (interfaceVisibleLevel == 1)
                {
                    // UI表示をオン
                    SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("UI表示をオンにしました");
                    interfaceVisibleLevelField.SetValue(__instance, 0);

                    // MenuScreenManager
                    if (__instance.mainCanvas != null)
                        __instance.mainCanvas.SetActive(true);
                    if (__instance.unitInformationCanvas != null)
                        __instance.unitInformationCanvas.SetActive(true);

                    // ChatInputManager（nullチェック）
                    var chatInputManager = SingletonMonoBehaviour<ChatInputManager>.Instance;
                    if (chatInputManager != null)
                    {
                        if (chatInputManager.chatInputCanvas != null)
                            chatInputManager.chatInputCanvas.gameObject.SetActive(true);
                        if (chatInputManager.textLogCanvas != null)
                            chatInputManager.textLogCanvas.gameObject.SetActive(true);
                    }

                    // FieldCameraManager（nullチェック）
                    var fieldCameraManager = SingletonMonoBehaviour<FieldCameraManager>.Instance;
                    if (fieldCameraManager != null && fieldCameraManager.miniMapCanvas != null)
                    {
                        fieldCameraManager.miniMapCanvas.gameObject.SetActive(true);
                    }

                    // InputManagerController（nullチェック）
                    if (InputManagerController.Instance != null && InputManagerController.Instance.inputButtonCanvas != null)
                    {
                        InputManagerController.Instance.inputButtonCanvas.gameObject.SetActive(true);
                    }

                    // GoodsPresentManager（nullチェック）
                    var goodsPresentManager = SingletonMonoBehaviour<GoodsPresentManager>.Instance;
                    if (goodsPresentManager != null && goodsPresentManager.canvas != null)
                    {
                        goodsPresentManager.canvas.SetActive(true);
                    }

                    // screenModeButtonの位置変更（アニメーションなし）
                    if (__instance.screenModeButton != null)
                    {
                        var rectTransform = __instance.screenModeButton.GetComponent<RectTransform>();
                        if (rectTransform != null)
                        {
                            rectTransform.anchoredPosition = new Vector2(-460f, -150f);
                        }
                    }
                }

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"PressedInterfaceVisibleSwitchButton エラー: {ex}");
                return true; // エラー時は元のメソッドを実行
            }
        }
    }


    // ==========================================
    // マイカード: GetUserInformation をオフライン対応
    // ==========================================
    // ローカルのプレイヤー情報からUserInformationを作成して返す
    [HarmonyPatch(typeof(NCMBManager), "GetUserInformation")]
    public static class NCMBManager_GetUserInformation_Patch
    {
        static bool Prefix(NCMBManager __instance, List<string> _list, List<string> _originalList)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // リクエストされた各ユーザーIDに対してローカルデータを作成
                foreach (string userId in _originalList)
                {
                    // 既に取得済みかチェック
                    bool alreadyExists = false;
                    foreach (var existing in gm.userInformationList)
                    {
                        if (existing.userID == userId)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        UserInformation userInfo = MyCardBoardHelper.CreateLocalPlayerUserInformation(gm);
                        gm.userInformationList.Add(userInfo);
                    }
                }

                // デリゲートを呼び出して成功を通知
                if (gm.delegateOnGotUserInformation != null)
                {
                    gm.delegateOnGotUserInformation(_originalList, true);
                }

                // デリゲートをデフォルトにリセット（元のGetUserInformationと同じ動作）
                gm.initializeDelegate();

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserInformation] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // ユーザー投稿: GetUserContentsRanking をオフライン対応
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserContentsRanking")]
    public static class NCMBManager_GetUserContentsRanking_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                if (!OfflineUserContentsStore.IsLocalContentMode) return true;

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserContentsRanking] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // ユーザー投稿: OpenRootMenuをオフライン用に最適化
    // ==========================================
    // 元のOpenRootMenuは GC.Collect() + Resources.UnloadUnusedAssets() を呼び出す。
    // GC.Collect()は同期的にGCを強制実行するためフリーズの主因。
    // Resources.UnloadUnusedAssets()は非同期だがmainスレッド側でのスキャン処理でヒッチを起こす。
    // オフラインではダウンロードされた動的アセットがないためどちらも不要。
    [HarmonyPatch(typeof(UserContentsScreenManager), "OpenRootMenu")]
    public static class UserContentsScreenManager_OpenRootMenu_Patch
    {
        static bool Prefix(UserContentsScreenManager __instance)
        {
            if (!OfflineUserContentsStore.IsLocalContentMode) return true;

            // GC.Collect() と Resources.UnloadUnusedAssets() を両方スキップ
            if (__instance.coroutine != null)
                __instance.StopCoroutine(__instance.coroutine);

            SingletonMonoBehaviour<MenuScreenManager>.Instance.OpenRootMenu(__instance.root);

            // 詳細ウィンドウを閉じる（再オープン時に前回の状態が残らないよう）
            // Awake() は初回のみ実行されるため、SetActive(true)で復帰した際に明示的に閉じ直す
            if (__instance.illustDetialWindow != null)
                __instance.illustDetialWindow.SetActive(false);
            if (__instance.storyDetialWindow != null)
                __instance.storyDetialWindow.SetActive(false);

            foreach (Transform item in __instance.newestRankingContent_Root.transform)
                UnityEngine.Object.Destroy(item.gameObject);

            __instance.GetNewestPublishedUserContents();

            // 「データ取得中」ボタンとシステムメッセージを非表示にする
            if (__instance.dataGetButton != null)
                __instance.dataGetButton.gameObject.SetActive(false);
            SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("");

            return false; // 元のOpenRootMenu（GC.Collect()含む）をスキップ
        }
    }


    // ==========================================
    // 投書箱: GetUserOpinionData をオフライン対応
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserOpinionData")]
    public static class NCMBManager_GetUserOpinionData_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                // マルチプレイ接続中(OnlineMode)も実NCMBは存在しないためバイパスする
                if (!PhotonNetwork.offlineMode && !OnlineMode.IsActive) return true;

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserOpinionData] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // 投書箱: SaveUserOpinionData をオフライン対応
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SaveUserOpinionData")]
    public static class NCMBManager_SaveUserOpinionData_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                // マルチプレイ接続中(OnlineMode)も実NCMBは存在しないためバイパスする
                if (!PhotonNetwork.offlineMode && !OnlineMode.IsActive) return true;

                SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("この環境では投書できません");

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveUserOpinionData] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // SaveMissionData: ミッションデータ保存（DB通信スキップ）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SaveMissionData")]
    public static class NCMBManager_SaveMissionData_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            // マルチプレイ(OnlineMode)中もNCMBは常にブロックされるため、ローカル保存で代替する
            if (!PhotonNetwork.offlineMode && !OnlineMode.IsActive) return true;

            var gm = SingletonMonoBehaviour<GameManager>.Instance;

            // missionDataUpdateTimeを更新（元のコードと同じ: NTPDateTimeを使用）
            gm.missionDataUpdateTime = gm.NTPDateTime;

            // ローカルに永続化
            OfflineSaveDataManager.SaveMissionDataOnly();

            // MenuScreenManagerのバッジ表示を更新
            if ((bool)SingletonMonoBehaviour<MenuScreenManager>.Instance)
            {
                SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowNewCount();
            }

            // MissionScreenManagerのバッジ表示を更新
            if ((bool)SingletonMonoBehaviour<MissionScreenManager>.Instance)
            {
                SingletonMonoBehaviour<MissionScreenManager>.Instance.ShowNewCount();
            }

            // 注意: 元の SaveMissionData はデリゲート (RunDelegateOnFinishedNetworkProcess) を呼ばない。
            // UpdateMissionData フローの画面更新は MissionScreenManager.UpdateMissionData 自身が
            // ShowMissionList() を直接呼ぶことで行われる (GameManager 側のハンドラは空実装)。
            // 以前ここでデリゲートを呼んでいたが、過去に設定された古いデリゲート
            // (破棄済みシーンのミッション画面の ShowMissionList) が発火して NRE になっていたため削除
            // (エリア移動等の自動ミッション加算 → SaveMissionData 経由で発生。2026-07-25 のログで確認)。

            return false;
        }
    }


    // ==========================================
    // SaveMyCardEditResult: マイカード編集保存
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SaveMyCardEditResult")]
    public static class NCMBManager_SaveMyCardEditResult_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _subPlayerName,
            string _comment, string _tapWord, string _enumEmotionID)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // ローカルのuserInformationを更新
                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == _userID)
                    {
                        userInfo.subPlayerName = _subPlayerName;
                        userInfo.comment = _comment;
                        userInfo.tapWord = _tapWord;
                        userInfo.tapEmotionID = _enumEmotionID;
                    }
                }

                // ローカルに永続化（再起動後も保持）
                if (_userID == gm.userID)
                {
                    OfflineSaveDataManager.SaveMyCardEdit(_subPlayerName, _comment, _tapWord, _enumEmotionID);
                }

                gm.ShowSystemMessage("マイカードを更新しました");

                SingletonMonoBehaviour<MenuScreenManager>.Instance.CloseMyCardEditor();
                List<string> list = new List<string> { _userID };
                gm.delegateOnGotUserInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowMyCardFront;
                gm.GetUserInformation(list);

                gm.IncrementMissionAchievement(MissionData.enumMissionID.Once_EditMyCard, 1);

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveMyCardEditResult] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // SaveMyCardAvater: マイカードアバター保存
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SaveMyCardAvater")]
    public static class NCMBManager_SaveMyCardAvater_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _avaterUnitID,
            List<string> _avaterAccessoryList)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == _userID)
                    {
                        userInfo.avaterUnitID = _avaterUnitID;
                        userInfo.avaterEquipAccessoryList = _avaterAccessoryList;
                    }
                }

                // ローカルに永続化（再起動後も保持）
                if (_userID == gm.userID)
                {
                    OfflineSaveDataManager.SaveMyCardAvater(_avaterUnitID, _avaterAccessoryList);
                }

                gm.ShowSystemMessage("マイカードキャラクターを更新しました");

                SingletonMonoBehaviour<MenuScreenManager>.Instance.CloseMyCardEditor();
                List<string> list = new List<string> { _userID };
                gm.delegateOnGotUserInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowMyCardFront;
                gm.GetUserInformation(list);

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveMyCardAvater] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // IncrementGoodCount: いいね（ローカル処理）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "IncrementGoodCount")]
    public static class NCMBManager_IncrementGoodCount_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                SingletonMonoBehaviour<AudioManager>.Instance.PlaySE("ok");
                gm.ShowSystemMessage("「いいね！」しました");

                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == _userID)
                    {
                        userInfo.goodCount++;
                        // 自分のカードならローカルに永続化
                        if (_userID == gm.userID)
                        {
                            OfflineSaveDataManager.SaveMyCardGoodCount(userInfo.goodCount);
                        }
                    }
                }

                // デリゲートを呼び出す
                var delegateField = __instance.GetType().GetField("delegateOnFinishedNetworkProcess",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (delegateField != null)
                {
                    var del = delegateField.GetValue(__instance) as System.Action<bool>;
                    if (del != null)
                    {
                        del(true);
                    }
                }

                // giveGoodListに追加
                __instance.GetType().GetMethod("AddGiveGoodUser",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { _userID });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[IncrementGoodCount] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetFollowerList: フォロワーリスト取得（空リストで成功）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetFollowerList")]
    public static class NCMBManager_GetFollowerList_Patch
    {
        static bool Prefix(NCMBManager __instance, int _skip)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // オフラインではフォロワーは存在しない
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetFollowerList] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetUserInformation_FromUserName: 名前からユーザー情報取得
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserInformation_FromUserName")]
    public static class NCMBManager_GetUserInformation_FromUserName_Patch
    {
        static bool Prefix(NCMBManager __instance, List<string> _list, List<string> _originalList)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // GetUserInformationと同じ処理
                foreach (string userId in _originalList)
                {
                    bool alreadyExists = false;
                    foreach (var existing in gm.userInformationList)
                    {
                        if (existing.userID == userId)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        UserInformation userInfo = new UserInformation();
                        userInfo.userID = userId;
                        userInfo.playerName = gm.playerName;
                        userInfo.subPlayerName = "";
                        userInfo.comment = "オフラインモード";
                        userInfo.tapWord = "";
                        userInfo.tapEmotionID = "0";
                        userInfo.guildName = gm.myGuildName;
                        userInfo.countryID = 0;
                        userInfo.goodCount = 0;
                        if (gm.primaryUnitData != null)
                        {
                            userInfo.avaterUnitID = ((int)gm.primaryUnitData.unitID).ToString();
                        }
                        gm.userInformationList.Add(userInfo);
                    }
                }

                if (gm.delegateOnGotUserInformation != null)
                {
                    gm.delegateOnGotUserInformation(_originalList, true);
                }

                gm.initializeDelegate();

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserInformation_FromUserName] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetUserInformation_MyCardBoard: マイカード掲示板にローカルプレイヤーのカードを表示
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserInformation_MyCardBoard")]
    public static class NCMBManager_GetUserInformation_MyCardBoard_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                var myCardController = SingletonMonoBehaviour<MyCardScreenController>.Instance;

                if (myCardController != null)
                {
                    // ローカルプレイヤーのUserInformationを作成
                    UserInformation userInfo = MyCardBoardHelper.CreateLocalPlayerUserInformation(gm);
                    myCardController.userInformationList.Clear();
                    myCardController.userInformationList.Add(userInfo);

                    // 直接表示を更新（PhotonChatをバイパス）
                    myCardController.RenewMyCardBoard();
                }

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserInformation_MyCardBoard] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetUserInformation_MyCardBoard_Random: マイカード掲示板ランダム（オフラインでは不要）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserInformation_MyCardBoard_Random")]
    public static class NCMBManager_GetUserInformation_MyCardBoard_Random_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserInformation_MyCardBoard_Random] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // MenuScreenManager.OpenMyUserContents: 二重呼び出し防止
    // ==========================================
    // 元コード: userIllustContentsDataList.Count > 0 の場合に
    // OnNetworkProcessFinished_GetUserContents を即座に呼び（CALL 1）、
    // さらに GetUserContents → RunDelegateOnFinishedNetworkProcess でも呼ぶ（CALL 2）。
    // オフラインでは GetUserContents が同期的に動作するため、同一フレームで2回
    // PressedMyUserContentsIllustTabButton が実行される。
    // これにより「公開中」ラベルが最初のサムネイルに表示されない場合がある。
    // 修正: 早期呼び出しをスキップし、GetUserContents からの1回のみにする。
    [HarmonyPatch(typeof(MenuScreenManager), "OpenMyUserContents")]
    public static class MenuScreenManager_OpenMyUserContents_Patch
    {
        static bool Prefix(MenuScreenManager __instance)
        {
            if (!OfflineUserContentsStore.IsLocalContentMode) return true;

            try
            {
                var ncmb = SingletonMonoBehaviour<NCMBManager>.Instance;
                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // SetDelegateOnFinishedNetworkProcess をリフレクションで呼び出す
                var setMethod = typeof(NCMBManager).GetMethod(
                    "SetDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance);
                if (setMethod != null)
                {
                    var callbackInfo = typeof(MenuScreenManager).GetMethod(
                        "OnNetworkProcessFinished_GetUserContents",
                        BindingFlags.Public | BindingFlags.Instance);
                    Type delegateType = setMethod.GetParameters()[0].ParameterType;
                    Delegate del = Delegate.CreateDelegate(delegateType, __instance, callbackInfo);
                    setMethod.Invoke(ncmb, new object[] { del });
                }

                // 早期呼び出しをスキップし、GetUserContents のみを呼ぶ（1回のみ）
                foreach (UserInformation userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == __instance.myCardUserID)
                    {
                        ncmb.GetUserContents(userInfo);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OpenMyUserContents] パッチエラー: {ex}");
                return true;
            }

            return false;
        }
    }


    // ==========================================
    // MyCardScreenController.Start: PhotonChat購読をスキップし直接カード表示
    // ==========================================
    [HarmonyPatch(typeof(MyCardScreenController), "Start")]
    public static class MyCardScreenController_Start_Patch
    {
        static bool Prefix(MyCardScreenController __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // isTwitterEnabledをtrueに設定（マイカード石板の表示に必要）
                gm.isTwitterEnabled = true;

                // disableImageを非表示にして石板を見えるようにする
                __instance.RenewVisibleDisableImage();

                // ローカルプレイヤーのカードを作成して直接表示
                UserInformation userInfo = MyCardBoardHelper.CreateLocalPlayerUserInformation(gm);
                __instance.userInformationList.Clear();
                __instance.userInformationList.Add(userInfo);
                __instance.currentUserInformation = userInfo;
                __instance.ShowMyCardFront(userInfo);

                return false; // 元のStart（PhotonChat購読）をスキップ
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[MyCardScreenController.Start] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // MyCardScreenController.Update: PhotonChat購読をスキップ
    // ==========================================
    [HarmonyPatch(typeof(MyCardScreenController), "Update")]
    public static class MyCardScreenController_Update_Patch
    {
        static bool Prefix(MyCardScreenController __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // オフラインではPhotonChat購読不要、カード更新のみ
                // userInformationListが空になったら再補充
                if (__instance.userInformationList.Count == 0)
                {
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;
                    UserInformation userInfo = MyCardBoardHelper.CreateLocalPlayerUserInformation(gm);
                    __instance.userInformationList.Add(userInfo);
                }

                return false; // 元のUpdate（PhotonChat購読リトライ含む）をスキップ
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[MyCardScreenController.Update] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // MyCardScreenController.RenewMyCardBoard: PhotonChatをバイパスして直接表示
    // ==========================================
    [HarmonyPatch(typeof(MyCardScreenController), "RenewMyCardBoard")]
    public static class MyCardScreenController_RenewMyCardBoard_Patch
    {
        static bool Prefix(MyCardScreenController __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                if (__instance.userInformationList.Count <= 0) return false;

                // 先頭のカードを取得して直接表示（PhotonChatをスキップ）
                var userInfo = __instance.userInformationList[0];
                __instance.userInformationList.RemoveAt(0);
                __instance.currentUserInformation = userInfo;
                __instance.ShowMyCardFront(userInfo);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[MyCardScreenController.RenewMyCardBoard] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // マイカード石板用ヘルパー
    // ==========================================
    public static class MyCardBoardHelper
    {
        /// <summary>
        /// ローカルプレイヤーのUserInformationを作成
        /// </summary>
        public static UserInformation CreateLocalPlayerUserInformation(GameManager gm)
        {
            UserInformation userInfo = new UserInformation();
            userInfo.userID = gm.userID ?? "@Offline_Player";
            userInfo.playerName = gm.playerName ?? "OfflinePlayer";
            userInfo.subPlayerName = "";
            userInfo.comment = "";
            userInfo.tapWord = "";
            userInfo.tapEmotionID = "0";
            userInfo.guildName = gm.myGuildName ?? "";
            userInfo.countryID = 0;
            userInfo.goodCount = 0;
            userInfo.avaterEquipAccessoryList = new List<string>();

            if (gm.primaryUnitData != null)
            {
                userInfo.avaterUnitID = ((int)gm.primaryUnitData.unitID).ToString();

                // 現在のキャラクターに装備されたアクセサリを設定
                if (gm.primaryUnitData.equipAccessoryList != null && gm.primaryUnitData.equipAccessoryList.Count > 0)
                {
                    userInfo.avaterEquipAccessoryList = new List<string>(gm.primaryUnitData.equipAccessoryList);
                }
            }
            else
            {
                userInfo.avaterUnitID = "0";
            }

            // 永続化済み（ES2: saveData_offline）のマイカード編集内容を基底値として反映。
            // 再起動後もサブネーム・コメント・タップワード等が復元される。
            OfflineSaveDataManager.ApplyMyCardTo(userInfo);

            // userInformationListに既存の自分のデータがあれば、編集済みの値を使う
            // （同一セッション内の編集はメモリ側が最新なので永続値より優先する）
            foreach (var existing in gm.userInformationList)
            {
                if (existing.userID == gm.userID)
                {
                    userInfo.subPlayerName = existing.subPlayerName ?? "";
                    userInfo.comment = existing.comment ?? "";
                    userInfo.tapWord = existing.tapWord ?? "";
                    userInfo.tapEmotionID = existing.tapEmotionID ?? "0";
                    userInfo.goodCount = existing.goodCount;
                    if (existing.avaterEquipAccessoryList != null)
                    {
                        userInfo.avaterEquipAccessoryList = existing.avaterEquipAccessoryList;
                    }
                    if (!string.IsNullOrEmpty(existing.avaterUnitID))
                    {
                        userInfo.avaterUnitID = existing.avaterUnitID;
                    }
                    break;
                }
            }

            return userInfo;
        }
    }


    // ==========================================
    // MenuScreenManager.ShowMyCardFront: NullReferenceException防止
    // ==========================================
    // 元コードの line 1708 で PhotonNetwork.room.Name にアクセスするが、
    // オフラインのシーン移動では PhotonNetwork.room が null になる場合がある。
    // Finalizer で例外を握りつぶし、カードが表示されない問題を防止する。
    [HarmonyPatch(typeof(MenuScreenManager), "ShowMyCardFront")]
    public static class MenuScreenManager_ShowMyCardFront_Patch
    {
        static void Prefix(List<string> _list, bool isSuccess)
        {
            if (!PhotonNetwork.offlineMode) return;

            // PhotonNetwork.room が null の場合、ダミールームを作成して NullReferenceException を防止
            if (PhotonNetwork.room == null)
            {
                try
                {
                    PhotonNetwork.CreateRoom("OfflineRoom", new RoomOptions { maxPlayers = 1 }, null);
                }
                catch (Exception ex)
                {
                    LogHelper.LogError($"[ShowMyCardFront] ダミールーム作成失敗: {ex.Message}");
                }
            }
        }

        static Exception Finalizer(Exception __exception)
        {
            if (__exception != null && PhotonNetwork.offlineMode)
            {
                LogHelper.LogError($"[ShowMyCardFront] 例外をキャッチ: {__exception}");
                return null;
            }
            return __exception;
        }
    }


    // ==========================================
    // MyCardScreenController.PressedMyCardOpenButton: オフライン対応
    // ==========================================
    // 石板の「マイカードを開く」ボタンでcurrentUserInformationが未設定の場合に補完し、
    // gm.userInformationList にも追加して ShowMyCardFront が正常動作するようにする
    [HarmonyPatch(typeof(MyCardScreenController), "PressedMyCardOpenButton")]
    public static class MyCardScreenController_PressedMyCardOpenButton_Patch
    {
        static void Prefix(MyCardScreenController __instance)
        {
            if (!PhotonNetwork.offlineMode) return;

            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // currentUserInformationが未設定 or userIDが空の場合、補完する
                if (__instance.currentUserInformation == null ||
                    string.IsNullOrEmpty(__instance.currentUserInformation.userID))
                {
                    UserInformation userInfo = MyCardBoardHelper.CreateLocalPlayerUserInformation(gm);
                    __instance.currentUserInformation = userInfo;
                }

                // gm.userInformationList に自分のデータがなければ追加（gm.userIDで検索）
                string userId = __instance.currentUserInformation.userID;
                bool found = false;
                foreach (var existing in gm.userInformationList)
                {
                    if (existing.userID == userId)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    UserInformation userInfo = MyCardBoardHelper.CreateLocalPlayerUserInformation(gm);
                    gm.userInformationList.Add(userInfo);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[PressedMyCardOpenButton] Prefix エラー: {ex}");
            }
        }

        static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                LogHelper.LogError($"[PressedMyCardOpenButton] 元メソッドで例外: {__exception}");
            }
            return __exception;
        }
    }


    // ==========================================
    // CheckPlayerName_NpcNameChange: 名前変更屋の名前重複チェック
    // ==========================================
    // オフラインでは他プレイヤーが存在しないため重複チェック不要、直接ChangePlayerNameへ
    [HarmonyPatch(typeof(NCMBManager), "CheckPlayerName_NpcNameChange")]
    public static class NCMBManager_CheckPlayerName_NpcNameChange_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _changedPlayerName, int _price)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // NGNameチェック（TitlePatchesと同じリスト）
                string[] ngNames = new string[]
                {
                    "坂本", "西郷", "ミケ", "syo君", "K", "M", "O", "N君", "かじゃねこ", "キリザキ君",
                    "モリ子", "N美", "美伊", "ノスタル志士", "獲威", "鋭", "霧崎鋭", "ei", "そっしー", "sossie",
                    "多弁探偵SAKA", "ホームズ西", "パニ山パニ夫", "陰<<イン>>", "奈優", "冬香", "坂木リョウ", "西森タカ", "緑間希乃", "三ヶ原音子",
                    "星野亜香里", "ゲラーマン", "坂本リョウマ", "西郷タカモリ", "緑野マッシュ", "三ヶ野原音狐子", "星野原亜香瑠璃", "一文梨無用之助", "底辺這擦周", "土場敷ぼり夫",
                    "如月ザラギー", "hal"
                };

                foreach (string ng in ngNames)
                {
                    if (_changedPlayerName == ng || _changedPlayerName.Contains("GUEST_"))
                    {
                        SingletonMonoBehaviour<GameManager>.Instance.ShowAlartWindow("", "その名前は使用できません");
                        __instance.GetType().GetField("delegateOnFinishedNetworkProcess",
                            BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.SetValue(__instance, null);
                        return false;
                    }
                }

                if (string.IsNullOrEmpty(_changedPlayerName))
                {
                    SingletonMonoBehaviour<GameManager>.Instance.ShowAlartWindow("", "名前を入力してください");
                    __instance.GetType().GetField("delegateOnFinishedNetworkProcess",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.SetValue(__instance, null);
                    return false;
                }

                // オフラインでは重複チェック不要、直接名前変更実行
                SingletonMonoBehaviour<NCMBManager>.Instance.ChangePlayerName(_userID, _changedPlayerName, _price);
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[CheckPlayerName_NpcNameChange] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // ChangePlayerName: 名前変更のDB保存をスキップ
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "ChangePlayerName")]
    public static class NCMBManager_ChangePlayerName_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _changedPlayerName, int _price)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // ローカルのユーザー情報を更新
                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == _userID)
                    {
                        userInfo.playerName = _changedPlayerName;
                    }
                }

                // Photonプレイヤー名も更新
                PhotonNetwork.playerName = _changedPlayerName;

                // セーブデータに保存
                OfflineSaveDataManager.SavePlayerName(_changedPlayerName);

                // 成功デリゲートを呼び出し（OnFinishedNetworkProcess_BuyNameChangeが呼ばれる）
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ChangePlayerName] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // ハッピー尺坂くじ: 結果表示をPhotonChat経由からローカル演出に置き換え
    // 元の演出: キャラ頭上にくじウィンドウ（アイコン+賞名+枠）がポップアップ
    // ==========================================
    [HarmonyPatch(typeof(ItemController), "OnFinishedNetworkProcess_LotterySyakusakaKuji")]
    public static class ItemController_OnFinishedNetworkProcess_LotterySyakusakaKuji_Patch
    {
        static bool Prefix(ItemController __instance, bool _result)
        {
            if (!PhotonNetwork.offlineMode) return true;
            if (!_result) return false;

            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                int rewardType = __instance.rewardType;

                // 元のコードと同じ形式: "アイテムID,賞名メッセージ"
                string rewardKey = "";
                string rewardMessage = "";
                string se = "CharacterGet";

                switch (rewardType)
                {
                    case 0:
                        rewardKey = ItemData.enumItemID.Item_SYAKUSAKA.ToString();
                        rewardMessage = "Ａ賞\u3000尺取り坂虫 獲得！";
                        se = "kansei";
                        break;
                    case 1:
                        rewardKey = __instance.rewardAccessoryID.ToString();
                        rewardMessage = "Ｂ賞\u3000" + AccessoryData.GetAccessoryName(__instance.rewardAccessoryID) + " 獲得！";
                        se = "jingle_positive_001";
                        break;
                    case 2:
                        rewardKey = __instance.rewardItemID.ToString();
                        rewardMessage = "Ｃ賞\u3000" + ItemData.GetItemName(__instance.rewardItemID) + " 獲得！";
                        se = "CharacterGet";
                        break;
                    case 3:
                        rewardKey = ItemData.enumItemID.Item_Gold.ToString();
                        rewardMessage = "Ａ賞\u3000500リング 獲得！";
                        se = "kansei";
                        break;
                    case 4:
                        rewardKey = ItemData.enumItemID.Item_JAGASAKA.ToString();
                        rewardMessage = "Ａ賞\u3000じゃが坂 獲得！";
                        se = "kansei";
                        break;
                    case 5:
                        rewardKey = ItemData.enumItemID.Item_JAGAMIKE.ToString();
                        rewardMessage = "Ａ賞\u3000ごまミケ 獲得！";
                        se = "kansei";
                        break;
                    case 6:
                        rewardKey = ItemData.enumItemID.Item_RUFRAIN.ToString();
                        rewardMessage = "Ａ賞\u3000ルーフレイン 獲得！";
                        se = "kansei";
                        break;
                    case 7:
                        rewardKey = __instance.rewardAccessoryID.ToString();
                        rewardMessage = "Ａ賞\u3000商人のオーラ 獲得！";
                        se = "kansei";
                        break;
                }

                // キャラ頭上のくじウィンドウ演出（元のChatInputManagerの処理を再現）
                bool shownPopup = false;
                if (gm.myPlayerObject != null)
                {
                    var uiController = gm.myPlayerObject.GetComponent<FieldCharacterUIController>();
                    if (uiController != null && uiController.characterChatLabelObject != null)
                    {
                        var chatLabel = uiController.characterChatLabelObject.GetComponent<CharacterChatLabel>();
                        if (chatLabel != null && chatLabel.syakusakaKujiChatWindowRoot != null)
                        {
                            // 他のチャットウィンドウを閉じる
                            chatLabel.chatWindowRoot.SetActive(false);
                            chatLabel.illustChatWindowRoot.SetActive(false);
                            chatLabel.syakusakaKujiChatWindowRoot.SetActive(true);

                            // 報酬アイコンを設定
                            Sprite rewardIcon = null;
                            foreach (var itemData in gm.stockItemDataList)
                            {
                                if (itemData.property.itemID.ToString() == rewardKey)
                                {
                                    rewardIcon = AssetBundleManager.GetAsset<Sprite>("sprite", itemData.property.imageFileName);
                                    break;
                                }
                            }
                            if (rewardIcon == null)
                            {
                                foreach (var accessoryData in gm.stockAccessoryDataList)
                                {
                                    if (accessoryData.property.accessoryID.ToString() == rewardKey)
                                    {
                                        rewardIcon = AssetBundleManager.GetAsset<Sprite>("sprite", accessoryData.property.accessoryIconName);
                                        break;
                                    }
                                }
                            }
                            if (rewardIcon != null)
                            {
                                chatLabel.syakusakaKujiRewardItemIcon.sprite = rewardIcon;
                            }

                            // テキストと枠の設定
                            chatLabel.syakusakaKujiText.text = rewardMessage;
                            chatLabel.silverFrame.gameObject.SetActive(false);
                            chatLabel.goldFrame.gameObject.SetActive(false);
                            chatLabel.rainbowFrame.gameObject.SetActive(false);

                            if (rewardMessage.Contains("Ａ賞"))
                            {
                                chatLabel.syakusakaKujiText.color = new Color(1f, 2f / 15f, 0.47058824f);
                                chatLabel.rainbowFrame.gameObject.SetActive(true);
                            }
                            if (rewardMessage.Contains("Ｂ賞"))
                            {
                                chatLabel.syakusakaKujiText.color = new Color(2f / 15f, 0.4745098f, 1f);
                                chatLabel.goldFrame.gameObject.SetActive(true);
                            }
                            if (rewardMessage.Contains("Ｃ賞"))
                            {
                                chatLabel.syakusakaKujiText.color = new Color(0f, 48f / 85f, 0.16862746f);
                                chatLabel.silverFrame.gameObject.SetActive(true);
                            }

                            // ポップアップアニメーション
                            chatLabel.transform.DOKill();
                            chatLabel.transform.localScale = new Vector3(0f, 0f, 0f);
                            chatLabel.transform.DOScale(new Vector3(1f, 1f, 1f), 0.2f);
                            chatLabel.transform.DOScale(new Vector3(0f, 0f, 0f), 0.2f).SetDelay(5f);

                            shownPopup = true;
                        }
                    }
                }

                // ポップアップが出せなかった場合はシステムメッセージで代替
                if (!shownPopup)
                {
                    gm.ShowSystemMessage("尺坂くじ結果: " + rewardMessage);
                }

                SingletonMonoBehaviour<AudioManager>.Instance.PlaySE(se);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[LotterySyakusakaKuji] 結果表示エラー: {ex}");
            }

            return false;
        }
    }


    // ==========================================
    // 待合室の「参加者募集」ボタン: PhotonChat送信の代わりにローカルで通知表示
    // ==========================================
    [HarmonyPatch(typeof(MatchingRoomController), "SendMatchingRequest")]
    public static class MatchingRoomController_SendMatchingRequest_Patch
    {
        static bool Prefix(int _type)
        {
            if (!PhotonNetwork.offlineMode) return true;

            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                string playerName = gm.playerName;
                string gameName = gm.matchingRoomData != null ? gm.matchingRoomData.gameName : "";
                int currentPlayer = 1;
                int maxPlayer = 4;

                if (PhotonNetwork.room != null)
                {
                    currentPlayer = PhotonNetwork.room.PlayerCount;
                }

                var punController = SingletonMonoBehaviour<PUNController>.Instance;
                if (punController != null)
                {
                    maxPlayer = punController.joinRoomMaxPlayerCount;
                }

                // 元のゲームと同じ通知表示を呼び出す
                gm.ShowMatchingRequestWindow(
                    playerName,
                    "",  // roomName（オフラインでは不要）
                    "",  // loadSceneName
                    gameName,
                    currentPlayer,
                    maxPlayer,
                    _type
                );
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SendMatchingRequest] パッチエラー: {ex}");
            }

            return false;
        }
    }

    // ==========================================
    // ConfigScreenManager: 専用設定パネルを常に非表示
    // ==========================================
    [HarmonyPatch(typeof(ConfigScreenManager), "RenewCurrentSystemConfigStatus")]
    public static class ConfigScreenManager_RenewCurrentSystemConfigStatus_Patch
    {
        static void Postfix(ConfigScreenManager __instance)
        {
            try
            {
                if (__instance != null && __instance.GMSettingRoot != null)
                {
                    __instance.GMSettingRoot.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Config] GMSettingRoot 非表示エラー: {ex}");
            }
        }
    }

}

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
    // NCMBManagerのサーバー通信をバイパス
    // ==========================================

    // GetUserData - ユーザーデータ取得をバイパス
    [HarmonyPatch(typeof(NCMBManager), "GetUserData")]
    public static class NCMBManager_GetUserData_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                // ローカルデータを設定
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;

                    // セーブデータからプレイヤー名を復元
                    string savedName = OfflineSaveDataManager.LoadPlayerName();
                    if (!string.IsNullOrEmpty(savedName))
                    {
                        gm.playerName = savedName;
                        PhotonNetwork.playerName = savedName;
                    }
                    else if (string.IsNullOrEmpty(gm.playerName))
                    {
                        gm.playerName = "OfflinePlayer";
                    }

                    // セーブデータを読み込み
                    gm.LoadSaveData();

                    // キャラクター・アイテム・アクセサリのプレハブを初期化
                    // （元のGetUserDataと同じ: サーバーデータが無い場合の初期状態）

                    // 既存データをクリア（重複防止）
                    if (gm.stockUnitDataList != null)
                    {
                        gm.stockUnitDataList.Clear();
                    }
                    if (gm.stockUnitRoot != null)
                    {
                        foreach (Transform child in gm.stockUnitRoot.transform)
                        {
                            UnityEngine.Object.Destroy(child.gameObject);
                        }
                    }

                    // 全キャラクターをレベル1・全所持で作成（オフラインではガチャが使えないため）
                    // セーブデータがあればLoadAllData()でレベル等が復元される
                    for (int i = 0; i < 37; i++)
                    {
                        gm.CreateHoldUnitPrefab(i, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, "", 1, "");
                    }
                    gm.stockUnitDataList.Sort((UnitData a, UnitData b) => a.property.sortOrder - b.property.sortOrder);

                    // partyUnitPrefabArrayを初期化
                    if (gm.partyUnitPrefabArray == null)
                    {
                        gm.partyUnitPrefabArray = new GameObject[3];
                    }

                    // アイテムプレハブを作成
                    if (gm.stockItemDataList == null)
                    {
                        gm.stockItemDataList = new System.Collections.Generic.List<ItemData>();
                    }
                    gm.CreateStockItemPrefab();

                    // アクセサリプレハブを作成
                    gm.CreateStockAccessoryPrefab();

                    // オフライン専用セーブデータからロード（前回のプレイデータを復元）
                    // HasSaveDataはプレイヤー名で判定するが、タイトル画面で既に保存されるため
                    // コインタグで判定（SaveAllDataが呼ばれるまで存在しない）
                    bool isFirstLaunch = !OfflineSaveDataManager.HasGameData();
                    OfflineSaveDataManager.LoadAllData();

                    // 初回起動時の初期データ設定
                    LogHelper.LogInfo($"[GetUserData] isFirstLaunch={isFirstLaunch}, HasGameData={OfflineSaveDataManager.HasGameData()}");
                    if (isFirstLaunch)
                    {
                        LogHelper.LogInfo("[GetUserData] 初回起動: 初期アイテム付与開始");
                        gm.SetMyCoin(2000);
                        gm.IncrementStockItemValue(ItemData.enumItemID.Item_SyakusakaKuji, 99);
                        gm.IncrementStockItemValue(ItemData.enumItemID.Item_Omikuji, 99);
                        LogHelper.LogInfo($"[GetUserData] 初期付与完了: coin={gm.myCoin}, stockItemCount={gm.stockItemDataList.Count}");
                    }

                    // ミッションデータを初期化（LoadAllDataでセーブデータから復元済みの場合はスキップ）
                    if (gm.missionDataList == null || gm.missionDataList.Count == 0)
                    {
                        gm.missionDataList = new List<string>();
                        foreach (MissionData.enumMissionID value in Enum.GetValues(typeof(MissionData.enumMissionID)))
                        {
                            GameObject obj = new GameObject();
                            MissionData missionData = obj.AddComponent<MissionData>();
                            missionData.Initialize(value);
                            int id = (int)missionData.myEnumMissionID;
                            gm.missionDataList.Add(id + "," + missionData.myEnumMissionID.ToString() + ",0,0");
                            UnityEngine.Object.Destroy(obj);
                        }
                        gm.missionDataUpdateTime = DateTime.Now;
                    }

                    // NTPDateTimeを設定（ミッションの日付比較に必要）
                    gm.NTPDateTime = DateTime.Now;

                    // LoadAllData後、プライマリキャラクターが未設定なら先頭キャラを設定
                    if (gm.primaryUnitData == null && gm.stockUnitDataList != null && gm.stockUnitDataList.Count > 0)
                    {
                        // 所持キャラを探す
                        foreach (var unitData in gm.stockUnitDataList)
                        {
                            if (unitData.stockValue > 0)
                            {
                                gm.primaryUnitData = unitData;
                                gm.partyUnitPrefabArray[0] = unitData.gameObject;
                                break;
                            }
                        }
                        // 所持キャラがいない場合（初回起動）、先頭キャラを初期キャラとして設定
                        if (gm.primaryUnitData == null)
                        {
                            var firstUnit = gm.stockUnitDataList[0];
                            firstUnit.stockValue = 1;
                            gm.primaryUnitData = firstUnit;
                            gm.partyUnitPrefabArray[0] = firstUnit.gameObject;
                        }
                    }
                }

                // デリゲートを呼び出して成功を通知
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false; // 元のメソッドを実行しない
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GetUserData パッチエラー: {ex}");
                return true; // エラー時は元のメソッドを実行
            }
        }
    }


    // CheckPlayerName - サーバー通信をバイパスしつつNGNameチェックは元のメソッドを使用
    [HarmonyPatch(typeof(NCMBManager), "CheckPlayerName")]
    public static class NCMBManager_CheckPlayerName_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                var titleManager = SingletonMonoBehaviour<TitleSceneManager>.Instance;
                if (titleManager != null)
                {
                    // 元のTitleSceneManager.CheckNGName()を呼び出し
                    if (!titleManager.CheckNGName())
                    {
                        __instance.GetType().GetField("isBusy",
                            BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.SetValue(__instance, false);
                        return false;
                    }

                    // NGでなければキャラクター選択画面へ
                    titleManager.ShowCharacterSelectWindow();
                }

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"CheckPlayerName パッチエラー: {ex}");
                return true;
            }
        }
    }


    // RegistrationUserData - ユーザー登録をバイパス
    [HarmonyPatch(typeof(NCMBManager), "RegistrationUserData")]
    public static class NCMBManager_RegistrationUserData_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                // デリゲートを呼び出して成功を通知
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"RegistrationUserData パッチエラー: {ex}");
                return true;
            }
        }
    }


    // GetJoinRequest - 参加リクエスト取得をスキップ
    [HarmonyPatch(typeof(NCMBManager), "GetJoinRequest")]
    public static class NCMBManager_GetJoinRequest_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GetJoinRequest パッチエラー: {ex}");
                return true;
            }
        }
    }


    // GetBlockerList - ブロックリスト取得をスキップ
    [HarmonyPatch(typeof(NCMBManager), "GetBlockerList")]
    public static class NCMBManager_GetBlockerList_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GetBlockerList パッチエラー: {ex}");
                return true;
            }
        }
    }


    // GetGiftMessage - ギフトメッセージ取得をスキップ
    [HarmonyPatch(typeof(NCMBManager), "GetGiftMessage")]
    public static class NCMBManager_GetGiftMessage_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GetGiftMessage パッチエラー: {ex}");
                return true;
            }
        }
    }


    // SetLastLogin - 最終ログイン時刻設定をスキップ
    [HarmonyPatch(typeof(NCMBManager), "SetLastLogin")]
    public static class NCMBManager_SetLastLogin_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"SetLastLogin パッチエラー: {ex}");
                return true;
            }
        }
    }


    // GetUserMessage - ユーザーメッセージ取得をスキップ
    [HarmonyPatch(typeof(NCMBManager), "GetUserMessage")]
    public static class NCMBManager_GetUserMessage_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GetUserMessage パッチエラー: {ex}");
                return true;
            }
        }
    }


    // GetSystemInfo - システム情報取得をスキップ
    [HarmonyPatch(typeof(NCMBManager), "GetSystemInfo")]
    public static class NCMBManager_GetSystemInfo_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"GetSystemInfo パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // SaveUserData - ユーザーデータ保存をバイパス
    // ==========================================
    // ゲーム終了時（ステアゲ遺跡、アトラクション等）にリワードデータを
    // サーバーへ保存しようとするが、オフラインでは通信不可のためスキップ。
    // 報酬はローカルのGameManagerに既に反映されているので、
    // 成功を返してホーム画面への遷移を許可する。
    [HarmonyPatch(typeof(NCMBManager), "SaveUserData")]
    public static class NCMBManager_SaveUserData_Patch
    {
        static bool Prefix(NCMBManager __instance, int? _coin, int? _crystal,
            List<string> _unitDataStringList, List<string> _itemDataStringList,
            List<string> _accessoryDataStringList, string _giftMessageID, bool _isSaveMissionData)
        {
            try
            {
                // ローカルのGameManagerに報酬を反映
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm != null)
                {
                    if (_coin.HasValue)
                    {
                        gm.SetMyCoin(_coin.Value);
                    }
                    if (_crystal.HasValue)
                    {
                        gm.SetMyCrystal(_crystal.Value);
                    }

                    // UnitDataの更新をローカルに反映（パラメータ強化、クラスアビリティ取得等）
                    if (_unitDataStringList != null && _unitDataStringList.Count > 0)
                    {
                        UnitData.RenewStockUnitDataFromStringList(_unitDataStringList);

                        // フィールド上のキャラクターのパラメータを再同期
                        if (gm.myPlayerObject != null)
                        {
                            var paramController = gm.myPlayerObject.GetComponent<FieldCharacterParameterController>();
                            if (paramController != null)
                            {
                                paramController.SyncParameterAll();
                            }
                        }
                    }

                    // ItemDataの更新をローカルに反映
                    if (_itemDataStringList != null && _itemDataStringList.Count > 0)
                    {
                        ItemData.RenewStockItemDataFromStringList(_itemDataStringList);
                    }

                    // AccessoryDataの更新をローカルに反映
                    if (_accessoryDataStringList != null && _accessoryDataStringList.Count > 0)
                    {
                        AccessoryData.RenewStockAccessoryDataFromStringList(_accessoryDataStringList);
                    }

                    // ミッションデータの更新（報酬受け取り時）
                    if (_isSaveMissionData)
                    {
                        var missionScreenManager = SingletonMonoBehaviour<MissionScreenManager>.Instance;
                        if (missionScreenManager != null && missionScreenManager.saveMissionDataList != null
                            && missionScreenManager.saveMissionDataList.Count > 0)
                        {
                            gm.missionDataList = new List<string>(missionScreenManager.saveMissionDataList);
                        }
                        OfflineSaveDataManager.SaveMissionDataOnly();
                    }

                    // オフライン専用セーブデータに永続化
                    OfflineSaveDataManager.SaveAllData();
                }

                // デリゲートを呼び出して成功を通知
                // 注意: 呼び出し元が SaveUserData() の後に SetDelegateOnFinishedNetworkProcess() を
                // 設定するパターンがあるため（例: ItemController.UseItem）、
                // 1フレーム遅延させてデリゲート設定を待つ
                __instance.StartCoroutine(CallDelegateNextFrame(__instance));

                return false; // 元のサーバー通信処理をスキップ
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveUserData] パッチエラー: {ex}");
                // エラー時も成功扱いにしてホーム遷移を許可
                try
                {
                    __instance.StartCoroutine(CallDelegateNextFrame(__instance));
                }
                catch { }
                return false;
            }
        }

        static IEnumerator CallDelegateNextFrame(NCMBManager instance)
        {
            yield return null; // 1フレーム待機してデリゲート設定を待つ

            try
            {
                instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(instance, new object[] { true });

                instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(instance, false);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveUserData] デリゲート呼び出しエラー: {ex}");
            }
        }
    }


    // ==========================================
    // SaveEquipAccessory - アクセサリ装備データ保存をバイパス
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SaveEquipAccessory")]
    public static class NCMBManager_SaveEquipAccessory_Patch
    {
        static bool Prefix(NCMBManager __instance, string[] _equipAccessory)
        {
            try
            {
                // _equipAccessory には全キャラ分の装備文字列が含まれる
                // 形式: "unitID,accessoryID,bonePath,posX,posY,posZ,rotX,rotY,rotZ,scaleX,scaleY,scaleZ"
                OfflineSaveDataManager.SaveEquipAccessory(_equipAccessory);

                // マイカードのアクセサリも同期
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm.primaryUnitData != null)
                {
                    string primaryUnitID = ((int)gm.primaryUnitData.unitID).ToString();
                    foreach (var userInfo in gm.userInformationList)
                    {
                        if (userInfo.userID == gm.userID)
                        {
                            // avaterUnitIDがプライマリキャラと同じ場合のみアクセサリを同期
                            if (userInfo.avaterUnitID == primaryUnitID &&
                                gm.primaryUnitData.equipAccessoryList != null)
                            {
                                userInfo.avaterEquipAccessoryList = new List<string>(gm.primaryUnitData.equipAccessoryList);
                            }
                            break;
                        }
                    }
                }

                // 成功メッセージを表示
                gm.ShowSystemMessage("装備データをセーブしました");

                // デリゲートを呼び出して成功を通知
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false; // 元のサーバー通信処理をスキップ
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveEquipAccessory] パッチエラー: {ex}");
                // エラー時も成功扱いにして画面遷移を許可
                try
                {
                    __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                        BindingFlags.Public | BindingFlags.Instance)
                        ?.Invoke(__instance, new object[] { true });
                }
                catch { }
                return false;
            }
        }
    }

}

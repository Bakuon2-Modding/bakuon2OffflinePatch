using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BakuonOfflinePatch
{
    /// <summary>
    /// オフラインモード用のローカルセーブデータ管理
    /// キャラクター、アイテム、アクセサリ、通貨などをES2でローカル保存
    /// </summary>
    public static class OfflineSaveDataManager
    {
        // ES2保存用のタグ名
        private const string TAG_COIN = "offline_coin";
        private const string TAG_CRYSTAL = "offline_crystal";
        private const string TAG_UNIT_DATA = "offline_unitData";
        private const string TAG_ITEM_DATA = "offline_itemData";
        private const string TAG_ACCESSORY_DATA = "offline_accessoryData";
        private const string TAG_EQUIP_ACCESSORY = "offline_equipAccessory";
        private const string TAG_PRIMARY_UNIT_ID = "offline_primaryUnitID";
        private const string TAG_PLAYER_NAME = "offline_playerName";
        private const string TAG_GUILD_NAME = "offline_guildName";
        private const string TAG_IS_GUILD_MASTER = "offline_isGuildMaster";
        private const string TAG_GUILD_INFO = "offline_guildInfo";
        private const string TAG_MISSION_DATA = "offline_missionData";
        private const string TAG_MISSION_UPDATE_TIME = "offline_missionUpdateTime";
        private const string TAG_COUNTRY = "offline_country";

        // マイカード（プロフィールカード）の編集内容
        private const string TAG_MYCARD_SUBNAME = "offline_mycard_subName";
        private const string TAG_MYCARD_COMMENT = "offline_mycard_comment";
        private const string TAG_MYCARD_TAPWORD = "offline_mycard_tapWord";
        private const string TAG_MYCARD_TAPEMOTION = "offline_mycard_tapEmotionID";
        private const string TAG_MYCARD_AVATAR_UNIT = "offline_mycard_avaterUnitID";
        private const string TAG_MYCARD_AVATAR_ACC = "offline_mycard_avaterAccessory";
        private const string TAG_MYCARD_GOODCOUNT = "offline_mycard_goodCount";

        private const string SAVE_FILE = "saveData_offline";

        /// <summary>
        /// セーブデータが存在するか確認（プレイヤー名タグ）
        /// </summary>
        public static bool HasSaveData()
        {
            try
            {
                return ES2.Exists(SAVE_FILE + "?tag=" + TAG_PLAYER_NAME);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// ゲームデータ（通貨等）が保存済みか確認
        /// タイトル画面でのSavePlayerNameより後に初めてSaveAllDataで書き込まれるタグを使用
        /// </summary>
        public static bool HasGameData()
        {
            try
            {
                return ES2.Exists(SAVE_FILE + "?tag=" + TAG_COIN);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// プレイヤー名を保存
        /// </summary>
        public static void SavePlayerName(string name)
        {
            try
            {
                ES2.Save(name, SAVE_FILE + "?tag=" + TAG_PLAYER_NAME);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] プレイヤー名保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// プレイヤー名を読み込み（未保存なら null）
        /// </summary>
        public static string LoadPlayerName()
        {
            try
            {
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_PLAYER_NAME))
                {
                    return ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_PLAYER_NAME);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineLoad] プレイヤー名読み込みエラー: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// セーブデータを全削除（初期化）
        /// </summary>
        public static void DeleteAllData()
        {
            try
            {
                if (ES2.Exists(SAVE_FILE))
                {
                    ES2.Delete(SAVE_FILE);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] セーブデータ初期化エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 全データをローカルに保存
        /// </summary>
        public static void SaveAllData()
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm == null) return;

                // プレイヤー名を保存
                if (!string.IsNullOrEmpty(gm.playerName))
                {
                    ES2.Save(gm.playerName, SAVE_FILE + "?tag=" + TAG_PLAYER_NAME);
                }

                // 通貨を保存
                ES2.Save(gm.myCoin, SAVE_FILE + "?tag=" + TAG_COIN);
                ES2.Save(gm.myCrystal, SAVE_FILE + "?tag=" + TAG_CRYSTAL);

                // キャラクターデータを保存
                var unitDataList = UnitData.GetStockUnitDataStringList();
                ES2.Save(unitDataList, SAVE_FILE + "?tag=" + TAG_UNIT_DATA);

                // アイテムデータを保存
                var itemDataList = ItemData.GetStockItemDataStringList();
                ES2.Save(itemDataList, SAVE_FILE + "?tag=" + TAG_ITEM_DATA);

                // アクセサリデータを保存
                var accessoryDataList = AccessoryData.GetStockAccessoryDataStringList();
                ES2.Save(accessoryDataList, SAVE_FILE + "?tag=" + TAG_ACCESSORY_DATA);

                // 選択中のキャラクターIDを保存
                if (gm.primaryUnitData != null)
                {
                    ES2.Save((int)gm.primaryUnitData.unitID, SAVE_FILE + "?tag=" + TAG_PRIMARY_UNIT_ID);
                }

                // 所属国家を保存
                ES2.Save(gm.myCountry, SAVE_FILE + "?tag=" + TAG_COUNTRY);

                // ギルド情報を保存
                SaveGuildData(gm);

                // ミッションデータを保存
                SaveMissionData(gm);

                // ユーザーコンテンツを保存
                OfflineUserContentsStore.Save();

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] 保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 装備アクセサリをローカルに保存
        /// </summary>
        public static void SaveEquipAccessory(string[] equipAccessory)
        {
            try
            {
                if (equipAccessory != null)
                {
                    var list = new List<string>(equipAccessory);
                    ES2.Save(list, SAVE_FILE + "?tag=" + TAG_EQUIP_ACCESSORY);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] 装備アクセサリ保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ローカルデータを読み込んでゲームに反映
        /// </summary>
        public static void LoadAllData()
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm == null)
                {
                    LogHelper.LogWarning("[OfflineLoad] GameManagerがnullです");
                    return;
                }

                // 通貨を読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_COIN))
                {
                    gm.myCoin = ES2.Load<int>(SAVE_FILE + "?tag=" + TAG_COIN);
                }

                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_CRYSTAL))
                {
                    gm.myCrystal = ES2.Load<int>(SAVE_FILE + "?tag=" + TAG_CRYSTAL);
                }

                // キャラクターデータを読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_UNIT_DATA))
                {
                    var unitDataList = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_UNIT_DATA);
                    if (unitDataList != null && unitDataList.Count > 0)
                    {
                        UnitData.RenewStockUnitDataFromStringList(unitDataList);
                    }
                }

                // アイテムデータを読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_ITEM_DATA))
                {
                    var itemDataList = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_ITEM_DATA);
                    if (itemDataList != null && itemDataList.Count > 0)
                    {
                        ItemData.RenewStockItemDataFromStringList(itemDataList);
                    }
                }

                // アクセサリデータを読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_ACCESSORY_DATA))
                {
                    var accessoryDataList = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_ACCESSORY_DATA);
                    if (accessoryDataList != null && accessoryDataList.Count > 0)
                    {
                        AccessoryData.RenewStockAccessoryDataFromStringList(accessoryDataList);
                    }
                }

                // 選択中のキャラクターIDを読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_PRIMARY_UNIT_ID))
                {
                    int primaryUnitID = ES2.Load<int>(SAVE_FILE + "?tag=" + TAG_PRIMARY_UNIT_ID);
                    var stockUnit = UnitData.GetStockUnitData((UnitData.enumUnitID)primaryUnitID);
                    if (stockUnit != null)
                    {
                        gm.primaryUnitData = stockUnit;

                        // partyUnitPrefabArrayも更新（CreatePlayerPrefabで使われるため）
                        if (gm.partyUnitPrefabArray != null && gm.partyUnitPrefabArray.Length > 0)
                        {
                            gm.partyUnitPrefabArray[0] = stockUnit.gameObject;
                        }

                    }
                }

                // 所属国家を読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_COUNTRY))
                {
                    gm.myCountry = ES2.Load<int>(SAVE_FILE + "?tag=" + TAG_COUNTRY);
                }

                // ギルド情報を読み込み
                LoadGuildData(gm);

                // ミッションデータを読み込み
                LoadMissionData(gm);

                // ユーザーコンテンツを読み込み
                OfflineUserContentsStore.EnsureLoaded();

                // 装備アクセサリを読み込み（各UnitDataのequipAccessoryListに設定）
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_EQUIP_ACCESSORY))
                {
                    var equipAccessoryList = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_EQUIP_ACCESSORY);
                    if (equipAccessoryList != null && equipAccessoryList.Count > 0)
                    {
                        // 全キャラのequipAccessoryListをクリア
                        foreach (var stockUnit in gm.stockUnitDataList)
                        {
                            stockUnit.equipAccessoryList.Clear();
                        }

                        // 各アクセサリを対応するキャラクターに追加
                        foreach (var equipAccessory in equipAccessoryList)
                        {
                            string[] parts = equipAccessory.Split(',');
                            if (parts.Length >= 2)
                            {
                                int unitID;
                                if (int.TryParse(parts[0], out unitID))
                                {
                                    var stockUnit = UnitData.GetStockUnitData((UnitData.enumUnitID)unitID);
                                    if (stockUnit != null)
                                    {
                                        stockUnit.equipAccessoryList.Add(equipAccessory);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineLoad] 読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ギルド情報を保存
        /// </summary>
        private static void SaveGuildData(GameManager gm)
        {
            try
            {
                // ギルド名とマスターフラグを保存
                ES2.Save(gm.myGuildName ?? "", SAVE_FILE + "?tag=" + TAG_GUILD_NAME);
                ES2.Save(gm.isGuildMaster, SAVE_FILE + "?tag=" + TAG_IS_GUILD_MASTER);

                // guildInformationListを文字列リストとして保存
                // 形式: "guildName,guildLevel,guildEXP,guildPolicy,guildApproval,guildComment,guildMasterUserID,guildMasterName,guildMemberCount"
                var guildStrings = new List<string>();
                foreach (var gi in gm.guildInformationList)
                {
                    if (string.IsNullOrEmpty(gi.guildName)) continue;
                    string entry = string.Join("\t",
                        gi.guildName,
                        gi.guildLevel ?? "1",
                        gi.guildEXP ?? "0",
                        gi.guildPolicy ?? "",
                        gi.guildApproval ?? "0",
                        gi.guildComment ?? "",
                        gi.guildMasterUserID ?? "",
                        gi.guildMasterName ?? "",
                        gi.guildMemberCount.ToString()
                    );
                    guildStrings.Add(entry);
                }
                ES2.Save(guildStrings, SAVE_FILE + "?tag=" + TAG_GUILD_INFO);

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] ギルド保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ギルド情報を読み込み
        /// </summary>
        private static void LoadGuildData(GameManager gm)
        {
            try
            {
                // ギルド名を読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_GUILD_NAME))
                {
                    gm.myGuildName = ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_GUILD_NAME);
                }

                // ギルドマスターフラグを読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_IS_GUILD_MASTER))
                {
                    gm.isGuildMaster = ES2.Load<bool>(SAVE_FILE + "?tag=" + TAG_IS_GUILD_MASTER);
                }

                // guildInformationListを読み込み
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_GUILD_INFO))
                {
                    var guildStrings = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_GUILD_INFO);
                    if (guildStrings != null)
                    {
                        foreach (var entry in guildStrings)
                        {
                            string[] parts = entry.Split('\t');
                            if (parts.Length >= 9)
                            {
                                var gi = new GuildInformation();
                                gi.guildName = parts[0];
                                gi.guildLevel = parts[1];
                                gi.guildEXP = parts[2];
                                gi.guildPolicy = parts[3];
                                gi.guildApproval = parts[4];
                                gi.guildComment = parts[5];
                                gi.guildMasterUserID = parts[6];
                                gi.guildMasterName = parts[7];
                                int memberCount;
                                if (int.TryParse(parts[8], out memberCount))
                                {
                                    gi.guildMemberCount = memberCount;
                                }
                                else
                                {
                                    gi.guildMemberCount = 1;
                                }
                                gi.guildMemberUserIDList = new List<string> { gm.userID };

                                // 重複チェックして追加
                                bool exists = false;
                                foreach (var existing in gm.guildInformationList)
                                {
                                    if (existing.guildName == gi.guildName)
                                    {
                                        exists = true;
                                        break;
                                    }
                                }
                                if (!exists)
                                {
                                    gm.guildInformationList.Add(gi);
                                }
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineLoad] ギルド読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ミッションデータを保存
        /// </summary>
        private static void SaveMissionData(GameManager gm)
        {
            try
            {
                if (gm.missionDataList != null && gm.missionDataList.Count > 0)
                {
                    ES2.Save(gm.missionDataList, SAVE_FILE + "?tag=" + TAG_MISSION_DATA);
                }
                ES2.Save(gm.missionDataUpdateTime.ToString("o"), SAVE_FILE + "?tag=" + TAG_MISSION_UPDATE_TIME);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] ミッション保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ミッションデータを読み込み
        /// </summary>
        private static void LoadMissionData(GameManager gm)
        {
            try
            {
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MISSION_DATA))
                {
                    var missionDataList = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_MISSION_DATA);
                    if (missionDataList != null && missionDataList.Count > 0)
                    {
                        gm.missionDataList = missionDataList;
                    }
                }

                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MISSION_UPDATE_TIME))
                {
                    string timeStr = ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_MISSION_UPDATE_TIME);
                    DateTime parsed;
                    if (DateTime.TryParse(timeStr, out parsed))
                    {
                        gm.missionDataUpdateTime = parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineLoad] ミッション読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ミッションデータのみを保存（外部から呼び出し用）
        /// </summary>
        public static void SaveMissionDataOnly()
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm == null) return;
                SaveMissionData(gm);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] ミッション単独保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 装備アクセサリを読み込み
        /// </summary>
        public static List<string> LoadEquipAccessory()
        {
            try
            {
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_EQUIP_ACCESSORY))
                {
                    return ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_EQUIP_ACCESSORY);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineLoad] 装備アクセサリ読み込みエラー: {ex.Message}");
            }
            return new List<string>();
        }

        /// <summary>
        /// マイカードの編集内容（サブネーム・コメント・タップワード）を保存
        /// </summary>
        public static void SaveMyCardEdit(string subPlayerName, string comment, string tapWord, string tapEmotionID)
        {
            try
            {
                ES2.Save(subPlayerName ?? "", SAVE_FILE + "?tag=" + TAG_MYCARD_SUBNAME);
                ES2.Save(comment ?? "", SAVE_FILE + "?tag=" + TAG_MYCARD_COMMENT);
                ES2.Save(tapWord ?? "", SAVE_FILE + "?tag=" + TAG_MYCARD_TAPWORD);
                ES2.Save(string.IsNullOrEmpty(tapEmotionID) ? "0" : tapEmotionID, SAVE_FILE + "?tag=" + TAG_MYCARD_TAPEMOTION);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] マイカード保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// マイカードのアバター（キャラ・装備アクセサリ）を保存
        /// </summary>
        public static void SaveMyCardAvater(string avaterUnitID, List<string> avaterEquipAccessoryList)
        {
            try
            {
                ES2.Save(avaterUnitID ?? "", SAVE_FILE + "?tag=" + TAG_MYCARD_AVATAR_UNIT);
                ES2.Save(avaterEquipAccessoryList ?? new List<string>(), SAVE_FILE + "?tag=" + TAG_MYCARD_AVATAR_ACC);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] マイカードアバター保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// マイカードの「いいね」数を保存
        /// </summary>
        public static void SaveMyCardGoodCount(int goodCount)
        {
            try
            {
                ES2.Save(goodCount, SAVE_FILE + "?tag=" + TAG_MYCARD_GOODCOUNT);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineSave] マイカードいいね保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存済みマイカード内容を UserInformation に反映（未保存タグはスキップ）
        /// </summary>
        public static void ApplyMyCardTo(UserInformation userInfo)
        {
            if (userInfo == null) return;
            try
            {
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MYCARD_SUBNAME))
                    userInfo.subPlayerName = ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_MYCARD_SUBNAME);
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MYCARD_COMMENT))
                    userInfo.comment = ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_MYCARD_COMMENT);
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MYCARD_TAPWORD))
                    userInfo.tapWord = ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_MYCARD_TAPWORD);
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MYCARD_TAPEMOTION))
                    userInfo.tapEmotionID = ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_MYCARD_TAPEMOTION);

                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MYCARD_AVATAR_UNIT))
                {
                    string unit = ES2.Load<string>(SAVE_FILE + "?tag=" + TAG_MYCARD_AVATAR_UNIT);
                    if (!string.IsNullOrEmpty(unit)) userInfo.avaterUnitID = unit;
                }
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MYCARD_AVATAR_ACC))
                {
                    var acc = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_MYCARD_AVATAR_ACC);
                    if (acc != null) userInfo.avaterEquipAccessoryList = acc;
                }
                if (ES2.Exists(SAVE_FILE + "?tag=" + TAG_MYCARD_GOODCOUNT))
                    userInfo.goodCount = ES2.Load<int>(SAVE_FILE + "?tag=" + TAG_MYCARD_GOODCOUNT);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[OfflineLoad] マイカード読み込みエラー: {ex.Message}");
            }
        }
    }


    // ==========================================
    // NCMBManager.SaveUnitData_WithoutCheckBusyAndError パッチ - ローカル保存
    // ==========================================
    // 注意: NCMBManager.SaveUserData パッチは ServerPatches.cs に定義
    [HarmonyPatch(typeof(NCMBManager), "SaveUnitData_WithoutCheckBusyAndError")]
    public static class NCMBManager_SaveUnitData_WithoutCheck_Patch
    {
        static bool Prefix(List<string> _unitDataStringList)
        {
            try
            {
                if (_unitDataStringList != null && _unitDataStringList.Count > 0)
                {
                    UnitData.RenewStockUnitDataFromStringList(_unitDataStringList);
                    OfflineSaveDataManager.SaveAllData();
                }
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveUnitData] パッチエラー: {ex}");
                return false;
            }
        }
    }


    // ==========================================
    // GameManager.LoadCommonSaveData パッチ
    // saveData（元ゲーム）ではなく saveData_offline から読み込む
    // ==========================================
    [HarmonyPatch(typeof(GameManager), "LoadCommonSaveData")]
    public static class GameManager_LoadCommonSaveData_Patch
    {
        private const string FILE = "saveData_offline";

        static bool Prefix(GameManager __instance)
        {
            try
            {
                // niconicoMailAddress / niconicoPassword はオフラインでは不要
                __instance.niconicoMailAddress = "";
                __instance.niconicoPassword = "";

                __instance.BGMVolume                                 = Load("BGMVolume",                              0.1f);
                __instance.SEVolume                                  = Load("SEVolume",                               0.2f);
                __instance.youtubeVolume                             = Load("youtubeVolume",                          0.1f);
                __instance.videoThumbnailSize                        = Load("VideoThumbnailSize",                     3.5f);
                __instance.isPlayerLabelEnabled                      = Load("IsPlayerLabelEnabled",                   true);
                __instance.isDamageLabelEnabled                      = Load("IsDamageLabelEnabled",                   true);
                __instance.isYoutubeEnabled                          = Load("IsYoutubeEnabled",                       true);
                __instance.isYoutubeATypeEnabled                     = Load("IsYoutubeATypeEnabled",                  true);
                __instance.isTwitterEnabled                          = Load("IsTwitterEnabled",                       true);
                __instance.isDrawingCanvasEnabled                    = Load("IsDrawingCanvasEnabled",                 true);
                __instance.isUserIllustContentsBoardEnabled          = Load("IsUserContentsIllustBoardEnabled",       true);
                __instance.isUserStoryContentsBoardEnabled           = Load("IsUserContentsStoryBoardEnabled",        true);
                __instance.safeAreaRatio                             = Load("safeAreaRatio",                          0f);
                __instance.isLowResolutionEnabled                    = Load("IsLowResolutionEnabled",                 false);
                __instance.isServerChatEnabled                       = Load("IsServerChatEnabled",                    true);
                __instance.isAccessoryEnabled                        = Load("IsAccessoryEnabled",                     true);
                __instance.uiScrollSpeed_mouseConfig                 = Load("uiScrollSpeed_mouseConfig",              1f);
                __instance.cameraMoveDirectionVertical_mouseConfig   = Load("cameraMoveDirectionVertical_mouseConfig",   0);
                __instance.cameraMoveDirectionHorizontal_mouseConfig = Load("cameraMoveDirectionHorizontal_mouseConfig", 0);
                __instance.cameraMoveSpeedVertical_mouseConfig       = Load("cameraMoveSpeedVertical_mouseConfig",    1f);
                __instance.cameraMoveSpeedHorizontal_mouseConfig     = Load("cameraMoveSpeedHorizontal_mouseConfig",  1f);
                __instance.cameraMoveAxisVertical_padConfig          = Load("cameraMoveAxisVertical_padConfig",       2);
                __instance.cameraMoveDirectionVertical_padConfig     = Load("cameraMoveDirectionVertical_padConfig",  0);
                __instance.cameraMoveAxisHorizontal_padConfig        = Load("cameraMoveAxisHorizontal_padConfig",     1);
                __instance.cameraMoveDirectionHorizontal_padConfig   = Load("cameraMoveDirectionHorizontal_padConfig", 0);
                __instance.cameraMoveSpeedVertical_padConfig         = Load("cameraMoveSpeedVertical_padConfig",      1f);
                __instance.cameraMoveSpeedHorizontal_padConfig       = Load("cameraMoveSpeedHorizontal_padConfig",    1f);
                __instance.keycord_a_padConfig                       = Load("keycord_a_padConfig",                   KeyCode.JoystickButton2);
                __instance.keycord_b_padConfig                       = Load("keycord_b_padConfig",                   KeyCode.JoystickButton3);
                __instance.keycord_c_padConfig                       = Load("keycord_c_padConfig",                   KeyCode.JoystickButton1);
                __instance.keycord_dodge_padConfig                   = Load("keycord_dodge_padConfig",               KeyCode.JoystickButton5);
                __instance.keycord_jump_padConfig                    = Load("keycord_jump_padConfig",                KeyCode.JoystickButton0);
                __instance.keycord_bomb_padConfig                    = Load("keycord_bomb_padConfig",                KeyCode.JoystickButton4);

                // TipsManager は GameManager.Awake 時点では未初期化の場合があるためnullチェック
                var tipsManager = SingletonMonoBehaviour<TipsManager>.Instance;
                if (tipsManager != null && ES2.Exists(FILE + "?tag=isReadTutorial"))
                {
                    tipsManager.isFinishedTutorial = ES2.Load<bool>(FILE + "?tag=isReadTutorial");
                }

                // AudioManager は Instance(=FindObjectOfType) が非nullでも Awake 未実行だと
                // _bgmSource / _seSourceList が未初期化のため ChangeVolume 内部で NRE になる。
                // Awake 済み(=_bgmSource 初期化済み)を確認してから呼ぶ。
                var audioManager = SingletonMonoBehaviour<AudioManager>.Instance;
                var bgmSource = audioManager != null
                    ? AccessTools.Field(typeof(AudioManager), "_bgmSource")?.GetValue(audioManager)
                    : null;
                if (audioManager != null && bgmSource != null)
                {
                    audioManager.ChangeVolume(__instance.BGMVolume / 5f, __instance.SEVolume / 5f);
                    __instance.SetLinearVolumeToMixerGroup("Master", __instance.SEVolume / 5f);
                }
                else
                {
                    LogHelper.LogInfo("[CommonSettings] AudioManager 未初期化のため音量適用をスキップ");
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[CommonSettings] 読み込みエラー: {ex}");
            }
            return false;
        }

        private static float Load(string tag, float defaultValue)
        {
            return ES2.Exists(FILE + "?tag=" + tag) ? ES2.Load<float>(FILE + "?tag=" + tag) : defaultValue;
        }

        private static bool Load(string tag, bool defaultValue)
        {
            return ES2.Exists(FILE + "?tag=" + tag) ? ES2.Load<bool>(FILE + "?tag=" + tag) : defaultValue;
        }

        private static int Load(string tag, int defaultValue)
        {
            return ES2.Exists(FILE + "?tag=" + tag) ? ES2.Load<int>(FILE + "?tag=" + tag) : defaultValue;
        }

        private static KeyCode Load(string tag, KeyCode defaultValue)
        {
            return ES2.Exists(FILE + "?tag=" + tag) ? ES2.Load<KeyCode>(FILE + "?tag=" + tag) : defaultValue;
        }
    }


    // ==========================================
    // ConfigScreenManager.ApplyConfig パッチ
    // saveData（元ゲーム）ではなく saveData_offline へ保存する
    // ==========================================
    [HarmonyPatch(typeof(ConfigScreenManager), "ApplyConfig")]
    public static class ConfigScreenManager_ApplyConfig_Patch
    {
        private const string FILE = "saveData_offline";

        static bool Prefix(ConfigScreenManager __instance)
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm == null) return false;

                // niconicoMailAddress / niconicoPassword は保存しない（オフラインでは不要）
                ES2.Save(gm.BGMVolume,                                FILE + "?tag=BGMVolume");
                ES2.Save(gm.SEVolume,                                 FILE + "?tag=SEVolume");
                ES2.Save(gm.youtubeVolume,                            FILE + "?tag=youtubeVolume");

                var videoSlider = (UnityEngine.UI.Slider)typeof(ConfigScreenManager)
                    .GetField("videoThumbnailSizeSlider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.GetValue(__instance);
                if (videoSlider != null)
                    ES2.Save(videoSlider.value,                       FILE + "?tag=VideoThumbnailSize");

                ES2.Save(gm.isPlayerLabelEnabled,                     FILE + "?tag=IsPlayerLabelEnabled");
                ES2.Save(gm.isDamageLabelEnabled,                     FILE + "?tag=IsDamageLabelEnabled");
                ES2.Save(gm.isYoutubeEnabled,                         FILE + "?tag=IsYoutubeEnabled");
                ES2.Save(gm.isYoutubeATypeEnabled,                    FILE + "?tag=IsYoutubeATypeEnabled");
                ES2.Save(gm.isTwitterEnabled,                         FILE + "?tag=IsTwitterEnabled");
                ES2.Save(gm.isDrawingCanvasEnabled,                   FILE + "?tag=IsDrawingCanvasEnabled");
                ES2.Save(gm.isUserIllustContentsBoardEnabled,         FILE + "?tag=IsUserContentsIllustBoardEnabled");
                ES2.Save(gm.isUserStoryContentsBoardEnabled,          FILE + "?tag=IsUserContentsStoryBoardEnabled");
                ES2.Save(gm.safeAreaRatio,                            FILE + "?tag=safeAreaRatio");
                ES2.Save(gm.isLowResolutionEnabled,                   FILE + "?tag=IsLowResolutionEnabled");
                ES2.Save(gm.isServerChatEnabled,                      FILE + "?tag=IsServerChatEnabled");
                ES2.Save(gm.isAccessoryEnabled,                       FILE + "?tag=IsAccessoryEnabled");
                ES2.Save(gm.uiScrollSpeed_mouseConfig,                FILE + "?tag=uiScrollSpeed_mouseConfig");
                ES2.Save(gm.cameraMoveDirectionVertical_mouseConfig,   FILE + "?tag=cameraMoveDirectionVertical_mouseConfig");
                ES2.Save(gm.cameraMoveSpeedVertical_mouseConfig,       FILE + "?tag=cameraMoveSpeedVertical_mouseConfig");
                ES2.Save(gm.cameraMoveDirectionHorizontal_mouseConfig, FILE + "?tag=cameraMoveDirectionHorizontal_mouseConfig");
                ES2.Save(gm.cameraMoveSpeedHorizontal_mouseConfig,     FILE + "?tag=cameraMoveSpeedHorizontal_mouseConfig");
                ES2.Save(gm.cameraMoveAxisVertical_padConfig,          FILE + "?tag=cameraMoveAxisVertical_padConfig");
                ES2.Save(gm.cameraMoveDirectionVertical_padConfig,     FILE + "?tag=cameraMoveDirectionVertical_padConfig");
                ES2.Save(gm.cameraMoveSpeedVertical_padConfig,         FILE + "?tag=cameraMoveSpeedVertical_padConfig");
                ES2.Save(gm.cameraMoveAxisHorizontal_padConfig,        FILE + "?tag=cameraMoveAxisHorizontal_padConfig");
                ES2.Save(gm.cameraMoveDirectionHorizontal_padConfig,   FILE + "?tag=cameraMoveDirectionHorizontal_padConfig");
                ES2.Save(gm.cameraMoveSpeedHorizontal_padConfig,       FILE + "?tag=cameraMoveSpeedHorizontal_padConfig");
                ES2.Save(gm.keycord_a_padConfig,                      FILE + "?tag=keycord_a_padConfig");
                ES2.Save(gm.keycord_b_padConfig,                      FILE + "?tag=keycord_b_padConfig");
                ES2.Save(gm.keycord_c_padConfig,                      FILE + "?tag=keycord_c_padConfig");
                ES2.Save(gm.keycord_dodge_padConfig,                  FILE + "?tag=keycord_dodge_padConfig");
                ES2.Save(gm.keycord_jump_padConfig,                   FILE + "?tag=keycord_jump_padConfig");
                ES2.Save(gm.keycord_bomb_padConfig,                   FILE + "?tag=keycord_bomb_padConfig");

                // チュートリアル状態も offline に保存
                var tipsManager = SingletonMonoBehaviour<TipsManager>.Instance;
                if (tipsManager != null)
                    ES2.Save(tipsManager.isFinishedTutorial,          FILE + "?tag=isReadTutorial");

                // 入力フィールドの値を emotionList に反映してから保存（元コードの再現）
                for (int i = 0; i < gm.emotionList.Count; i++)
                {
                    int myEnumEmotionID = (int)__instance.emotionDataList[i].property.myEnumEmotionID;
                    gm.emotionList[i] = myEnumEmotionID + "," + __instance.emotionChatInputFieldList[i].text;
                }
                ES2.Save(gm.emotionList, FILE + "?tag=" + gm.userID + "_emotionChat");

                // ショートカットボタンの表示を更新
                if (SingletonMonoBehaviour<ChatInputManager>.Instance != null)
                    SingletonMonoBehaviour<ChatInputManager>.Instance.UpdateShotcutButton();

                // 元の処理のうちES2以外の部分はそのまま実行
                // Rewired.ReInput.userDataStore.Save() をリフレクションで呼ぶ
                try
                {
                    var reInputType = Type.GetType("Rewired.ReInput, Rewired_Core");
                    if (reInputType != null)
                    {
                        var userDataStore = reInputType.GetProperty("userDataStore",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        userDataStore?.GetType().GetMethod("Save")?.Invoke(userDataStore, null);
                    }
                }
                catch { }
                SafeAreaController.SetAllSafeArea();
                gm.ShowSystemMessage("設定をセーブしました");

                // isChangedConfig をリセット（privateフィールド）
                typeof(ConfigScreenManager)
                    .GetField("isChangedConfig", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.SetValue(__instance, false);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[CommonSettings] 保存エラー: {ex}");
            }
            return false;
        }
    }


    // ==========================================
    // GameManager.LoadSaveData パッチ
    // emotionList を saveData_offline から読み込む
    // その他の項目（latestRoomName 等）もオフライン側に分離
    // ==========================================
    [HarmonyPatch(typeof(GameManager), "LoadSaveData")]
    public static class GameManager_LoadSaveData_Patch
    {
        private const string FILE = "saveData_offline";

        static bool Prefix(GameManager __instance)
        {
            try
            {
                string uid = __instance.userID;

                __instance.latestRoomName = LoadStr(uid, "latestRoomName", "");
                __instance.resumeSuteageSeed = LoadInt(uid, "resumeSuteageSeed", 0);
                __instance.resumeSuteageDungeonMasterDataPropertyName = LoadStr(uid, "resumeSuteageDungeonMasterDataPropertyName", "");

                string macroTag = FILE + "?tag=" + uid + "_macro";
                if (ES2.Exists(macroTag))
                {
                    __instance.macroList = ES2.LoadList<string>(macroTag);
                }
                else
                {
                    __instance.macroList.Clear();
                    __instance.macroList.Add("ナイス！");
                    __instance.macroList.Add("お気になさらず");
                    __instance.macroList.Add("シールド張ります");
                    __instance.macroList.Add("回復お願い");
                    __instance.macroList.Add("先にザコ倒そう");
                    __instance.macroList.Add("今は耐えよう");
                    __instance.macroList.Add("今だ攻めよう");
                    __instance.macroList.Add("！！！？");
                }

                string emotionTag = FILE + "?tag=" + uid + "_emotionChat";
                if (ES2.Exists(emotionTag))
                {
                    __instance.emotionList = ES2.LoadList<string>(emotionTag);
                    return false;
                }

                __instance.emotionList.Clear();
                __instance.emotionList.Add("1,こんにちは！");
                __instance.emotionList.Add("2,よろしく！");
                __instance.emotionList.Add("4,どうもありがとう！");
                __instance.emotionList.Add("5,いいよ！");
                __instance.emotionList.Add("7,だめです");
                __instance.emotionList.Add("8,えへへ");
                __instance.emotionList.Add("12,敬礼！");
                __instance.emotionList.Add("13,ガッカリ");
                __instance.emotionList.Add("18,ゴロゴロ");
                __instance.emotionList.Add("22,踊りましょう！");
                __instance.emotionList.Add("0,先に雑魚倒そう！");
                __instance.emotionList.Add("0,回復お願い！");
                __instance.emotionList.Add("0,シールド張ります！");
                __instance.emotionList.Add("0,今だ攻めよう！");
                __instance.emotionList.Add("0,グッジョブ！");
                __instance.emotionList.Add("0,ナイス！");
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[LoadSaveData] 読み込みエラー: {ex}");
            }
            return false;
        }

        private static string LoadStr(string uid, string key, string defaultValue)
        {
            string tag = FILE + "?tag=" + uid + "_" + key;
            return ES2.Exists(tag) ? ES2.Load<string>(tag) : defaultValue;
        }

        private static int LoadInt(string uid, string key, int defaultValue)
        {
            string tag = FILE + "?tag=" + uid + "_" + key;
            return ES2.Exists(tag) ? ES2.Load<int>(tag) : defaultValue;
        }
    }


}

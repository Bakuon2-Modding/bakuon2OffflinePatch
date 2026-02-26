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


}

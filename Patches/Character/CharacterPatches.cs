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
    // キャラクター変更時にオフラインセーブデータへ保存
    // ==========================================
    // 元のEnterCharacterChangeはES2で "saveData?tag=userID_primaryUnitID" に保存するが、
    // オフラインパッチでは "saveData_offline?tag=offline_primaryUnitID" を使うため、
    // Postfixで追加保存する。
    [HarmonyPatch(typeof(CharacterScreenManager), "EnterCharacterChange")]
    public static class CharacterScreenManager_EnterCharacterChange_Patch
    {
        static void Postfix()
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm == null || gm.primaryUnitData == null) return;

                // マイカードのavaterUnitIDを新しいキャラクターに同期
                string newUnitID = ((int)gm.primaryUnitData.unitID).ToString();
                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == gm.userID)
                    {
                        userInfo.avaterUnitID = newUnitID;
                        // アクセサリも同期
                        if (gm.primaryUnitData.equipAccessoryList != null)
                        {
                            userInfo.avaterEquipAccessoryList = new List<string>(gm.primaryUnitData.equipAccessoryList);
                        }
                        break;
                    }
                }

                OfflineSaveDataManager.SaveAllData();
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[CharacterChange] 保存エラー: {ex}");
            }
        }
    }

}

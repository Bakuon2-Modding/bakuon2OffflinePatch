using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BakuonOfflinePatch
{
    // ==========================================
    // ギルドシステム: オフライン対応パッチ
    // ==========================================
    // NCMBデータベース通信をすべてスキップし、ローカルで動作するようにする

    // ==========================================
    // CreateGuild: ギルド作成
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "CreateGuild")]
    public static class NCMBManager_CreateGuild_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName, string _policy, string _approval, string _comment)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // ギルド名が空かチェック
                if (string.IsNullOrEmpty(_guildName))
                {
                    gm.ShowSystemMessage("ギルド名が空です");
                    return false;
                }

                // GuildInformationを作成してリストに追加
                GuildInformation guildInfo = new GuildInformation();
                guildInfo.guildName = _guildName;
                guildInfo.guildLevel = "1";
                guildInfo.guildEXP = "0";
                guildInfo.guildPolicy = _policy;
                guildInfo.guildApproval = _approval;
                guildInfo.guildComment = _comment;
                guildInfo.guildMasterUserID = gm.userID;
                guildInfo.guildMasterName = gm.playerName;
                guildInfo.guildMemberCount = 1;
                guildInfo.guildMemberUserIDList = new List<string> { gm.userID };

                // 既存のギルド情報を更新 or 追加
                bool found = false;
                for (int i = 0; i < gm.guildInformationList.Count; i++)
                {
                    if (gm.guildInformationList[i].guildName == _guildName)
                    {
                        gm.guildInformationList[i] = guildInfo;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    gm.guildInformationList.Add(guildInfo);
                }

                gm.ShowSystemMessage("ギルド[" + _guildName + "]を作成しました");

                // CreateGuild2相当の処理をインラインで実行（DB通信なし）
                gm.myGuildName = _guildName;
                gm.isGuildMaster = true;

                // userInformationListを更新
                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == gm.userID)
                    {
                        userInfo.guildName = _guildName;
                    }
                }

                // GuildScreenManagerを閉じる
                if ((bool)SingletonMonoBehaviour<GuildScreenManager>.Instance)
                {
                    SingletonMonoBehaviour<GuildScreenManager>.Instance.CloseRootMenu();
                }

                // マイカードのギルド名を更新
                SingletonMonoBehaviour<MenuScreenManager>.Instance.myCardGuildName = _guildName;

                // ギルドカードを表示
                List<string> list = new List<string> { _guildName };
                gm.delegateOnGotGuildInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowGuildCardFront;
                gm.GetGuildInformation(list);

                // ミッション達成
                gm.IncrementMissionAchievement(MissionData.enumMissionID.Once_JoinGuild, 1);

                // フィールドキャラクターのギルド名を更新
                if (gm.myPlayerObject != null)
                {
                    var fieldController = gm.myPlayerObject.GetComponent<FieldCharacterController>();
                    if (fieldController != null)
                    {
                        fieldController.myGuildName = _guildName;
                    }
                }

                // セーブデータに保存
                OfflineSaveDataManager.SaveAllData();

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] CreateGuild パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // CreateGuild2: ギルド作成後のUserData更新
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "CreateGuild2")]
    public static class NCMBManager_CreateGuild2_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName, bool _isGuildMaster)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                gm.myGuildName = _guildName;
                gm.isGuildMaster = _isGuildMaster;

                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == gm.userID)
                    {
                        userInfo.guildName = _guildName;
                    }
                }

                if ((bool)SingletonMonoBehaviour<GuildScreenManager>.Instance)
                {
                    SingletonMonoBehaviour<GuildScreenManager>.Instance.CloseRootMenu();
                }

                SingletonMonoBehaviour<MenuScreenManager>.Instance.myCardGuildName = _guildName;

                List<string> list = new List<string> { _guildName };
                gm.delegateOnGotGuildInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowGuildCardFront;
                gm.GetGuildInformation(list);

                gm.IncrementMissionAchievement(MissionData.enumMissionID.Once_JoinGuild, 1);

                if (gm.myPlayerObject != null)
                {
                    var fieldController = gm.myPlayerObject.GetComponent<FieldCharacterController>();
                    if (fieldController != null)
                    {
                        fieldController.myGuildName = _guildName;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] CreateGuild2 パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetGuildInformation: ギルド情報取得
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetGuildInformation", new Type[] { typeof(List<string>), typeof(List<string>) })]
    public static class NCMBManager_GetGuildInformation_Patch
    {
        static bool Prefix(NCMBManager __instance, List<string> _list, List<string> _originalList)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // リクエストされたギルド名に対して、既にguildInformationListにある情報を返す
                // 無い場合はダミー情報を作成
                foreach (string guildName in _list)
                {
                    bool exists = false;
                    foreach (var existing in gm.guildInformationList)
                    {
                        if (existing.guildName == guildName)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        // ローカルに情報がないギルドにはダミーデータを作成
                        GuildInformation guildInfo = new GuildInformation();
                        guildInfo.guildName = guildName;
                        guildInfo.guildLevel = "1";
                        guildInfo.guildEXP = "0";
                        guildInfo.guildPolicy = "自由";
                        guildInfo.guildApproval = "0";
                        guildInfo.guildComment = "";
                        guildInfo.guildMasterUserID = gm.userID;
                        guildInfo.guildMasterName = gm.playerName;
                        guildInfo.guildMemberCount = 1;
                        gm.guildInformationList.Add(guildInfo);
                    }
                }

                // デリゲートを呼び出す
                if (gm.delegateOnGotGuildInformation != null)
                {
                    gm.delegateOnGotGuildInformation(_originalList, true);
                }

                // デリゲートをデフォルトにリセット（元のGetGuildInformationと同じ動作）
                gm.initializeDelegate();

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] GetGuildInformation パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetGuildMember: ギルドメンバー取得
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetGuildMember")]
    public static class NCMBManager_GetGuildMember_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName, int _skip)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // ギルド情報にメンバーリストを設定
                foreach (var guildInfo in gm.guildInformationList)
                {
                    if (guildInfo.guildName == _guildName)
                    {
                        if (guildInfo.guildMemberUserIDList.Count == 0)
                        {
                            // 自分自身をメンバーとして追加
                            guildInfo.guildMemberUserIDList.Add(gm.userID);
                        }
                        break;
                    }
                }

                gm.ShowSystemMessage("ギルドメンバー取得成功");
                SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowGuildMemberList(_guildName);

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] GetGuildMember パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // LeaveGuild: ギルド脱退
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "LeaveGuild")]
    public static class NCMBManager_LeaveGuild_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _guildName)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                gm.ShowSystemMessage("ギルドを脱退しました");

                // userInformationListを更新
                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == _userID)
                    {
                        userInfo.guildName = "";
                    }
                }

                gm.myGuildName = "";
                gm.isGuildMaster = false;

                // ギルドカードを閉じる
                SingletonMonoBehaviour<MenuScreenManager>.Instance.CloseGuildCard();
                if ((bool)SingletonMonoBehaviour<GuildScreenManager>.Instance)
                {
                    SingletonMonoBehaviour<GuildScreenManager>.Instance.CloseRootMenu();
                }

                // マイカードを再表示
                List<string> list = new List<string> { _userID };
                gm.delegateOnGotUserInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowMyCardFront;
                gm.GetUserInformation(list);

                // フィールドキャラクターのギルド名をクリア
                if (gm.myPlayerObject != null)
                {
                    var fieldController = gm.myPlayerObject.GetComponent<FieldCharacterController>();
                    if (fieldController != null)
                    {
                        fieldController.myGuildName = "";
                    }
                }

                // ギルド情報リストからも削除
                gm.guildInformationList.RemoveAll(g => g.guildName == _guildName);

                // セーブデータに保存
                OfflineSaveDataManager.SaveAllData();

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] LeaveGuild パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // BanishGuildMember: ギルドメンバー追放
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "BanishGuildMember")]
    public static class NCMBManager_BanishGuildMember_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _guildName)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                gm.ShowSystemMessage("ギルドから追放しました");

                // userInformationListを更新
                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == _userID)
                    {
                        userInfo.guildName = "";
                    }
                }

                // ギルドメンバーリストから削除
                foreach (var guildInfo in gm.guildInformationList)
                {
                    if (guildInfo.guildName == _guildName)
                    {
                        guildInfo.guildMemberUserIDList.Remove(_userID);
                        guildInfo.guildMemberCount = Math.Max(1, guildInfo.guildMemberCount - 1);
                        break;
                    }
                }

                // ギルドカードを閉じてマイカードを再表示
                SingletonMonoBehaviour<MenuScreenManager>.Instance.CloseGuildCard();
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
                LogHelper.LogError($"[Guild] BanishGuildMember パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // JoinGuild: ギルド加入
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "JoinGuild")]
    public static class NCMBManager_JoinGuild_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _guildName)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                gm.ShowSystemMessage("ギルドに加入しました");
                gm.myGuildName = _guildName;
                gm.isGuildMaster = false;

                // userInformationListを更新
                foreach (var userInfo in gm.userInformationList)
                {
                    if (userInfo.userID == _userID)
                    {
                        userInfo.guildName = _guildName;
                    }
                }

                // ギルドメンバーリストに追加
                foreach (var guildInfo in gm.guildInformationList)
                {
                    if (guildInfo.guildName == _guildName)
                    {
                        if (!guildInfo.guildMemberUserIDList.Contains(_userID))
                        {
                            guildInfo.guildMemberUserIDList.Add(_userID);
                            guildInfo.guildMemberCount++;
                        }
                        break;
                    }
                }

                SingletonMonoBehaviour<MenuScreenManager>.Instance.CloseMyCardEditor();

                List<string> list = new List<string> { _userID };
                gm.delegateOnGotUserInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowMyCardFront;
                gm.GetUserInformation(list);

                if (gm.myPlayerObject != null)
                {
                    var fieldController = gm.myPlayerObject.GetComponent<FieldCharacterController>();
                    if (fieldController != null)
                    {
                        fieldController.myGuildName = _guildName;
                    }
                }

                gm.IncrementMissionAchievement(MissionData.enumMissionID.Once_JoinGuild, 1);

                OfflineSaveDataManager.SaveAllData();

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] JoinGuild パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // SearchGuild: ギルド検索（オフラインでは空結果を返す）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SearchGuild")]
    public static class NCMBManager_SearchGuild_Patch
    {
        static bool Prefix(NCMBManager __instance, int _skip, List<string> _list,
            string _policy, string _apploval, int _memberCountMin, int _memberCountMax)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("オフラインモードではギルド検索は利用できません");

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] SearchGuild パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // SaveGuildCardEditResult: ギルドカード編集結果保存
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SaveGuildCardEditResult")]
    public static class NCMBManager_SaveGuildCardEditResult_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName, string _policy,
            string _approval, string _comment)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                // ローカルのギルド情報を更新
                foreach (var guildInfo in gm.guildInformationList)
                {
                    if (guildInfo.guildName == _guildName)
                    {
                        guildInfo.guildPolicy = _policy;
                        guildInfo.guildApproval = _approval;
                        guildInfo.guildComment = _comment;
                        break;
                    }
                }

                gm.ShowSystemMessage("ギルド情報を更新しました");

                // エディタを閉じてギルドカードを再表示
                SingletonMonoBehaviour<MenuScreenManager>.Instance.CloseGuildCardEditor();
                List<string> list = new List<string> { _guildName };
                gm.delegateOnGotGuildInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowGuildCardFront;
                gm.GetGuildInformation(list);

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] SaveGuildCardEditResult パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // UpdateGuildInformation: ギルド情報更新
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "UpdateGuildInformation")]
    public static class NCMBManager_UpdateGuildInformation_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName, int _policy,
            int _approval, string _comment)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                foreach (var guildInfo in gm.guildInformationList)
                {
                    if (guildInfo.guildName == _guildName)
                    {
                        guildInfo.guildPolicy = _policy.ToString();
                        guildInfo.guildApproval = _approval.ToString();
                        guildInfo.guildComment = _comment;
                        break;
                    }
                }

                gm.ShowSystemMessage("ギルド[" + _guildName + "]\n情報を更新しました");

                List<string> list = new List<string> { _guildName };
                SingletonMonoBehaviour<MenuScreenManager>.Instance.myCardGuildName = _guildName;
                gm.delegateOnGotGuildInformation = SingletonMonoBehaviour<MenuScreenManager>.Instance.ShowGuildCardFront;
                gm.GetGuildInformation(list);

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] UpdateGuildInformation パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // UpdateGuildMemberCount: メンバー数更新（スキップ）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "UpdateGuildMemberCount", new Type[] { typeof(string) })]
    public static class NCMBManager_UpdateGuildMemberCount_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName)
        {
            if (!PhotonNetwork.offlineMode) return true;
            // オフラインではDB更新不要（ローカルデータは既に更新済み）
            return false;
        }
    }


    // ==========================================
    // UpdateGuildMemberCount2: メンバー数DB更新（スキップ）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "UpdateGuildMemberCount2")]
    public static class NCMBManager_UpdateGuildMemberCount2_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName, int _count)
        {
            if (!PhotonNetwork.offlineMode) return true;
            return false;
        }
    }


    // ==========================================
    // CheckGuildMemberCount: ギルド人数チェック（常に成功を返す）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "CheckGuildMemberCount")]
    public static class NCMBManager_CheckGuildMemberCount_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // オフラインでは常に加入可能
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] CheckGuildMemberCount パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // EraceGuild: ギルド削除（スキップ）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "EraceGuild")]
    public static class NCMBManager_EraceGuild_Patch
    {
        static bool Prefix(NCMBManager __instance, string _guildName)
        {
            if (!PhotonNetwork.offlineMode) return true;

            // ローカルのギルド情報を削除
            SingletonMonoBehaviour<GameManager>.Instance.guildInformationList
                .RemoveAll(g => g.guildName == _guildName);

            return false;
        }
    }


    // ==========================================
    // SendJoinRequest: ギルド加入申請（オフラインでは直接加入）
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "SendJoinRequest")]
    public static class NCMBManager_SendJoinRequest_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID, string _playerName,
            string _guildName, string _guildMasterUserID)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // オフラインでは承認プロセスをスキップして直接加入
                SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage(
                    "オフラインモードでは承認なしで加入します");

                SingletonMonoBehaviour<NCMBManager>.Instance.JoinGuild(_userID, _guildName);

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Guild] SendJoinRequest パッチエラー: {ex}");
                return true;
            }
        }
    }


}

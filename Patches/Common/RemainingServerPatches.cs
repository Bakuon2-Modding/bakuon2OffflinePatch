using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BakuonOfflinePatch
{
    // ======================================================
    // 層2: 未パッチNCMBManagerメソッドへの個別対応
    // ======================================================
    // NCMBSettings.Connection安全網があるため通信は絶対に発生しないが、
    // isBusyリセットとUXのために個別パッチを追加する。

    // 共通ヘルパー
    internal static class NCMBSkipHelper
    {
        // FieldInfo/MethodInfo を静的キャッシュ → 毎回のリフレクション検索をゼロに
        private static readonly FieldInfo s_isBusyField =
            typeof(NCMBManager).GetField("isBusy",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly MethodInfo s_runDelegateMethod =
            typeof(NCMBManager).GetMethod("RunDelegateOnFinishedNetworkProcess",
                BindingFlags.Public | BindingFlags.Instance);

        // Invoke の引数配列もキャッシュ（true/false の2値で使い回す）
        private static readonly object[] s_argsTrue  = new object[] { true };
        private static readonly object[] s_argsFalse = new object[] { false };

        public static void ResetBusy(NCMBManager instance)
        {
            try { s_isBusyField?.SetValue(instance, false); }
            catch { }
        }

        public static void CallDelegate(NCMBManager instance, bool result)
        {
            try { s_runDelegateMethod?.Invoke(instance, result ? s_argsTrue : s_argsFalse); }
            catch { }
        }

        public static void SkipSuccess(NCMBManager instance)
        {
            ResetBusy(instance);
            CallDelegate(instance, true);
        }
    }

    // ======================================================
    // ソーシャル機能 (フォロー・ブロック)
    // ======================================================

    // FollowPlayer - ローカルのfollowListを更新してメッセージ表示
    [HarmonyPatch(typeof(NCMBManager), "FollowPlayer")]
    public static class NCMBManager_FollowPlayer_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID)
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm != null)
                {
                    if (!gm.followList.Contains(_userID))
                        gm.followList.Add(_userID);
                    gm.ShowSystemMessage("フォローしました！");
                    gm.IncrementMissionAchievement(MissionData.enumMissionID.Once_FollowAnotherPlayer, 1);
                }
            }
            catch (Exception ex) { LogHelper.LogError($"[FollowPlayer] {ex}"); }
            finally { NCMBSkipHelper.ResetBusy(__instance); }
            return false;
        }
    }

    // FollowReleasePlayer - ローカルのfollowListから削除してメッセージ表示
    [HarmonyPatch(typeof(NCMBManager), "FollowReleasePlayer")]
    public static class NCMBManager_FollowReleasePlayer_Patch
    {
        static bool Prefix(NCMBManager __instance, string _userID)
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm != null)
                {
                    gm.followList.Remove(_userID);
                    gm.ShowSystemMessage("フォローを解除しました！");
                }
            }
            catch (Exception ex) { LogHelper.LogError($"[FollowReleasePlayer] {ex}"); }
            finally { NCMBSkipHelper.ResetBusy(__instance); }
            return false;
        }
    }

    // BlockPlayer - ブロック登録（ローカルメッセージのみ）
    [HarmonyPatch(typeof(NCMBManager), "BlockPlayer")]
    public static class NCMBManager_BlockPlayer_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try { SingletonMonoBehaviour<GameManager>.Instance?.ShowSystemMessage("ブロックしました"); }
            catch { }
            finally { NCMBSkipHelper.ResetBusy(__instance); }
            return false;
        }
    }

    // BlockReleasePlayer - ブロック解除（ローカルメッセージのみ）
    [HarmonyPatch(typeof(NCMBManager), "BlockReleasePlayer")]
    public static class NCMBManager_BlockReleasePlayer_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try { SingletonMonoBehaviour<GameManager>.Instance?.ShowSystemMessage("ブロックを解除しました"); }
            catch { }
            finally { NCMBSkipHelper.ResetBusy(__instance); }
            return false;
        }
    }

    // AddGiveGoodUser - いいね送信（オフラインでは処理なし）
    [HarmonyPatch(typeof(NCMBManager), "AddGiveGoodUser")]
    public static class NCMBManager_AddGiveGoodUser_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    // ======================================================
    // メッセージ機能
    // ======================================================

    // SendUserMessage - オフラインのため送信不可を通知
    [HarmonyPatch(typeof(NCMBManager), "SendUserMessage")]
    public static class NCMBManager_SendUserMessage_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                SingletonMonoBehaviour<GameManager>.Instance?.ShowSystemMessage(
                    "オフラインモードのためメッセージ送信はできません");
            }
            catch { }
            finally
            {
                NCMBSkipHelper.ResetBusy(__instance);
                NCMBSkipHelper.CallDelegate(__instance, false);
            }
            return false;
        }
    }

    // MarkAsReadUserMessage - 既読処理（受信ボックス）
    [HarmonyPatch(typeof(NCMBManager), "MarkAsReadUserMessage")]
    public static class NCMBManager_MarkAsReadUserMessage_Patch
    {
        static bool Prefix(NCMBManager __instance, string _objectID, string _userID,
            RecieveMessage _recieveMessage)
        {
            try
            {
                // ローカルで既読フラグを更新
                if (_recieveMessage != null && !_recieveMessage.readUserList.Contains(_userID))
                    _recieveMessage.readUserList.Add(_userID);

                SingletonMonoBehaviour<GameManager>.Instance?.ShowSystemMessage("メッセージを既読にしました");
                SingletonMonoBehaviour<RecieveBoxScreenManager>.Instance?.ShowRecieveMessage();
            }
            catch (Exception ex) { LogHelper.LogError($"[MarkAsReadUserMessage] {ex}"); }
            finally { NCMBSkipHelper.ResetBusy(__instance); }
            return false;
        }
    }

    // InVisibleUserMessage - メッセージ非表示
    [HarmonyPatch(typeof(NCMBManager), "InVisibleUserMessage")]
    public static class NCMBManager_InVisibleUserMessage_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    // ======================================================
    // ランキング機能
    // ======================================================

    // FetchRank - ランキング取得（オフラインでは1位として扱う）
    [HarmonyPatch(typeof(NCMBManager), "FetchRank")]
    public static class NCMBManager_FetchRank_Patch
    {
        static bool Prefix()
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm != null) gm.currentRank = 1;
                // FetchTopRankersを呼ぶ（こちらもパッチ済み）
                SingletonMonoBehaviour<NCMBManager>.Instance?.FetchTopRankers();
            }
            catch (Exception ex) { LogHelper.LogError($"[FetchRank] {ex}"); }
            return false;
        }
    }

    // FetchTopRankers - トップランカー取得（オフラインでは空リスト）
    [HarmonyPatch(typeof(NCMBManager), "FetchTopRankers")]
    public static class NCMBManager_FetchTopRankers_Patch
    {
        static bool Prefix()
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm != null) gm.rankerUserInformationList.Clear();
                // FetchNeighborsを呼ぶ（こちらもパッチ済み）
                SingletonMonoBehaviour<NCMBManager>.Instance?.FetchNeighbors();
            }
            catch (Exception ex) { LogHelper.LogError($"[FetchTopRankers] {ex}"); }
            return false;
        }
    }

    // FetchNeighbors - 近傍ランカー取得（オフラインでは空リストでUI更新）
    [HarmonyPatch(typeof(NCMBManager), "FetchNeighbors")]
    public static class NCMBManager_FetchNeighbors_Patch
    {
        static bool Prefix()
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm != null) gm.neigherRankerUserInformationList.Clear();
                SingletonMonoBehaviour<RankingScreenManager>.Instance?.ShowRanking();
            }
            catch (Exception ex) { LogHelper.LogError($"[FetchNeighbors] {ex}"); }
            return false;
        }
    }

    // ======================================================
    // 防衛ゲームスコア
    // ======================================================

    // GetDefenseGameScore - スコア取得（オフラインでは空、安全のみ確保）
    [HarmonyPatch(typeof(NCMBManager), "GetDefenseGameScore")]
    public static class NCMBManager_GetDefenseGameScore_Patch
    {
        static bool Prefix()
        {
            return false; // スコアリストは空のまま
        }
    }

    // SaveDefenseGameScore - スコア保存（オフラインではスキップ）
    [HarmonyPatch(typeof(NCMBManager), "SaveDefenseGameScore")]
    public static class NCMBManager_SaveDefenseGameScore_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    // ======================================================
    // ギルド追加操作 (AgreeJoinRequest)
    // ======================================================

    // AgreeJoinRequest - ギルド加入承認（ローカルでJoinGuild相当の処理）
    [HarmonyPatch(typeof(NCMBManager), "AgreeJoinRequest")]
    public static class NCMBManager_AgreeJoinRequest_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            // オフラインではギルド加入申請がないため処理なし
            NCMBSkipHelper.SkipSuccess(__instance);
            return false;
        }
    }

    // AgreeJoinRequest2 - ギルド加入承認2
    [HarmonyPatch(typeof(NCMBManager), "AgreeJoinRequest2")]
    public static class NCMBManager_AgreeJoinRequest2_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.SkipSuccess(__instance);
            return false;
        }
    }

    // ======================================================
    // マイホーム・マップ編集
    // ======================================================

    // SaveEditMapData - マップ編集データ（メッセージ表示、セーブ済みフラグを更新）
    [HarmonyPatch(typeof(NCMBManager), "SaveEditMapData")]
    public static class NCMBManager_SaveEditMapData_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            try
            {
                SingletonMonoBehaviour<GameManager>.Instance?.ShowSystemMessage("マップデータをセーブしました");
                try
                {
                    var editor = SingletonMonoBehaviour<RiumMapEditorManager>.Instance;
                    if (editor != null) editor.isEdited = false;
                }
                catch { }
            }
            catch (Exception ex) { LogHelper.LogError($"[SaveEditMapData] {ex}"); }
            finally { NCMBSkipHelper.ResetBusy(__instance); }
            return false;
        }
    }

    // GetAnotherMyHomeMapEditData - 他プレイヤーの家データ取得（オフライン不可）
    [HarmonyPatch(typeof(NCMBManager), "GetAnotherMyHomeMapEditData")]
    public static class NCMBManager_GetAnotherMyHomeMapEditData_Patch
    {
        static bool Prefix()
        {
            SingletonMonoBehaviour<GameManager>.Instance?.ShowSystemMessage(
                "オフラインモードのため他プレイヤーの家データは取得できません");
            return false;
        }
    }

    // ======================================================
    // 国家変更
    // ======================================================

    // ChangeCountry - ローカルで国家・ギルド・コインを更新してマイホームへ
    [HarmonyPatch(typeof(NCMBManager), "ChangeCountry")]
    public static class NCMBManager_ChangeCountry_Patch
    {
        static bool Prefix(int _countryID, int _price)
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm != null)
                {
                    gm.myCountry = _countryID;
                    gm.myGuildName = "";
                    gm.isGuildMaster = false;
                    gm.SetMyCoin(gm.myCoin - _price);
                    OfflineSaveDataManager.SaveAllData();
                    SingletonMonoBehaviour<AudioManager>.Instance?.PlaySE("casher");
                    gm.ShowSystemMessage("国家移住に成功しました");
                    SingletonMonoBehaviour<PUNController>.Instance?.StartJoinMyHome();
                }
            }
            catch (Exception ex) { LogHelper.LogError($"[ChangeCountry] {ex}"); }
            return false;
        }
    }

    // ======================================================
    // アクセサリデータ取得
    // ======================================================

    // GetUserAccessoryData - ローカルセーブからアクセサリ再ロード
    [HarmonyPatch(typeof(NCMBManager), "GetUserAccessoryData")]
    public static class NCMBManager_GetUserAccessoryData_Patch
    {
        static bool Prefix()
        {
            try
            {
                // アクセサリプレハブを再初期化（セーブデータからLoadAllDataで復元済みのはず）
                SingletonMonoBehaviour<GameManager>.Instance?.CreateStockAccessoryPrefab();
            }
            catch (Exception ex) { LogHelper.LogError($"[GetUserAccessoryData] {ex}"); }
            return false;
        }
    }

    // ======================================================
    // キックプレイヤー管理（管理者機能・スキップ）
    // ======================================================

    [HarmonyPatch(typeof(NCMBManager), "GetGlobalKickedPlayer")]
    public static class NCMBManager_GetGlobalKickedPlayer_Patch
    {
        static bool Prefix() { return false; }
    }

    [HarmonyPatch(typeof(NCMBManager), "RegistrationKickedPlayer")]
    public static class NCMBManager_RegistrationKickedPlayer_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    // ======================================================
    // システム情報・汎用削除（スキップ）
    // ======================================================

    [HarmonyPatch(typeof(NCMBManager), "SaveSystemInfo")]
    public static class NCMBManager_SaveSystemInfo_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "DeleteNCMBObject")]
    public static class NCMBManager_DeleteNCMBObject_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    // ======================================================
    // プレゼント・景品機能（サーバーイベント・スキップ）
    // ======================================================

    [HarmonyPatch(typeof(NCMBManager), "SaveWinnerGoodsPresent")]
    public static class NCMBManager_SaveWinnerGoodsPresent_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "GetGoodPresentWinnerList")]
    public static class NCMBManager_GetGoodPresentWinnerList_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.SkipSuccess(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "GetAttractionGoodPresentWinnerList")]
    public static class NCMBManager_GetAttractionGoodPresentWinnerList_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.SkipSuccess(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "GetAttractionGoodPresentUserIDList")]
    public static class NCMBManager_GetAttractionGoodPresentUserIDList_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.SkipSuccess(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "AddAttractionGoodPresentJoinedUser")]
    public static class NCMBManager_AddAttractionGoodPresentJoinedUser_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "SetAttractionGoodPresentWinnerUserID")]
    public static class NCMBManager_SetAttractionGoodPresentWinnerUserID_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "AddGoodsPresentRecord")]
    public static class NCMBManager_AddGoodsPresentRecord_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(NCMBManager), "GoodsPresentRecordCopyPresentID")]
    public static class NCMBManager_GoodsPresentRecordCopyPresentID_Patch
    {
        static bool Prefix(NCMBManager __instance)
        {
            NCMBSkipHelper.ResetBusy(__instance);
            return false;
        }
    }
}

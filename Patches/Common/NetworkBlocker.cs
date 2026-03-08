using HarmonyLib;
using NCMB;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace BakuonOfflinePatch
{
    // ======================================================
    // 層1 安全網: 全NCMB通信を確実にブロック
    // ======================================================
    // 個別パッチで漏れた場合の最終防衛ライン。
    // NCMBSettings.Connection は全NCMB通信が通過する唯一の経路。
    // コールバックをエラーで呼び出してisBusy等をリセットする。
    [HarmonyPatch]
    public static class NCMBSettings_Connection_Patch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("NCMB.NCMBSettings");
            if (type == null)
            {
                LogHelper.LogError("[NetworkBlocker] NCMBSettings型が見つかりません");
                return null;
            }
            return AccessTools.Method(type, "Connection");
        }

        static bool Prefix(object connection, object callback)
        {
            // URLをログ出力（どのコードが通信しようとしたか記録）
            string url = "(不明)";
            try
            {
                var reqField = connection?.GetType().GetField(
                    "_request", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                var req = reqField?.GetValue(connection);
                if (req != null)
                {
                    var urlProp = req.GetType().GetProperty("url");
                    url = urlProp?.GetValue(req, null)?.ToString() ?? "(不明)";
                }
            }
            catch { }

            LogHelper.LogWarning($"[NetworkBlocker] NCMB通信をブロック: {url}");

            // コールバックをエラーで呼び出してisBusy等をリセット
            if (callback is Delegate del)
            {
                try
                {
                    var ncmbEx = new NCMBException();
                    ncmbEx.ErrorCode = "E_OFFLINE";
                    ncmbEx.ErrorMessage = "オフラインモードのため通信不可";

                    var paramTypes = del.Method.GetParameters();
                    if (paramTypes.Length == 3 && paramTypes[0].ParameterType == typeof(int))
                    {
                        if (paramTypes[1].ParameterType == typeof(string))
                            del.DynamicInvoke(0, "{}", ncmbEx);        // HttpClientCallback
                        else
                            del.DynamicInvoke(0, new byte[0], ncmbEx); // HttpClientFileDataCallback
                    }
                    else if (paramTypes.Length == 2)
                    {
                        del.DynamicInvoke(new byte[0], ncmbEx); // NCMBExecuteScriptCallback
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.LogWarning($"[NetworkBlocker] コールバック呼び出しエラー: {ex.Message}");
                }
            }

            return false; // StartCoroutine(SendRequest) を呼ばせない
        }
    }

    // ======================================================
    // お知らせ画像のHTTP通信をブロック
    // ======================================================

    // NoticeScreenManager.ShowNotice → http://bakuon.xsrv.jp/ へのWWW通信をスキップ
    [HarmonyPatch(typeof(NoticeScreenManager), "ShowNotice")]
    public static class NoticeScreenManager_ShowNotice_Patch
    {
        static bool Prefix()
        {
            return false; // 画像ダウンロードコルーチンを開始しない
        }
    }

    // NpcNoticeController.ShowNotice → http://bakuon.xsrv.jp/ へのWWW通信をスキップ
    [HarmonyPatch(typeof(NpcNoticeController), "ShowNotice")]
    public static class NpcNoticeController_ShowNotice_Patch
    {
        static bool Prefix(NpcNoticeController __instance)
        {
            // ウィンドウ自体は表示するが、画像ダウンロードはしない
            try
            {
                var mainWindowField = __instance.GetType().GetField(
                    "mainWindow", BindingFlags.Public | BindingFlags.Instance);
                var mainWindow = mainWindowField?.GetValue(__instance) as GameObject;
                mainWindow?.SetActive(true);
            }
            catch { }
            return false;
        }
    }

    // ======================================================
    // ランキング画面・マッチングのPhotonChat通信をブロック
    // ======================================================

    // RankingScreenManager.OutputRankingResult → PhotonChat.AddFriends/RemoveFriends をスキップ
    [HarmonyPatch(typeof(RankingScreenManager), "OutputRankingResult")]
    public static class RankingScreenManager_OutputRankingResult_Patch
    {
        static bool Prefix()
        {
            return false;
        }
    }

    // MatchingRoomController.SendRoomInformation → オフライン時はチャットチャンネルへの送信をスキップ
    [HarmonyPatch(typeof(MatchingRoomController), "SendRoomInformation")]
    public static class MatchingRoomController_SendRoomInformation_Patch
    {
        static bool Prefix()
        {
            if (OnlineMode.IsActive) return true; // オンライン時は元のメソッドを実行
            return false;
        }
    }

    // ======================================================
    // NCMBManager.ShowErrorAlart → オフライン時はエラーダイアログを抑制
    // ======================================================
    // 安全網がエラーコールバックを呼び出すと、内部処理を経てShowErrorAlartが呼ばれる。
    // オフラインモードではすべてのNCMBエラーは想定内なので、ダイアログを表示せずログのみ記録する。
    [HarmonyPatch(typeof(NCMBManager), "ShowErrorAlart")]
    public static class NCMBManager_ShowErrorAlart_Patch
    {
        static bool Prefix(object e)
        {
            if (!PhotonNetwork.offlineMode) return true;

            try
            {
                string code = "(不明)";
                string msg = "(不明)";
                if (e != null)
                {
                    var codeField = e.GetType().GetProperty("ErrorCode");
                    var msgField = e.GetType().GetProperty("ErrorMessage");
                    code = codeField?.GetValue(e, null)?.ToString() ?? code;
                    msg = msgField?.GetValue(e, null)?.ToString() ?? msg;
                }
                LogHelper.LogWarning($"[NetworkBlocker] ShowErrorAlartを抑制: {code} / {msg}");
            }
            catch { }

            return false; // ダイアログを表示しない
        }
    }

    // ======================================================
    // GameManager.GetTime → http://ntp.nict.jp へのWWW通信をブロック
    // ======================================================
    // NCMBSettings.Connectionを経由しない直接WWW通信のため、個別にパッチが必要。
    // NTPDateTimeはGetUserDataパッチでDateTime.Nowに設定済みのため機能的に問題なし。
    [HarmonyPatch(typeof(GameManager), "GetTime")]
    public static class GameManager_GetTime_Patch
    {
        static bool Prefix(ref IEnumerator __result, GameManager __instance)
        {
            __result = LocalGetTime(__instance);
            return false;
        }

        static IEnumerator LocalGetTime(GameManager instance)
        {
            // サーバーへのアクセスなしにローカル時刻を設定
            instance.NTPDateTime = DateTime.Now;
            yield break;
        }
    }
}

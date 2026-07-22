using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BakuonOfflinePatch
{
    // ==========================================
    // NCMBNTPDate.RunDateScript: サーバー時刻取得をローカル時刻に置換
    // ==========================================
    // 元のコードはNCMBサーバーの date.js スクリプトで現在時刻を取得し、
    // コールバック内で各種処理（アクセサリ屋の商品表示、ミッション更新等）を実行する。
    // オフラインではサーバーが存在しないため、ローカル時刻を使用して同等の処理を直接実行する。
    [HarmonyPatch(typeof(NCMBNTPDate), "RunDateScript")]
    public static class NCMBNTPDate_RunDateScript_Patch
    {
        static bool Prefix(NCMBNTPDate __instance,
            NCMBNTPDate.InvokeMethodType _invokeMethodType,
            MissionData.enumMissionID _missionID,
            int incrementValue,
            NpcShopController _npcShopController)
        {
            if (!PhotonNetwork.offlineMode && !OnlineMode.IsActive) return true;

            try
            {
                // サーバー時刻の代わりにローカル時刻を使用
                SingletonMonoBehaviour<GameManager>.Instance.NTPDateTime = DateTime.Now;

                switch (_invokeMethodType)
                {
                    case NCMBNTPDate.InvokeMethodType.ShowSellAccessory:
                        if (_npcShopController != null)
                        {
                            _npcShopController.coroutine = __instance.StartCoroutine(
                                _npcShopController.CreateAccessoryShopIconButtonPrefabCoroutine());
                        }
                        break;

                    case NCMBNTPDate.InvokeMethodType.IncrementMissionAchievement:
                        SingletonMonoBehaviour<GameManager>.Instance.IncrementMissionAchievementInvoked(
                            _missionID, incrementValue);
                        break;

                    case NCMBNTPDate.InvokeMethodType.OpenMissonRootMenu:
                        SingletonMonoBehaviour<MissionScreenManager>.Instance.SelectTabButton(
                            SingletonMonoBehaviour<MissionScreenManager>.Instance.dairyTabButton);
                        break;

                    case NCMBNTPDate.InvokeMethodType.UpdateMissionData:
                        SingletonMonoBehaviour<NCMBManager>.Instance.SetDelegateOnFinishedNetworkProcess(
                            SingletonMonoBehaviour<MissionScreenManager>.Instance.OnFinishedNetworkProcess_SaveMisisonData);
                        SingletonMonoBehaviour<NCMBManager>.Instance.SaveMissionData();
                        break;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[RunDateScript] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // NCMBManager.GetFieldTreasure_Coin: リング（通貨）ピックアップのオフライン対応
    // ==========================================
    // 元のコードはNCMBサーバーの UserData テーブルに Coin をインクリメント保存し、
    // 成功コールバックで GotFieldTreasure を呼んでローカルの所持金を加算する。
    // オフラインではサーバーが存在しないため、直接 GotFieldTreasure を呼び出す。
    [HarmonyPatch(typeof(NCMBManager), "GetFieldTreasure_Coin")]
    public static class NCMBManager_GetFieldTreasure_Coin_Patch
    {
        static bool Prefix(GameObject _gameObject, string _userID, int _value)
        {
            if (!PhotonNetwork.offlineMode && !OnlineMode.IsActive) return true;

            try
            {
                if (_gameObject == null) return false;

                // FieldTreasureController（非ネットワーク版）
                var ftc = _gameObject.GetComponent<FieldTreasureController>();
                if (ftc != null)
                {
                    ftc.GotFieldTreasure(_userID);
                    ftc.DestoryMine();
                    return false;
                }

                // FieldTreasureController_Network（ネットワーク版）
                var ftcNet = _gameObject.GetComponent<FieldTreasureController_Network>();
                if (ftcNet != null)
                {
                    // コインは所有者(通常はMasterClient)のクライアントでしか OnTriggerEnter が
                    // 発火しないため、マルチプレイ中は原作同様 RPC で全クライアントへ通知する。
                    // ローカル直接呼び出しだと取得者が別クライアントの場合に加算・SEが届かない。
                    if (OnlineMode.IsActive && !PhotonNetwork.offlineMode && PhotonNetwork.inRoom)
                    {
                        ftcNet.myPhotonView.RPC("GotFieldTreasure", PhotonTargets.All, _userID);
                    }
                    else
                    {
                        ftcNet.GotFieldTreasure(_userID);
                    }
                    ftcNet.DestoryMine();
                    return false;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetFieldTreasure_Coin] パッチエラー: {ex}");
                return true;
            }
        }
    }
}

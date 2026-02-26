using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace BakuonOfflinePatch
{
    // ==========================================
    // 動画再生の絵馬（動画の石版）: 動画再生をスキップ
    // ==========================================
    // ニコニコ動画へのネットワークアクセスを防止するため、
    // 動画再生と動画情報取得を無効化する

    // 動画再生本体をスキップ
    [HarmonyPatch(typeof(NiconicoScreenSystem), "StartPlayVideo")]
    public static class NiconicoScreenSystem_StartPlayVideo_Patch
    {
        static bool Prefix()
        {
            SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("オフラインモードでは動画再生は無効です");
            return false;
        }
    }

    // 動画ID入力時のメタデータ取得をスキップ
    [HarmonyPatch(typeof(CommonItemInformationWindowController), "YoutuveVideoIDEndEdit")]
    public static class CommonItemInformationWindowController_YoutuveVideoIDEndEdit_Patch
    {
        static bool Prefix()
        {
            SingletonMonoBehaviour<GameManager>.Instance.ShowSystemMessage("オフラインモードでは動画再生は無効です");
            return false;
        }
    }
}

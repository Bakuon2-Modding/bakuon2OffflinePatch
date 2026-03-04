using HarmonyLib;
using System;
using MBakuon;
using UnityEngine;
using UnityEngine.UI;

namespace BakuonOfflinePatch
{
    // ==========================================
    // SuteageIsekiController.RenewInformation パッチ
    // ==========================================
    //
    // 修正1 (レーダーチャート): 元メソッドは Volumes[] 変更後に SetVerticesDirty() を呼ばないため
    //   Canvas が再描画されない。Finalizer で明示的に SetVerticesDirty() を呼ぶことで修正。
    //
    // 修正2 (アイテムスロット): 元メソッドが途中で例外終了した場合も
    //   quickItemSlotButtonList / itemSlotButtonList の color を確実に補正する。

    [HarmonyPatch(typeof(SuteageIsekiController), "RenewInformation")]
    public static class SuteageIsekiController_RenewInformation_Patch
    {
        static Exception Finalizer(SuteageIsekiController __instance, Exception __exception)
        {
            if (__exception != null)
                LogHelper.LogWarning($"[SuteagePatch] RenewInformation 例外: {__exception.GetType().Name}: {__exception.Message}\n{__exception.StackTrace}");

            if (__instance == null || __instance.playerData == null)
                return null;

            // ① レーダーチャート補正
            try
            {
                var poly = __instance.raderChartPolygon;
                if (poly != null)
                {
                    if (poly.Volumes == null || poly.Volumes.Length < 6)
                        System.Array.Resize(ref poly.Volumes, 6);

                    poly.Volumes[5] = (float)__instance.playerData.atk_SkillA / 300f;
                    poly.Volumes[0] = (float)__instance.playerData.atk_SkillB / 300f;
                    poly.Volumes[1] = (float)__instance.playerData.atk_SkillC / 300f;
                    poly.Volumes[4] = (float)__instance.playerData.def_ShortRange / 70f;
                    poly.Volumes[2] = (float)__instance.playerData.def_OutRange / 70f;
                    poly.Volumes[3] = (float)__instance.playerData.ap_append * 10f / 700f;
                    poly.SetVerticesDirty();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SuteagePatch] レーダーチャート補正エラー: {ex.Message}");
            }

            // ② クイックアイテムスロット + メニュー内アイテムスロット補正
            try
            {
                FixItemSlots(__instance, 0);
                FixItemSlots(__instance, 1);
                FixItemSlots(__instance, 2);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SuteagePatch] アイテムスロット補正エラー: {ex.Message}");
            }

            return null;
        }

        private static void FixItemSlots(SuteageIsekiController controller, int index)
        {
            var item = controller.playerData.stockSuteageItemDataList[index];
            Texture2D tex = null;
            if (item != null)
                tex = AssetBundleManager.GetAsset<Texture2D>("sprite", item.property.iconFileName);

            // quickItemSlotButtonList（HUD）
            if (index < controller.quickItemSlotButtonList.Count)
            {
                var slotGO = controller.quickItemSlotButtonList[index];
                if (slotGO != null)
                {
                    var imgT = slotGO.transform.Find("Image");
                    if (imgT != null)
                    {
                        var img = imgT.GetComponent<Image>();
                        if (img != null)
                        {
                            if (item != null)
                            {
                                if (tex != null)
                                    img.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), Vector2.zero);
                                img.color = new Color(1f, 1f, 1f, 1f);
                            }
                            else
                            {
                                img.color = new Color(0f, 0f, 0f, 0f);
                            }
                        }
                    }
                }
            }

            // itemSlotButtonList（メニュー内スロット）
            if (index < controller.itemSlotButtonList.Count)
            {
                var slotGO = controller.itemSlotButtonList[index];
                if (slotGO != null)
                {
                    var imgT = slotGO.transform.Find("Image");
                    if (imgT != null)
                    {
                        var img = imgT.GetComponent<Image>();
                        if (img != null)
                        {
                            if (item != null)
                            {
                                if (tex != null)
                                    img.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), Vector2.zero);
                                img.color = new Color(1f, 1f, 1f, 1f);
                            }
                            else
                            {
                                img.color = new Color(0f, 0f, 0f, 0f);
                            }
                        }
                    }
                }
            }
        }
    }
}

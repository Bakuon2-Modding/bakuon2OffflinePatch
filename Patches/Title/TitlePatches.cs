using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BakuonOfflinePatch
{
    // ==========================================
    // タイトル画面パッチ
    // ==========================================
    // TitleSceneManagerの処理をオフライン用にカスタマイズ

    [HarmonyPatch(typeof(TitleSceneManager), "Start")]
    public static class TitleSceneManager_Start_Patch
    {
        static void Postfix(TitleSceneManager __instance)
        {
            try
            {
                // GameManagerの設定をオフライン用に調整
                if (SingletonMonoBehaviour<GameManager>.Instance != null)
                {
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;

                    // サーバーモード・ボットモードを強制的に無効化
                    gm.isServerMode = false;
                    gm.isBotMode = false;
                }

                // 「戻る」ボタンを非表示にする（OAuth画面への戻りボタンはオフラインでは不要）
                HideBackButton(__instance);

                // 名前入力フィールドをセットアップ
                SetupNameInput(__instance);

                // バージョン表示
                ShowVersionLabel(__instance);

                // ダウンロードデータが見つからない場合、警告を表示
                if (LocalAssetBundleLoader.IsBundleNotFound)
                {
                    ShowBundleWarning(__instance);
                }
            }
            catch (Exception ex)
            {
                OfflinePatchPlugin.Logger.LogError($"TitleSceneManager.Start パッチエラー: {ex}");
            }
        }

        /// <summary>
        /// userIDInputField を名前入力用に転用
        /// </summary>
        private static void SetupNameInput(TitleSceneManager instance)
        {
            try
            {
                var inputField = instance.userIDInputField;
                if (inputField == null) return;

                // 入力を有効化
                inputField.interactable = true;

                // セーブデータからプレイヤー名を読み込み
                string savedName = OfflineSaveDataManager.LoadPlayerName();
                if (!string.IsNullOrEmpty(savedName))
                {
                    inputField.text = savedName;
                }
                else
                {
                    inputField.text = "";
                }

                // プレースホルダーのテキストを変更
                var placeholder = inputField.placeholder;
                if (placeholder != null)
                {
                    var tmpPlaceholder = placeholder.GetComponent<TextMeshProUGUI>();
                    if (tmpPlaceholder != null)
                    {
                        tmpPlaceholder.text = "名前を入力...";
                    }
                }

                // 文字数制限
                inputField.characterLimit = 10;

                // 「ユーザーID」ラベルを「プレイヤー名」に変更
                var parentTransform = inputField.transform.parent;
                if (parentTransform != null)
                {
                    var labels = parentTransform.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var label in labels)
                    {
                        if (label.text.Contains("ユーザ") && label.text.Contains("ID"))
                        {
                            label.text = "プレイヤー名";
                            break;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Title] 名前入力セットアップエラー: {ex}");
            }
        }

        private static void HideBackButton(TitleSceneManager instance)
        {
            try
            {
                var allButtons = UnityEngine.Object.FindObjectsOfType<Button>();
                foreach (var button in allButtons)
                {
                    string btnName = button.gameObject.name;

                    // テキストに「戻る」を含むボタンを非表示
                    foreach (var t in button.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (t.text.Contains("戻る"))
                        {
                            button.gameObject.SetActive(false);
                                    return;
                        }
                    }

                    // ボタン名に Back/戻 を含む場合も非表示
                    if (btnName.IndexOf("back", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        btnName.Contains("戻"))
                    {
                        button.gameObject.SetActive(false);
                            return;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Title] 戻るボタン非表示エラー: {ex}");
            }
        }

        private static void ShowVersionLabel(TitleSceneManager instance)
        {
            try
            {
                GameObject canvasObj = instance.canvas;
                if (canvasObj == null) return;

                // 既存のTMPコンポーネントからフォントアセットを取得
                var existingTmp = canvasObj.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existingTmp == null) return;

                var versionObj = new GameObject("OfflinePatch_VersionLabel");
                versionObj.transform.SetParent(canvasObj.transform, false);

                var tmpText = versionObj.AddComponent<TextMeshProUGUI>();
                tmpText.font = existingTmp.font;
                tmpText.text = $"Offline Patch v{PluginInfo.PLUGIN_VERSION}";
                tmpText.fontSize = 18;
                tmpText.alignment = TextAlignmentOptions.BottomRight;
                tmpText.color = new Color(1f, 1f, 1f, 0.5f);
                tmpText.enableWordWrapping = false;

                // 右下に配置
                var rectTransform = versionObj.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(1f, 0f);
                rectTransform.anchorMax = new Vector2(1f, 0f);
                rectTransform.pivot = new Vector2(1f, 0f);
                rectTransform.anchoredPosition = new Vector2(-15f, 10f);
                rectTransform.sizeDelta = new Vector2(300f, 30f);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Title] バージョン表示エラー: {ex}");
            }
        }

        private static void ShowBundleWarning(TitleSceneManager instance)
        {
            try
            {
                GameObject canvasObj = instance.canvas;
                if (canvasObj == null)
                {
                    LogHelper.LogWarning("[Title] Canvas が見つかりません。警告テキストを表示できません。");
                    return;
                }

                // 既存のTMPコンポーネントからフォントアセットを取得
                var existingTmp = canvasObj.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existingTmp == null) return;

                var warningObj = new GameObject("OfflinePatch_BundleWarning");
                warningObj.transform.SetParent(canvasObj.transform, false);

                var tmpText = warningObj.AddComponent<TextMeshProUGUI>();
                tmpText.font = existingTmp.font;
                tmpText.text = "<color=yellow>ダウンロードデータが見つかりません</color>\n" +
                               "<size=70%>3Dモデルや床データ等が表示されない場合があります</size>";
                tmpText.fontSize = 24;
                tmpText.alignment = TextAlignmentOptions.Bottom;
                tmpText.overflowMode = TextOverflowModes.Overflow;
                tmpText.enableWordWrapping = false;
                tmpText.richText = true;

                // 下部中央に配置
                var rectTransform = warningObj.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0.5f, 0f);
                rectTransform.anchorMax = new Vector2(0.5f, 0f);
                rectTransform.pivot = new Vector2(0.5f, 0f);
                rectTransform.anchoredPosition = new Vector2(0f, 40f);
                rectTransform.sizeDelta = new Vector2(600f, 70f);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Title] 警告テキスト表示エラー: {ex}");
            }
        }
    }

    // ==========================================
    // GameStart パッチ: NGNameチェック後、名前をセーブデータに保存してからログイン
    // ==========================================
    [HarmonyPatch(typeof(TitleSceneManager), "GameStart")]
    public static class TitleSceneManager_GameStart_Patch
    {
        static bool Prefix(TitleSceneManager __instance)
        {
            try
            {
                // 入力フィールドの名前をプレイヤー名として使用
                string inputName = __instance.userIDInputField.text;
                if (string.IsNullOrEmpty(inputName))
                {
                    inputName = "OfflinePlayer";
                }

                // CheckNGNameはnameInputFieldを参照するため、userIDInputFieldの値を同期
                __instance.nameInputField.text = inputName;

                // 元のNGNameチェックを呼び出し（TitleSceneManager.CheckNGName）
                if (!__instance.CheckNGName())
                {
                    return false; // NGネームの場合はGameStartを中断
                }

                // GameManager に名前を設定
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                gm.playerName = inputName;
                gm.userID = "@Offline_" + inputName;

                // Photon プレイヤー名も設定
                PhotonNetwork.playerName = inputName;

                // セーブデータにプレイヤー名を保存
                OfflineSaveDataManager.SavePlayerName(inputName);

                return true; // 元のGameStartを実行
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Title] GameStart Prefix エラー: {ex}");
                return true;
            }
        }
    }
}

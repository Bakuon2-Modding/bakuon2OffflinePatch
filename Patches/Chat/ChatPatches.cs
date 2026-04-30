using BepInEx;
using HarmonyLib;
using System;
using UnityEngine;
using DG.Tweening;
using TMPro;

namespace BakuonOfflinePatch
{
    // ==========================================
    // ショートカット（エモーション）ボタンのオフラインチャット対応
    // ==========================================
    // PressedEmotionButton はアニメーション再生後に chatClient.PublishMessage を
    // 直接呼ぶため、オフラインでは NullReferenceException が発生する。
    // Finalizer で例外を握りつぶし、ローカルで吹き出しを表示する。

    [HarmonyPatch(typeof(ChatInputManager), "PressedEmotionButton")]
    public static class ChatInputManager_PressedEmotionButton_Patch
    {
        // Prefix: chatClient が使えない場合、先に吹き出しを表示する
        // void なので元メソッドの実行は止めない（アニメーション再生は継続）
        static void Prefix(GameObject _button)
        {
            try
            {
                bool chatAvailable = false;
                string diag = "";
                try
                {
                    var photonChat = SingletonMonoBehaviour<PhotonChatManager>.Instance;
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;
                    string ch = gm != null ? gm.currentRoomChatChannel : "(gm=null)";
                    string state = "(null)";
                    bool canChatChannel = false;
                    if (photonChat != null && photonChat.chatClient != null)
                    {
                        chatAvailable = photonChat.chatClient.CanChat;
                        state = photonChat.chatClient.State.ToString();
                        if (!string.IsNullOrEmpty(ch))
                            canChatChannel = photonChat.chatClient.CanChatInChannel(ch);
                    }
                    diag = $"button={_button.name}, CanChat={chatAvailable}, State={state}, channel='{ch}', CanChatInChannel={canChatChannel}";
                }
                catch (Exception innerEx) { diag = $"diag error: {innerEx.Message}"; }
                LogHelper.LogInfo($"[ChatPatches] PressedEmotionButton Prefix: {diag}");

                if (!chatAvailable)
                {
                    // ボタン名から emotionList のインデックスを取得しメッセージを抽出
                    string buttonName = _button.name;

                    if (buttonName.StartsWith("Emotion"))
                    {
                        string indexStr = buttonName.Replace("Emotion", "");
                        int index;
                        if (int.TryParse(indexStr, out index) && index >= 0 && index < 16)
                        {
                            string emotionData = SingletonMonoBehaviour<GameManager>.Instance.emotionList[index];
                            string[] parts = emotionData.Split(',');
                            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
                            {
                                ChatBalloonHelper.ShowLocalChatBalloon(parts[1]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ChatPatches] PressedEmotionButton Prefix エラー: {ex}");
            }
        }

        // Finalizer: chatClient.PublishMessage の NullReferenceException を握りつぶす
        static Exception Finalizer(Exception __exception)
        {
            if (__exception != null)
            {
                return null;
            }
            return null;
        }
    }

    // ==========================================
    // マクロボタンのオフラインチャット対応
    // ==========================================
    // EnterMacroText → PublishMacro 経由で呼ばれるマクロメッセージ

    [HarmonyPatch(typeof(ChatInputManager), "PublishMacro")]
    public static class ChatInputManager_PublishMacro_Patch
    {
        static bool Prefix(ChatInputManager __instance, string _macroString)
        {
            try
            {
                if (string.IsNullOrEmpty(_macroString))
                {
                    return false;
                }

                // PhotonChatが接続されていない場合（オフラインモード）
                bool chatAvailable = false;
                string diag = "";
                try
                {
                    var photonChat = SingletonMonoBehaviour<PhotonChatManager>.Instance;
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;
                    string ch = gm != null ? gm.currentRoomChatChannel : "(gm=null)";
                    string state = "(null)";
                    bool canChatChannel = false;
                    if (photonChat != null && photonChat.chatClient != null)
                    {
                        chatAvailable = photonChat.chatClient.CanChat;
                        state = photonChat.chatClient.State.ToString();
                        if (!string.IsNullOrEmpty(ch))
                            canChatChannel = photonChat.chatClient.CanChatInChannel(ch);
                    }
                    diag = $"macro='{_macroString}', CanChat={chatAvailable}, State={state}, channel='{ch}', CanChatInChannel={canChatChannel}";
                }
                catch (Exception innerEx) { diag = $"diag error: {innerEx.Message}"; }
                LogHelper.LogInfo($"[ChatPatches] PublishMacro Prefix: {diag}");

                if (!chatAvailable)
                {
                    ChatBalloonHelper.ShowLocalChatBalloon(_macroString);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ChatPatches] PublishMacro パッチエラー: {ex}");
                return true;
            }
        }
    }

    // ==========================================
    // 通常チャット入力のオフライン対応
    // ==========================================
    [HarmonyPatch(typeof(ChatInputManager), "InputText")]
    public static class ChatInputManager_InputText_Patch
    {
        static bool Prefix(ChatInputManager __instance, string _inputString)
        {
            try
            {
                if (string.IsNullOrEmpty(_inputString))
                {
                    return false;
                }

                bool chatAvailable = false;
                string diag = "";
                try
                {
                    var photonChat = SingletonMonoBehaviour<PhotonChatManager>.Instance;
                    var gm = SingletonMonoBehaviour<GameManager>.Instance;
                    string ch = gm != null ? gm.currentRoomChatChannel : "(gm=null)";
                    string state = "(null)";
                    bool canChatChannel = false;
                    if (photonChat != null && photonChat.chatClient != null)
                    {
                        chatAvailable = photonChat.chatClient.CanChat;
                        state = photonChat.chatClient.State.ToString();
                        if (!string.IsNullOrEmpty(ch))
                            canChatChannel = photonChat.chatClient.CanChatInChannel(ch);
                    }
                    diag = $"input='{_inputString}', CanChat={chatAvailable}, State={state}, channel='{ch}', CanChatInChannel={canChatChannel}";
                }
                catch (Exception innerEx) { diag = $"diag error: {innerEx.Message}"; }
                LogHelper.LogInfo($"[ChatPatches] InputText Prefix: {diag}");

                if (!chatAvailable)
                {
                    // スラッシュコマンド (/big, /gold 等) は吹き出し/チャットログに出さない。
                    // 元のゲーム挙動 (PrivateServer 接続時) と一致させる。
                    // ConfigMod 等の他 Prefix がコマンド処理を担当する。
                    if (_inputString.TrimStart().StartsWith("/"))
                    {
                        return false;
                    }

                    // 通常チャット: 吹き出し + ログに追加
                    ChatBalloonHelper.ShowLocalChatBalloon(_inputString);
                    ChatBalloonHelper.AddToChatLog(_inputString);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ChatPatches] InputText パッチエラー: {ex}");
                return true;
            }
        }
    }

    // ==========================================
    // 受信側診断: AddChatText でローカルキャラの myUserID と一致するか確認
    // ==========================================
    [HarmonyPatch(typeof(ChatInputManager), "AddChatText")]
    public static class ChatInputManager_AddChatText_Diag_Patch
    {
        static void Prefix(string _channelName, string _sender, string _messeageType,
                           string _playerName, string _chatString, string _userID)
        {
            try
            {
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                var photonChat = SingletonMonoBehaviour<PhotonChatManager>.Instance;
                string gmUserID = gm != null ? gm.userID : "(gm=null)";
                string photonUserName = photonChat != null ? photonChat.UserName : "(null)";
                string playerObjUserID = "(none)";
                bool senderMatchesPlayerObj = false;
                if (gm != null && gm.myPlayerObject != null)
                {
                    var fcc = gm.myPlayerObject.GetComponent<FieldCharacterController>();
                    if (fcc != null)
                    {
                        playerObjUserID = fcc.myUserID;
                        senderMatchesPlayerObj = (fcc.myUserID == _sender);
                    }
                }
                LogHelper.LogInfo(
                    $"[ChatPatches] AddChatText: ch='{_channelName}', type='{_messeageType}', " +
                    $"sender='{_sender}', text='{_chatString}', " +
                    $"gm.userID='{gmUserID}', photonChat.UserName='{photonUserName}', " +
                    $"myPlayerObject.myUserID='{playerObjUserID}', match={senderMatchesPlayerObj}");
            }
            catch { }
        }
    }

    // ==========================================
    // 共通ヘルパー: ローカル吹き出し表示
    // ==========================================
    public static class ChatBalloonHelper
    {
        /// <summary>
        /// チャットログ欄にメッセージを追加する（通常チャット用）
        /// </summary>
        public static void AddToChatLog(string message)
        {
            try
            {
                var chatInputManager = SingletonMonoBehaviour<ChatInputManager>.Instance;
                if (chatInputManager == null)
                {
                    LogHelper.LogWarning("[ChatPatches] ChatInputManager is null, cannot add to chat log");
                    return;
                }

                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                string playerName = gm != null ? gm.playerName : "Player";
                string userID = gm != null ? gm.userID : "";

                // 元コードの AddChatText "Range" タイプと同じ処理
                chatInputManager.AddChatLogData(
                    ChatLogData.ChatLogType.TEXT,
                    playerName,
                    message,
                    null,
                    "white",
                    false,
                    userID
                );

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ChatPatches] AddToChatLog エラー: {ex}");
            }
        }

        public static void ShowLocalChatBalloon(string message)
        {
            try
            {
                LogHelper.LogInfo($"[ChatPatches] ShowLocalChatBalloon called: '{message}'");
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                if (gm == null || gm.myPlayerObject == null)
                {
                    LogHelper.LogWarning("[ChatPatches] GameManager or myPlayerObject is null");
                    return;
                }

                var uiController = gm.myPlayerObject.GetComponent<FieldCharacterUIController>();
                if (uiController == null || uiController.characterChatLabelObject == null)
                {
                    LogHelper.LogWarning("[ChatPatches] FieldCharacterUIController or characterChatLabelObject is null");
                    return;
                }

                var chatLabel = uiController.characterChatLabelObject.GetComponent<CharacterChatLabel>();
                if (chatLabel == null)
                {
                    LogHelper.LogWarning("[ChatPatches] CharacterChatLabel is null");
                    return;
                }

                // 吹き出し表示（元コードの AddChatText "Macro" 処理と同じ手順）
                chatLabel.chatWindowRoot.SetActive(true);
                chatLabel.illustChatWindowRoot.SetActive(false);
                chatLabel.syakusakaKujiChatWindowRoot.SetActive(false);

                // テキスト設定（元コードと同じ書式）
                message = message.TrimEnd('\r', '\n');
                chatLabel.chatWindowText.GetComponent<TextMeshProUGUI>().text = "<color=black>" + message + "</color>";

                // アニメーション（表示 → 5秒後に非表示）
                chatLabel.transform.DOKill();
                chatLabel.transform.localScale = new Vector3(0f, 0f, 0f);
                chatLabel.transform.DOScale(new Vector3(1f, 1f, 1f), 0.2f);
                chatLabel.transform.DOScale(new Vector3(0f, 0f, 0f), 0.2f).SetDelay(5f);

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ChatPatches] ShowLocalChatBalloon エラー: {ex}");
            }
        }
    }
}

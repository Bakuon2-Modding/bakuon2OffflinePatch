using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;

namespace BakuonOfflinePatch
{
    // ==========================================
    // ユーザーコンテンツ ローカルストア（メモリキャッシュ + ES2永続化）
    // ==========================================
    public static class OfflineUserContentsStore
    {
        public static List<UserContentsData> localContentsList = new List<UserContentsData>();
        private static bool isLoaded = false;
        private static bool publishedCacheDirty = true;
        private static List<UserContentsData> cachedPublished = new List<UserContentsData>();
        private static List<UserContentsData> cachedIllust = new List<UserContentsData>();
        private static List<UserContentsData> cachedStory = new List<UserContentsData>();

        private const string SAVE_FILE = "saveData_offline";
        private const string TAG_USER_CONTENTS = "offline_userContents";

        public static string ImageDirectory
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "UserContentsImages");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
        }

        /// <summary>
        /// ES2からコンテンツを読み込み（初回のみ）
        /// </summary>
        public static void EnsureLoaded()
        {
            if (isLoaded) return;
            isLoaded = true;
            Load();
        }

        /// <summary>
        /// ES2からコンテンツを読み込み
        /// </summary>
        public static void Load()
        {
            InvalidateCache();
            try
            {
                localContentsList.Clear();
                if (!ES2.Exists(SAVE_FILE + "?tag=" + TAG_USER_CONTENTS)) return;

                var lines = ES2.LoadList<string>(SAVE_FILE + "?tag=" + TAG_USER_CONTENTS);
                if (lines == null || lines.Count == 0) return;

                int i = 0;
                while (i < lines.Count)
                {
                    string headerLine = lines[i];
                    string[] h = headerLine.Split('\t');
                    if (h.Length < 16)
                    {
                        i++;
                        continue;
                    }

                    var data = new UserContentsData();
                    int typeVal;
                    int.TryParse(h[0], out typeVal);
                    data.myContentsType = (UserContentsData.ContentsType)typeVal;
                    data.NCMBobjectID = h[1];
                    data.id = h[2];
                    data.title = h[3];
                    data.autorComment = h[4];
                    data.isAppearNewest = h[5] == "1";
                    data.isLimited = h[6] == "1";
                    long ticks;
                    if (long.TryParse(h[7], out ticks))
                    {
                        data.postTime = new DateTime(ticks);
                    }
                    data.imageUrl = h[8];
                    data.isPublished = h[9] == "1";
                    int bgId;
                    int.TryParse(h[10], out bgId);
                    data.backgroundImageID = bgId;
                    int c1;
                    int.TryParse(h[11], out c1);
                    data.character1_unitID = (UnitData.enumUnitID)c1;
                    int c2;
                    int.TryParse(h[12], out c2);
                    data.character2_unitID = (UnitData.enumUnitID)c2;
                    int vc;
                    int.TryParse(h[13], out vc);
                    data.viewCount = vc;
                    int ws;
                    int.TryParse(h[14], out ws);
                    data.weeklyScore = ws;
                    int ts;
                    int.TryParse(h[15], out ts);
                    data.totalScore = ts;
                    int partsCount = 0;
                    if (h.Length > 16)
                    {
                        int.TryParse(h[16], out partsCount);
                    }

                    data.userID = SingletonMonoBehaviour<GameManager>.Instance != null
                        ? SingletonMonoBehaviour<GameManager>.Instance.userID
                        : "";
                    data.goodUserIDList = new List<string>();
                    data.badUserIDList = new List<string>();
                    data.userContentsStoryPartsDataList = new List<UserContentsData.userContentsStoryPartsData>();

                    // ストーリーパーツ行を読み込み
                    for (int p = 0; p < partsCount && (i + 1 + p) < lines.Count; p++)
                    {
                        string partLine = lines[i + 1 + p];
                        string[] pp = partLine.Split('\t');
                        if (pp.Length >= 6)
                        {
                            var part = new UserContentsData.userContentsStoryPartsData();
                            int pu1;
                            int.TryParse(pp[0], out pu1);
                            part.character1_unitID = (UnitData.enumUnitID)pu1;
                            int pf1;
                            int.TryParse(pp[1], out pf1);
                            part.character1_face = pf1;
                            int pu2;
                            int.TryParse(pp[2], out pu2);
                            part.character2_unitID = (UnitData.enumUnitID)pu2;
                            int pf2;
                            int.TryParse(pp[3], out pf2);
                            part.character2_face = pf2;
                            part.nameString = pp[4];
                            part.mainTextString = pp[5];
                            data.userContentsStoryPartsDataList.Add(part);
                        }
                    }

                    localContentsList.Add(data);
                    i += 1 + partsCount;
                }

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[UserContents] 読み込みエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ES2にコンテンツを保存
        /// </summary>
        public static void Save()
        {
            try
            {
                var lines = new List<string>();
                foreach (var data in localContentsList)
                {
                    int partsCount = data.userContentsStoryPartsDataList != null
                        ? data.userContentsStoryPartsDataList.Count : 0;

                    // ヘッダー行
                    string header = string.Join("\t",
                        ((int)data.myContentsType).ToString(),
                        data.NCMBobjectID ?? "",
                        data.id ?? "",
                        (data.title ?? "").Replace("\t", " ").Replace("\n", " "),
                        (data.autorComment ?? "").Replace("\t", " ").Replace("\n", " "),
                        data.isAppearNewest ? "1" : "0",
                        data.isLimited ? "1" : "0",
                        data.postTime.Ticks.ToString(),
                        data.imageUrl ?? "",
                        data.isPublished ? "1" : "0",
                        data.backgroundImageID.ToString(),
                        ((int)data.character1_unitID).ToString(),
                        ((int)data.character2_unitID).ToString(),
                        data.viewCount.ToString(),
                        data.weeklyScore.ToString(),
                        data.totalScore.ToString(),
                        partsCount.ToString()
                    );
                    lines.Add(header);

                    // ストーリーパーツ行
                    if (data.userContentsStoryPartsDataList != null)
                    {
                        foreach (var part in data.userContentsStoryPartsDataList)
                        {
                            string partLine = string.Join("\t",
                                ((int)part.character1_unitID).ToString(),
                                part.character1_face.ToString(),
                                ((int)part.character2_unitID).ToString(),
                                part.character2_face.ToString(),
                                (part.nameString ?? "").Replace("\t", " ").Replace("\n", " "),
                                (part.mainTextString ?? "").Replace("\t", " ").Replace("\n", " ")
                            );
                            lines.Add(partLine);
                        }
                    }
                }

                ES2.Save(lines, SAVE_FILE + "?tag=" + TAG_USER_CONTENTS);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[UserContents] 保存エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// コンテンツを追加または更新
        /// </summary>
        public static void AddOrUpdate(UserContentsData data)
        {
            EnsureLoaded();

            // 既存のものを探して更新
            for (int i = 0; i < localContentsList.Count; i++)
            {
                if (localContentsList[i].id == data.id)
                {
                    localContentsList[i] = data;
                    InvalidateCache();
                    Save();
                    return;
                }
            }

            // 新規追加
            localContentsList.Add(data);
            InvalidateCache();
            Save();
        }

        /// <summary>
        /// コンテンツを削除
        /// </summary>
        public static bool RemoveByObjectID(string ncmbObjectID)
        {
            EnsureLoaded();
            for (int i = localContentsList.Count - 1; i >= 0; i--)
            {
                if (localContentsList[i].NCMBobjectID == ncmbObjectID)
                {
                    localContentsList.RemoveAt(i);
                    InvalidateCache();
                    Save();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// IDでコンテンツを検索
        /// </summary>
        public static UserContentsData FindByID(string id)
        {
            EnsureLoaded();
            foreach (var data in localContentsList)
            {
                if (data.id == id) return data;
            }
            return null;
        }

        /// <summary>
        /// 公開済みコンテンツを取得
        /// </summary>
        public static List<UserContentsData> GetPublished()
        {
            EnsureLoaded();
            if (publishedCacheDirty)
            {
                cachedPublished.Clear();
                cachedIllust.Clear();
                cachedStory.Clear();
                foreach (var data in localContentsList)
                {
                    if (data.isPublished)
                    {
                        cachedPublished.Add(data);
                        if (data.myContentsType == UserContentsData.ContentsType.IMAGE)
                            cachedIllust.Add(data);
                        else if (data.myContentsType == UserContentsData.ContentsType.STORY)
                            cachedStory.Add(data);
                    }
                }
                publishedCacheDirty = false;
            }
            return cachedPublished;
        }

        public static List<UserContentsData> GetPublishedIllust()
        {
            GetPublished(); // キャッシュを更新
            return cachedIllust;
        }

        public static List<UserContentsData> GetPublishedStory()
        {
            GetPublished(); // キャッシュを更新
            return cachedStory;
        }

        public static void InvalidateCache()
        {
            publishedCacheDirty = true;
        }

        /// <summary>
        /// ローカル画像のフルパスを取得
        /// </summary>
        public static string GetLocalImagePath(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl)) return null;
            if (!imageUrl.StartsWith("local:")) return null;
            string fileName = imageUrl.Substring(6);
            return Path.Combine(ImageDirectory, fileName);
        }
    }


    // ==========================================
    // Win32 ファイルダイアログ P/Invoke
    // ==========================================
    public static class Win32FileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool GetOpenFileName(ref OpenFileName ofn);

        public static string ShowOpenFileDialog(string title, string filter)
        {
            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.lpstrFilter = filter;
            ofn.lpstrFile = new string(new char[256]);
            ofn.nMaxFile = ofn.lpstrFile.Length;
            ofn.lpstrFileTitle = new string(new char[64]);
            ofn.nMaxFileTitle = ofn.lpstrFileTitle.Length;
            ofn.lpstrTitle = title;
            ofn.Flags = 0x00080000 | 0x00001000; // OFN_EXPLORER | OFN_FILEMUSTEXIST

            if (GetOpenFileName(ref ofn))
            {
                return ofn.lpstrFile;
            }
            return null;
        }
    }


    // ==========================================
    // SaveUserContentsData: ローカル保存に変更
    // ==========================================
    // 注意: UIPatches.csの既存パッチを置き換える。
    // UIPatches.csの NCMBManager_SaveUserContentsData_Patch は削除する必要がある。
    [HarmonyPatch(typeof(NCMBManager), "SaveUserContentsData")]
    public static class NCMBManager_SaveUserContentsData_Patch
    {
        static bool Prefix(NCMBManager __instance, UserContentsData _userContentsData)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // NCMBobjectIDがなければ生成
                if (string.IsNullOrEmpty(_userContentsData.NCMBobjectID))
                {
                    _userContentsData.NCMBobjectID = "local_" + Guid.NewGuid().ToString("N");
                }

                // userIDを設定
                _userContentsData.userID = SingletonMonoBehaviour<GameManager>.Instance.userID;

                // ローカルに保存
                OfflineUserContentsStore.AddOrUpdate(_userContentsData);

                // 成功コールバック
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[SaveUserContentsData] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetUserContents: ローカルコンテンツを返す
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserContents")]
    public static class NCMBManager_GetUserContents_Patch
    {
        static bool Prefix(NCMBManager __instance, UserInformation _userInformation)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                OfflineUserContentsStore.EnsureLoaded();

                _userInformation.userIllustContentsDataList.Clear();
                _userInformation.userStoryContentsDataList.Clear();

                foreach (var data in OfflineUserContentsStore.localContentsList)
                {
                    if (data.myContentsType == UserContentsData.ContentsType.IMAGE)
                    {
                        _userInformation.userIllustContentsDataList.Add(data);
                    }
                    else if (data.myContentsType == UserContentsData.ContentsType.STORY)
                    {
                        _userInformation.userStoryContentsDataList.Add(data);
                    }
                }

                // RunDelegateOnFinishedNetworkProcess を使用（delegateOnFinishedNetworkProcessはAction<bool>ではなく独自デリゲート型）
                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserContents] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetUserContents_ForBoard: 石板用コンテンツを返す
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserContents_ForBoard")]
    public static class NCMBManager_GetUserContents_ForBoard_Patch
    {
        static bool Prefix(NCMBManager __instance, UserContentsData.ContentsType _type)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                OfflineUserContentsStore.EnsureLoaded();
                var published = OfflineUserContentsStore.GetPublished();

                var boardManager = SingletonMonoBehaviour<UserContentsBoardManager>.Instance;
                if (boardManager != null)
                {
                    foreach (var data in published)
                    {
                        if (_type == UserContentsData.ContentsType.IMAGE && data.myContentsType == UserContentsData.ContentsType.IMAGE)
                        {
                            boardManager.userContentsDataList_Illust.Add(data);
                        }
                        else if (_type == UserContentsData.ContentsType.STORY && data.myContentsType == UserContentsData.ContentsType.STORY)
                        {
                            boardManager.userContentsDataList_Story.Add(data);
                        }
                    }
                    boardManager.currentUserContentsDataStoryPartsListIndex = 0;
                }

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserContents_ForBoard] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetUserContents_FromID: IDでコンテンツを検索
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "GetUserContents_FromID")]
    public static class NCMBManager_GetUserContents_FromID_Patch
    {
        static bool Prefix(NCMBManager __instance, string _id)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var data = OfflineUserContentsStore.FindByID(_id);
                var boardManager = SingletonMonoBehaviour<UserContentsBoardManager>.Instance;
                if (boardManager != null && data != null)
                {
                    boardManager.popUpTargetUserContentsData = data;
                }

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetUserContents_FromID] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // IncrementCountUserContentsData: ローカル更新
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "IncrementCountUserContentsData")]
    public static class NCMBManager_IncrementCountUserContentsData_Patch
    {
        static bool Prefix(NCMBManager __instance, string _id, int _incrementValue_ViewCount,
            string _addUserID_GoodCount, string _removeUserID_GoodCount,
            string _addUserID_BadCount, string _removeUserID_BadCount)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                var data = OfflineUserContentsStore.FindByID(_id);
                if (data != null)
                {
                    if (_incrementValue_ViewCount > 0)
                    {
                        data.viewCount += _incrementValue_ViewCount;
                        data.weeklyScore += 1;
                        data.totalScore += 1;
                    }
                    if (!string.IsNullOrEmpty(_addUserID_GoodCount))
                    {
                        if (!data.goodUserIDList.Contains(_addUserID_GoodCount))
                        {
                            data.goodUserIDList.Add(_addUserID_GoodCount);
                        }
                        data.weeklyScore += 30;
                        data.totalScore += 30;
                    }
                    if (!string.IsNullOrEmpty(_removeUserID_GoodCount))
                    {
                        data.goodUserIDList.Remove(_removeUserID_GoodCount);
                        data.weeklyScore -= 30;
                        data.totalScore -= 30;
                    }
                    if (!string.IsNullOrEmpty(_addUserID_BadCount))
                    {
                        if (!data.badUserIDList.Contains(_addUserID_BadCount))
                        {
                            data.badUserIDList.Add(_addUserID_BadCount);
                        }
                    }
                    if (!string.IsNullOrEmpty(_removeUserID_BadCount))
                    {
                        data.badUserIDList.Remove(_removeUserID_BadCount);
                    }

                    OfflineUserContentsStore.Save();
                }

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[IncrementCountUserContentsData] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // DeleteNCMBObjectFromID: ローカル削除
    // ==========================================
    [HarmonyPatch(typeof(NCMBManager), "DeleteNCMBObjectFromID")]
    public static class NCMBManager_DeleteNCMBObjectFromID_Patch
    {
        static bool Prefix(NCMBManager __instance, string _id, string _className)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                if (_className == "UserContents")
                {
                    OfflineUserContentsStore.RemoveByObjectID(_id);
                }

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[DeleteNCMBObjectFromID] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetNewestPublishedUserContents: ローカルコンテンツを返す
    // ==========================================
    // 注意: UIPatches.csの既存パッチを置き換える
    [HarmonyPatch(typeof(NCMBManager), "GetNewestPublishedUserContents")]
    public static class NCMBManager_GetNewestPublishedUserContents_Patch
    {
        static bool Prefix(NCMBManager __instance, int _skip)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                OfflineUserContentsStore.EnsureLoaded();
                var published = OfflineUserContentsStore.GetPublished();

                // publishedUserContentsDataList にローカルデータを追加
                var screenManager = SingletonMonoBehaviour<UserContentsScreenManager>.Instance;
                if (screenManager != null)
                {
                    foreach (var data in published)
                    {
                        screenManager.publishedUserContentsDataList.Add(data);
                    }
                }

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetNewestPublishedUserContents] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // GetPublishedUserContents: ローカルコンテンツを返す
    // ==========================================
    // 注意: UIPatches.csの既存パッチを置き換える
    [HarmonyPatch(typeof(NCMBManager), "GetPublishedUserContents")]
    public static class NCMBManager_GetPublishedUserContents_Patch
    {
        static bool Prefix(NCMBManager __instance, int _skip)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                OfflineUserContentsStore.EnsureLoaded();
                var published = OfflineUserContentsStore.GetPublished();

                var screenManager = SingletonMonoBehaviour<UserContentsScreenManager>.Instance;
                if (screenManager != null)
                {
                    foreach (var data in published)
                    {
                        screenManager.publishedUserContentsDataList.Add(data);
                    }
                }

                __instance.GetType().GetMethod("RunDelegateOnFinishedNetworkProcess",
                    BindingFlags.Public | BindingFlags.Instance)
                    ?.Invoke(__instance, new object[] { true });

                __instance.GetType().GetField("isBusy",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(__instance, false);

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[GetPublishedUserContents] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // LoadImagePostForm: ローカルファイル選択に置換
    // ==========================================
    [HarmonyPatch(typeof(UserContentPopupWindowController), "LoadImagePostForm")]
    public static class UserContentPopupWindowController_LoadImagePostForm_Patch
    {
        static bool Prefix(UserContentPopupWindowController __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // Win32ファイルダイアログで画像を選択
                string filter = "画像ファイル (*.png;*.jpg;*.bmp)\0*.png;*.jpg;*.jpeg;*.bmp\0すべてのファイル (*.*)\0*.*\0";
                string filePath = Win32FileDialog.ShowOpenFileDialog("画像を選択", filter);

                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return false;
                }

                // GUID名でコピー
                string ext = Path.GetExtension(filePath).ToLower();
                string guidName = Guid.NewGuid().ToString("N") + ext;
                string destPath = Path.Combine(OfflineUserContentsStore.ImageDirectory, guidName);
                File.Copy(filePath, destPath, true);

                // imageUrlを設定
                __instance.tempUserContentsData.imageUrl = "local:" + guidName;

                // 画像をロードして表示
                __instance.StartCoroutine(LoadLocalImage(__instance, destPath));

                SingletonMonoBehaviour<GameManager>.Instance.ShowAlartWindow("システムメッセージ", "画像を読み込みました");

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[LoadImagePostForm] パッチエラー: {ex}");
                return true;
            }
        }

        private static IEnumerator LoadLocalImage(UserContentPopupWindowController controller, string localPath)
        {
            string url = "file:///" + localPath.Replace("\\", "/");
            WWW www = new WWW(url);
            yield return www;

            if (www.texture != null)
            {
                var sprite = Sprite.Create(www.textureNonReadable,
                    new Rect(0f, 0f, www.textureNonReadable.width, www.textureNonReadable.height),
                    Vector2.zero);
                controller.myUserContentsData.imageSprite = sprite;
                controller.postImage.sprite = sprite;
                controller.postImage.preserveAspect = true;
                controller.illustThumbnailImage.sprite = sprite;
                controller.illustThumbnailImage.preserveAspect = true;
            }
            else
            {
                LogHelper.LogError($"[LoadLocalImage] 画像読み込み失敗: {www.error}");
            }
        }
    }


    // ==========================================
    // UserContentPopupWindowController.GetImage: ローカル画像対応
    // ==========================================
    [HarmonyPatch(typeof(UserContentPopupWindowController), "GetImage")]
    public static class UserContentPopupWindowController_GetImage_Patch
    {
        static bool Prefix(UserContentPopupWindowController __instance, string _url, ref IEnumerator __result)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                if (!string.IsNullOrEmpty(_url) && _url.StartsWith("local:"))
                {
                    string localPath = OfflineUserContentsStore.GetLocalImagePath(_url);
                    if (localPath != null && File.Exists(localPath))
                    {
                        __result = LoadLocalImageCoroutine(__instance, localPath);
                        return false;
                    }
                }

                // local:ではないURLの場合、サーバーはダウンしているのでスキップ
                __result = EmptyCoroutine();
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[UserContentPopup.GetImage] パッチエラー: {ex}");
                return true;
            }
        }

        private static IEnumerator LoadLocalImageCoroutine(UserContentPopupWindowController controller, string localPath)
        {
            string url = "file:///" + localPath.Replace("\\", "/");
            WWW www = new WWW(url);
            yield return www;

            if (www.texture != null)
            {
                var sprite = Sprite.Create(www.textureNonReadable,
                    new Rect(0f, 0f, www.textureNonReadable.width, www.textureNonReadable.height),
                    Vector2.zero);
                controller.myUserContentsData.imageSprite = sprite;
                controller.postImage.sprite = sprite;
                controller.postImage.preserveAspect = true;
                controller.illustThumbnailImage.sprite = sprite;
                controller.illustThumbnailImage.preserveAspect = true;
            }
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }
    }


    // ==========================================
    // UserContentsBoardManager.GetImage: ローカル画像対応
    // ==========================================
    [HarmonyPatch(typeof(UserContentsBoardManager), "GetImage")]
    public static class UserContentsBoardManager_GetImage_Patch
    {
        static bool Prefix(UserContentsBoardManager __instance, string _url, ref IEnumerator __result)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                if (!string.IsNullOrEmpty(_url) && _url.StartsWith("local:"))
                {
                    string localPath = OfflineUserContentsStore.GetLocalImagePath(_url);
                    if (localPath != null && File.Exists(localPath))
                    {
                        __result = LoadBoardImageCoroutine(__instance, localPath);
                        return false;
                    }
                }

                // local:ではないURLはスキップ
                __result = EmptyCoroutine();
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[BoardManager.GetImage] パッチエラー: {ex}");
                return true;
            }
        }

        private static IEnumerator LoadBoardImageCoroutine(UserContentsBoardManager boardManager, string localPath)
        {
            string url = "file:///" + localPath.Replace("\\", "/");
            WWW www = new WWW(url);
            yield return www;

            if (www.texture != null)
            {
                GC.Collect();
                Resources.UnloadUnusedAssets();
                boardManager.illustImage.sprite = Sprite.Create(www.textureNonReadable,
                    new Rect(0f, 0f, www.textureNonReadable.width, www.textureNonReadable.height),
                    Vector2.zero);
                boardManager.illustImage.preserveAspect = true;
            }
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }
    }


    // ==========================================
    // UserContentsBoardManager.Start: PhotonChat購読をスキップ、ボード有効化
    // ==========================================
    [HarmonyPatch(typeof(UserContentsBoardManager), "Start")]
    public static class UserContentsBoardManager_Start_Patch
    {
        static bool Prefix(UserContentsBoardManager __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // PhotonChat購読をスキップ
                // isUserContentsBoardEnabledを有効化
                var gm = SingletonMonoBehaviour<GameManager>.Instance;
                gm.isUserIllustContentsBoardEnabled = true;
                gm.isUserStoryContentsBoardEnabled = true;

                __instance.RenewDisableImage();

                // ローカルコンテンツを読み込み
                OfflineUserContentsStore.EnsureLoaded();

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[UserContentsBoardManager.Start] パッチエラー: {ex}");
                return true;
            }
        }
    }


    // ==========================================
    // UserContentsBoardManager.Update: オフラインではローカルコンテンツ直接表示
    // ==========================================
    [HarmonyPatch(typeof(UserContentsBoardManager), "Update")]
    public static class UserContentsBoardManager_Update_Patch
    {
        static bool Prefix(UserContentsBoardManager __instance)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                // イラスト更新タイマー
                __instance.illustRenewTimer -= Time.deltaTime;
                if (__instance.illustRenewTimer <= 0f)
                {
                    __instance.illustRenewTimer = __instance.illustRenewInterval;
                    UpdateIllustBoard(__instance);
                }

                // ストーリー更新タイマー
                __instance.storyRenewTimer -= Time.deltaTime;
                if (__instance.storyRenewTimer <= 0f)
                {
                    __instance.storyRenewTimer = float.MaxValue;
                    UpdateStoryBoard(__instance);
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[UserContentsBoardManager.Update] パッチエラー: {ex}");
                return true;
            }
        }

        private static void UpdateIllustBoard(UserContentsBoardManager boardManager)
        {
            try
            {
                var illustList = OfflineUserContentsStore.GetPublishedIllust();
                if (illustList.Count == 0) return;

                // ランダムに1つ選んで表示
                int index = UnityEngine.Random.Range(0, illustList.Count);
                var current = illustList[index];
                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                boardManager.RenewIllust(current.id, current.imageUrl, current.title, gm.playerName);
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[UpdateIllustBoard] エラー: {ex.Message}");
            }
        }

        private static void UpdateStoryBoard(UserContentsBoardManager boardManager)
        {
            try
            {
                var storyList = OfflineUserContentsStore.GetPublishedStory();
                if (storyList.Count == 0) return;

                // ランダムに1つ選んで表示
                int index = UnityEngine.Random.Range(0, storyList.Count);
                var current = storyList[index];
                var gm = SingletonMonoBehaviour<GameManager>.Instance;

                if (current.userContentsStoryPartsDataList.Count == 0) return;

                int partIndex = boardManager.currentUserContentsDataStoryPartsListIndex;
                if (partIndex >= current.userContentsStoryPartsDataList.Count)
                {
                    partIndex = 0;
                    boardManager.currentUserContentsDataStoryPartsListIndex = 0;
                }

                var part = current.userContentsStoryPartsDataList[partIndex];

                boardManager.RenewStory(
                    current.id,
                    current.backgroundImageID,
                    current.character1_unitID.ToString(),
                    part.character1_face,
                    current.character2_unitID.ToString(),
                    part.character2_face,
                    part.nameString,
                    part.mainTextString,
                    current.title,
                    gm.playerName
                );

                boardManager.currentUserContentsDataStoryPartsListIndex++;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[UpdateStoryBoard] エラー: {ex.Message}");
            }
        }
    }


    // ==========================================
    // UserContentsBoardManager.OnDestroy: PhotonChat購読解除をスキップ
    // ==========================================
    [HarmonyPatch(typeof(UserContentsBoardManager), "OnDestroy")]
    public static class UserContentsBoardManager_OnDestroy_Patch
    {
        static bool Prefix()
        {
            if (PhotonNetwork.offlineMode) return false;
            return true;
        }
    }


    // ==========================================
    // UserContentsThumbnailController.GetImage: ローカル画像対応
    // ==========================================
    // "local:filename" はローカルファイルから読み込み、それ以外はサーバーがないためスキップ。
    // IEnumeratorメソッドのため、__result を必ず設定して StartCoroutine(null) 例外を防ぐ。
    [HarmonyPatch(typeof(UserContentsThumbnailController), "GetImage")]
    public static class UserContentsThumbnailController_GetImage_Patch
    {
        static bool Prefix(UserContentsThumbnailController __instance, string _url, ref IEnumerator __result)
        {
            try
            {
                if (!PhotonNetwork.offlineMode) return true;

                if (!string.IsNullOrEmpty(_url) && _url.StartsWith("local:"))
                {
                    string localPath = OfflineUserContentsStore.GetLocalImagePath(_url);
                    if (localPath != null && File.Exists(localPath))
                    {
                        __result = LoadLocalThumbnailCoroutine(__instance, localPath);
                        return false;
                    }
                }

                // local:でないURL（旧サーバーURL等）はサーバーがないためスキップ
                __result = EmptyCoroutine();
                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[Thumbnail.GetImage] パッチエラー: {ex}");
                __result = EmptyCoroutine();
                return false;
            }
        }

        private static IEnumerator LoadLocalThumbnailCoroutine(UserContentsThumbnailController controller, string localPath)
        {
            string url = "file:///" + localPath.Replace("\\", "/");
            WWW www = new WWW(url);
            yield return www;

            if (www.texture != null)
            {
                var sprite = Sprite.Create(www.textureNonReadable,
                    new Rect(0f, 0f, www.textureNonReadable.width, www.textureNonReadable.height),
                    Vector2.zero);
                controller.myUserContentsData.imageThumbnailSprite = sprite;
                controller.illustImage.sprite = sprite;
            }
            else
            {
                LogHelper.LogWarning($"[Thumbnail.GetImage] 画像読み込み失敗: {www.error}");
            }
        }

        private static IEnumerator EmptyCoroutine()
        {
            yield break;
        }
    }
}

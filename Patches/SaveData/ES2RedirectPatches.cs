using System;
using System.Reflection;
using HarmonyLib;

namespace BakuonOfflinePatch
{
    /// <summary>
    /// ES2のSave/Load/Exists/Delete操作をインターセプトし、
    /// "saveData"への全操作を"saveData_offline"にリダイレクトする。
    /// ES2Settings.filenameData (ES2FilenameData型) の内部フィールドを直接書き換える。
    /// </summary>
    public static class ES2RedirectPatches
    {
        private const string ORIGINAL_FILE = "saveData";
        private const string OFFLINE_FILE = "saveData_offline";

        // filenameDataフィールド (ES2Settings -> ES2FilenameData)
        private static FieldInfo _filenameDataField;

        // ES2FilenameData内のstringフィールド群
        private static FieldInfo _fd_fullString;
        private static FieldInfo _fd_filename;
        private static FieldInfo _fd_filePath;
        private static FieldInfo _fd_playerPrefsPath;

        public static void ApplyPatches(Harmony harmony)
        {
            bool patched = TryPatchES2Settings(harmony);

            if (!patched)
            {
                LogHelper.LogWarning("[ES2Redirect] ES2Settings patch failed!");
            }
        }

        private static bool TryPatchES2Settings(Harmony harmony)
        {
            try
            {
                // ES2Settings型を検索
                Type es2SettingsType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        es2SettingsType = asm.GetType("ES2Settings");
                        if (es2SettingsType != null) break;
                    }
                    catch { }
                }

                if (es2SettingsType == null)
                {
                    LogHelper.LogWarning("[ES2Redirect] ES2Settings type not found");
                    return false;
                }

                // filenameDataフィールドを取得
                _filenameDataField = es2SettingsType.GetField("filenameData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (_filenameDataField == null)
                {
                    LogHelper.LogWarning("[ES2Redirect] filenameData field not found");
                    return false;
                }

                // ES2FilenameData型の内部フィールドを取得
                Type fdType = _filenameDataField.FieldType;
                var bindFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                _fd_fullString = fdType.GetField("fullString", bindFlags);
                _fd_filename = fdType.GetField("filename", bindFlags);
                _fd_filePath = fdType.GetField("filePath", bindFlags);
                _fd_playerPrefsPath = fdType.GetField("playerPrefsPath", bindFlags);

                // コンストラクタをパッチ
                var postfix = new HarmonyMethod(typeof(ES2RedirectPatches), nameof(ES2Settings_Ctor_Postfix));
                int ctorCount = 0;
                foreach (var ctor in es2SettingsType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    try
                    {
                        harmony.Patch(ctor, postfix: postfix);
                        ctorCount++;
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogWarning($"[ES2Redirect] Failed to patch ctor: {ex.Message}");
                    }
                }

                return ctorCount > 0;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ES2Redirect] Error: {ex}");
                return false;
            }
        }

        static void ES2Settings_Ctor_Postfix(object __instance)
        {
            try
            {
                if (_filenameDataField == null) return;

                object fdObj = _filenameDataField.GetValue(__instance);
                if (fdObj == null) return;

                // filenameフィールドをチェック（これが "saveData" を含むかが判定基準）
                if (_fd_filename == null) return;
                string filename = _fd_filename.GetValue(fdObj) as string;
                if (filename == null || !filename.Contains(ORIGINAL_FILE) || filename.Contains(OFFLINE_FILE))
                    return;

                // リダイレクト実行: 全関連フィールドを書き換え
                string newFilename = filename.Replace(ORIGINAL_FILE, OFFLINE_FILE);
                _fd_filename.SetValue(fdObj, newFilename);

                if (_fd_fullString != null)
                {
                    string fullStr = _fd_fullString.GetValue(fdObj) as string;
                    if (fullStr != null && fullStr.Contains(ORIGINAL_FILE))
                    {
                        _fd_fullString.SetValue(fdObj, fullStr.Replace(ORIGINAL_FILE, OFFLINE_FILE));
                    }
                }

                if (_fd_filePath != null)
                {
                    string filePath = _fd_filePath.GetValue(fdObj) as string;
                    if (filePath != null && filePath.Contains(ORIGINAL_FILE))
                    {
                        _fd_filePath.SetValue(fdObj, filePath.Replace(ORIGINAL_FILE, OFFLINE_FILE));
                    }
                }

                if (_fd_playerPrefsPath != null)
                {
                    string ppPath = _fd_playerPrefsPath.GetValue(fdObj) as string;
                    if (ppPath != null && ppPath.Contains(ORIGINAL_FILE))
                    {
                        _fd_playerPrefsPath.SetValue(fdObj, ppPath.Replace(ORIGINAL_FILE, OFFLINE_FILE));
                    }
                }

                // ES2FilenameDataがstruct(値型)の場合、修正したコピーを書き戻す必要がある
                _filenameDataField.SetValue(__instance, fdObj);

            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ES2Redirect] Postfix error: {ex}");
            }
        }
    }
}

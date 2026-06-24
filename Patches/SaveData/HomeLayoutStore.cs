using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BakuonOfflinePatch
{
    // マイホーム(マイルーム)の内装データ(GameManager.editMapList)を、
    // 人間が読める JSON ファイルとしてローカルへ永続化するヘルパー。
    //
    // 狙い:
    //   ・要件1 (保存): ゲーム本体は内装を NCMB(UserData.EditMap)へ保存するが、
    //     オフラインでは NCMB をバイパスするため何処にも残らない。ここでローカル保存する。
    //   ・要件3 (手動コピー&入替): 保存先を分かりやすいフォルダの JSON ファイルに集約し、
    //     エクスプローラ等で myroom.json を差し替えるだけで内装を入れ替えられるようにする。
    //
    // 内装データの各行は本体仕様どおり
    //   "itemIndex,posX,posY,posZ,eulerX,eulerY,eulerZ,scaleX,scaleY,scaleZ"
    // のカンマ区切り文字列。List<string> をそのまま保持する。
    public static class HomeLayoutStore
    {
        // 自分の部屋のアクティブな内装ファイル名 (これを差し替えると部屋が入れ替わる)
        private const string MyRoomFile = "myroom.json";

        // List<string> は JsonUtility で直接往復できないためラッパ型を経由する
        [Serializable]
        private class LayoutFile
        {
            public List<string> lines = new List<string>();
        }

        // 保存先フォルダ: <persistentDataPath>/MyRoomLayouts/
        internal static string LayoutDir
        {
            get { return Path.Combine(Application.persistentDataPath, "MyRoomLayouts"); }
        }

        // フォルダを用意する
        private static void EnsureDir()
        {
            try
            {
                if (!Directory.Exists(LayoutDir))
                {
                    Directory.CreateDirectory(LayoutDir);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"[HomeLayout] フォルダ準備に失敗: {ex.Message}");
            }
        }

        private static string MyRoomPath { get { return Path.Combine(LayoutDir, MyRoomFile); } }

        // 自分の部屋の内装を保存
        internal static void SaveMyRoom(List<string> lines)
        {
            WriteLines(MyRoomPath, lines);
            LogHelper.LogInfo($"[HomeLayout] 自分の内装を保存: {MyRoomPath} ({(lines != null ? lines.Count : 0)}件)");
        }

        // 自分の部屋の内装を読み込み (無ければ空リスト)
        internal static List<string> LoadMyRoom()
        {
            var lines = ReadLines(MyRoomPath);
            LogHelper.LogInfo($"[HomeLayout] 自分の内装を読込: {MyRoomPath} ({lines.Count}件)");
            return lines;
        }

        // 任意名で内装を出力 (訪問先の内装を visited_{name}.json として残す)
        public static void ExportNamed(string name, List<string> lines)
        {
            string path = Path.Combine(LayoutDir, "visited_" + Sanitize(name) + ".json");
            WriteLines(path, lines);
            LogHelper.LogInfo($"[HomeLayout] 訪問先の内装を保存: {path} ({(lines != null ? lines.Count : 0)}件)");
        }

        // ---- 入出力共通 ----

        private static void WriteLines(string path, List<string> lines)
        {
            try
            {
                EnsureDir();
                var file = new LayoutFile { lines = lines ?? new List<string>() };
                string json = JsonUtility.ToJson(file, true);
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[HomeLayout] 保存に失敗 ({path}): {ex.Message}");
            }
        }

        private static List<string> ReadLines(string path)
        {
            try
            {
                if (!File.Exists(path)) return new List<string>();
                string json = File.ReadAllText(path, new UTF8Encoding(false));
                var file = JsonUtility.FromJson<LayoutFile>(json);
                if (file == null || file.lines == null) return new List<string>();
                return file.lines;
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[HomeLayout] 読込に失敗 ({path}): {ex.Message}");
                return new List<string>();
            }
        }

        // ファイル名に使えない文字を除去
        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            var sb = new StringBuilder(name.Length);
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in name)
            {
                bool bad = false;
                for (int i = 0; i < invalid.Length; i++)
                {
                    if (c == invalid[i]) { bad = true; break; }
                }
                sb.Append(bad ? '_' : c);
            }
            return sb.ToString();
        }
    }
}

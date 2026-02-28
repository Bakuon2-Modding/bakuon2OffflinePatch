using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;

namespace BakuonOfflinePatch
{
    // ==========================================
    // ローカルAssetBundleローダー
    // ==========================================
    // dl_Data/bakumatsu_bakuon2/ にキャッシュされている
    // AssetBundleをローカルからロードする
    //
    // フォルダ構造:
    //   dl_Data/bakumatsu_bakuon2/
    //     scene/00000000000000000000000001000000/__data
    //     suteage/00000000000000000000000001000000/__data
    //     city/00000000000000000000000001000000/__data
    //     etc...

    public static class LocalAssetBundleLoader
    {
        private static bool _initialized = false;
        private static string _bundlePath = null;
        private static bool _bundleNotFound = false;
        private static Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>();

        /// <summary>
        /// ダウンロードデータが見つからなかったかどうか
        /// </summary>
        public static bool IsBundleNotFound { get { return _bundleNotFound; } }

        // AssetBundle名のリスト
        private static readonly string[] BundleNames = new string[]
        {
            "scene",
            "suteage",
            "city",
            "accessory",
            "animation",
            "audio",
            "roomeditor",
            "sprite"
        };

        // AssetBundleフォルダ名（キャッシュ内のサブフォルダ）
        private const string BUNDLE_FOLDER_NAME = "bakumatsu_bakuon2";

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            string gameDir = Path.GetDirectoryName(Application.dataPath);
            _bundlePath = null;
            _bundleNotFound = false;

            // dl_Data フォルダを確認
            if (_bundlePath == null)
            {
                string dlDataPath = Path.Combine(gameDir, "dl_Data", BUNDLE_FOLDER_NAME);
                if (Directory.Exists(dlDataPath) && HasAnyBundleFile(dlDataPath))
                {
                    _bundlePath = dlDataPath;
                }
            }

            // 結果を確認
            if (_bundlePath != null)
            {
                LogHelper.LogInfo($"[AssetBundle] AssetBundleフォルダ: {_bundlePath}");
            }
            else
            {
                _bundleNotFound = true;
                LogHelper.LogWarning($"[AssetBundle] ダウンロードデータが見つかりません (dl_Data: {Path.Combine(gameDir, "dl_Data", BUNDLE_FOLDER_NAME)})");
            }
        }

        /// <summary>
        /// 指定パスに1つ以上のバンドルファイルが存在するか確認
        /// </summary>
        private static bool HasAnyBundleFile(string basePath)
        {
            foreach (var bundleName in BundleNames)
            {
                string bundleFile = Path.Combine(basePath, bundleName, "00000000000000000000000001000000", "__data");
                if (File.Exists(bundleFile))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// バンドル名からファイルパスを取得
        /// </summary>
        private static string GetBundleFilePath(string bundleName)
        {
            // キャッシュ構造: bundleName/00000000000000000000000001000000/__data
            return Path.Combine(_bundlePath, bundleName, "00000000000000000000000001000000", "__data");
        }

        /// <summary>
        /// ローカルからAssetBundleをロード
        /// </summary>
        public static AssetBundle LoadBundle(string bundleName)
        {
            if (_loadedBundles.ContainsKey(bundleName))
            {
                return _loadedBundles[bundleName];
            }

            string bundleFile = GetBundleFilePath(bundleName);
            if (!File.Exists(bundleFile))
            {
                LogHelper.LogWarning($"[AssetBundle] ファイルが見つかりません: {bundleFile}");
                return null;
            }

            try
            {
                AssetBundle bundle = AssetBundle.LoadFromFile(bundleFile);
                if (bundle != null)
                {
                    _loadedBundles[bundleName] = bundle;

                    // AssetBundleManager.bundleDic にも登録（ゲームコードからアクセス可能にする）
                    RegisterToAssetBundleManager(bundleName, bundle);

                    return bundle;
                }
                else
                {
                    LogHelper.LogError($"[AssetBundle] ロード失敗: {bundleName}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[AssetBundle] ロードエラー ({bundleName}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// AssetBundleManager.bundleDic にバンドルを登録
        /// これによりゲームコードの AssetBundleManager.GetAsset() が動作する
        /// </summary>
        private static void RegisterToAssetBundleManager(string bundleName, AssetBundle bundle)
        {
            try
            {
                // AssetBundleManager.bundleDic (private static) にアクセス
                var bundleDicField = typeof(AssetBundleManager).GetField("bundleDic",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (bundleDicField == null)
                {
                    LogHelper.LogWarning("[AssetBundle] bundleDicフィールドが見つかりません");
                    return;
                }

                var bundleDic = bundleDicField.GetValue(null) as Dictionary<string, AssetBundle>;

                // bundleDicがnullの場合は初期化
                if (bundleDic == null)
                {
                    bundleDic = new Dictionary<string, AssetBundle>();
                    bundleDicField.SetValue(null, bundleDic);

                    // initializedフラグも設定
                    var initializedField = typeof(AssetBundleManager).GetField("initialized",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (initializedField != null)
                    {
                        initializedField.SetValue(null, true);
                    }
                }

                // バンドルを登録
                if (!bundleDic.ContainsKey(bundleName))
                {
                    bundleDic[bundleName] = bundle;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[AssetBundle] bundleDic登録エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// すべてのバンドルをロード
        /// </summary>
        public static void LoadAllBundles()
        {
            foreach (var bundleName in BundleNames)
            {
                LoadBundle(bundleName);
            }

            LogHelper.LogInfo($"[AssetBundle] ロード完了: {_loadedBundles.Count}/{BundleNames.Length} バンドル");
        }

        /// <summary>
        /// ロード済みバンドルを取得
        /// </summary>
        public static AssetBundle GetBundle(string bundleName)
        {
            if (_loadedBundles.TryGetValue(bundleName, out AssetBundle bundle))
            {
                return bundle;
            }
            return null;
        }

        /// <summary>
        /// バンドルパスが有効かチェック
        /// </summary>
        public static bool IsAvailable()
        {
            return !string.IsNullOrEmpty(_bundlePath) && Directory.Exists(_bundlePath);
        }

        /// <summary>
        /// バンドルがロード済みかチェック
        /// </summary>
        public static bool AreBundlesLoaded()
        {
            return _loadedBundles.Count > 0;
        }

        /// <summary>
        /// シーン名からバンドル内のフルパスを検索
        /// </summary>
        public static string FindScenePath(string sceneName)
        {
            // どのバンドルにシーンがあるか探す
            string[] bundlesToSearch = { "scene", "suteage", "city" };

            foreach (var bundleName in bundlesToSearch)
            {
                var bundle = GetBundle(bundleName);
                if (bundle == null) continue;

                var scenePaths = bundle.GetAllScenePaths();
                foreach (var path in scenePaths)
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (name.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// シーンがAssetBundle内に存在するかチェック
        /// </summary>
        public static bool HasScene(string sceneName)
        {
            return FindScenePath(sceneName) != null;
        }
    }

    // ==========================================
    // 統一AssetBundleシーンローダー
    // ==========================================
    // _Systemシーンがロードされたら、対応するAssetBundleシーンを追加ロード
    public static class AssetBundleSceneLoader
    {
        private static bool _initialized = false;
        private static HashSet<string> _loadedScenes = new HashSet<string>();
        private static readonly Queue<string> _pendingScenes = new Queue<string>();
        private static readonly HashSet<string> _queuedScenes = new HashSet<string>();
        private static bool _queueRunning = false;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                // _Systemシーンがロードされたら、対応するAssetBundleシーンをロード
                if (!scene.name.EndsWith("_System")) return;

                // 対応するシーン名を取得 (Home_System → Home, City_XXX_System → City_XXX)
                string assetBundleSceneName = scene.name.Replace("_System", "");

                // 既にロード済みならスキップ
                if (_loadedScenes.Contains(assetBundleSceneName)) return;

                // AssetBundleにシーンがあるか確認
                if (!LocalAssetBundleLoader.HasScene(assetBundleSceneName))
                {
                    return;
                }
                _loadedScenes.Add(assetBundleSceneName);
                if (_queuedScenes.Add(assetBundleSceneName))
                {
                    _pendingScenes.Enqueue(assetBundleSceneName);
                    if (!_queueRunning)
                    {
                        _queueRunning = true;
                        CoroutineRunner.Instance.StartCoroutine(ProcessQueue());
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[ABSceneLoader] OnSceneLoaded エラー: {ex}");
            }
        }

        private static IEnumerator ProcessQueue()
        {
            while (_pendingScenes.Count > 0)
            {
                string sceneName = _pendingScenes.Dequeue();
                _queuedScenes.Remove(sceneName);

                yield return new WaitForSeconds(UnityEngine.Random.Range(0.05f, 0.25f));

                while (!HitchMonitor.IsFrameLight(Time.unscaledDeltaTime))
                {
                    yield return null;
                }

                HitchMonitor.MarkEvent("ABSceneLoader.Start:" + sceneName);
                yield return LoadSceneFromBundle(sceneName);
                HitchMonitor.MarkEvent("ABSceneLoader.Done:" + sceneName);
            }
            _queueRunning = false;
        }

        private static IEnumerator LoadSceneFromBundle(string sceneName)
        {
            // シーンをアディティブにロード
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (asyncLoad == null)
            {
                LogHelper.LogError($"[ABSceneLoader] シーン {sceneName} のロードを開始できませんでした");
                _loadedScenes.Remove(sceneName);
                yield break;
            }

            while (!asyncLoad.isDone)
            {
                yield return null;
            }

        }

        /// <summary>
        /// シーンアンロード時のクリーンアップ
        /// </summary>
        public static void OnSceneUnloaded(string sceneName)
        {
            _loadedScenes.Remove(sceneName);
        }
    }

    // ==========================================
    // TitleSceneManager パッチ
    // ==========================================
    // タイトルシーン開始時にローカルバンドルをロード
    [HarmonyPatch(typeof(TitleSceneManager), "Start")]
    public static class TitleSceneManager_Start_LoadBundles_Patch
    {
        static void Postfix()
        {
            try
            {
                // ローカルバンドルが利用可能ならロード
                if (LocalAssetBundleLoader.IsAvailable() && !LocalAssetBundleLoader.AreBundlesLoaded())
                {
                    LocalAssetBundleLoader.LoadAllBundles();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[AssetBundle] TitleSceneManager.Start パッチエラー: {ex}");
            }
        }
    }

    // ==========================================
    // AssetBundleDownloadSceneController パッチ
    // ==========================================
    // ダウンロードシーンでローカルバンドルをロード（フォールバック）
    [HarmonyPatch(typeof(AssetBundleDownloadSceneController), "StartDownload")]
    public static class AssetBundleDownloadSceneController_StartDownload_Patch
    {
        static bool Prefix(AssetBundleDownloadSceneController __instance)
        {
            try
            {
                // ローカルバンドルが利用可能ならロード
                if (LocalAssetBundleLoader.IsAvailable())
                {
                    LocalAssetBundleLoader.LoadAllBundles();
                }

                // タイトルシーンへ
                SingletonMonoBehaviour<GameManager>.Instance.isLoadedAssetBundle = true;
                SingletonMonoBehaviour<GameManager>.Instance.LoadTitleScene();

                return false; // 元のメソッドをスキップ
            }
            catch (Exception ex)
            {
                LogHelper.LogError($"[AssetBundle] StartDownload パッチエラー: {ex}");
                return true;
            }
        }
    }
}

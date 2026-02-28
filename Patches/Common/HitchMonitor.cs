using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BakuonOfflinePatch
{
    public class HitchMonitor : MonoBehaviour
    {
        private static HitchMonitor _instance;
        private float _lastTime;
        private float _lastLogTime;
        private float _lastHitchTime;
        private float _lastHeavyOpTime;
        private static string _lastEvent;
        private static float _lastEventTime;

        private const float HitchThresholdSec = 0.05f; // 50ms
        private const float LogCooldownSec = 0.5f;
        private const float HeavyOpMinIntervalSec = 5f;
        private const float PostHitchQuietSec = 0.5f;
        private const float EventWindowSec = 10f;

        public static void Initialize()
        {
            if (_instance != null) return;
            var go = new GameObject("OfflinePatch_HitchMonitor");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<HitchMonitor>();
        }

        private void Awake()
        {
            _lastTime = Time.realtimeSinceStartup;
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;
            }
            catch { }
        }

        private void OnDestroy()
        {
            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;
            }
            catch { }
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            float dt = now - _lastTime;
            _lastTime = now;

            if (dt > HitchThresholdSec)
            {
                _lastHitchTime = now;
                if (now - _lastLogTime > LogCooldownSec)
                {
                    _lastLogTime = now;
                    LogHitch(dt);
                }
            }

            HitchGuard.Process(now, dt, ref _lastHeavyOpTime, _lastHitchTime);
        }

        private static void LogHitch(float dt)
        {
            try
            {
                var scene = SceneManager.GetActiveScene().name;
                long heapKb = GC.GetTotalMemory(false) / 1024;
                string ev = "";
                if (!string.IsNullOrEmpty(_lastEvent))
                {
                    float age = Time.realtimeSinceStartup - _lastEventTime;
                    ev = $" last={_lastEvent} age={age:F1}s";
                }
                LogHelper.LogWarning($"[Hitch] {dt * 1000f:F1} ms scene={scene} heap={heapKb}KB{ev}");
            }
            catch { }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            MarkEvent($"SceneLoaded:{scene.name}:{mode}");
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            MarkEvent($"SceneUnloaded:{scene.name}");
        }

        public static bool IsFrameLight(float dt)
        {
            return dt <= HitchThresholdSec * 0.75f;
        }

        public static float HitchThresholdSeconds => HitchThresholdSec;
        public static float PostHitchQuietSeconds => PostHitchQuietSec;
        public static float HeavyOpMinIntervalSeconds => HeavyOpMinIntervalSec;

        public static void MarkEvent(string name)
        {
            _lastEvent = name ?? "";
            _lastEventTime = Time.realtimeSinceStartup;
        }
    }

    public static class HitchGuard
    {
        private static bool _unloadPending;
        private static string _pendingReason = "";

        public static void RequestGCAndUnload(string reason)
        {
            if (!_unloadPending)
                _pendingReason = reason ?? "";
            _unloadPending = true;
        }

        public static void Process(float now, float dt, ref float lastHeavyOpTime, float lastHitchTime)
        {
            if (!_unloadPending) return;
            if (now - lastHeavyOpTime < HitchMonitor.HeavyOpMinIntervalSeconds) return;
            if (now - lastHitchTime < HitchMonitor.PostHitchQuietSeconds) return;
            if (!HitchMonitor.IsFrameLight(dt)) return;

            try
            {
                Resources.UnloadUnusedAssets();
            }
            catch (Exception ex)
            {
                LogHelper.LogWarning($"[HitchGuard] Deferred Unload failed: {ex.Message}");
            }
            finally
            {
                lastHeavyOpTime = now;
                _unloadPending = false;
                LogHelper.LogInfo($"[HitchGuard] Deferred Unload (reason={_pendingReason})");
            }
        }
    }

}

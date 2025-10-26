using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using TootTallyCore.Graphics;
using TootTallyCore.Utils.Helpers;
using TootTallyCore.Utils.TootTallyGlobals;
using UnityEngine;

namespace TootTallyURCounter
{
    public static class URCounterManager
    {
        public static float AdjustedTimingWindow { get; private set; }

        private static bool _lastIsTooting, _isSlider, _isStarted;
        private static float _trackTime, _lastTiming, _nextTiming;
        private static int _lastIndex;
        private static float _savedLatency;
        private static bool _lastReleasedToot, _releasedToot;
        private static int _timingCount;
        private static float _timingSum;
        private static float _averageTiming;
        private static int _lastSample;

        private static List<float> _noteTimingList, _tapTimingList;
        private static URCounterGraphicController _graphicController;

        [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
        [HarmonyPostfix]
        public static void OnGameControllerStart(GameController __instance)
        {
            if (TootTallyGlobalVariables.isTournamentHosting) return;

            AdjustedTimingWindow = (Plugin.Instance.TimingWindow.Value / 1000f) / TootTallyGlobalVariables.gameSpeedMultiplier;
            _isSlider = false;
            _isStarted = false;
            _lastIsTooting = false;
            _timingSum = 0;
            _timingCount = 0;
            _lastIndex = -1;
            _lastSample = 0;
            _savedLatency = __instance.latency_offset;
            _trackTime = -__instance.noteoffset - __instance.latency_offset;
            _nextTiming = __instance.leveldata.Count > 0 ? B2s(__instance.leveldata[0][0], __instance.tempo) : 0;
            _noteTimingList = new List<float>();
            _tapTimingList = new List<float>();
            _noteTimingList.Add(_nextTiming);
            _graphicController = new URCounterGraphicController(__instance.ui_score_shadow.transform.parent.parent, __instance.singlenote.transform.GetChild(3).GetComponent<LineRenderer>().material);
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.Update))]
        [HarmonyPostfix]
        public static void UpdateTrackTimer(GameController __instance)
        {
            if (!_isStarted || __instance.paused || __instance.quitting || __instance.retrying || _graphicController == null) return;

            _trackTime += Time.deltaTime * TootTallyGlobalVariables.gameSpeedMultiplier;
            if (_lastSample != __instance.musictrack.timeSamples)
            {
                _trackTime = __instance.musictrack.time - __instance.noteoffset - __instance.latency_offset;
                _lastSample = __instance.musictrack.timeSamples;
            }

            _graphicController.UpdateTimingBarAlpha();
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.isNoteButtonPressed))]
        [HarmonyPostfix]
        public static void OnButtonPressedRegisterTapTiming(GameController __instance, bool __result)
        {
            if (!_lastIsTooting && __result && ShouldRecordTap())
                RecordTapTiming(__instance);

            _lastIsTooting = __result;
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.playsong))]
        [HarmonyPostfix]
        public static void OnPlaySong() => _isStarted = true;

        public static bool ShouldRecordTap() => _noteTimingList.Count > _tapTimingList.Count && _graphicController != null;

        public static void RecordTapTiming(GameController __instance)
        {
            float msTiming;
            if (_noteTimingList.Count - 2 == _tapTimingList.Count)
            {
                msTiming = Mathf.Clamp(-(_lastTiming - _trackTime), -AdjustedTimingWindow, AdjustedTimingWindow);
                OnTapRecord(msTiming / TootTallyGlobalVariables.gameSpeedMultiplier);
            }

            //Intentionally registering 1 tap for 2 notes if they are close enough together and 1 tap was missed
            msTiming = -(_nextTiming - _trackTime);
            if (msTiming == 0)
            {
                //Plugin.LogInfo($"Tap was perfectly on time.");
                OnTapRecord(0);
            }
            else if (Mathf.Abs(msTiming) <= AdjustedTimingWindow)
            {
                //Plugin.LogInfo($"Tap was {(Math.Sign(msTiming) == 1 ? "early" : "late")} by {msTiming / TootTallyGlobalVariables.gameSpeedMultiplier}ms");
                OnTapRecord(msTiming / TootTallyGlobalVariables.gameSpeedMultiplier);
            }
        }

        public static void OnTapRecord(float tapTiming)
        {
            _tapTimingList.Add(tapTiming);
            _timingSum += tapTiming;
            _timingCount++;
            _averageTiming = _timingSum / _timingCount;

            if (_graphicController != null)
            {
                _graphicController.AddTiming(tapTiming);
                _graphicController.SetAveragePosition(_averageTiming);
            }
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.syncTrackPositions))]
        [HarmonyPostfix]
        public static void OnSyncTrack(GameController __instance)
        {
            if (__instance == null || __instance.musictrack == null || __instance.musictrack.clip == null) return;
            _trackTime = ((float)__instance.musictrack.timeSamples / __instance.musictrack.clip.frequency) - __instance.noteoffset - __instance.latency_offset;
        }

        public static float B2s(float time, float bpm) => time / bpm * 60f;

        [HarmonyPatch(typeof(GameController), nameof(GameController.getScoreAverage))]
        [HarmonyPrefix]
        public static void GetNoteScore(GameController __instance)
        {
            _lastReleasedToot = _releasedToot;
            _releasedToot = __instance.released_button_between_notes && !__instance.force_no_gap_gameobject_to_appear;
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.grabNoteRefs))]
        [HarmonyPrefix]
        public static void GetIsSlider(GameController __instance)
        {
            if (__instance.currentnoteindex + 1 >= __instance.leveldata.Count) return;
            if (!_isStarted)
            {
                _noteTimingList = new List<float>();
                _tapTimingList = new List<float>();
            }


            _isSlider = Mathf.Abs(__instance.leveldata[__instance.currentnoteindex + 1][0] - (__instance.leveldata[__instance.currentnoteindex][0] + __instance.leveldata[__instance.currentnoteindex][1])) < 0.05f;

            if (!_isSlider)
            {
                if (_noteTimingList.Count - 2 >= _tapTimingList.Count) // 2 notes buffer
                    if (!_lastReleasedToot)
                    {
                        //Plugin.LogInfo($"Tap was not released: {Mathf.Max(_lastTiming - _nextTiming, -AdjustedTimingWindow)}");
                        OnTapRecord(-AdjustedTimingWindow);
                    }
                    else
                    {
                        //Plugin.LogInfo($"Tap not registered: {Mathf.Min(_trackTime - _nextTiming, AdjustedTimingWindow)}");
                        OnTapRecord(AdjustedTimingWindow);
                    }

                _lastTiming = _nextTiming;
                _nextTiming = B2s(__instance.leveldata[__instance.currentnoteindex + 1][0], __instance.tempo);
                _noteTimingList.Add(_nextTiming);
            }
        }

        [HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
        [HarmonyPostfix]
        public static void GetNoteScore(PointSceneController __instance)
        {
            if (_tapTimingList.Count <= 1) return;
            var standardDev = GetStandardDeviation(_tapTimingList);
            var text = GameObjectFactory.CreateSingleText(__instance.fullpanel.transform, "URLabel", GetURStringText(standardDev));
            text.rectTransform.anchoredPosition = new Vector2(0, 417);
            text.fontSize = 8;
            text.fontStyle = TMPro.FontStyles.Bold;
            text.color = new Color(.1294f, .2549f, .2549f, 1f);
            text.outlineColor = new Color(0, 0, 0, 0);
        }

        private static string GetURStringText(double sd) => $"UR: {sd * 1000f:0.0}ms - MN: {_averageTiming * 1000f:0.0}ms - Timing Deviation: {(_averageTiming - sd) * 1000f:0.0}ms ~ {(_averageTiming + sd) * 1000f:0.0}ms";

        private static string GetString() => $"Average: {_averageTiming * 1000f:0.0}ms";
        private static string GetString2() => "Average:" + (_averageTiming * 1000f).ToString("0.0") + "ms";

        public static double GetStandardDeviation(List<float> values)
        {
            return Mathf.Sqrt(values.Average(value => FastPow(value - _averageTiming, 2)));
        }

        public static float FastPow(double num, int exp)
        {
            double result = 1d;
            while (exp > 0)
            {
                if (exp % 2 == 1)
                    result *= num;
                exp >>= 1;
                num *= num;
            }
            return (float)result;
        }
    }
}

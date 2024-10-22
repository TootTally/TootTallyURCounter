using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TootTallyURCounter
{
    public class URCounterGraphicController
    {
        private static GameObject _urBar, _urBarDataPrefab;
        private static LineRenderer[] _urBarDataArray;
        private static double[] _urBarAlphaMultArray;
        private static LineRenderer _urAveragePointerLineRenderer;
        private static int _currentIndex;
        private readonly Color _colorToFadeout;
        private readonly Color _startColor;
        private const int TIMING_LINE_COUNT = 25;

        public URCounterGraphicController(Transform canvas, Material material)
        {

            _urBarDataArray = new LineRenderer[TIMING_LINE_COUNT];
            _urBarAlphaMultArray = new double[TIMING_LINE_COUNT];
            _urBarDataPrefab = new GameObject("DataBarPrefab", typeof(RectTransform), typeof(LineRenderer));
            var lineRenderer = _urBarDataPrefab.GetComponent<LineRenderer>();
            lineRenderer.material = material;
            lineRenderer.numCapVertices = 5;
            lineRenderer.useWorldSpace = false;
            var rect = _urBarDataPrefab.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.anchorMin = rect.anchorMax = Vector2.one / 2f;

            _colorToFadeout = new Color(0, 0, 0, .005f);
            _startColor = new Color(1, 1, 1, .5f);
            _currentIndex = 0;
            CreateURBar(canvas);
            SetTimingBarPrefab();
            CreateTimingBars();
        }

        public void UpdateTimingBarAlpha()
        {
            for (int i = 0; i < _urBarDataArray.Length; i++)
            {
                _urBarAlphaMultArray[i] += Time.deltaTime / 10f;
                _urBarDataArray[i].startColor = _urBarDataArray[i].endColor -= (float)_urBarAlphaMultArray[i] * _colorToFadeout;
            }
        }

        private void CreateURBar(Transform canvas)
        {
            _urBar = GameObject.Instantiate(_urBarDataPrefab, canvas);
            var rect = _urBar.GetComponent<RectTransform>();
            rect.anchoredPosition = Vector2.zero;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, -.155f);
            var urLine = _urBar.GetComponent<LineRenderer>();
            urLine.widthMultiplier = .12f;
            urLine.positionCount = 7;
            urLine.colorGradient = new Gradient()
            {
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1,1,0), 0),
                    new GradientColorKey(new Color(0,1,0), .20f),
                    new GradientColorKey(new Color(0,.25f,1), .45f),
                    new GradientColorKey(new Color(0,.25f,1), .55f),
                    new GradientColorKey(new Color(0,1,0), .80f),
                    new GradientColorKey(new Color(1,1,0), 1f),
                },
                mode = GradientMode.Blend
            };
            urLine.SetPositions(new Vector3[]
            {
                new Vector2(-125, 0),
                new Vector2(-100, 0),
                new Vector2(-56, 0),
                new Vector2(0, 0),
                new Vector2(56, 0),
                new Vector2(100, 0),
                new Vector2(125, 0),
            });
            var middleLine = GameObject.Instantiate(_urBarDataPrefab, _urBar.transform);
            var middleLineRenderer = middleLine.GetComponent<LineRenderer>();
            middleLineRenderer.widthMultiplier = .05f;
            middleLineRenderer.startColor = middleLineRenderer.endColor = new Color(1, 1, 1, .8f);
            middleLineRenderer.SetPositions(new Vector3[]
            {
                new Vector2(0,-7),
                new Vector2(0,7)
            });

            _urAveragePointerLineRenderer = GameObject.Instantiate(_urBarDataPrefab, _urBar.transform).GetComponent<LineRenderer>();
            _urAveragePointerLineRenderer.numCapVertices = 2;
            _urAveragePointerLineRenderer.numCornerVertices = 2;
            _urAveragePointerLineRenderer.widthMultiplier = .12f;
            _urAveragePointerLineRenderer.startColor = _urAveragePointerLineRenderer.endColor = new Color(1, 1, 1, 1);
            _urAveragePointerLineRenderer.endWidth = 0;
            _urAveragePointerLineRenderer.SetPositions(new Vector3[]
            {
                new Vector2(0,10),
                new Vector2(0,7)
            });
        }

        private void SetTimingBarPrefab()
        {
            var lineRenderer = _urBarDataPrefab.GetComponent<LineRenderer>();
            lineRenderer.widthMultiplier = .05f;
            lineRenderer.startColor = lineRenderer.endColor = _startColor;
            _urBarDataPrefab.GetComponent<LineRenderer>().enabled = false;
        }

        public void AddTiming(float tapTiming)
        {
            if (_urBarDataPrefab == null || _urBarDataArray == null || _urBar == null) return;

            _urBarAlphaMultArray[_currentIndex] = 0f;
            var urBarData = _urBarDataArray[_currentIndex++];
            urBarData.enabled = true;
            urBarData.startColor = urBarData.endColor = _startColor;
            var posX = TimingToPosX(tapTiming);
            urBarData.SetPositions(new Vector3[]
            {
                new Vector2(posX,-5),
                new Vector2(posX,5)
            });
            if (_currentIndex >= _urBarDataArray.Length) _currentIndex = 0;
        }

        public void SetAveragePosition(float averageTiming)
        {
            var posX = TimingToPosX(averageTiming);
            _urAveragePointerLineRenderer.SetPositions(new Vector3[]
            {
                new Vector2(posX,10),
                new Vector2(posX,7)
            });
        }

        public void CreateTimingBars()
        {
            for (int i = 0; i < TIMING_LINE_COUNT; i++)
                _urBarDataArray[i] = (GameObject.Instantiate(_urBarDataPrefab, _urBar.transform).GetComponent<LineRenderer>());
        }

        public float TimingToPosX(float timing) => (timing / URCounterManager.AdjustedTimingWindow) * -120f;

    }
}

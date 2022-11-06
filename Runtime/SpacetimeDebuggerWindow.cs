using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngineX;
/*
namespace CCC.Debug
{
    internal class SpaceTimeDebuggerWindow : IDisposable
    {
        [System.Serializable]
        public class ModelSpaceTimeDebugger
        {
            public bool FixedHorizontalSpacing;

            public List<ModelClock> AvailableClocks = new List<ModelClock>();
            public ModelClock DisplayedClock;

            public List<ModelStream> AvailableStreams = new List<ModelStream>();
            public List<ModelStream> DisplayedStreams = new List<ModelStream>();
        }

        [System.Serializable]
        public class ModelClock
        {
            public SpacetimeDebugger.ClockImpl ClockImpl;
        }

        [System.Serializable]
        public class ModelStream
        {
            public SpacetimeDebugger.StreamImpl StreamImpl;
            public bool GlobalVerticalSpace;
            public bool DisplaySettings;
        }

        [Updater.StaticUpdateMethod(UpdateType.Update)]
        private static void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F3))
            {
                if (s_windowInstance != null)
                {
                    s_windowInstance.Dispose();
                    s_windowInstance = null;
                }
                else
                {
                    s_windowInstance = new SpaceTimeDebuggerWindow();
                }
            }
        }

        private static GUIStyle s_graphBoxStyle;
        private static bool s_initStyle = false;
        private static SpaceTimeDebuggerWindow s_windowInstance;

        private const float GRAPH_MARGIN_LEFT = 60;
        private const float GRAPH_MARGIN_RIGHT = 0;
        private const float GRAPH_MARGIN_UP = 0;
        private const float GRAPH_MARGIN_DOWN = 20;
        private const float MINMAX_GRAPH_PADDING = 2f;
        private const float INSPECTOR_WIDTH = 70;

        //private ModelSpaceTimeDebugger _model;

        private GraphDrawer _graphDrawer = new GraphDrawer();
        private Rect _graphContainerRect;
        private Rect _graphRect;
        private List<ViewElementStreamSettings> _viewDisplayedStreamSettings = new List<ViewElementStreamSettings>();
        private ModelSpaceTimeDebugger _model;
        private DropdownButton _currentClockButton = null;
        private GUIContent _minXLabel = new GUIContent();
        private GUIContent _maxXLabel = new GUIContent();
        private GUIContent _minYLabel = new GUIContent();
        private GUIContent _maxYLabel = new GUIContent();
        private IWindow _window;

        public SpaceTimeDebuggerWindow()
        {
            _model = new ModelSpaceTimeDebugger();

            //_graphDrawer.AutoZoomPadding = new Vector2(GRAPH_PADDING, GRAPH_PADDING);
            _currentClockButton = new DropdownButton("Clock", _model.AvailableClocks.Select(c => c.ClockImpl.Name).ToArray());
            _currentClockButton.ItemPickedCallback = OnClockPicked;

            _window = WindowManager.CreateWindow(WindowSettings.Default("Space Time Debugger", new Vector2(450, 200)), Draw);
        }

        public void Dispose()
        {
            WindowManager.DestroyWindow(_window);
        }

        private void OnClockPicked(int i)
        {
            _model.DisplayedClock = _model.AvailableClocks[i];
        }

        private void Draw(IWindow window)
        {
            UpdateModel();

            if (!s_initStyle)
            {
                InitStyle();
            }

            using (new GUILayout.HorizontalScope())
            {
                UpdateCurves();

                GUILayout.Box(GUIContent.none, s_graphBoxStyle, GUILayout.ExpandHeight(true));

                if (Event.current.type == EventType.Repaint)
                {
                    _graphContainerRect = GUILayoutUtility.GetLastRect();

                    _graphRect = _graphContainerRect;
                    _graphRect.x += GRAPH_MARGIN_LEFT;
                    _graphRect.width -= GRAPH_MARGIN_RIGHT + GRAPH_MARGIN_LEFT;
                    _graphRect.y += GRAPH_MARGIN_UP;
                    _graphRect.height -= GRAPH_MARGIN_DOWN + GRAPH_MARGIN_UP;
                }

                if (Event.current.type == EventType.Repaint)
                {
                    // drag background
                    GUI.Box(_graphRect, GUIContent.none, s_graphBoxStyle);

                    // render graph in reverse since GUI's vertical axis is inverted
                    var invertedGraphRect = _graphRect;
                    invertedGraphRect.y += _graphRect.height;
                    invertedGraphRect.height = -_graphRect.height;

                    _graphDrawer.ScreenDisplayRect = invertedGraphRect;
                    _graphDrawer.Draw();
                }


                DrawGraphMinMax();

                using (new GUILayout.VerticalScope(GUILayout.Width(INSPECTOR_WIDTH)))
                {
                    _currentClockButton.CurrentItemIndex = _model.AvailableClocks.IndexOf(_model.DisplayedClock);
                    _currentClockButton.Draw(window);

                    _model.FixedHorizontalSpacing = GUILayout.Toggle(_model.FixedHorizontalSpacing, "Fixed Horizontal Spacing");

                    GUILayout.Label($"Displayed Streams");


                    for (int i = 0; i < _model.DisplayedStreams.Count; i++)
                    {
                        if (i >= _viewDisplayedStreamSettings.Count)
                            _viewDisplayedStreamSettings.Add(new ViewElementStreamSettings());

                        _viewDisplayedStreamSettings[i].Draw(_model, _model.DisplayedStreams[i], window);
                    }
                }
            }
        }

        private void UpdateModel()
        {
            _model.AvailableClocks.Clear();
            _model.AvailableStreams.Clear();
            _model.DisplayedStreams.Clear();

            int i = 0;
            foreach (var item in SpacetimeDebugger.Clocks.Values)
            {
                if (i >= _model.AvailableClocks.Count)
                    _model.AvailableClocks.Add(new ModelClock());

                _model.AvailableClocks[i].ClockImpl = item;
                i++;
            }

            i = 0;
            foreach (var item in SpacetimeDebugger.Streams.Values)
            {
                if (i >= _model.AvailableStreams.Count)
                    _model.AvailableStreams.Add(new ModelStream());

                _model.AvailableStreams[i].StreamImpl = item;
                i++;
            }

            _model.DisplayedStreams.AddRange(_model.AvailableStreams);
            _model.DisplayedClock = _model.AvailableClocks.Count > 0 ? _model.AvailableClocks[0] : null;
        }

        private void DrawGraphMinMax()
        {
            Rect valueRect = _graphDrawer.ValueDisplayRect;

            _minXLabel.text = valueRect.xMin.ToString("N1");
            _maxXLabel.text = valueRect.xMax.ToString("N1");
            _minYLabel.text = valueRect.yMin.ToString();
            _maxYLabel.text = valueRect.yMax.ToString();

            Vector2 minXLabelSize = GUI.skin.label.CalcSize(_minXLabel);
            Vector2 maxXLabelSize = GUI.skin.label.CalcSize(_maxXLabel);
            Vector2 minYLabelSize = GUI.skin.label.CalcSize(_minYLabel);
            Vector2 maxYLabelSize = GUI.skin.label.CalcSize(_maxYLabel);

            Rect minXRect = new Rect(new Vector2(_graphRect.xMin, _graphRect.yMax + MINMAX_GRAPH_PADDING), minXLabelSize);
            Rect maxXRect = new Rect(new Vector2(_graphRect.xMax - maxXLabelSize.x, _graphRect.yMax + MINMAX_GRAPH_PADDING), maxXLabelSize);

            Rect minYRect = new Rect(new Vector2(_graphRect.xMin - minYLabelSize.x - MINMAX_GRAPH_PADDING, _graphRect.yMax - minYLabelSize.y), minYLabelSize);
            Rect maxYRect = new Rect(new Vector2(_graphRect.xMin - maxYLabelSize.x - MINMAX_GRAPH_PADDING, _graphRect.yMin), maxYLabelSize);

            GUI.Label(minXRect, _minXLabel);
            GUI.Label(maxXRect, _maxXLabel);
            GUI.Label(minYRect, _minYLabel);
            GUI.Label(maxYRect, _maxYLabel);
        }

        private void UpdateCurves()
        {
            double timeEnd = SpacetimeDebugger.Stopwatch.Elapsed.TotalSeconds;
            double timeBegin = timeEnd - 3f;
            float min = float.MaxValue;
            float max = float.MinValue;

            int curveIndex = 0;

            // update from streams
            for (int i = 0; i < _model.DisplayedStreams.Count; i++)
            {
                UpdateCurveFromStream(_model.DisplayedStreams[curveIndex], getCurve(), timeBegin, timeEnd, ref min, ref max);
            }

            if (_model.DisplayedStreams.Count == 0)
            {
                min = max = 0f;
            }

            // update from clock
            if (_model.DisplayedClock != null)
            {
                UpdateCurveFromClock(_model.DisplayedClock, getCurve(), timeBegin, timeEnd, min, max);
            }

            // update from seconds (baked into the graph)
            {
                List<double> seconds = ListPool<double>.Take();
                for (int i = (int)Math.Ceiling(timeBegin); i < timeEnd; i++)
                {
                    seconds.Add(i);
                }
                UpdateCurveFromValues(Color.white, getCurve(), min, max, seconds);
                ListPool<double>.Release(seconds);
            }

            // update from 60 tps
            {
                List<double> ticks = ListPool<double>.Take();
                double t = Math.Floor(timeBegin);
                while (t < timeEnd)
                {
                    if (t > timeBegin)
                        ticks.Add(t);
                    t += 1d / 60d;
                }
                UpdateCurveFromValues(Color.gray, getCurve(), min, max, ticks);
                ListPool<double>.Release(ticks);
            }

            for (int r = _graphDrawer.Curves.Count - 1; r >= curveIndex; r--)
            {
                _graphDrawer.Curves.RemoveAt(r);
            }

            GraphDrawer.Curve getCurve()
            {
                if (curveIndex >= _graphDrawer.Curves.Count)
                    _graphDrawer.Curves.Add(new GraphDrawer.Curve());

                return _graphDrawer.Curves[curveIndex++];
            }
        }

        public void UpdateCurveFromClock(ModelClock modelClock, GraphDrawer.Curve curve, double timeBegin, double timeEnd, float min, float max)
        {
            var timeList = ListPool<double>.Take();
            modelClock.ClockImpl.GetValues(timeBegin, timeEnd, timeList);
            UpdateCurveFromValues(modelClock.ClockImpl.Color, curve, min, max, timeList);

            ListPool<double>.Release(timeList);
        }

        public void UpdateCurveFromStream(ModelStream modelStream, GraphDrawer.Curve curve, double timeBegin, double timeEnd, ref float min, ref float max)
        {
            var timeValueList = ListPool<(double, float)>.Take();

            modelStream.StreamImpl.GetValues(timeBegin, timeEnd, timeValueList);

            Color c = modelStream.StreamImpl.Color;
            UpdateCurveFromValues(c, curve, ref min, ref max, timeValueList);

            ListPool<(double, float)>.Release(timeValueList);
        }

        private static void UpdateCurveFromValues(Color color, GraphDrawer.Curve curve, float min, float max, List<double> timeList)
        {
            curve.Color = color;
            curve.Positions.Clear();
            foreach (double time in timeList)
            {
                float t = (float)time;
                curve.Positions.Add(new Vector2(t, min));
                curve.Positions.Add(new Vector2(t, max));
                curve.Positions.Add(new Vector2(t, min));
            }
        }

        private static void UpdateCurveFromValues(Color color, GraphDrawer.Curve curve, ref float min, ref float max, List<(double, float)> timeValueList)
        {
            curve.Color = color;
            curve.Positions.Clear();
            foreach ((double time, float value) in timeValueList)
            {
                min = Mathf.Min(min, value);
                max = Mathf.Max(max, value);
                curve.Positions.Add(new Vector2((float)time, value));
            }
        }

        private static void InitStyle()
        {
            s_initStyle = true;
            s_graphBoxStyle = new GUIStyle(GUI.skin.box);
            s_graphBoxStyle.padding.left = 0;
            s_graphBoxStyle.padding.top = 0;
            s_graphBoxStyle.padding.bottom = 0;
            s_graphBoxStyle.padding.right = 0;
            s_graphBoxStyle.margin.left = 0;
            s_graphBoxStyle.margin.top = 0;
            s_graphBoxStyle.margin.bottom = 0;
            s_graphBoxStyle.margin.right = 0;
        }

        public class ViewElementStreamSettings
        {
            private DropdownButton _verticalSpaceDropdownButton;

            private static readonly GUIContent s_settingsButtonOn = new GUIContent("v");
            private static readonly GUIContent s_settingsButtonOff = new GUIContent(">");

            private static readonly GUIContent s_removeButtonContent = new GUIContent("x");

            private static readonly string[] s_verticalSpaceOptions = new string[] { "Global", "Local" };
            private static readonly GUIContent s_verticalSpaceLabel = new GUIContent("Vertical Space");

            public ViewElementStreamSettings()
            {
                _verticalSpaceDropdownButton = new DropdownButton(s_verticalSpaceLabel, s_verticalSpaceOptions);
            }

            public void Draw(ModelSpaceTimeDebugger modelSpaceTimeDebugger, ModelStream modelStream, IWindow window)
            {
                GUILayout.BeginHorizontal();

                if (GUILayout.Button(modelStream.DisplaySettings ? s_settingsButtonOn : s_settingsButtonOff, GUILayout.Width(18)))
                {
                    modelStream.DisplaySettings = !modelStream.DisplaySettings;
                }

                GUILayout.Label(modelStream.StreamImpl.Name);

                if (GUILayout.Button(s_removeButtonContent, GUILayout.Width(18)))
                {
                    modelSpaceTimeDebugger.DisplayedStreams.Remove(modelStream);
                }

                GUILayout.EndHorizontal();

                if (modelStream.DisplaySettings)
                {
                    _verticalSpaceDropdownButton.Draw(window);
                }
            }
        }
    }
}

*/
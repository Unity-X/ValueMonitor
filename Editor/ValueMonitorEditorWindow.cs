using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System;
using UnityEngine.PlayerLoop;

namespace UnityX.ValueMonitor.Editor
{

    public class ValueMonitorEditorWindow : EditorWindow
    {
#if UNITY_X_VALUE_MONITOR
        internal class Resources
        {
            public VisualTreeAsset StreamAsset;
            public VisualTreeAsset ClockAsset;
            public VisualTreeAsset RootAsset;
            public StyleSheet StyleAsset;
            public VisualTreeAsset LogAsset;
        }

        private Resources _resources;
        private ToolbarMenu _clockMenu;
        private ListView _streamsList;
        private uint _lastMonitorVersion;
        private Action<DropdownMenuAction> _clocksMenuClickCallback;
        private Func<DropdownMenuAction, DropdownMenuAction.Status> _clocksMenuEnabledCallback;
        private Toggle _interpolateValueToggle;
        private ListView _logList;
        private List<Monitor.RecordedLog> _frameLogs = new List<Monitor.RecordedLog>();
        private TextField _logDetails;
        private GraphElement _graphElement;
        private Toggle _followToggle;

        [MenuItem("Window/Analysis/Value Monitor")]
        public static void ShowValueMonitor()
        {
            ValueMonitorEditorWindow wnd = GetWindow<ValueMonitorEditorWindow>();
            wnd.titleContent = new GUIContent("Value Monitor");
        }

        public void CreateGUI()
        {
            _lastMonitorVersion = uint.MaxValue;
            _clocksMenuClickCallback = OnClocksMenuClicked;
            _clocksMenuEnabledCallback = IsClockMenuItemEnabled;
            _resources = new Resources()
            {
                RootAsset = EditorResources.LoadValueMonitorWindowVisualTreeAsset(),
                StreamAsset = EditorResources.LoadValueMonitorWindowStreamVisualTreeAsset(),
                ClockAsset = EditorResources.LoadValueMonitorWindowClockVisualTreeAsset(),
                StyleAsset = EditorResources.LoadValueMonitorWindowStyleSheetAsset(),
                LogAsset = EditorResources.LoadValueMonitorWindowLogVisualTreeAsset(),
            };

            VisualElement root = rootVisualElement;

            // Import UXML and stylesheet
            _resources.RootAsset.CloneTree(root);
            root.styleSheets.Add(_resources.StyleAsset);

            _clockMenu = root.Q<ToolbarMenu>("Clocks");
            _streamsList = root.Q<ListView>("StreamsList");
            _interpolateValueToggle = root.Q<Toggle>("InterpolateValueToggle");
            _logList = root.Q<ListView>("Logs");
            _logDetails = root.Q<TextField>("LogDetails");
            _followToggle = root.Q<Toggle>("FollowToggle");

            VisualElement graphFocusPoint = root.Q("GraphFocusPoint");
            VisualElement graphFocusFrame = root.Q("GraphFocusFrame");
            VisualElement graphSelectedTimeSpan = root.Q("GraphSelectedTimeSpan");
            VisualElement graphSelectedFrame = root.Q("GraphSelectedFrame");
            VisualElement graphLabelContainer = root.Q("GraphLabelContainer");

            _graphElement = new GraphElement(graphFocusPoint, graphFocusFrame, graphLabelContainer, graphSelectedTimeSpan);
            _graphElement.name = "graph";
            _graphElement.CameraMovedManually += OnGraphCameraMoved;
            _graphElement.TimeSpanSelected += OnGraphTimeSpanSelected;
            _graphElement.TimeSpanDeselected += OnGraphTimeSpanDeselected;
            root.Q("GraphContainer").Add(_graphElement);

            _streamsList.itemsAdded += OnStreamsAdded;
            _streamsList.itemsSource = ValueMonitorWindowState.instance.StreamSettings;
            _streamsList.makeItem = () => new StreamElement(_resources);
            _streamsList.bindItem = (VisualElement visual, int index) =>
            {
                var data = ValueMonitorWindowState.instance.StreamSettings[index];
                if (data == null)
                {
                    data = new ValueMonitorWindowState.StreamSetting();
                    ValueMonitorWindowState.instance.StreamSettings[index] = data;
                    ValueMonitorWindowState.instance.Save();
                }
                ((StreamElement)visual).Bind(data);
            };

            _interpolateValueToggle.SetValueWithoutNotify(ValueMonitorWindowState.instance.InterpolateValues);
            _interpolateValueToggle.RegisterValueChangedCallback((ChangeEvent<bool> evnt) =>
            {
                ValueMonitorWindowState.instance.InterpolateValues = evnt.newValue;
                ValueMonitorWindowState.instance.Save();
                _graphElement.MarkDirtyRepaint();
            });

            _logList.selectedIndicesChanged += OnSelectedLogsIndicesChanged;
            _logList.makeItem = () => new LogElement(_resources);
            _logList.itemsSource = _frameLogs;
            _logList.bindItem = (VisualElement visual, int logIndex) => (visual as LogElement).Bind(_frameLogs[logIndex]);

            _followToggle.value = ValueMonitorWindowState.instance.FollowLatestValuesInGraph;
            _followToggle.RegisterValueChangedCallback((ChangeEvent<bool> evnt) =>
            {
                ValueMonitorWindowState.instance.FollowLatestValuesInGraph = evnt.newValue;
                ValueMonitorWindowState.instance.Save();
            });

            OnGraphTimeSpanDeselected(); // simulate selecting nothing
        }

        private void OnGraphCameraMoved()
        {
            _followToggle.value = false;
        }

        private void OnSelectedLogsIndicesChanged(IEnumerable<int> logsIndices)
        {
            if (_logList.selectedItem is Monitor.RecordedLog log)
            {
                _logDetails.SetValueWithoutNotify($"{log.Condition}\n{log.StackTrace}");
                _logDetails.visible = true;
            }
            else
            {
                _logDetails.SetValueWithoutNotify(string.Empty);
                _logDetails.visible = false;
            }
        }

        private void OnGraphTimeSpanSelected(double stopwatchTimeBegin, double stopwatchTimeEnd)
        {
            Monitor.ClockImpl activeClock = GetActiveClock();
            double clockTimeBegin = StopwatchTimeToClockTime(activeClock, stopwatchTimeBegin);
            double clockTimeEnd = StopwatchTimeToClockTime(activeClock, stopwatchTimeEnd);
            _logList.headerTitle = $"Logs from {clockTimeBegin} to {clockTimeEnd}";

            _frameLogs.Clear();
            Monitor.GetRecordedLogs(stopwatchTimeBegin, stopwatchTimeEnd, _frameLogs);
            _logList.RefreshItems();
        }

        private void OnGraphTimeSpanDeselected()
        {
            _logList.headerTitle = "Logs";
            _frameLogs.Clear();
            _logList.RefreshItems();
            _logDetails.visible = false;
        }

        private void Update()
        {
            if (_lastMonitorVersion != Monitor.Version)
            {
                _lastMonitorVersion = Monitor.Version;
                Repaint();

                UpdateClocksMenu();
            }
        }

        private void UpdateClocksMenu()
        {
            var menuItems = _clockMenu.menu.MenuItems();

            menuItems.Add(new DropdownMenuAction("No Clock (Unscaled Time)", _clocksMenuClickCallback, _clocksMenuEnabledCallback, userData: null));

            foreach (var clock in Monitor.Clocks.Values)
            {
                string displayName = clock.DisplayName;
                if (string.IsNullOrEmpty(displayName))
                {
                    displayName = clock.Id;
                }
                menuItems.Add(new DropdownMenuAction(displayName, _clocksMenuClickCallback, _clocksMenuEnabledCallback, userData: clock.Id));
            }

            // update menu text
            {
                string preferredClockId = ValueMonitorWindowState.instance.PreferredClockId;
                string preferredClockName;

                if (string.IsNullOrEmpty(preferredClockId))
                {
                    preferredClockName = "No Clock (Unscaled Time)";
                }
                else
                {
                    if (Monitor.Clocks.TryGetValue(preferredClockId, out var clock))
                    {
                        if (!string.IsNullOrEmpty(clock.DisplayName))
                        {
                            preferredClockName = clock.DisplayName;
                        }
                        else
                        {
                            preferredClockName = clock.Id;
                        }
                    }
                    else
                    {
                        preferredClockName = $"Unscaled Time (<i>{preferredClockId}</i> not found)";
                    }
                }

                _clockMenu.text = preferredClockName; // unscale time, valid clock id, valid clock name, invalid clock id
            }
        }

        private DropdownMenuAction.Status IsClockMenuItemEnabled(DropdownMenuAction menuAction)
        {
            if (string.IsNullOrEmpty(ValueMonitorWindowState.instance.PreferredClockId) && menuAction.userData == null)
                return DropdownMenuAction.Status.Checked;

            return ValueMonitorWindowState.instance.PreferredClockId == (string)menuAction.userData
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal;
        }

        private void OnClocksMenuClicked(DropdownMenuAction menuAction)
        {
            ValueMonitorWindowState.instance.PreferredClockId = menuAction.userData as string;
            ValueMonitorWindowState.instance.Save();
            UpdateClocksMenu();

            // force update logs display
            if (_graphElement.IsTimeSpanSelected)
                OnGraphTimeSpanSelected(_graphElement.SelectedTimeSpanStopwatchStart, _graphElement.SelectedTimeSpanStopwatchEnd);
        }

        private void OnStreamsAdded(IEnumerable<int> streamIndices)
        {
            foreach (var streamIndex in streamIndices)
            {
                GenericMenu genericMenu = new GenericMenu();
                foreach (var item in Monitor.Streams.Keys)
                {
                    genericMenu.AddItem(new GUIContent(item), false, () =>
                    {
                        ValueMonitorWindowState.instance.StreamSettings[streamIndex].Id = item;
                        ValueMonitorWindowState.instance.Save();
                        _streamsList.RefreshItem(streamIndex);
                    });
                }

                Rect pos = new Rect(_streamsList.worldBound);
                pos.x = pos.xMax - 50;
                genericMenu.DropDown(pos);
            }
        }

        private static double ClockTimeToStopwatchTime(Monitor.ClockImpl clock, double clockTime)
        {
            if (clock == null || clock.ClockTimes.Count < 2)
                return clockTime;
            else
                return clock.GetInterpolatedStopwatchTime(clockTime);
        }

        private static double StopwatchTimeToClockTime(Monitor.ClockImpl clock, double stopwatchTime)
        {
            if (clock == null || clock.ClockTimes.Count < 2)
                return stopwatchTime;
            else
                return clock.GetInterpolatedClockTime(stopwatchTime);
        }

        private static Monitor.ClockImpl GetActiveClock()
        {
            Monitor.ClockImpl activeClock;
            string clockId = ValueMonitorWindowState.instance.PreferredClockId;
            if (string.IsNullOrEmpty(clockId) || !Monitor.Clocks.TryGetValue(clockId, out activeClock))
            {
                activeClock = null;
            }
            return activeClock;
        }

        internal class StreamElement : VisualElement
        {
            private Label _viewLabel;
            private Toggle _viewVisibility;
            private VisualElement _checkmark;
            private ValueMonitorWindowState.StreamSetting _setting;

            public StreamElement(Resources resources)
            {
                resources.StreamAsset.CloneTree(this);

                _viewLabel = this.Q<Label>("Label");
                _viewVisibility = this.Q<Toggle>("Visibility");
                _checkmark = this.Q<VisualElement>("unity-checkmark");

                _viewVisibility.RegisterValueChangedCallback(OnVisibilityChanged);
            }

            public void Bind(ValueMonitorWindowState.StreamSetting setting)
            {
                _setting = setting;

                bool isUnsetElement = string.IsNullOrEmpty(_setting.Id);
                Monitor.StreamImpl streamData = null;
                bool hasStreamData = !isUnsetElement && Monitor.Streams.TryGetValue(_setting.Id, out streamData);
                string labelText = hasStreamData ? streamData.DisplayName : _setting.Id;
                if (string.IsNullOrEmpty(labelText))
                    labelText = "<i>Unset</i>";
                _viewLabel.text = labelText;
                _viewVisibility.SetValueWithoutNotify(_setting.Visible);
                _checkmark.style.unityBackgroundImageTintColor = hasStreamData ? Color.Lerp(Color.white, streamData.Color, 0.7f) : Color.white;
            }

            private void OnVisibilityChanged(ChangeEvent<bool> evt)
            {
                _setting.Visible = evt.newValue;
            }
        }

        internal class LogElement : VisualElement
        {
            private Label _viewLabel;
            private VisualElement _viewIcon;

            public LogElement(Resources resources)
            {
                resources.LogAsset.CloneTree(this);

                _viewLabel = this.Q<Label>();
                _viewIcon = this.Q<VisualElement>("icon");
            }

            public void Bind(Monitor.RecordedLog log)
            {
                var activeClock = GetActiveClock();
                var clockTime = StopwatchTimeToClockTime(activeClock, log.StopwatchTime);
                _viewLabel.text = $"[{clockTime}] {log.Condition}";

                GUIContent guiContent = log.Type switch
                {
                    LogType.Error or LogType.Assert or LogType.Exception => EditorGUIUtility.TrTextContentWithIcon("valuemonitor console error", MessageType.Error),
                    LogType.Warning => EditorGUIUtility.TrTextContentWithIcon("valuemonitor console warning", MessageType.Warning),
                    _ => EditorGUIUtility.TrTextContentWithIcon("valuemonitor console info", MessageType.Info),
                };
                Texture2D iconTexture = guiContent.image as Texture2D;
                _viewIcon.style.backgroundImage = iconTexture;
            }
        }

        public class GraphElement : ImmediateModeElement
        {
            public class MoveGraphDragManipulator : PointerManipulator
            {
                private bool _dragStarted;
                private GraphElement _graph;

                public MoveGraphDragManipulator(GraphElement target)
                {
                    this.target = target;
                    _graph = target;
                }

                protected override void RegisterCallbacksOnTarget()
                {
                    target.RegisterCallback<PointerDownEvent>(HandlePointerDown);
                    target.RegisterCallback<PointerMoveEvent>(HandlePointerMove);
                    target.RegisterCallback<PointerUpEvent>(HandlePointerUp);
                    target.RegisterCallback<PointerCaptureOutEvent>(HandlePointerCaptureOut);
                }

                protected override void UnregisterCallbacksFromTarget()
                {
                    target.UnregisterCallback<PointerDownEvent>(HandlePointerDown);
                    target.UnregisterCallback<PointerMoveEvent>(HandlePointerMove);
                    target.UnregisterCallback<PointerUpEvent>(HandlePointerUp);
                    target.UnregisterCallback<PointerCaptureOutEvent>(HandlePointerCaptureOut);
                }

                private void HandlePointerDown(PointerDownEvent evt)
                {
                    if (evt.button == 2) // middle mouse button
                    {
                        target.CapturePointer(evt.pointerId);
                        _dragStarted = true;
                    }
                }

                private void HandlePointerMove(PointerMoveEvent evt)
                {
                    if (_dragStarted && target.HasPointerCapture(evt.pointerId))
                    {
                        _graph._graphDrawer.ValueDisplayRect.x -= _graph._graphDrawer.ScreenToValueVector(Vector2.right * evt.deltaPosition.x).x;
                        _graph.CameraMovedManually?.Invoke();
                        _graph.MarkDirtyRepaint();
                    }
                }

                private void HandlePointerUp(PointerUpEvent evt)
                {
                    if (_dragStarted && target.HasPointerCapture(evt.pointerId))
                    {
                        target.ReleasePointer(evt.pointerId);
                    }
                }

                private void HandlePointerCaptureOut(PointerCaptureOutEvent evt)
                {
                    _dragStarted = false;
                }
            }

            public class SelectTimeSpanDragManipulator : PointerManipulator
            {
                private bool _dragStarted;
                private GraphElement _graph;

                public SelectTimeSpanDragManipulator(GraphElement target)
                {
                    _graph = target;
                    this.target = target;
                }

                protected override void RegisterCallbacksOnTarget()
                {
                    target.RegisterCallback<PointerDownEvent>(HandlePointerDown);
                    target.RegisterCallback<PointerMoveEvent>(HandlePointerMove);
                    target.RegisterCallback<PointerUpEvent>(HandlePointerUp);
                    target.RegisterCallback<PointerCaptureOutEvent>(HandlePointerCaptureOut);
                    _graph.CameraAutomaticallyFollowed += OnCameraAutomaticallyMovedFromFollow;
                }

                protected override void UnregisterCallbacksFromTarget()
                {
                    target.UnregisterCallback<PointerDownEvent>(HandlePointerDown);
                    target.UnregisterCallback<PointerMoveEvent>(HandlePointerMove);
                    target.UnregisterCallback<PointerUpEvent>(HandlePointerUp);
                    target.UnregisterCallback<PointerCaptureOutEvent>(HandlePointerCaptureOut);
                    _graph.CameraAutomaticallyFollowed -= OnCameraAutomaticallyMovedFromFollow;
                }

                private void HandlePointerDown(PointerDownEvent evt)
                {
                    if (evt.button == 0) // left mouse button
                    {
                        target.CapturePointer(evt.pointerId);
                        _dragStarted = true;
                        _graph.OnGraphStartDrag(evt.localPosition);
                    }
                }

                private void HandlePointerMove(PointerMoveEvent evt)
                {
                    if (_dragStarted)
                        _graph.OnGraphContinueDrag(evt.localPosition);
                }

                private void OnCameraAutomaticallyMovedFromFollow()
                {
                    if (_dragStarted)
                        _graph.OnGraphContinueDrag(_graph._lastMouseScreenPosition.Value);
                }

                private void HandlePointerUp(PointerUpEvent evt)
                {
                    if (_dragStarted && target.HasPointerCapture(evt.pointerId))
                    {
                        target.ReleasePointer(evt.pointerId);
                    }

                    _dragStarted = false;
                }

                private void HandlePointerCaptureOut(PointerCaptureOutEvent evt)
                {
                    _dragStarted = false;
                }
            }

            public static class Style
            {
                public const string CLASS_GRID_LABEL = "grid-label";
                public const string CLASS_POINTED_TIME_LABEL = "pointed-time";
                public const string CLASS_STREAM_HOVER_LABEL_VALUE = "stream-hover-value";
                public const string CLASS_STREAM_HOVER_LABEL_NAME = "stream-hover-name";
                public const string CLASS_STREAM_HOVER_LABEL_TIME = "stream-hover-time";
                public static readonly Color GridAxisColor = new Color(1, 1, 1);
                public static readonly Color GridColor = new Color(0.6f, 0.6f, 0.6f);
                public static readonly Color GridMiniColor = new Color(0.35f, 0.35f, 0.35f);
                public const float GRID_CELL_PER_PIXEL = 1 / 200f;
                public const float CURVES_PADDING_VERTICAL = 40f;
                public const float STREAM_HOVER_DIST_MIN = 25f;
                public const float CURRENT_TIME_VERTICAL_PADDING = 26f;
            }
            private class LabelBank
            {
                public int NextUsedIndex = 0;
                public List<Label> Labels = new List<Label>();
            }

            private GLGraphDrawer _graphDrawer;

            private Vector2? _lastMouseScreenPosition;
            private Vector2? _lastMouseValuePosition;
            private readonly VisualElement _focusPointElement;
            private readonly VisualElement _focusFrameElement;
            private readonly VisualElement _labelContainerElement;
            private readonly VisualElement _selectedTimeSpan;
            private double _selectedTimeSpanStopwatchStart;
            private double _selectedTimeSpanStopwatchEnd;
            private bool _isTimeSpanSelected = false;
            private double? _forceCurrentPointedTime;
            private Color? _forceCurrentPointedTimeColor;
            private Dictionary<string, LabelBank> _availableLabels = new Dictionary<string, LabelBank>();
            private Vector2 _startDragValuePos;
            private Vector2 _endDragValuePos;

            public event Action<double, double> TimeSpanSelected;
            public event Action TimeSpanDeselected;
            public event Action CameraMovedManually;
            public event Action CameraAutomaticallyFollowed;

            public double SelectedTimeSpanStopwatchStart { get => _selectedTimeSpanStopwatchStart; }
            public double SelectedTimeSpanStopwatchEnd { get => _selectedTimeSpanStopwatchEnd; }
            public bool IsTimeSpanSelected { get => _isTimeSpanSelected; }

            public GraphElement(VisualElement focusPointElement, VisualElement focusFrameElement, VisualElement labelContainer, VisualElement selectedTimeSpan)
            {
                InitializeGraph();
                new MoveGraphDragManipulator(this);
                new SelectTimeSpanDragManipulator(this);
                RegisterCallback<MouseMoveEvent>(OnMouseMove);
                RegisterCallback<WheelEvent>(OnScrollWheel);
                RegisterCallback<MouseMoveEvent>(OnMouseMove);
                RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
                _focusPointElement = focusPointElement;
                _focusFrameElement = focusFrameElement;
                _labelContainerElement = labelContainer;
                _selectedTimeSpan = selectedTimeSpan;
            }

            private void OnMouseLeave(MouseLeaveEvent evt)
            {
                _lastMouseScreenPosition = null;
                _lastMouseValuePosition = null;
                MarkDirtyRepaint();
            }

            private void OnMouseMove(MouseMoveEvent evt)
            {
                _lastMouseScreenPosition = evt.localMousePosition;
                _lastMouseValuePosition = _graphDrawer.ScreenToValuePos(evt.localMousePosition);
                MarkDirtyRepaint();
            }

            private void OnScrollWheel(WheelEvent evt)
            {
                float widthDelta = _graphDrawer.ValueDisplayRect.width * evt.delta.y * 0.03f;
                float viewportMousePosRatio = (evt.mousePosition.x - _graphDrawer.ScreenDisplayRect.xMin) / _graphDrawer.ScreenDisplayRect.width;
                float offsetDelta = -widthDelta * viewportMousePosRatio;
                _graphDrawer.ValueDisplayRect.x += offsetDelta;
                _graphDrawer.ValueDisplayRect.width += widthDelta;
                MarkDirtyRepaint();
                evt.StopPropagation();
            }

            private void OnGraphStartDrag(Vector2 screenPosition)
            {
                Vector2 valuePos = _graphDrawer.ScreenToValuePos(screenPosition);
                _startDragValuePos = valuePos;
                _endDragValuePos = valuePos;

                if (_isTimeSpanSelected)
                {
                    _isTimeSpanSelected = false;
                    MarkDirtyRepaint();
                    TimeSpanDeselected?.Invoke();
                }
                else
                {
                    StartOrUpdateSelectedTimeSpan();
                }
            }
            private void OnGraphContinueDrag(Vector2 screenPosition)
            {
                Vector2 valuePos = _graphDrawer.ScreenToValuePos(screenPosition);
                _endDragValuePos = valuePos;
                StartOrUpdateSelectedTimeSpan();
            }

            private void StartOrUpdateSelectedTimeSpan()
            {
                double clockLower = Mathf.Min(_startDragValuePos.x, _endDragValuePos.x);
                double clockUpper = Mathf.Max(_startDragValuePos.x, _endDragValuePos.x);
                double stopwatchLower;
                double stopwatchUpper;

                Monitor.ClockImpl activeClock = GetActiveClock();
                if (activeClock != null)
                {
                    // find frame indices
                    int frameIndexLower = activeClock.ClockTimes.BinarySearch(clockLower);
                    if (frameIndexLower < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                        frameIndexLower = ~frameIndexLower;
                    frameIndexLower--;

                    int frameIndexUpper = activeClock.ClockTimes.BinarySearch(clockUpper);
                    if (frameIndexUpper < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                        frameIndexUpper = ~frameIndexUpper;

                    // if frame indices are valid, adjust clock time lower/upper to snap to those frames
                    if (frameIndexLower >= 0)
                    {
                        clockLower = activeClock.ClockTimes[frameIndexLower];
                    }
                    if (frameIndexUpper < activeClock.ClockTimes.Count)
                    {
                        clockUpper = activeClock.ClockTimes[frameIndexUpper];
                    }

                    stopwatchLower = ClockTimeToStopwatchTime(activeClock, clockLower);
                    stopwatchUpper = ClockTimeToStopwatchTime(activeClock, clockUpper);
                }
                else
                {
                    stopwatchLower = clockLower;
                    stopwatchUpper = clockUpper;
                }

                SelectTimeSpan(stopwatchLower, stopwatchUpper);
            }

            private void SelectTimeSpan(double stopwatchTimeStart, double stopwatchTimeEnd)
            {
                _selectedTimeSpanStopwatchStart = stopwatchTimeStart;
                _selectedTimeSpanStopwatchEnd = stopwatchTimeEnd;
                _isTimeSpanSelected = true;

                // invoke event
                TimeSpanSelected?.Invoke(stopwatchTimeStart, stopwatchTimeEnd);
                MarkDirtyRepaint();
            }

            private void InitializeGraph()
            {
                _graphDrawer = new GLGraphDrawer(new Material(EditorResources.LoadGLGraphShaderAsset()));
                _graphDrawer.AutoZoomHorizontal = false;
                _graphDrawer.ValueDisplayRect.xMin = 0;
                _graphDrawer.ValueDisplayRect.xMax = 7;
                _graphDrawer.GridCellPerPixel = Style.GRID_CELL_PER_PIXEL;
                _graphDrawer.AutoZoomPadding = new Vector2(0, -Style.CURVES_PADDING_VERTICAL);
            }

            protected override void ImmediateRepaint()
            {
                if (_graphDrawer.Material == null) // can occur when playmode is stopped
                    InitializeGraph();

                Monitor.ClockImpl activeClock = GetActiveClock();
                List<Monitor.StreamImpl> displayedStreams = GetDisplayedStreams();

                if (ValueMonitorWindowState.instance.FollowLatestValuesInGraph && displayedStreams.Count > 0)
                {
                    // Find the last stream stopwatch time (could be none, if all streams have no recorded entries)
                    double? lastStreamStopwatchTime = null;
                    foreach (var stream in displayedStreams)
                    {
                        if (stream.StopwatchTimes.Count > 0)
                        {
                            lastStreamStopwatchTime = Math.Max(lastStreamStopwatchTime ?? double.MinValue, stream.StopwatchTimes[stream.StopwatchTimes.Count - 1]);
                        }
                    }

                    if (lastStreamStopwatchTime != null)
                    {
                        double lastStreamClockTime = StopwatchTimeToClockTime(activeClock, lastStreamStopwatchTime.Value);
                        float padding = _graphDrawer.ScreenToValueVector(Vector2.right * 30f).x;
                        float delta = (float)lastStreamClockTime - _graphDrawer.ValueDisplayRect.xMax + padding;

                        float newValue = Mathf.Max(0, _graphDrawer.ValueDisplayRect.x + delta);
                        if (newValue != _graphDrawer.ValueDisplayRect.x)
                        {
                            _graphDrawer.ValueDisplayRect.x = newValue;
                            if (_lastMouseScreenPosition.HasValue)
                            {
                                _lastMouseValuePosition = _graphDrawer.ScreenToValuePos(_lastMouseScreenPosition.Value);
                            }
                            CameraAutomaticallyFollowed?.Invoke();
                        }
                    }
                }

                UpdateGraphFromValueStreams(activeClock, displayedStreams);
                _graphDrawer.ScreenDisplayRect = new Rect(0, contentRect.height, contentRect.width, -contentRect.height);
                _graphDrawer.Draw();

                BeginLabels();

                // Draw bottom labels
                DrawAxisLabels();
                DrawFocusedPoint(activeClock, displayedStreams);
                DrawFocusedFrame(activeClock);
                DrawSelectedTimeSpan(activeClock);
                DrawCurrentPointedTime();

                EndLabels();
            }

            private void UpdateGraphFromValueStreams(Monitor.ClockImpl clock, List<Monitor.StreamImpl> streams)
            {
                double minDisplayClockTime = _graphDrawer.ValueDisplayRect.xMin;
                double maxDisplayClockTime = _graphDrawer.ValueDisplayRect.xMax;
                double minDisplayStopwatchTime = ClockTimeToStopwatchTime(clock, minDisplayClockTime);

                bool interpolateValues = ValueMonitorWindowState.instance.InterpolateValues;
                int curveIndex = 0;
                foreach (var stream in streams)
                {
                    // add curve if needed
                    if (curveIndex >= _graphDrawer.Curves.Count)
                    {
                        _graphDrawer.Curves.Add(new GLGraphDrawer.Curve());
                    }
                    GLGraphDrawer.Curve curve = _graphDrawer.Curves[curveIndex];

                    curve.Positions.Clear();
                    curve.Color = stream.Color;

                    int startIndex = stream.StopwatchTimes.BinarySearch(minDisplayStopwatchTime);
                    if (startIndex < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                        startIndex = ~startIndex;
                    startIndex--;
                    for (int i = Mathf.Max(startIndex, 0); i < stream.Values.Count; i++)
                    {
                        double stopwatchTime = stream.StopwatchTimes[i];
                        double clockTime = StopwatchTimeToClockTime(clock, stopwatchTime);

                        if (!interpolateValues && i > 0)
                        {
                            curve.Positions.Add(new Vector2((float)clockTime, stream.Values[i - 1]));
                        }
                        curve.Positions.Add(new Vector2((float)clockTime, stream.Values[i]));

                        if (clockTime > maxDisplayClockTime) // if we exited the graph, exit loop
                            break;
                    }
                    curveIndex++;
                }

                for (int r = _graphDrawer.Curves.Count - 1; r >= curveIndex; r--)
                {
                    _graphDrawer.Curves.RemoveAt(r);
                }
            }

            private void DrawFocusedPoint(Monitor.ClockImpl activeClock, List<Monitor.StreamImpl> displayedStreans)
            {
                Vector2? focusPointPosition = null;
                Color focusPointColor = default;

                // Draw mouse hover labels
                if (_lastMouseValuePosition != null && _lastMouseScreenPosition != null)
                {
                    double mouseStopwatchTime = ClockTimeToStopwatchTime(activeClock, _lastMouseValuePosition.Value.x);
                    float closestStreamDistance = Style.STREAM_HOVER_DIST_MIN;
                    Monitor.StreamImpl closestStream = null;
                    Vector2 closestStreamValuePoint = default;

                    foreach (var stream in displayedStreans)
                    {
                        if (stream.StopwatchTimes.Count == 0)
                            continue;

                        int indexAfterMouse = stream.StopwatchTimes.BinarySearch(mouseStopwatchTime);
                        if (indexAfterMouse < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                            indexAfterMouse = ~indexAfterMouse;

                        int indexBeforeMouse = indexAfterMouse - 1;

                        indexBeforeMouse = Mathf.Clamp(indexBeforeMouse, 0, stream.StopwatchTimes.Count - 1);
                        indexAfterMouse = Mathf.Clamp(indexAfterMouse, 0, stream.StopwatchTimes.Count - 1);

                        int valIndex
                            = Math.Abs(mouseStopwatchTime - stream.StopwatchTimes[indexBeforeMouse])
                            < Math.Abs(mouseStopwatchTime - stream.StopwatchTimes[indexAfterMouse])
                            ? indexBeforeMouse : indexAfterMouse;

                        double valueX = StopwatchTimeToClockTime(activeClock, stream.StopwatchTimes[valIndex]);
                        Vector2 valuePoint = new Vector2((float)valueX, stream.Values[valIndex]);
                        Vector2 screenPoint = _graphDrawer.ValueToScreenPos(valuePoint);
                        float distanceToMouse = Vector2.Distance(_lastMouseScreenPosition.Value, screenPoint);
                        if (distanceToMouse < closestStreamDistance)
                        {
                            closestStreamDistance = distanceToMouse;
                            closestStream = stream;
                            closestStreamValuePoint = valuePoint;
                        }
                    }

                    if (closestStream != null)
                    {
                        Vector2 screenPos = _graphDrawer.ValueToScreenPos(closestStreamValuePoint);
                        {
                            Label valueLabel = GetNextLabel(Style.CLASS_STREAM_HOVER_LABEL_VALUE);
                            valueLabel.style.left = Mathf.RoundToInt(screenPos.x);
                            valueLabel.style.top = Mathf.RoundToInt(screenPos.y);
                            valueLabel.style.color = closestStream.Color;
                            valueLabel.text = closestStreamValuePoint.y.ToString(closestStream.DisplayFormat);
                        }
                        {
                            Label nameLabel = GetNextLabel(Style.CLASS_STREAM_HOVER_LABEL_NAME);
                            nameLabel.style.left = Mathf.RoundToInt(screenPos.x);
                            nameLabel.style.top = Mathf.RoundToInt(screenPos.y);
                            nameLabel.style.color = closestStream.Color;
                            nameLabel.text = closestStream.DisplayName + ":";
                        }
                        _forceCurrentPointedTime = closestStreamValuePoint.x;
                        _forceCurrentPointedTimeColor = closestStream.Color;
                        focusPointPosition = screenPos;
                        focusPointColor = closestStream.Color;
                    }
                    else
                    {
                        _forceCurrentPointedTime = null;
                        _forceCurrentPointedTimeColor = null;
                    }
                }

                if (focusPointPosition != null)
                {
                    _focusPointElement.transform.position = focusPointPosition.Value - (_focusPointElement.contentRect.size / 2f);
                    _focusPointElement.style.unityBackgroundImageTintColor = focusPointColor;
                    _focusPointElement.visible = true;
                }
                else
                {
                    _focusPointElement.visible = false;
                }
            }

            private void DrawFocusedFrame(Monitor.ClockImpl activeClock)
            {
                if (_lastMouseValuePosition.HasValue && _lastMouseScreenPosition.HasValue)
                {
                    if (activeClock != null)
                    {
                        // find frame indices
                        int frameIndexUpper = activeClock.ClockTimes.BinarySearch(_lastMouseValuePosition.Value.x);
                        if (frameIndexUpper < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                            frameIndexUpper = ~frameIndexUpper;
                        int frameIndexLower = frameIndexUpper - 1;

                        // find the visual bounds
                        float focusedFrameValMin = frameIndexLower < 0 ? 0 : (float)activeClock.ClockTimes[frameIndexLower];
                        float focusedFrameValMax = frameIndexUpper >= activeClock.ClockTimes.Count ? contentRect.xMax : (float)activeClock.ClockTimes[frameIndexUpper];
                        float focusedFrameBoundMin = _graphDrawer.ValueToScreenPos(new Vector2(focusedFrameValMin, 0)).x;
                        float focusedFrameBoundMax = _graphDrawer.ValueToScreenPos(new Vector2(focusedFrameValMax, 0)).x;

                        _focusFrameElement.transform.position = new Vector3(focusedFrameBoundMin, 0, 0);
                        _focusFrameElement.transform.scale = new Vector3(Mathf.Max(2, focusedFrameBoundMax - focusedFrameBoundMin), contentRect.height, 1);
                    }
                    else
                    {
                        _focusFrameElement.transform.position = new Vector3(_lastMouseScreenPosition.Value.x - 1, 0, 0);
                        _focusFrameElement.transform.scale = new Vector3(2, contentRect.height, 1);
                    }

                    _focusFrameElement.visible = true;
                }
                else
                {
                    _focusFrameElement.visible = false;
                }
            }

            private void DrawCurrentPointedTime()
            {
                if ((_lastMouseValuePosition.HasValue && _lastMouseScreenPosition.HasValue) || _forceCurrentPointedTime.HasValue)
                {
                    float timeScreenPos;
                    float timeValue;
                    if (_forceCurrentPointedTime.HasValue)
                    {
                        timeValue = (float)_forceCurrentPointedTime.Value;
                        timeScreenPos = _graphDrawer.ValueToScreenPos(Vector2.right * (float)_forceCurrentPointedTime.Value).x;
                    }
                    else
                    {
                        timeScreenPos = _lastMouseScreenPosition.Value.x;
                        timeValue = _lastMouseValuePosition.Value.x;
                    }

                    var label = GetNextLabel(Style.CLASS_POINTED_TIME_LABEL);
                    label.transform.position = new Vector3(timeScreenPos, contentRect.yMax - Style.CURRENT_TIME_VERTICAL_PADDING, 0);
                    label.style.color = _forceCurrentPointedTimeColor.HasValue ? _forceCurrentPointedTimeColor.Value : Color.white;
                    label.text = timeValue.ToString();
                }
            }

            private void DrawAxisLabels()
            {
                if (_graphDrawer.LastGridDrawInfo.CellSizeX > 0 && _graphDrawer.LastGridDrawInfo.CellSizeY > 0)
                {
                    int index = 0;
                    double p;
                    while (true)
                    {
                        p = _graphDrawer.LastGridDrawInfo.CellOffsetX + (_graphDrawer.LastGridDrawInfo.CellSizeX * index);
                        if (p > _graphDrawer.ValueDisplayRect.xMax)
                            break;
                        Label label = GetNextLabel(Style.CLASS_GRID_LABEL);
                        label.transform.position = new Vector3(_graphDrawer.ValueToScreenPos(new Vector2((float)p, 0)).x, contentRect.height - 14, 0);
                        label.text = p.ToString();
                        index++;
                    }
                    index = 0;
                    while (true)
                    {
                        p = _graphDrawer.LastGridDrawInfo.CellOffsetY + (_graphDrawer.LastGridDrawInfo.CellSizeY * index);
                        if (p > _graphDrawer.ValueDisplayRect.yMax)
                            break;

                        // make sure we're putting the text inside the box
                        float yPos = _graphDrawer.ValueToScreenPos(new Vector2(0, (float)p)).y;
                        if (yPos < Mathf.Abs(_graphDrawer.ScreenDisplayRect.height) - 5)
                        {
                            Label label = GetNextLabel(Style.CLASS_GRID_LABEL);
                            label.transform.position = new Vector3(0, yPos, 0);
                            label.text = p.ToString();
                        }
                        index++;
                    }
                }
            }

            private void DrawSelectedTimeSpan(Monitor.ClockImpl activeClock)
            {
                // find the visual bounds
                if (_isTimeSpanSelected)
                {
                    double clockStart = StopwatchTimeToClockTime(activeClock, _selectedTimeSpanStopwatchStart);
                    double clockEnd = StopwatchTimeToClockTime(activeClock, _selectedTimeSpanStopwatchEnd);

                    float boundMin = _graphDrawer.ValueToScreenPos(new Vector2((float)clockStart, 0)).x;
                    float boundMax = _graphDrawer.ValueToScreenPos(new Vector2((float)clockEnd, 0)).x;

                    _selectedTimeSpan.transform.position = new Vector3(boundMin, 0, 0);
                    _selectedTimeSpan.transform.scale = new Vector3(Mathf.Max(2, boundMax - boundMin), contentRect.height, 1);
                    _selectedTimeSpan.visible = true;
                }
                else
                {
                    _selectedTimeSpan.visible = false;
                }
            }

            private List<Monitor.StreamImpl> GetDisplayedStreams()
            {
                var streams = new List<Monitor.StreamImpl>();
                foreach (var streamSetting in ValueMonitorWindowState.instance.StreamSettings)
                {
                    if (!streamSetting.Visible || string.IsNullOrEmpty(streamSetting.Id))
                        continue;

                    if (Monitor.Streams.TryGetValue(streamSetting.Id, out Monitor.StreamImpl stream))
                    {
                        streams.Add(stream);
                    }
                }
                return streams;
            }

            private void BeginLabels()
            {
                foreach (var bank in _availableLabels.Values)
                {
                    bank.NextUsedIndex = 0;
                }
            }

            private Label GetNextLabel(string styleClass)
            {
                if (!_availableLabels.TryGetValue(styleClass, out LabelBank labelBank))
                {
                    labelBank = new LabelBank();
                    _availableLabels[styleClass] = labelBank;
                }

                if (labelBank.NextUsedIndex >= labelBank.Labels.Count)
                {
                    Label newLabel = new Label();
                    newLabel.pickingMode = PickingMode.Ignore;
                    newLabel.style.position = Position.Absolute;
                    newLabel.AddToClassList(styleClass);
                    _labelContainerElement.Add(newLabel);
                    labelBank.Labels.Add(newLabel);
                }

                return labelBank.Labels[labelBank.NextUsedIndex++];
            }

            private void EndLabels()
            {
                // remove unused labels from banks
                foreach (var labelBank in _availableLabels.Values)
                {
                    for (int i = labelBank.Labels.Count - 1; i >= labelBank.NextUsedIndex; i--)
                    {
                        _labelContainerElement.Remove(labelBank.Labels[i]);
                        labelBank.Labels.RemoveAt(i);
                    }
                }
            }
        }
#endif
    }
}
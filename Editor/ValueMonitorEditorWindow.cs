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
        internal class Resources
        {
            public VisualTreeAsset StreamAsset;
            public VisualTreeAsset ClockAsset;
            public VisualTreeAsset RootAsset;
            public StyleSheet StyleAsset;
        }

        private Resources _resources;
        private ToolbarMenu _clockMenu;
        private ListView _streamsList;
        private uint _lastMonitorVersion;
        private Action<DropdownMenuAction> _clocksMenuClickCallback;
        private Func<DropdownMenuAction, DropdownMenuAction.Status> _clocksMenuEnabledCallback;

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
            };

            VisualElement root = rootVisualElement;

            // Import UXML and stylesheet
            VisualElement content = _resources.RootAsset.Instantiate();
            root.Add(content);
            root.styleSheets.Add(_resources.StyleAsset);

            VisualElement graphFocusPoint = content.Q("GraphFocusPoint");

            GraphElement graphElement = new GraphElement(graphFocusPoint);
            graphElement.name = "graph";
            root.Q("GraphContainer").Add(graphElement);

            _clockMenu = root.Q<ToolbarMenu>("Clocks");

            //_clocksList = root.Q<ListView>("ClocksList");
            //_clocksList.itemsAdded += OnClockAdded;
            //_clocksList.itemsSource = ValueMonitorWindowState.instance.ClockSettings;
            //_clocksList.makeItem = () => new ClockElement(_resources);
            //_clocksList.bindItem = (VisualElement visual, int index) =>
            //{
            //    var data = ValueMonitorWindowState.instance.ClockSettings[index];
            //    if (data == null)
            //    {
            //        data = new ValueMonitorWindowState.ClockSetting();
            //        ValueMonitorWindowState.instance.ClockSettings[index] = data;
            //    }
            //    ((ClockElement)visual).Bind(data);
            //};

            _streamsList = root.Q<ListView>("StreamsList");
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
                }
                ((StreamElement)visual).Bind(data);
            };
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

            menuItems.Add(new DropdownMenuAction("Unscaled Time", _clocksMenuClickCallback, _clocksMenuEnabledCallback, userData: null));

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
                    preferredClockName = "Unscaled Time";
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
                        preferredClockName = $"Game Time (<i>{preferredClockId}/<i> not found)";
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
            UpdateClocksMenu();
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
                        _streamsList.RefreshItem(streamIndex);
                    });
                }
                genericMenu.DropDown(_streamsList.worldBound);
            }
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

        internal class ClockElement : VisualElement
        {
            private Label _viewLabel;
            private RadioButton _radioButton;
            private ValueMonitorWindowState.ClockSetting _setting;

            public ClockElement(Resources resources)
            {
                resources.ClockAsset.CloneTree(this);

                _viewLabel = this.Q<Label>("Label");
                _radioButton = this.Q<RadioButton>();

                _radioButton.RegisterValueChangedCallback(OnRadioButtonChanged);
            }

            public void Bind(ValueMonitorWindowState.ClockSetting setting)
            {
                _setting = setting;

                bool isUnsetElement = string.IsNullOrEmpty(_setting.Id);
                Monitor.StreamImpl streamData = null;
                bool hasStreamData = !isUnsetElement && Monitor.Streams.TryGetValue(_setting.Id, out streamData);
                string labelText = hasStreamData ? streamData.DisplayName : _setting.Id;
                if (string.IsNullOrEmpty(labelText))
                    labelText = "<i>Unset</i>";
                _viewLabel.text = labelText;
                _radioButton.SetValueWithoutNotify(ValueMonitorWindowState.instance.PreferredClockId == _setting.Id);
            }

            private void OnRadioButtonChanged(ChangeEvent<bool> evt)
            {
                if (evt.newValue == true)
                {
                    ValueMonitorWindowState.instance.PreferredClockId = _setting.Id;
                }
            }
        }
    }

    public class GraphElement : ImmediateModeElement
    {
        public class DragManipulator : PointerManipulator
        {
            private bool _enabled;
            private GraphElement _graph;

            public DragManipulator(GraphElement target)
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
                target.CapturePointer(evt.pointerId);
                _enabled = true;
            }

            private void HandlePointerMove(PointerMoveEvent evt)
            {
                if (_enabled && target.HasPointerCapture(evt.pointerId))
                {
                    _graph._graphDrawer.ValueDisplayRect.x -= _graph._graphDrawer.ScreenToValueVector(Vector2.right * evt.deltaPosition.x).x;
                    _graph.MarkDirtyRepaint();
                }
            }

            private void HandlePointerUp(PointerUpEvent evt)
            {
                if (_enabled && target.HasPointerCapture(evt.pointerId))
                {
                    target.ReleasePointer(evt.pointerId);
                }
            }

            private void HandlePointerCaptureOut(PointerCaptureOutEvent evt)
            {
                if (_enabled)
                {
                    _enabled = false;
                }
            }
        }

        public static class Style
        {
            public const string CLASS_GRID_LABEL = "grid-label";
            public const string CLASS_STREAM_HOVER_LABEL_VALUE = "stream-hover-value";
            public const string CLASS_STREAM_HOVER_LABEL_NAME = "stream-hover-name";
            public static readonly Color GridAxisColor = new Color(1, 1, 1);
            public static readonly Color GridColor = new Color(0.6f, 0.6f, 0.6f);
            public static readonly Color GridMiniColor = new Color(0.35f, 0.35f, 0.35f);
            public const float GRID_CELL_PER_PIXEL = 1 / 200f;
            public const float CURVES_PADDING_VERTICAL = 40f;
            public const float STREAM_HOVER_DIST_MIN = 25f;
        }

        private GLGraphDrawer _graphDrawer;

        private DragManipulator _dragManipulator;
        private Vector2? _lastMouseScreenPosition;
        private Vector2? _lastMouseValuePosition;
        private VisualElement _focusPointElement;

        public bool StairDisplay { get; set; } = true;

        public GraphElement(VisualElement focusPointElement)
        {
            InitializeGraph();
            _dragManipulator = new DragManipulator(this);
            RegisterCallback<WheelEvent>(OnScrollWheel);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
            _focusPointElement = focusPointElement;
        }

        private void OnMouseLeave(MouseLeaveEvent evt)
        {
            _lastMouseScreenPosition = null;
            _lastMouseValuePosition = null;
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

        private void InitializeGraph()
        {
            _graphDrawer = new GLGraphDrawer(new Material(EditorResources.LoadGLGraphShaderAsset()));
            _graphDrawer.AutoZoomHorizontal = false;
            _graphDrawer.ValueDisplayRect.xMin = 0;
            _graphDrawer.ValueDisplayRect.xMax = 25;
            _graphDrawer.GridCellPerPixel = Style.GRID_CELL_PER_PIXEL;
            _graphDrawer.AutoZoomPadding = new Vector2(0, -Style.CURVES_PADDING_VERTICAL);

            if (Monitor.Streams.Count == 0)
            {
                Monitor.Streams.Add("test1", new Monitor.StreamImpl("Tejst1") { DisplayName = "Tejst1" });
                Monitor.Streams["test1"].Color = Color.magenta;
                Monitor.Streams["test1"].Values.Add(1);
                Monitor.Streams["test1"].Values.Add(2);
                Monitor.Streams["test1"].Values.Add(5);
                Monitor.Streams["test1"].Values.Add(1);
                Monitor.Streams["test1"].Values.Add(1);
                Monitor.Streams["test1"].Values.Add(3);
                Monitor.Streams["test1"].StopwatchTimes.Add(1);
                Monitor.Streams["test1"].StopwatchTimes.Add(2);
                Monitor.Streams["test1"].StopwatchTimes.Add(3);
                Monitor.Streams["test1"].StopwatchTimes.Add(4);
                Monitor.Streams["test1"].StopwatchTimes.Add(5);
                Monitor.Streams["test1"].StopwatchTimes.Add(6);
                Monitor.Streams.Add("test2", new Monitor.StreamImpl("Tejst2") { DisplayName = "Tejst2" });
                Monitor.Streams["test2"].Color = Color.green;
                Monitor.Streams["test2"].Values.Add(1.5f);
                Monitor.Streams["test2"].Values.Add(3.5f);
                Monitor.Streams["test2"].Values.Add(2);
                Monitor.Streams["test2"].Values.Add(5);
                Monitor.Streams["test2"].Values.Add(1.5f);
                Monitor.Streams["test2"].Values.Add(1);
                Monitor.Streams["test2"].StopwatchTimes.Add(1);
                Monitor.Streams["test2"].StopwatchTimes.Add(2);
                Monitor.Streams["test2"].StopwatchTimes.Add(3);
                Monitor.Streams["test2"].StopwatchTimes.Add(4);
                Monitor.Streams["test2"].StopwatchTimes.Add(5);
                Monitor.Streams["test2"].StopwatchTimes.Add(6);
            }
        }

        protected override void ImmediateRepaint()
        {
            if (_graphDrawer.Material == null) // can occur when playmode is stopped
                InitializeGraph();

            List<Monitor.StreamImpl> displayedStreans = GetDisplayedStreams();

            UpdateGraphFromValueStreams(displayedStreans);
            _graphDrawer.ScreenDisplayRect = new Rect(0, contentRect.height, contentRect.width, -contentRect.height);
            _graphDrawer.Draw();


            BeginLabels();

            // Draw bottom labels
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

            Vector2? focusPointPosition = null;
            Color focusPointColor = default;

            // Draw mouse hover labels
            if (_lastMouseValuePosition != null && _lastMouseScreenPosition != null)
            {
                double mouseValueX = _lastMouseValuePosition.Value.x;
                float closestStreamDistance = Style.STREAM_HOVER_DIST_MIN;
                Monitor.StreamImpl closestStream = null;
                int closestStreamValueIndex = 0;

                foreach (var stream in displayedStreans)
                {
                    if (stream.StopwatchTimes.Count == 0)
                        continue;

                    int indexAfterMouse = stream.StopwatchTimes.BinarySearch(mouseValueX);
                    if (indexAfterMouse < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                        indexAfterMouse = ~indexAfterMouse;

                    int indexBeforeMouse = indexAfterMouse - 1;

                    indexBeforeMouse = Mathf.Clamp(indexBeforeMouse, 0, stream.StopwatchTimes.Count - 1);
                    indexAfterMouse = Mathf.Clamp(indexAfterMouse, 0, stream.StopwatchTimes.Count - 1);

                    int valIndex
                        = Math.Abs(mouseValueX - stream.StopwatchTimes[indexBeforeMouse])
                        < Math.Abs(mouseValueX - stream.StopwatchTimes[indexAfterMouse])
                        ? indexBeforeMouse : indexAfterMouse;

                    Vector2 valuePoint = new Vector2((float)stream.StopwatchTimes[valIndex], stream.Values[valIndex]);
                    Vector2 screenPoint = _graphDrawer.ValueToScreenPos(valuePoint);
                    float distanceToMouse = Vector2.Distance(_lastMouseScreenPosition.Value, screenPoint);
                    if (distanceToMouse < closestStreamDistance)
                    {
                        closestStreamDistance = distanceToMouse;
                        closestStream = stream;
                        closestStreamValueIndex = valIndex;
                    }
                }

                if (closestStream != null)
                {
                    float valueY = closestStream.Values[closestStreamValueIndex];
                    float valueX = (float)closestStream.StopwatchTimes[closestStreamValueIndex];
                    Vector2 valuePos = new Vector2(valueX, valueY);
                    Vector2 screenPos = _graphDrawer.ValueToScreenPos(valuePos);
                    {
                        Label valueLabel = GetNextLabel(Style.CLASS_STREAM_HOVER_LABEL_VALUE);
                        valueLabel.style.left = Mathf.RoundToInt(screenPos.x);
                        valueLabel.style.top = Mathf.RoundToInt(screenPos.y);
                        valueLabel.style.color = closestStream.Color;
                        valueLabel.text = valueY.ToString(closestStream.DisplayFormat);
                    }
                    {
                        Label nameLabel = GetNextLabel(Style.CLASS_STREAM_HOVER_LABEL_NAME);
                        nameLabel.style.left = Mathf.RoundToInt(screenPos.x);
                        nameLabel.style.top = Mathf.RoundToInt(screenPos.y);
                        nameLabel.style.color = closestStream.Color;
                        nameLabel.text = closestStream.DisplayName + ":";
                    }
                    focusPointPosition = screenPos;
                    focusPointColor = closestStream.Color;
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

            EndLabels();
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

        private void UpdateGraphFromValueStreams(List<Monitor.StreamImpl> streams)
        {
            int curveIndex = 0;
            double minDisplayTime = _graphDrawer.ValueDisplayRect.xMin;
            double maxDisplayTime = _graphDrawer.ValueDisplayRect.xMax;
            foreach (var stream in streams)
            {
                if (curveIndex >= _graphDrawer.Curves.Count)
                {
                    _graphDrawer.Curves.Add(new GLGraphDrawer.Curve());
                }
                GLGraphDrawer.Curve curve = _graphDrawer.Curves[curveIndex];

                curve.Positions.Clear();
                curve.Color = stream.Color;

                int startIndex = stream.StopwatchTimes.BinarySearch(minDisplayTime);
                if (startIndex < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                    startIndex = ~startIndex;
                startIndex--;
                for (int i = Mathf.Max(startIndex, 0); i < stream.Values.Count; i++)
                {
                    double time = stream.StopwatchTimes[i];

                    if (StairDisplay && i > 0)
                    {
                        curve.Positions.Add(new Vector2((float)time, stream.Values[i - 1]));
                    }
                    curve.Positions.Add(new Vector2((float)time, stream.Values[i]));

                    if (time > maxDisplayTime) // if we exited the graph, exit loop
                        break;
                }
                curveIndex++;
            }

            for (int r = _graphDrawer.Curves.Count - 1; r >= curveIndex; r--)
            {
                _graphDrawer.Curves.RemoveAt(r);
            }
        }

        private void BeginLabels()
        {
            foreach (var bank in _availableLabels.Values)
            {
                bank.NextUsedIndex = 0;
            }
        }

        private class LabelBank
        {
            public int NextUsedIndex = 0;
            public List<Label> Labels = new List<Label>();
        }
        private Dictionary<string, LabelBank> _availableLabels = new Dictionary<string, LabelBank>();
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
                newLabel.style.position = Position.Absolute;
                newLabel.AddToClassList(styleClass);
                Add(newLabel);
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
                    Remove(labelBank.Labels[i]);
                    labelBank.Labels.RemoveAt(i);
                }
            }
        }
    }
}
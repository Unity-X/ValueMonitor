
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("UnityX.ValueMonitor.Editor")]

namespace UnityX.ValueMonitor
{
    public static class Monitor
    {
        public enum Persistence
        {
            RemovedOnSubsystemRegistration,
            PersistsUntilDisposed
        }

        internal class TimedElementImpl
        {
            public readonly string Id;

            public TimedElementImpl(string id)
            {
                Id = id;
            }

            public string DisplayName;
            public Color Color;
            public Persistence Persistence;

            public List<double> StopwatchTimes = new List<double>();

            protected int FindTimeIndex(double time, int roundingAdjustment)
            {
                int index = StopwatchTimes.BinarySearch(time);
                if (index < 0)
                {
                    index = Mathf.Abs(index) + roundingAdjustment;
                }

                return Mathf.Clamp(index, 0, StopwatchTimes.Count - 1);
            }

            protected (int indexBegin, int indexEnd) FindTimeIndices(double timeBegin, double timeEnd)
            {
                return (FindTimeIndex(timeBegin, roundingAdjustment: -1), FindTimeIndex(timeEnd, roundingAdjustment: 0));
            }
        }

        internal class ClockImpl : TimedElementImpl
        {
            public ClockImpl(string id) : base(id)
            {
            }

            public void Tick()
            {
                StopwatchTimes.Add(Stopwatch.Elapsed.TotalSeconds);
            }

            public void GetValues(double timeBegin, double timeEnd, List<double> result)
            {
                int indexBegin = FindTimeIndex(timeBegin, roundingAdjustment: -1);
                int indexEnd = FindTimeIndex(timeEnd, roundingAdjustment: 0);

                result.Clear();
                for (int i = indexBegin; i < indexEnd; i++)
                {
                    result.Add((StopwatchTimes[i]));
                }
            }
        }

        internal class StreamImpl
        {
            public readonly string Id;

            public StreamImpl(string id)
            {
                Id = id;
            }

            public string DisplayFormat = "0.##";
            public string DisplayName;
            public Color Color = Color.white;
            public Persistence Persistence = Persistence.RemovedOnSubsystemRegistration;

            public List<double> StopwatchTimes = new List<double>();
            public List<float> Values = new List<float>();

            public void Log(float value)
            {
                StopwatchTimes.Add(Stopwatch.Elapsed.TotalSeconds);
                Values.Add(value);
            }

            public void GetValues(double timeBegin, double timeEnd, List<(double time, float value)> result)
            {
                int indexBegin = FindTimeIndex(timeBegin, roundingAdjustment: -1);
                int indexEnd = FindTimeIndex(timeEnd, roundingAdjustment: 0);

                result.Clear();
                for (int i = indexBegin; i < indexEnd; i++)
                {
                    result.Add((StopwatchTimes[i], Values[i]));
                }
            }

            private int FindTimeIndex(double time, int roundingAdjustment)
            {
                int index = StopwatchTimes.BinarySearch(time);
                if (index < 0)
                {
                    index = Mathf.Abs(index) + roundingAdjustment;
                }

                return Mathf.Clamp(index, 0, StopwatchTimes.Count - 1);
            }
        }

        public struct Stream : IDisposable
        {
            internal StreamImpl Impl;

            public Color Color
            {
                get => Impl.Color;
                set => Impl.Color = value;
            }

            public string DisplayName
            {
                get => Impl.DisplayName;
                set => Impl.DisplayName = value;
            }

            public string DisplayFormat
            {
                get => Impl.DisplayFormat;
                set => Impl.DisplayFormat = value;
            }

            public void Dispose()
            {
                Streams.Remove(Impl.Id);
            }

            public void Log(float value)
            {
                Impl.Log(value);
            }
        }

        public struct Clock : IDisposable
        {
            internal ClockImpl Impl;

            public Color Color
            {
                get => Impl.Color;
                set => Impl.Color = value;
            }

            public string DisplayName
            {
                get => Impl.DisplayName;
                set => Impl.DisplayName = value;
            }

            public void Dispose()
            {
                Clocks.Remove(Impl.Id);
            }

            public void Tick()
            {
                Impl.Tick();
            }
        }

        internal static Stopwatch Stopwatch;
        internal static Dictionary<string, StreamImpl> Streams = new Dictionary<string, StreamImpl>();
        internal static Dictionary<string, ClockImpl> Clocks = new Dictionary<string, ClockImpl>();

        private static bool s_initialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)] // initializes in build & playmode
        private static void Initialize()
        {
            if (s_initialized)
                return;
            s_initialized = true;
            Stopwatch = Stopwatch.StartNew();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearUnpersistentData()
        {
            List<string> toRemove = new List<string>();
            foreach (var stream in Streams)
            {
                if (stream.Value.Persistence == Persistence.RemovedOnSubsystemRegistration)
                {
                    toRemove.Add(stream.Key);
                }
            }
            foreach (var item in toRemove)
            {
                Streams.Remove(item);
            }
            toRemove.Clear();
            foreach (var clock in Clocks)
            {
                if (clock.Value.Persistence == Persistence.RemovedOnSubsystemRegistration)
                {
                    toRemove.Add(clock.Key);
                }
            }
            foreach (var item in toRemove)
            {
                Clocks.Remove(item);
            }
            toRemove.Clear();
        }

        public static Clock GetClock(string id, Color color)
        {
            Clock clock = GetClock(id);
            clock.Color = color;
            return clock;
        }
        public static Stream GetStream(string id, Color color)
        {
            Stream stream = GetStream(id);
            stream.Color = color;
            return stream;
        }

        public static Clock GetClock(string id)
        {
            if (!Clocks.TryGetValue(id, out ClockImpl clockImpl))
            {
                clockImpl = new ClockImpl(id)
                {
                    Color = Color.white,
                    DisplayName = id,
                };
                Clocks.Add(clockImpl.Id, clockImpl);
            }
            return new Clock() { Impl = clockImpl };
        }
        public static Stream GetStream(string id)
        {
            if (!Streams.TryGetValue(id, out StreamImpl streamImpl))
            {
                streamImpl = new StreamImpl(id)
                {
                    Color = Color.white,
                    DisplayName = id,
                };
                Streams.Add(streamImpl.Id, streamImpl);
            }
            return new Stream() { Impl = streamImpl };
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
        public class AutoMonitor : Attribute
        {
            // TODO, search for all static members with this attribute and log them at every clock
        }
    }
}

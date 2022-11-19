
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;

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
            public List<double> ClockTimes = new List<double>();

            public ClockImpl(string id) : base(id)
            {
            }

            public void Tick(double newTime)
            {
                if (ClockTimes.Count > 0 && ClockTimes[ClockTimes.Count - 1] >= newTime)
                {
                    throw new ArgumentException($"New clock ({Id}) tick time ({newTime}) is not greater than previous tick time ({ClockTimes[ClockTimes.Count - 1]})");
                }
                StopwatchTimes.Add(Stopwatch.Elapsed.TotalSeconds);
                ClockTimes.Add(newTime);
                Version++;
            }

            public void GetValues(double timeBegin, double timeEnd, List<double> result)
            {
                int indexBegin = FindTimeIndex(timeBegin, roundingAdjustment: -1);
                int indexEnd = FindTimeIndex(timeEnd, roundingAdjustment: 0);

                result.Clear();
                for (int i = indexBegin; i < indexEnd; i++)
                {
                    result.Add(StopwatchTimes[i]);
                }
            }

            public double GetInterpolatedStopwatchTime(double clockTime)
            {
                return MapTime(clockTime, ClockTimes, StopwatchTimes);
            }

            public double GetInterpolatedClockTime(double stopwatchTime)
            {
                return MapTime(stopwatchTime, StopwatchTimes, ClockTimes);
            }

            private static double MapTime(double a, List<double> timesA, List<double> timesB)
            {
                if (timesA.Count < 2 || timesB.Count < 2)
                    throw new Exception("The clock needs at least to ticks to get any interpolated value");

                int index = timesA.BinarySearch(a);
                if (index < 0) // if exact value is not found, result is bitwise complement (~ operator). Bring it back to positive value by reversing operation.
                    index = ~index;

                index = Mathf.Clamp(index, 1, timesA.Count - 1);

                double lowBoundA = timesA[index - 1];
                double highBoundA = timesA[index];
                double lerp = (a - lowBoundA) / (highBoundA - lowBoundA);

                double lowBoundB = timesB[index - 1];
                double highBoundB = timesB[index];
                return lerp * (highBoundB - lowBoundB) + lowBoundB;
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
                Version++;
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

            public void Tick(double newTime)
            {
                Impl.Tick(newTime);
            }
        }

        internal static uint Version = 0;
        internal static Stopwatch Stopwatch;
        internal static Dictionary<string, StreamImpl> Streams = new Dictionary<string, StreamImpl>();
        internal static Dictionary<string, ClockImpl> Clocks = new Dictionary<string, ClockImpl>();

        private static bool s_initialized = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)] // initializes in build & playmode
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void Initialize()
        {
            if (s_initialized)
                return;
            s_initialized = true;
            CreateDefaultClocksIfNeeded();
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
            CreateDefaultClocksIfNeeded();
            Version++;
        }

        private static void CreateDefaultClocksIfNeeded()
        {
            if (!Clocks.ContainsKey("Time Clock"))
            {
                var clock = GetClock("Time Clock");
                clock.Impl.Persistence = Persistence.PersistsUntilDisposed;
            }
            if (!Clocks.ContainsKey("Fixed Time Clock"))
            {
                var clock = GetClock("Fixed Time Clock");
                clock.Impl.Persistence = Persistence.PersistsUntilDisposed;
            }
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
                Version++;
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
                Version++;
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

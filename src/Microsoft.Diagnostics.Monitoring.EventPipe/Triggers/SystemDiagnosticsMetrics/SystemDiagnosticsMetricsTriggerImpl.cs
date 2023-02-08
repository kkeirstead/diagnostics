// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.SystemDiagnosticsMetrics
{
    // The core implementation of the EventCounter trigger that processes
    // the trigger settings and evaluates the counter payload. Primary motivation
    // for the implementation is for unit testability separate from TraceEvent.
    internal sealed class SystemDiagnosticsMetricsTriggerImpl
    {
        private readonly long _intervalTicks;
        private readonly Func<double, bool> _valueFilterDefault;
        private readonly Func<Dictionary<string, double>, bool> _valueFilterHistogram;
        private readonly long _windowTicks;

        private long? _latestTicks;
        private long? _targetTicks;

        public SystemDiagnosticsMetricsTriggerImpl(SystemDiagnosticsMetricsTriggerSettings settings)
        {
            if (null == settings)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.HistogramMode.HasValue)
            {
                Func<double, double, bool> evalFunc;

                if (settings.HistogramMode.Value == HistogramMode.GreaterThan)
                {
                    // double check if this should be > or >=
                    evalFunc = (actual, expected) => actual > expected;
                }
                else
                {
                    evalFunc = (actual, expected) => actual < expected;
                }

                _valueFilterHistogram = histogramValues =>
                {
                    foreach (var kvp in settings.HistogramPercentiles)
                    {
                        if (!histogramValues.TryGetValue(kvp.Key, out var value) || !evalFunc(value, kvp.Value))
                        {
                            return false;
                        }
                    }

                    return true;
                };
            }
            else
            {
                if (settings.GreaterThan.HasValue)
                {
                    double minValue = settings.GreaterThan.Value;
                    if (settings.LessThan.HasValue)
                    {
                        double maxValue = settings.LessThan.Value;
                        _valueFilterDefault = value => value > minValue && value < maxValue;
                    }
                    else
                    {
                        _valueFilterDefault = value => value > minValue;
                    }
                }
                else if (settings.LessThan.HasValue)
                {
                    double maxValue = settings.LessThan.Value;
                    _valueFilterDefault = value => value < maxValue;
                }
            }

            _intervalTicks = (long)(settings.CounterIntervalSeconds * TimeSpan.TicksPerSecond);
            _windowTicks = settings.SlidingWindowDuration.Ticks;
        }

        public bool HasSatisfiedCondition(ICounterPayload payload)
        {
            EventType eventType = payload.EventType;

            if (eventType == EventType.Error)
            {
                // not currently logging the error messages

                return false;
            }
            else
            {
                bool passesValueFilter = false;

                if (eventType == EventType.Histogram)
                {
                    Dictionary<string, double> payloadDict = new();

                    if (payload is PercentilePayload percentilePayload)
                    {
                        payloadDict = percentilePayload.Quantiles.ToDictionary(keySelector: p => p.Percentage.ToString(), elementSelector: p => p.Value);
                    }

                    passesValueFilter = _valueFilterHistogram(payloadDict);
                }
                else
                {
                    passesValueFilter = _valueFilterDefault(payload.Value);
                }

                long payloadTimestampTicks = payload.Timestamp.Ticks;
                long payloadIntervalTicks = (long)(payload.Interval * TimeSpan.TicksPerSecond);

                if (!passesValueFilter)
                {
                    // Series was broken; reset state.
                    _latestTicks = null;
                    _targetTicks = null;
                    return false;
                }
                else if (!_targetTicks.HasValue)
                {
                    // This is the first event in the series. Record latest and target times.
                    _latestTicks = payloadTimestampTicks;
                    // The target time should be the start of the first passing interval + the requisite time window.
                    // The start of the first passing interval is the payload time stamp - the interval time.
                    _targetTicks = payloadTimestampTicks - payloadIntervalTicks + _windowTicks;
                }
                else if (_latestTicks.Value + (1.5 * _intervalTicks) < payloadTimestampTicks)
                {
                    // Detected that an event was skipped/dropped because the time between the current
                    // event and the previous is more that 150% of the requested interval; consecutive
                    // counter events should not have that large of an interval. Reset for current
                    // event to be first event in series. Record latest and target times.
                    _latestTicks = payloadTimestampTicks;
                    // The target time should be the start of the first passing interval + the requisite time window.
                    // The start of the first passing interval is the payload time stamp - the interval time.
                    _targetTicks = payloadTimestampTicks - payloadIntervalTicks + _windowTicks;
                }
                else
                {
                    // Update latest time to the current event time.
                    _latestTicks = payloadTimestampTicks;
                }

                // Trigger is satisfied when the latest time is larger than the target time.
                return _latestTicks >= _targetTicks;
            }
        }

        private double GetPercentile(string metadata)
        {
            string percentile = metadata.Substring(metadata.IndexOf("Percentile=")); // These should be a constant shared with TraceEventExtensions
            percentile = percentile.Replace("Percentile=", string.Empty); // this assumes that Percentile is the last metadata - might not want to do this

            return Convert.ToDouble(percentile);
        }
    }
}

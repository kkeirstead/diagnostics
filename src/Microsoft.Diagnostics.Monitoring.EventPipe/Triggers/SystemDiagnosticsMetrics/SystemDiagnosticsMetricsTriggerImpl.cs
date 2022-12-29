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
        private readonly Func<double, bool> _valueFilter;
        private readonly Func<Dictionary<double, double>, bool> _valueFilter2; // temporary
        private readonly long _windowTicks;

        private readonly bool _isHistogram;

        private long? _latestTicks;
        private long? _targetTicks;

        public SystemDiagnosticsMetricsTriggerImpl(SystemDiagnosticsMetricsTriggerSettings settings)
        {
            if (null == settings)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // needs to be updated for histogram
            if (settings.HistogramMode.HasValue)
            {
                if (settings.HistogramMode.Value == HistogramMode.GreaterThan)
                {
                    _valueFilter2 = histogramValues =>
                    {
                        foreach (var kvp in settings.HistogramPercentiles)
                        {
                            if (histogramValues.TryGetValue(kvp.Key, out var value))
                            {
                                // double check if this should be < or <=
                                if (value <= kvp.Value)
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                return false;
                            }
                        }

                        return true;
                    };
                }
                else if (settings.HistogramMode.Value == HistogramMode.LessThan)
                {
                    _valueFilter2 = histogramValues =>
                    {
                        foreach (var kvp in settings.HistogramPercentiles)
                        {
                            if (histogramValues.TryGetValue(kvp.Key, out var value))
                            {
                                // double check if this should be > or >=
                                if (value >= kvp.Value)
                                {
                                    return false;
                                }
                            }
                            else
                            {
                                return false;
                            }
                        }

                        return true;
                    };
                }
                else
                {
                    _valueFilter = histogramValues => false;
                }
            }
            else
            {
                if (settings.GreaterThan.HasValue)
                {
                    double minValue = settings.GreaterThan.Value;
                    if (settings.LessThan.HasValue)
                    {
                        double maxValue = settings.LessThan.Value;
                        _valueFilter = value => value > minValue && value < maxValue;
                    }
                    else
                    {
                        _valueFilter = value => value > minValue;
                    }
                }
                else if (settings.LessThan.HasValue)
                {
                    double maxValue = settings.LessThan.Value;
                    _valueFilter = value => value < maxValue;
                }
            }

            _intervalTicks = (long)(settings.CounterIntervalSeconds * TimeSpan.TicksPerSecond);
            _windowTicks = settings.SlidingWindowDuration.Ticks;
            _isHistogram = settings.HistogramMode.HasValue;
        }

        public bool HasSatisfiedCondition(List<ICounterPayload> payloadList)
        {
            // distinguish between histogram/non-histogram to decide if only need the 0th index of payload


            if (!_isHistogram)
            {
                ICounterPayload payload = payloadList[0];

                long payloadTimestampTicks = payload.Timestamp.Ticks;
                long payloadIntervalTicks = (long)(payload.Interval * TimeSpan.TicksPerSecond);

                if (!_valueFilter(payload.Value))
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
            else
            {
                // TEMPORARY - copy-pasting everything from above, can probably combine this logic

                long payloadTimestampTicks = payloadList[0].Timestamp.Ticks;
                long payloadIntervalTicks = (long)(payloadList[0].Interval * TimeSpan.TicksPerSecond);

                Dictionary<double, double> payloadDict = new();
                
                // Convert payload list to dictionary
                payloadDict = payloadList.ToDictionary(keySelector: p => GetPercentile(p.Metadata), elementSelector: p => p.Value);

                if (!_valueFilter2(payloadDict))
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
            string percentile = metadata.Substring(metadata.IndexOf("Percentile"));
            percentile = percentile.Substring(0, percentile.IndexOf(","));
            percentile = percentile.Replace("Percentile=", string.Empty);

            return Convert.ToDouble(percentile); // This function is currently unverified
        }
    }
}

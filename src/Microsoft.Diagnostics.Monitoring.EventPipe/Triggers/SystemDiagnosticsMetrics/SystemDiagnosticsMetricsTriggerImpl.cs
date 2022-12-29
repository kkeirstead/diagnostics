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
                Func<double, double, bool> evalFunc = settings.HistogramMode.Value == HistogramMode.GreaterThan ?
                    (actual, expected) => actual > expected :
                    (actual, expected) => actual < expected;

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
                SharedTriggerImplHelper.SetDefaultValueFilter(ref _valueFilterDefault, settings.GreaterThan, settings.LessThan);
            }

            SharedTriggerImplHelper.SetIntervalAndWindowTicks(ref _intervalTicks, ref _windowTicks, settings.CounterIntervalSeconds, settings.SlidingWindowDuration.Ticks);
        }

        public bool HasSatisfiedCondition(List<ICounterPayload> payloadList)
        {
            ICounterPayload firstPayload = payloadList[0];

            EventType eventType = firstPayload.EventType;

            if (eventType == EventType.Error)
            {
                // not currently logging the error messages

                return false;
            }
            else
            {
                bool passesValueFilter = (eventType == EventType.Histogram) ?
                    _valueFilterHistogram(CreatePayloadDict(payloadList)) :
                    _valueFilterDefault(firstPayload.Value);

                return SharedTriggerImplHelper.HasSatisfiedCondition(ref _latestTicks, ref _targetTicks, _windowTicks, _intervalTicks, firstPayload, passesValueFilter);
            }
        }

        private Dictionary<string, double> CreatePayloadDict(List<ICounterPayload> payloadList)
        {
            return payloadList.ToDictionary(keySelector: p => GetPercentile(p.Metadata).ToString(), elementSelector: p => p.Value);
        }

        private double GetPercentile(string metadata)
        {
            string percentile = metadata.Substring(metadata.IndexOf(Constants.HistogramPercentileKey));
            if (percentile.Contains(","))
            {
                percentile = percentile[..percentile.IndexOf(",")];
            }
            percentile = percentile.Replace(Constants.HistogramPercentileKey, string.Empty);

            return Convert.ToDouble(percentile);
        }
    }
}

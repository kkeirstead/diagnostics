﻿// Licensed to the .NET Foundation under one or more agreements.
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

            if (!string.IsNullOrEmpty(settings.HistogramPercentile))
            {
                Func<double, double?, double?, bool> evalFunc = null;

                if (settings.GreaterThan.HasValue && settings.LessThan.HasValue)
                {
                    evalFunc = (actual, lowerBound, upperBound) => actual > lowerBound && actual < upperBound;
                }
                else if (settings.GreaterThan.HasValue)
                {
                    evalFunc = (actual, lowerBound, upperBound) => actual > lowerBound;
                }
                else if (settings.LessThan.HasValue)
                {
                    evalFunc = (actual, lowerBound, upperBound) => actual < upperBound;
                }

                _valueFilterHistogram = histogramValues =>
                {
                    if (!histogramValues.TryGetValue(settings.HistogramPercentile, out var value) || !evalFunc(value, settings.LessThan, settings.GreaterThan))
                    {
                        return false;
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

        public bool HasSatisfiedCondition(ICounterPayload payload)
        {
            EventType eventType = payload.EventType;

            if (eventType == EventType.Error || eventType == EventType.CounterEnded)
            {
                // not currently logging the error messages

                return false;
            }
            else
            {
                bool passesValueFilter = (payload is PercentilePayload percentilePayload) ?
                    _valueFilterHistogram(CreatePayloadDict(percentilePayload)) :
                    _valueFilterDefault(payload.Value);

                return SharedTriggerImplHelper.HasSatisfiedCondition(ref _latestTicks, ref _targetTicks, _windowTicks, _intervalTicks, payload, passesValueFilter);
            }
        }

        private Dictionary<string, double> CreatePayloadDict(PercentilePayload percentilePayload)
        {
            return percentilePayload.Quantiles.ToDictionary(keySelector: p => CounterUtilities.CreatePercentile(p.Percentage), elementSelector: p => p.Value);
        }
    }
}

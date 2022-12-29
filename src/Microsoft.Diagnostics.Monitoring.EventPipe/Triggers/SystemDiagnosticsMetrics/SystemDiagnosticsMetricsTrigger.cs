﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.SystemDiagnosticsMetrics
{
    /// <summary>
    /// Trigger that detects when the specified event source counter value is held
    /// above, below, or between threshold values for a specified duration of time.
    /// </summary>
    internal sealed class SystemDiagnosticsMetricsTrigger :
        ITraceEventTrigger
    {
        // A cache of the list of events that are expected from the specified event provider.
        // This is a mapping of event provider name to the event map returned by GetProviderEventMap.
        // This allows caching of the event map between multiple instances of the trigger that
        // use the same event provider as the source of counter events.
        private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, IReadOnlyCollection<string>>> _eventMapCache =
            new ConcurrentDictionary<string, IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(StringComparer.OrdinalIgnoreCase);
        
        // Only care for the SystemDiagnosticsMetrics events from any of the specified providers, thus
        // create a static readonly instance that is shared among all event maps.
        private static readonly string _eventProviderEvent = "System.Diagnostics.Metrics";

        private readonly CounterFilter _filter;
        private readonly SystemDiagnosticsMetricsTriggerImpl _impl;
        private readonly string _providerName;
        private string _sessionId;

        public SystemDiagnosticsMetricsTrigger(SystemDiagnosticsMetricsTriggerSettings settings, string sessionId)
        {
            if (null == settings)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (null == sessionId)
            {
                throw new ArgumentNullException(nameof(sessionId));
            }

            Validate(settings);

            _filter = new CounterFilter(settings.CounterIntervalSeconds);
            _filter.AddFilter(settings.ProviderName, new string[] { settings.CounterName });
            
            _impl = new SystemDiagnosticsMetricsTriggerImpl(settings);

            _providerName = settings.ProviderName;

            _sessionId = sessionId;
        }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> GetProviderEventMap()
        {
            //return null;
            return _eventMapCache.GetOrAdd(_providerName, CreateEventMapForProvider);
        }

        public bool HasSatisfiedCondition(TraceEvent traceEvent)
        {
            // Filter to the counter of interest before forwarding to the implementation
            if (traceEvent.TryGetCounterPayload(_filter, _sessionId, out List<ICounterPayload> payload))
            {
                return _impl.HasSatisfiedCondition(payload);
            }
            return false;
        }

        public static MonitoringSourceConfiguration CreateConfiguration(SystemDiagnosticsMetricsTriggerSettings settings)
        {
            Validate(settings);

            var config = new MetricSourceConfiguration(settings.CounterIntervalSeconds, new string[] { settings.ProviderName }, 1000, 1000); // need to fix these values

            return config;
        }

        private static void Validate(SystemDiagnosticsMetricsTriggerSettings settings)
        {
            ValidationContext context = new(settings);
            Validator.ValidateObject(settings, context, validateAllProperties: true);
        }

        private IReadOnlyDictionary<string, IReadOnlyCollection<string>> CreateEventMapForProvider(string providerName)
        {
            return new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(
                new Dictionary<string, IReadOnlyCollection<string>>()
                {
                    { _eventProviderEvent, new ReadOnlyCollection<string>(Array.Empty<string>()) }
                });
        }
    }
}
﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter;
using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Custom
{
    /// <summary>
    /// Trigger that detects when the specified event source counter value is held
    /// above, below, or between threshold values for a specified duration of time.
    /// </summary>
    internal sealed class CustomTrigger :
        ITraceEventTrigger
    {
        // A cache of the list of events that are expected from the specified event provider.
        // This is a mapping of event provider name to the event map returned by GetProviderEventMap.
        // This allows caching of the event map between multiple instances of the trigger that
        // use the same event provider as the source of counter events.
        //private static ConcurrentDictionary<string, IReadOnlyDictionary<string, IReadOnlyCollection<string>>> _eventMapCache =
        //    new ConcurrentDictionary<string, IReadOnlyDictionary<string, IReadOnlyCollection<string>>>(StringComparer.OrdinalIgnoreCase);

        // Only care for the EventCounters events from any of the specified providers, thus
        // create a static readonly instance that is shared among all event maps.
        //private static readonly IReadOnlyCollection<string> _eventProviderEvents =
        //    new ReadOnlyCollection<string>(new string[] { "EventCounters" });

        private readonly CounterFilter _filter;
        private readonly CustomTriggerImpl _impl;
        private readonly HashSet<string> _providerNames;

        public CustomTrigger(CustomTriggerSettings settings)
        {
            if (null == settings)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Validate(settings);

            _filter = new CounterFilter(settings.CounterIntervalSeconds);

            foreach (var provider in settings.Providers)
            {
                _filter.AddFilter(provider.ProviderName, new string[] { provider.CounterName });
                _providerNames.Add(provider.ProviderName); // Not being used at this time
            }

            _impl = new CustomTriggerImpl(settings);
        }

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> GetProviderEventMap()
        {
            return null; // This might get us all providers/counters? Try this for now.
        }

        public bool HasSatisfiedCondition(TraceEvent traceEvent)
        {
            // Filter to the counter of interest before forwarding to the implementation
            if (traceEvent.TryGetCounterPayload(_filter, out ICounterPayload payload))
            {
                return _impl.PushDataToExtension(payload);
            }
            return false;
        }

        public static MonitoringSourceConfiguration CreateConfiguration(CustomTriggerSettings settings)
        {
            Validate(settings);

            return new MetricSourceConfiguration(settings.CounterIntervalSeconds, settings.Providers.Select(provider => provider.ProviderName).ToArray());
        }

        private static void Validate(CustomTriggerSettings settings)
        {
            ValidationContext context = new(settings);
            Validator.ValidateObject(settings, context, validateAllProperties: true);
        }

        /*
        private IReadOnlyDictionary<string, IReadOnlyCollection<string>> CreateEventMapForProvider(string providerName)
        {
            return new ReadOnlyDictionary<string, IReadOnlyCollection<string>>(
                new Dictionary<string, IReadOnlyCollection<string>>()
                {
                    { _providerName, _eventProviderEvents }
                });
        }*/
    }
}

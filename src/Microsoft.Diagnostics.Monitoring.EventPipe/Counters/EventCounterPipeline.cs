// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.EventPipe
{
    internal class EventCounterPipeline : EventSourcePipeline<EventPipeCounterPipelineSettings>
    {
        private readonly List<IEnumerable<ICountersLogger>> _loggers = new();
        private readonly List<CounterFilter> _filter = new();
        private string _sessionId;

        public EventCounterPipeline() : base()
        {
        }

        public void AddPipeline(DiagnosticsClient client,
           EventPipeCounterPipelineSettings settings,
           IEnumerable<ICountersLogger> loggers)
        {
            AddToPipeline(client, settings);

            if (loggers == null)
            {
                throw new ArgumentNullException(nameof(loggers));
            }

            _loggers.Add(loggers);

            if (settings.CounterGroups.Length > 0)
            {
                _filter.Add(new CounterFilter(settings.CounterIntervalSeconds));
                int filterIndex = _filter.Count;
                foreach (var counterGroup in settings.CounterGroups)
                {
                    _filter[filterIndex - 1].AddFilter(counterGroup.ProviderName, counterGroup.CounterNames);
                }
            }
            else
            {
                _filter.Add(CounterFilter.AllCounters(settings.CounterIntervalSeconds));
            }
        }


        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            HashSet<string> providers = new();

            for (int index = 0; index < _filter.Count; ++index)
            {
                foreach (var provider in _filter[index].GetProviders())
                {
                    providers.Add(provider);
                }
            }

            var config = new MetricSourceConfiguration(Settings[0].CounterIntervalSeconds, providers, Settings[0].MaxHistograms, Settings[0].MaxTimeSeries);

            _sessionId = config.SessionId;

            return config;
        }

        protected override async Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
            for (int index = 0; index < _loggers.Count; ++index)
            {
                ExecuteCounterLoggerAction((metricLogger) => metricLogger.PipelineStarted(), index);
            }

            eventSource.Dynamic.All += traceEvent =>
            {
                try
                {
                    for (int index = 0; index < _filter.Count; ++index)
                    {
                        if (traceEvent.TryGetCounterPayload(_filter[index], _sessionId, out List<ICounterPayload> counterPayload))
                        {
                            ExecuteCounterLoggerAction((metricLogger) => {
                                foreach (var payload in counterPayload)
                                {
                                    metricLogger.Log(payload);
                                }
                            }, index);
                        }
                    }
                }
                catch (Exception)
                {
                }
            };

            using var sourceCompletedTaskSource = new EventTaskSource<Action>(
                taskComplete => taskComplete,
                handler => eventSource.Completed += handler,
                handler => eventSource.Completed -= handler,
                token);

            await sourceCompletedTaskSource.Task;

            for (int index = 0; index < _loggers.Count; ++index)
            {
                ExecuteCounterLoggerAction((metricLogger) => metricLogger.PipelineStopped(), index);
            }
        }

        private void ExecuteCounterLoggerAction(Action<ICountersLogger> action, int loggerIndex)
        {
            foreach (ICountersLogger logger in _loggers[loggerIndex])
            {
                try
                {
                    action(logger);
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }
    }
}

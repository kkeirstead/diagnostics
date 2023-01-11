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
        private readonly List<CounterFilter> _filters = new();
        private string _sessionId;

        private float CounterIntervalSeconds { get => Settings[0].CounterIntervalSeconds; }
        private int MaxHistograms { get => Settings[0].MaxHistograms; }
        private int MaxTimeSeries { get => Settings[0].MaxTimeSeries; }

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
                _filters.Add(new CounterFilter(settings.CounterIntervalSeconds));
                foreach (var counterGroup in settings.CounterGroups)
                {
                    _filters[_filters.Count - 1].AddFilter(counterGroup.ProviderName, counterGroup.CounterNames);
                }
            }
            else
            {
                _filters.Add(CounterFilter.AllCounters(settings.CounterIntervalSeconds));
            }

            Console.WriteLine("(+) Filter Count: " + _filters.Count);
            Console.WriteLine("(+) Logger Count: " + _loggers.Count);
        }


        protected override MonitoringSourceConfiguration CreateConfiguration()
        {
            HashSet<string> providers = new();

            for (int index = 0; index < _filters.Count; ++index)
            {
                foreach (var provider in _filters[index].GetProviders())
                {
                    providers.Add(provider);
                }
            }

            var config = new MetricSourceConfiguration(CounterIntervalSeconds, providers, MaxHistograms, MaxTimeSeries);

            _sessionId = config.SessionId;

            return config;
        }

        protected override void RemovePipeline(TimeSpan duration, Guid identifier)
        {
            int chosenIndex = -1;
            for (int index = 0; index < Settings.Count; ++index)
            {
                if (Settings[index].ID == identifier)
                {
                    chosenIndex = index;
                    break;
                }
            }

            _filters.RemoveAt(chosenIndex);
            _loggers.RemoveAt(chosenIndex);
            Settings.RemoveAt(chosenIndex);
            Client.RemoveAt(chosenIndex);

            for (int index = 0; index < Settings.Count; ++index)
            {
                if (Settings[index].Duration > TimeSpan.Zero)
                {
                    Settings[index].Duration -= duration;
                }
            }

            Console.WriteLine("(-) Filter Count: " + _filters.Count);
            Console.WriteLine("(-) Logger Count: " + _loggers.Count);

            CancellationTokenSource source = new CancellationTokenSource();

            //_ = Task.Run(() => StartAsync(source.Token));
        }

        protected override async Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
            for (int index = 0; index < _loggers.Count; ++index)
            {
                ExecuteCounterLoggerAction((metricLogger) => metricLogger.PipelineStarted(), index);
            }

            var traceEvents = new List<TraceEvent>();

            eventSource.Dynamic.All += traceEvent =>
            {
                try
                {
                    traceEvents.Add(traceEvent);

                    for (int index = 0; index < _filters.Count; ++index)
                    {
                        if (traceEvent.TryGetCounterPayload(_filters[index], _sessionId, out List<ICounterPayload> counterPayload))
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

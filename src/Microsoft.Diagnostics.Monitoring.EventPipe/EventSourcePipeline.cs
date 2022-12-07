﻿// Licensed to the .NET Foundation under one or more agreements.
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
    internal abstract class EventSourcePipeline<T> : Pipeline where T : EventSourcePipelineSettings
    {
        private readonly List<Lazy<DiagnosticsEventPipeProcessor>> _processor;
        public List<DiagnosticsClient> Client { get; }
        public List<T> Settings { get; }

        protected EventSourcePipeline()
        {

        }

        protected void AddToPipeline(DiagnosticsClient client, T settings)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            Client.Add(client);

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Settings.Add(settings);

            _processor.Add(new Lazy<DiagnosticsEventPipeProcessor>(CreateProcessor));
        }

        protected abstract MonitoringSourceConfiguration CreateConfiguration();

        private DiagnosticsEventPipeProcessor CreateProcessor()
        {
            return new DiagnosticsEventPipeProcessor(
                configuration: CreateConfiguration(),
                onEventSourceAvailable: OnEventSourceAvailable);
        }

        protected override Task OnRun(CancellationToken token)
        {
            try
            {
                return _processor[0].Value.Process(Client[0], Settings[0].Duration, token);
            }
            catch (InvalidOperationException e)
            {
                throw new PipelineException(e.Message, e);
            }
        }

        protected override async Task OnCleanup()
        {
            if (_processor[0].IsValueCreated)
            {
                await _processor[0].Value.DisposeAsync();
            }
            await base.OnCleanup();
        }

        protected override async Task OnStop(CancellationToken token)
        {
            if (_processor[0].IsValueCreated)
            {
                Task stoppingTask = _processor[0].Value.StopProcessing(token);

                var taskCompletionSource = new TaskCompletionSource<bool>();
                using IDisposable registration = token.Register(() => taskCompletionSource.SetCanceled());
                await Task.WhenAny(stoppingTask, taskCompletionSource.Task).Unwrap();
            }
        }

        /// <summary>
        /// Starts the pipeline and returns a <see cref="Task"/> that completes when the pipeline
        /// finishes running to completion.
        /// </summary>
        /// <param name="token">The token to monitor for cancellation requests.</param>
        /// <returns>
        /// A task that completes when the event pipe session for the pipeline has started. The inner
        /// task completes when the pipeline runs to completion.
        /// </returns>
        /// <remarks>
        /// The <paramref name="token"/> will cancel the running on the pipeline if it is signaled
        /// before the pipeline runs to completion.
        /// </remarks>
        public async Task<Task> StartAsync(CancellationToken token)
        {
            Task runTask = RunAsync(token);

            // Await both the session started or the run task and return when either is completed.
            // This works around an issue where the run task may fail but not cancel/fault the session
            // started task. Logically, the run task will not successfully complete before the session
            // started task. Thus, the combined task completes either when the session started task is
            // completed OR the run task has cancelled/failed.
            await Task.WhenAny(_processor.Value.SessionStarted, runTask).Unwrap();

            return runTask;
        }

        protected virtual Task OnEventSourceAvailable(EventPipeEventSource eventSource, Func<Task> stopSessionAsync, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter
{
    /// <summary>
    /// The settings for the <see cref="EventCounterTrigger"/>.
    /// </summary>
    internal sealed class EventCounterTriggerSettings :
        IValidatableObject
    {
        /// <summary>
        /// The name of the event provider from which counters will be monitored.
        /// </summary>
        [Required]
        public string ProviderName { get; set; }

        /// <summary>
        /// The name of the event counter from the event provider to monitor.
        /// </summary>
        [Required]
        public string CounterName { get; set; }

        /// <summary>
        /// The lower bound threshold that the event counter value must hold for
        /// the duration specified in <see cref="SlidingWindowDuration"/>.
        /// </summary>
        public double? GreaterThan { get; set; }

        /// <summary>
        /// The upper bound threshold that the event counter value must hold for
        /// the duration specified in <see cref="SlidingWindowDuration"/>.
        /// </summary>
        public double? LessThan { get; set; }

        /// <summary>
        /// The sliding duration of time in which the event counter must maintain a value
        /// above, below, or between the thresholds specified by <see cref="GreaterThan"/> and <see cref="LessThan"/>.
        /// </summary>
        [Range(typeof(TimeSpan), SharedTriggerSettingsConstants.SlidingWindowDuration_MinValue, SharedTriggerSettingsConstants.SlidingWindowDuration_MaxValue)]
        public TimeSpan SlidingWindowDuration { get; set; }

        /// <summary>
        /// The sampling interval of the event counter.
        /// </summary>
        [Range(SharedTriggerSettingsConstants.CounterIntervalSeconds_MinValue, SharedTriggerSettingsConstants.CounterIntervalSeconds_MaxValue)]
        public float CounterIntervalSeconds { get; set; }

        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            return SharedTriggerSettingsValidation.Validate(GreaterThan, LessThan);
        }
    }
}

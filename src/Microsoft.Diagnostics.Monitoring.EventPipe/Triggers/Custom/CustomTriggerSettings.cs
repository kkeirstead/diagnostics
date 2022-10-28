﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Custom
{
    /// <summary>
    /// The settings for the <see cref="CustomTrigger"/>.
    /// </summary>
    internal sealed class CustomTriggerSettings :
        IValidatableObject
    {
        internal const float CounterIntervalSeconds_MaxValue = 24 * 60 * 60; // 1 day
        internal const float CounterIntervalSeconds_MinValue = 1; // 1 second

        public string ExtensionName { get; set; }

        public string Args { get; set; }

        public Provider[] Providers { get; set; }

        public float CounterIntervalSeconds { get; set; }

        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            List<ValidationResult> results = new();

            return results;
        }
    }
}

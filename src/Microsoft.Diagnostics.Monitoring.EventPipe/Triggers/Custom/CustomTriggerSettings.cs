// Licensed to the .NET Foundation under one or more agreements.
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

        internal const string EitherGreaterThanLessThanMessage = "Either the " + nameof(GreaterThan) + " field or the " + nameof(LessThan) + " field are required.";

        internal const string GreaterThanMustBeLessThanLessThanMessage = "The " + nameof(GreaterThan) + " field must be less than the " + nameof(LessThan) + " field.";

        internal const string SlidingWindowDuration_MaxValue = "1.00:00:00"; // 1 day
        internal const string SlidingWindowDuration_MinValue = "00:00:01"; // 1 second

        public string ExtensionName { get; set; }

        public string Args { get; set; }

        //public Provider[] ProvidersToInclude { get; set; }



    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            List<ValidationResult> results = new();

            if (!GreaterThan.HasValue && !LessThan.HasValue)
            {
                results.Add(new ValidationResult(
                    EitherGreaterThanLessThanMessage,
                    new[]
                    {
                        nameof(GreaterThan),
                        nameof(LessThan)
                    }));
            }
            else if (GreaterThan.HasValue && LessThan.HasValue)
            {
                if (GreaterThan.Value >= LessThan.Value)
                {
                    results.Add(new ValidationResult(
                        GreaterThanMustBeLessThanLessThanMessage,
                        new[]
                        {
                            nameof(GreaterThan),
                            nameof(LessThan)
                        }));
                }
            }

            return results;
        }
    }
}

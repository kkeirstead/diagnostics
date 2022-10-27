// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.EventCounter;

namespace Microsoft.Diagnostics.Monitoring.EventPipe.Triggers.Custom
{
    /// <summary>
    /// The trigger factory for the <see cref="CustomTrigger"/>.
    /// </summary>
    internal sealed class CustomTriggerFactory :
        ITraceEventTriggerFactory<CustomTriggerSettings>
    {
        public ITraceEventTrigger Create(CustomTriggerSettings settings)
        {
            return new CustomTrigger(settings);
        }
    }
}

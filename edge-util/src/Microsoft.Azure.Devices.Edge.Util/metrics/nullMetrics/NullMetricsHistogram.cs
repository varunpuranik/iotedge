// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System.Collections.Generic;

    public class NullMetricsHistogram : IMetricsHistogram
    {
        public void Update(long value)
        {
        }

        public void Update(long value, Dictionary<string, string> tags)
        {
        }
    }
}
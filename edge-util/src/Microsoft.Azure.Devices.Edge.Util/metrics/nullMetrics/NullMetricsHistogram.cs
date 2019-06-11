// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Metrics.NullMetrics
{
    using System.Collections.Generic;

    public class NullMetricsHistogram : IMetricsHistogram
    {
        public void Update(double value)
        {
        }

        public void Update(double value, Dictionary<string, string> tags)
        {
        }
    }
}

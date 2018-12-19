// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    public class TwinInfo
    {
        public Shared.Twin Twin { get; }

        public TwinCollection ReportedPropertiesPatch { get; }

        [JsonConstructor]
        public TwinInfo(Shared.Twin twin, TwinCollection reportedPropertiesPatch)
        {
            this.Twin = twin;
            this.ReportedPropertiesPatch = reportedPropertiesPatch;
        }
    }    
}

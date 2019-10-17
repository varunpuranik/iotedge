// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Config
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class StoreLimits : IEquatable<StoreLimits>
    {
        [JsonConstructor]
        public StoreLimits(long maxSize)
            : this(maxSize, Option.None<int>())
        {
        }

        public StoreLimits(long maxSize, Option<int> checkFrequency)
        {
            this.MaxSize = maxSize;
            this.CheckFrequency = checkFrequency;
        }

        [JsonProperty(PropertyName = "maxSize")]
        public long MaxSize { get; }

        [JsonIgnore]
        [JsonProperty(PropertyName = "checkFrequency")]
        [JsonConverter(typeof(OptionConverter<long>), true)]
        public Option<int> CheckFrequency { get; }

        public bool Equals(StoreLimits other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return this.MaxSize == other.MaxSize &&
                this.CheckFrequency.Equals(other.CheckFrequency);
        }

        public override bool Equals(object obj)
            => this.Equals(obj as StoreLimits);

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = this.MaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ this.CheckFrequency.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(StoreLimits left, StoreLimits right) => Equals(left, right);

        public static bool operator !=(StoreLimits left, StoreLimits right) => !Equals(left, right);
    }
}

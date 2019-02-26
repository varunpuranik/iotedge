// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class EnvVal : IEquatable<EnvVal>
    {
        [JsonConstructor]
        public EnvVal(string value, string secretValue)
        {
            this.Value = Option.Maybe(value);
            this.SecretValue = Option.Maybe(secretValue);
        }

        [JsonProperty("value")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> Value { get; }

        [JsonProperty("secretKeyRef")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> SecretValue { get; }

        public override bool Equals(object obj) => this.Equals(obj as EnvVal);

        public bool Equals(EnvVal other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<Option<string>>.Default.Equals(this.Value, other.Value) &&
                   EqualityComparer<Option<string>>.Default.Equals(this.SecretValue, other.SecretValue);
        }

        public override int GetHashCode()
        {
            int hashCode = -874519432;
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<string>>.Default.GetHashCode(this.Value);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<string>>.Default.GetHashCode(this.SecretValue);
            return hashCode;
        }
    }
}

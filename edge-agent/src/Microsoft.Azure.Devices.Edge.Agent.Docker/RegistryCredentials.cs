// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Docker
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Newtonsoft.Json;

    public class RegistryCredentials : IEquatable<RegistryCredentials>
    {
        public RegistryCredentials(string address, string username, string password, string passwordKeyRef)
        {
            this.Address = Preconditions.CheckNonWhiteSpace(address, nameof(address));
            this.Username = Preconditions.CheckNonWhiteSpace(username, nameof(username));
            this.Password = Option.Maybe(password);
            this.SecretPassword = Option.Maybe(passwordKeyRef);
        }

        [JsonProperty(Required = Required.Always, PropertyName = "address")]
        public string Address { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "username")]
        public string Username { get; }

        [JsonProperty(PropertyName = "password")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> Password { get; }

        [JsonProperty(PropertyName = "passwordKeyRef")]
        [JsonConverter(typeof(OptionConverter<string>))]
        public Option<string> SecretPassword { get; }

        public override bool Equals(object obj) => this.Equals(obj as RegistryCredentials);

        public bool Equals(RegistryCredentials other) =>
            other != null && 
            this.Address == other.Address &&
            this.Username == other.Username &&
            EqualityComparer<Option<string>>.Default.Equals(this.Password, other.Password) &&
            EqualityComparer<Option<string>>.Default.Equals(this.SecretPassword, other.SecretPassword);

        public override int GetHashCode()
        {
            int hashCode = 217634204;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Address);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Username);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<string>>.Default.GetHashCode(this.Password);
            hashCode = hashCode * -1521134295 + EqualityComparer<Option<string>>.Default.GetHashCode(this.SecretPassword);
            return hashCode;
        }
    }
}

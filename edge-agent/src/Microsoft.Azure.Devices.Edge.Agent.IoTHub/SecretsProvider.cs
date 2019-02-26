// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.IoTHub
{
    using System;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json.Linq;

    public class SecretsProvider : ISecretsProvider
    {
        const string ApiVersion = @"api-version=2016-10-01";
        readonly IAadTokenProvider aadTokenProvider;

        public SecretsProvider(IAadTokenProvider aadTokenProvider)
        {
            this.aadTokenProvider = aadTokenProvider;
        }

        public async Task<string> GetSecret(string secretUrl)
        {
            Preconditions.CheckNonWhiteSpace(secretUrl, nameof(secretUrl));
            using (HttpClient client = new HttpClient())
            {
                Console.WriteLine($"Getting secret - {secretUrl}");
                var requestUri = new Uri($"{secretUrl}/?{ApiVersion}");
                
                var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
                string token = await this.aadTokenProvider.GetToken();
                request.Headers.Add("Authorization", $"Bearer {token}");
                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error getting token - {response.StatusCode} / {content}");
                }

                var responseJson = JObject.Parse(content);
                string secretValue = responseJson["value"].ToString();
                Console.WriteLine($"Received secret {secretUrl} = {secretValue}");
                return secretValue;
            }
        }
    }

    public interface IAadTokenProvider
    {
        Task<string> GetToken();
    }

    public class AadTokenProvider : IAadTokenProvider
    {
        readonly Uri tokenUri;

        public AadTokenProvider(string tokenUri)
        {
            this.tokenUri = new Uri(tokenUri);
        }

        public async Task<string> GetToken()
        {
            using (HttpClient client = new HttpClient())
            {
                var response = await client.GetAsync(this.tokenUri);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Error getting token - {response.StatusCode} / {content}");
                }

                Console.WriteLine($"Obtained token - {content}");
                return content;
            }
        }
    }
}

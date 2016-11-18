using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Slamby.API.Services.Interfaces;
using Slamby.Common.DI;
using Slamby.Common.Helpers;
using Slamby.Elastic.Factories;
using Slamby.License.Core.Models;

namespace Slamby.API.Services
{
    [SingletonDependency(ServiceType = typeof(ILicenseServerClient))]
    public class LicenseServerClient : ILicenseServerClient
    {
        readonly Uri LicenseServerUri = new Uri("https://license.slamby.com/");

        readonly ElasticClientFactory clientFactory;

        public LicenseServerClient(ElasticClientFactory clientFactory)
        {
            this.clientFactory = clientFactory;
        }

        public async Task<CheckResponseModel> RequestCheck(string xmlText, Guid instanceId, DateTime launchTime)
        {
            try
            {
                var elasticName = clientFactory.GetClient().RootNodeInfo()?.Name ?? string.Empty;

                using (var client = CreateHttpClient())
                {
                    var model = new CheckRequestModel()
                    {
                        Id = instanceId,
                        License = xmlText.ToBase64(),
                        Details = new CheckDetailsModel()
                        {
                            LaunchTime = launchTime,
                            Cores = Environment.ProcessorCount,
                            ElasticName = elasticName
                        }
                    };

                    var json = JsonConvert.SerializeObject(model);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("api/check", content);

                    if (response.StatusCode != System.Net.HttpStatusCode.BadRequest &&
                        response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return null;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var checkResponse = JsonConvert.DeserializeObject<CheckResponseModel>(responseBody);

                    return checkResponse;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<CreateResponseModel> Create(string email, Guid instanceId)
        {
            try
            {
                using (var client = CreateHttpClient())
                {
                    var model = new CreateOpenSourceModel()
                    {
                        Id = instanceId,
                        Email = email
                    };

                    var json = JsonConvert.SerializeObject(model);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync("api/create", content);

                    if (response.StatusCode != System.Net.HttpStatusCode.BadRequest &&
                        response.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        return null;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    var createResponse = JsonConvert.DeserializeObject<CreateResponseModel>(responseBody);

                    return createResponse;
                }
            }
            catch
            {
                return null;
            }
        }

        private HttpClient CreateHttpClient()
        {
            var client = new HttpClient();

            client.BaseAddress = LicenseServerUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }
    }
}

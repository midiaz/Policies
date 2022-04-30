using Newtonsoft.Json;
using Polly;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using TestPolicies.External;
using TestPolicies.Models;
using TestPolicies.Services.Interfaces;

namespace TestPolicies.Services
{
    public class WeatherForecastServices : IWeatherForecastServices
    {
        public async Task<IEnumerable> GetWeatherForecast(bool? useCache = true)
        {
            var OwnerId = "test_ownerID";
            IAsyncPolicy<HttpResponseMessage> policy;
            if (useCache.Value)
                policy = Helpers.PolicyFactory.Instance.GetPolicyByOwnerId(OwnerId);
            else
                policy = Helpers.PolicyFactory.Instance.GetPolicyWithCacheByOwnerId(OwnerId);

            var context = new Context($"GetWeatherForecast(pais={"Argentina"}, provincia={"Mendoza"})");
            var result = await policy.ExecuteAsync(async (ctx) => await ExternalAPI.Instance.GetWeatherForecast(), context);
            var content = await result.Content.ReadAsStringAsync();
            var summaries = JsonConvert.DeserializeObject<List<WeatherForecast>>(content);
            return summaries;
        }

        public async Task<IEnumerable> GetWeatherForecastWithoutPolicies()
        {
            var result =  await ExternalAPI.Instance.GetWeatherForecast();
            var content = await result.Content.ReadAsStringAsync();
            var summaries = JsonConvert.DeserializeObject<List<WeatherForecast>>(content);
            return summaries;
        }
    }
}

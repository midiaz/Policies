using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using TestPolicies.Models;

namespace TestPolicies.External
{
    public sealed class ExternalAPI
    {
        private readonly int _default_request_limit = 10;
        private int _requestCount = 0;

        private static readonly string[] Summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };

        private Random rng = new Random();

        public async Task<HttpResponseMessage> GetWeatherForecast()
        {
            _requestCount++;
            var unauthorized = rng.Next(_requestCount, _requestCount + 5);
            if (unauthorized == _requestCount)
            {
                throw new HttpRequestException("Unauthorized.", null, System.Net.HttpStatusCode.Unauthorized);
            }
            else if (_default_request_limit <= _requestCount)
            {
                _requestCount = 0;
                throw new HttpRequestException($"Request Limit Exceeded. Request Limit: {_default_request_limit}", null, System.Net.HttpStatusCode.TooManyRequests);
            }
            else
            {
                var result = new HttpResponseMessage();
                result.Content = new StringContent(JsonConvert.SerializeObject(GetSummaries()));
                result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                return await Task.FromResult(result);
            }


        }

        private List<WeatherForecast> GetSummaries()
        {

            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = rng.Next(-20, 55),
                Summary = Summaries[rng.Next(Summaries.Length)]
            })
            .ToList();
        }

        #region singleton
        private static ExternalAPI instance = null;

        ExternalAPI()
        {
        }

        public static ExternalAPI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ExternalAPI();
                }
                return instance;
            }
        }
        #endregion
    }
}

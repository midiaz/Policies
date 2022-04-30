using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using TestPolicies.Services.Interfaces;

namespace TestPolicies.Controllers
{
    [ApiController]
    public class PoliticasController : ControllerBase
    {
        private readonly IWeatherForecastServices _weatherForecastServices;
        public PoliticasController(ILogger<PoliticasController> logger, IWeatherForecastServices weatherForecastServices)
        {
            _logger = logger;
            _weatherForecastServices = weatherForecastServices;
        }



        private readonly ILogger<PoliticasController> _logger;

        /// <summary>
        /// Test GET ../WeatherForecast using policies
        /// </summary>
        /// <param name="useCache"></param>
        [HttpGet]
        [Route("test/WeatherForecast/WaitAndRetry")]
        public async Task<IActionResult> WaitAndRetry(bool? useCache = true)
        {
            try
            {
                var summaries = await _weatherForecastServices.GetWeatherForecast(useCache);
                return Ok(summaries);
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Test GET ../WeatherForecast without using policies
        /// </summary>
        [HttpGet]
        [Route("test/WeatherForecast")]
        public async Task<IActionResult> WeatherForecast()
        {
            try
            {
                var summaries = await _weatherForecastServices.GetWeatherForecastWithoutPolicies();
                return Ok(summaries);
            }
            catch (Exception ex)
            {

                return BadRequest(ex.Message);
            }
        }
    }
}

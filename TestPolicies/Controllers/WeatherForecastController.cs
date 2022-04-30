using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Threading.Tasks;
using TestPolicies.Services.Interfaces;

namespace TestPolicies.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private readonly ILogger<WeatherForecastController> _logger;
        private readonly IWeatherForecastServices _weatherForecastServices;
        public WeatherForecastController(ILogger<WeatherForecastController> logger, IWeatherForecastServices weatherForecastServices)
        {
            _logger = logger;
            _weatherForecastServices = weatherForecastServices;
        }

        [HttpGet]
        public async Task<IEnumerable> Get()
        {
            return await _weatherForecastServices.GetWeatherForecast();
        }
    }
}

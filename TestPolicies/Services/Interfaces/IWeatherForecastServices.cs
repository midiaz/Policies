using System.Collections;
using System.Threading.Tasks;

namespace TestPolicies.Services.Interfaces
{
    public interface IWeatherForecastServices
    {
        Task<IEnumerable> GetWeatherForecast(bool? useCache = true);
        Task<IEnumerable> GetWeatherForecastWithoutPolicies();
        
    }
}
using Microsoft.Extensions.Caching.Memory;
using Polly;
using Polly.Caching;
using Polly.Caching.Memory;
using Polly.CircuitBreaker;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;


namespace TestPolicies.Services.Helpers
{

    public sealed class PolicyFactory
    {

        #region Singleton
        private static PolicyFactory instance = null;
        private static readonly object mutex = new object();
        private static ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>> policiesByOwner = new ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>>();
        private static ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>> retryPolicyByOwner = new ConcurrentDictionary<string, IAsyncPolicy<HttpResponseMessage>>();
        private static readonly int defaultSleepTime = 5;
        private static readonly int defaultSleepTime401 = 3;
        private static readonly int defaultSleepTime500 = 3;
        private static readonly int defaultSleepTime503 = 3;
        private static readonly int defaultSleepTime429 = 10;
        private static readonly int defaultSleepTimeCircuitBreaker = 20;

        //private DateTime? circuitBreakerDateTime;
        //private DateTime? postPutCircuitBreakerDateTime;

        PolicyFactory()
        {
        }

        public static PolicyFactory Instance
        {
            get
            {
                lock (mutex)
                {
                    if (instance == null)
                    {
                        instance = new PolicyFactory();
                    }
                    return instance;
                }
            }
        }
        #endregion

        public IAsyncPolicy<HttpResponseMessage> GetPolicyByOwnerId(string ownerId)
        {
            try
            {
                lock (policiesByOwner)
                {
                    return policiesByOwner.GetOrAdd(ownerId, _ => CreatePolicy(ownerId));
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public IAsyncPolicy<HttpResponseMessage> GetPolicyWithCacheByOwnerId(string ownerId)
        {
            try
            {
                lock (retryPolicyByOwner)
                {
                    return retryPolicyByOwner.GetOrAdd(ownerId, _ => CreateRetryPolicyWithoutCache(ownerId));
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private double GetServerWaitDuration(HttpRequestException exception)
        {
            try
            {
                throw new NotImplementedException();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private double GetMillisecondsWaitDurationFromResponse(HttpResponseMessage response)
        {
            try
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    return defaultSleepTime401;

                if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    return defaultSleepTime503;

                if (response.StatusCode == HttpStatusCode.InternalServerError)
                    return defaultSleepTime500;

                if ((int)response.StatusCode == 429)
                    return defaultSleepTime429;

                var defaultSleepTimeInMilliseconds = (defaultSleepTime * 1000);
                var retryAfter = response?.Headers?.RetryAfter;
                if (retryAfter != null)
                {
                    return retryAfter.Delta.HasValue
                        ? retryAfter.Delta.Value.Milliseconds == 0 ? defaultSleepTimeInMilliseconds : retryAfter.Delta.Value.Milliseconds
                        : defaultSleepTimeInMilliseconds;
                }
                else
                {
                    //Log.Info($"Before rateLimitRemaining");
                    if (response.Headers.Contains("X-Rate-Limit-Remaining"))
                    {
                        var rateLimitRemaining = response.Headers.First(i => i.Key == "X-Rate-Limit-Remaining").Value?.FirstOrDefault();
                        if (rateLimitRemaining != null && !string.IsNullOrWhiteSpace(rateLimitRemaining) && rateLimitRemaining == "0")
                        {
                            if (response.Headers.Contains("X-Rate-Limit-Window"))
                            {
                                //Log.Info($"is rateLimitRemaining: {rateLimitRemaining}");
                                var sleepTime = response.Headers.First(i => i.Key == "X-Rate-Limit-Window").Value?.FirstOrDefault();
                                if (sleepTime != null && !string.IsNullOrWhiteSpace(sleepTime))
                                {
                                    var sleepTimeDouble = Double.Parse(sleepTime) * 1000;
                                    if (sleepTimeDouble == 0)
                                        sleepTimeDouble = defaultSleepTimeInMilliseconds;

                                    return (sleepTimeDouble);
                                }
                                else
                                {
                                    return defaultSleepTimeInMilliseconds;
                                }
                            }
                            else
                            {
                                return defaultSleepTimeInMilliseconds;
                            }
                        }
                        else
                        {
                            return defaultSleepTimeInMilliseconds;
                        }
                    }
                    else
                    {
                        return defaultSleepTimeInMilliseconds;
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public IAsyncCacheProvider memoryCacheProvider = new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions()));

        private readonly Func<HttpResponseMessage, Ttl> cacheOnly200OKfilter = (result) => new Ttl(timeSpan: result.StatusCode == HttpStatusCode.OK ? TimeSpan.FromMinutes(1) : TimeSpan.Zero,
                                                                                             slidingExpiration: false
                                                                                            );

        private IAsyncPolicy<HttpResponseMessage> CreatePolicy(string ownerId)
        {
            var retryPolicy = Policy.Handle<HttpRequestException>(ex => (int)ex.StatusCode == 429 ||
                                                                        ex.StatusCode == HttpStatusCode.Unauthorized ||
                                                                        ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
                                                                        ex.StatusCode == HttpStatusCode.InternalServerError)
                                    .Or<BrokenCircuitException>()
                                    .OrResult<HttpResponseMessage>(r => (int)r.StatusCode == 429 ||
                                                                        r.StatusCode == HttpStatusCode.Unauthorized ||
                                                                        r.StatusCode == HttpStatusCode.ServiceUnavailable ||
                                                                        r.StatusCode == HttpStatusCode.InternalServerError)
                                                                .WaitAndRetryAsync(
                                                                    retryCount: 5,
                                                                    sleepDurationProvider: (retryCount, response, context) =>
                                                                    {
                                                                        if (response == null)
                                                                        {
                                                                            return TimeSpan.FromSeconds(defaultSleepTime);
                                                                        }
                                                                        else if (response.Exception != null && response.Exception is BrokenCircuitException)
                                                                        {
                                                                            return TimeSpan.FromSeconds(defaultSleepTimeCircuitBreaker);
                                                                        }
                                                                        else if (response.Exception != null && response.Exception is HttpRequestException)
                                                                        {
                                                                            var httpException = (HttpRequestException)response.Exception;
                                                                            if (httpException.StatusCode == HttpStatusCode.Unauthorized)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime401);

                                                                            if (httpException.StatusCode == HttpStatusCode.ServiceUnavailable)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime503);

                                                                            if (httpException.StatusCode == HttpStatusCode.InternalServerError)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime500);

                                                                            if ((int)httpException.StatusCode == 429)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime429);

                                                                            var waitDuration = GetMillisecondsWaitDurationFromResponse(response.Result);
                                                                            return TimeSpan.FromMilliseconds(waitDuration);
                                                                        }
                                                                        else if (response.Result != null)
                                                                        {
                                                                            var waitDuration = GetMillisecondsWaitDurationFromResponse(response.Result);
                                                                            return TimeSpan.FromMilliseconds(waitDuration);
                                                                        }
                                                                        else
                                                                        {
                                                                            return TimeSpan.FromSeconds(defaultSleepTime);
                                                                        }
                                                                    },
                                                                    onRetryAsync: async (response, timespan, retryCount, context) =>
                                                                    {
                                                                        Console.WriteLine($"{DateTime.Now.ToLongTimeString()}-retryPolicyByOwner.onRetryAsync. retryCount:{retryCount}, timespan:{timespan.TotalSeconds}, endpoint:{context.OperationKey}");
                                                                        await Task.FromResult("");
                                                                    }
                                                                );


            var ttlStrategy = new ResultTtl<HttpResponseMessage>(cacheOnly200OKfilter);
            IAsyncPolicy<HttpResponseMessage> cacheOnly200OKpolicy = Policy.CacheAsync<HttpResponseMessage>(cacheProvider: memoryCacheProvider.AsyncFor<HttpResponseMessage>(),
                                                                                                            ttlStrategy: ttlStrategy,
                                                                                                            cacheKeyStrategy: (context) => context.OperationKey,
                                                                                                            onCacheGet: (context, key) => { },
                                                                                                            onCacheMiss: (context, key) => { },
                                                                                                            onCachePut: (context, key) => { },
                                                                                                            onCacheGetError: (context, key, exception) =>
                                                                                                            {
                                                                                                                Console.WriteLine(exception.Message);
                                                                                                            },
                                                                                                            onCachePutError: (context, key, exception) =>
                                                                                                            {
                                                                                                                Console.WriteLine(exception.Message);
                                                                                                            });



            var circuitBreakerPolicy = Policy.Handle<HttpRequestException>(ex => (int)ex.StatusCode == 429)
                .OrResult<HttpResponseMessage>(r => (int)r.StatusCode == 429)
                .CircuitBreakerAsync(1, TimeSpan.FromSeconds(defaultSleepTime429),
                (result, t) =>
                {
                    Console.WriteLine($"{DateTime.Now.ToLongTimeString()}-Circuit broken!. Message:{result.Exception.Message}");
                },
                () =>
                {
                    Console.WriteLine("{DateTime.Now.ToLongTimeString()}-Circuit Reset!.");
                });

            IAsyncPolicy<HttpResponseMessage> policies = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, cacheOnly200OKpolicy);

            return policies;
        }

        private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicyWithoutCache(string ownerId)
        {
            //Log.Error("INIT CreateRetryPolicyForPostPut");
            var retryPolicy = Policy.Handle<HttpRequestException>(ex => (int)ex.StatusCode == 429 || ex.StatusCode == HttpStatusCode.Unauthorized || ex.StatusCode == HttpStatusCode.ServiceUnavailable || ex.StatusCode == HttpStatusCode.InternalServerError)
                                    .Or<BrokenCircuitException>()
                                    .OrResult<HttpResponseMessage>(response => (int)response.StatusCode == 429 || response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.ServiceUnavailable || response.StatusCode == HttpStatusCode.InternalServerError)
                                                                .WaitAndRetryAsync(
                                                                    retryCount: 50,
                                                                    sleepDurationProvider: (retryCount, response, context) =>
                                                                    {
                                                                        if (response == null)
                                                                        {
                                                                            return TimeSpan.FromSeconds(defaultSleepTime);
                                                                        }
                                                                        else if (response.Exception != null && response.Exception is BrokenCircuitException)
                                                                        {
                                                                            return TimeSpan.FromSeconds(defaultSleepTimeCircuitBreaker);
                                                                        }
                                                                        else if (response.Exception != null && response.Exception is HttpRequestException)
                                                                        {
                                                                            var httpException = (HttpRequestException)response.Exception;
                                                                            if (httpException.StatusCode == HttpStatusCode.Unauthorized)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime401);

                                                                            if (httpException.StatusCode == HttpStatusCode.ServiceUnavailable)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime503);

                                                                            if (httpException.StatusCode == HttpStatusCode.InternalServerError)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime500);

                                                                            if ((int)httpException.StatusCode == 429)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime429);

                                                                            var waitDuration = GetServerWaitDuration((HttpRequestException)response.Exception);
                                                                            return TimeSpan.FromMilliseconds(waitDuration);
                                                                        }
                                                                        else if (response.Result != null)
                                                                        {
                                                                            if (response.Result.StatusCode == HttpStatusCode.Unauthorized)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime401);

                                                                            if (response.Result.StatusCode == HttpStatusCode.ServiceUnavailable)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime503);

                                                                            if (response.Result.StatusCode == HttpStatusCode.InternalServerError)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime500);

                                                                            if ((int)response.Result.StatusCode == 429)
                                                                                return TimeSpan.FromSeconds(defaultSleepTime429);

                                                                            var waitDuration = GetMillisecondsWaitDurationFromResponse(response.Result);
                                                                            return TimeSpan.FromMilliseconds(waitDuration);
                                                                        }
                                                                        else
                                                                        {
                                                                            return TimeSpan.FromSeconds(defaultSleepTime);
                                                                        }
                                                                    },
                                                                    onRetryAsync: async (response, timespan, retryCount, context) =>
                                                                    {
                                                                        Console.WriteLine($"{DateTime.Now.ToLongTimeString()}-onRetryAsync. Status: {response?.Result?.StatusCode} - Message: {response?.Exception?.Message} - retryCount:{retryCount}, timespan:{timespan.TotalSeconds}, endpoint:{context.OperationKey}");
                                                                        await Task.FromResult("");
                                                                    }
                                                                );

            var circuitBreakerPolicy = Policy.Handle<HttpRequestException>(ex => (int)ex.StatusCode == 429)
               .OrResult<HttpResponseMessage>(r => (int)r.StatusCode == 429)
                .CircuitBreakerAsync(1, TimeSpan.FromSeconds(defaultSleepTime429),
               (result, t) =>
               {
                   Console.WriteLine($"{DateTime.Now.ToLongTimeString()}-Circuit broken!. Message:{result.Exception.Message} - timesnap:{t}");
               },
                () =>
                {
                    Console.WriteLine($"{DateTime.Now.ToLongTimeString()}-Circuit Reset!.");
                });

            IAsyncPolicy<HttpResponseMessage> policies = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
            return policies;
        }
    }
}

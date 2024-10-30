using Caching;
using Microsoft.AspNetCore.Mvc;

namespace Events.API.Controllers
{
    public class BaseController : Controller
    {
        private readonly IConfiguration _config;
        protected readonly bool _usingRedis;

        public BaseController(IConfiguration configuration)
        {
            _config = configuration;

            RedisCaching.RedisUrl = GetRedisConnectionString();
            bool.TryParse(_config["RedisCaching:Enabled"], out _usingRedis);
        }

        private string GetRedisConnectionString()
        {
            var awsRedisEndpoint = Environment.GetEnvironmentVariable("elasticache_endpoint");

            if (String.IsNullOrEmpty(awsRedisEndpoint) is false)
                awsRedisEndpoint = $"{awsRedisEndpoint}:6379,ssl=true";

            return awsRedisEndpoint
                        ?? _config["RedisCaching:Url"]
                        ?? string.Empty;
        }

        protected async Task<T> RetreiveModelResult<T>(string redisDataKey, Func<Task<T>> funcName)
        {
            if (_usingRedis && RedisCaching.KeyCheck(redisDataKey))
            {
                return RedisCaching.GetKey<T>(redisDataKey);
            }

            var model = await funcName();

            if (_usingRedis)
            {
                RedisCaching.SetKey<T>(redisDataKey, model);
            }

            return model;
        }

        protected async Task<T> RetreiveModelAsync<T>(string redisDataKey, Func<Task<T>> funcName)
        {
            if (_usingRedis && RedisCaching.KeyCheck(redisDataKey))
            {
                return RedisCaching.GetKey<T>(redisDataKey);
            }

            var model = await funcName();

            if (_usingRedis)
            {
                RedisCaching.SetKey<T>(redisDataKey, model);
            }

            return model;
        }

        protected void DeleteAsync(string redisDataKey)
        {
            if (_usingRedis && RedisCaching.KeyCheck(redisDataKey))
            {
                RedisCaching.DeleteKey(redisDataKey);
            }
        }

        protected void DeleteStartsWithAsync(string redisDataKey)
        {
            if (_usingRedis)
            {
                RedisCaching.DeleteKeyStartWith(redisDataKey);
            }
        }
    }
}
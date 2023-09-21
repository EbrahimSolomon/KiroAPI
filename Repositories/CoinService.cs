using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Kiron_Interactive.CachingLayer;
using KironAPI.Models;

namespace KironAPI.Repositories
{
    public class CoinService
    {
        private const string Url = "https://www.gov.uk/bank-holidays.json";
        private const string CacheKey = "CoinStats";
        private readonly CacheManager _cacheManager;
        private readonly HttpClient _httpClient;
        private static readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public CoinService(CacheManager cacheManager)
        {
            _cacheManager = cacheManager;
            _httpClient = new HttpClient();
        }

        public async Task<CoinModel> GetCoinStatsAsync()
        {
            if (!_cacheManager.Contains(CacheKey))
            {
                await _semaphoreSlim.WaitAsync();
                try
                {
                    // Check again to see if another thread fetched the data before this one acquired the semaphore
                    if (!_cacheManager.Contains(CacheKey))
                    {
                        var response = await _httpClient.GetStringAsync(Url);
                        var data = JsonSerializer.Deserialize<CoinModel>(response);

                        _cacheManager.Add(CacheKey, data, TimeSpan.FromHours(1));
                        return data;
                    }
                }
                finally
                {
                    _semaphoreSlim.Release();
                }
            }

            return _cacheManager.Get<CoinModel>(CacheKey);
        }
    }
}

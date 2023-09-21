using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KironAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace KironAPI.Controllers
{
    [ApiController]
[Route("api/coin")]
public class CoinStatsController : ControllerBase
{
    private readonly CoinService _coinService;

    public CoinStatsController(CoinService coinService)
    {
        _coinService = coinService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCoinStatsAsync()
    {
        var coinStats = await _coinService.GetCoinStatsAsync();
        return Ok(coinStats);
    }
}

}
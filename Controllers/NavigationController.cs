using KironAPI.Repositories;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("[controller]")]
public class NavigationController : ControllerBase
{
    private readonly NavigationRepository _navigationRepository; 

    public NavigationController(NavigationRepository navigationRepository)
    {
        _navigationRepository = navigationRepository;
    }

    [HttpGet("get-navigation-hierarchy")]
    public async Task<IActionResult> GetNavigationHierarchy()
    {
        var hierarchy = await _navigationRepository.GetNavigationHierarchy();
        return Ok(hierarchy);
    }
}

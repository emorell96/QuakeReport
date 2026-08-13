using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.Contracts.Dtos;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/earthquakes")]
public class EarthquakesController(IActiveEarthquakeService activeEarthquakeService) : ControllerBase
{
    [HttpGet("active")]
    [ProducesResponseType<EarthquakeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EarthquakeResponse>> GetActive(CancellationToken cancellationToken)
    {
        var earthquake = await activeEarthquakeService.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
        {
            return NotFound();
        }

        return Ok(earthquake.ToResponse());
    }
}

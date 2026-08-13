using Microsoft.AspNetCore.Mvc;
using QuakeReport.ApiService.Dtos;
using QuakeReport.ApiService.Earthquakes;
using QuakeReport.ApiService.Map;
using QuakeReport.Contracts.Dtos;

namespace QuakeReport.ApiService.Controllers;

[ApiController]
[Route("api/map")]
public sealed class MapController(
    IActiveEarthquakeService earthquakes,
    IMapService mapService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MapOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MapOverviewResponse>> Get(
        CancellationToken cancellationToken = default)
    {
        var earthquake = await earthquakes.GetActiveEarthquakeAsync(cancellationToken);
        if (earthquake is null)
        {
            return UnprocessableEntity("No active earthquake is configured.");
        }

        var overview = await mapService.GetOverviewAsync(earthquake, cancellationToken);
        var response = new MapOverviewResponse(
            overview.Earthquake.ToResponse(),
            overview.Elements
                .Select(element => new MapElementResponse(
                    element.MarkerId,
                    element.EntityId,
                    element.Type,
                    element.Title,
                    element.Summary,
                    element.Address,
                    element.Latitude,
                    element.Longitude))
                .ToList());

        return Ok(response);
    }
}

using CarGarage.Api.Configuration;
using CarGarage.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CarGarage.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IOptions<ServiceOptions> options, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Check() =>
        Ok(new HealthResponse("ok", options.Value.Name, clock.GetUtcNow()));
}

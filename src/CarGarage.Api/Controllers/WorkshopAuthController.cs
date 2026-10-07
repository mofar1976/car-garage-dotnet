using CarGarage.Application.Models;
using CarGarage.Application.Services;
using CarGarage.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarGarage.Api.Controllers;

[Route("api/auth/workshop")]
public sealed class WorkshopAuthController(AuthService auth) : AuthControllerBase(auth)
{
    protected override AuthAudience Audience => AuthAudience.WORKSHOP;

    [HttpPost("register"), Authorize(Policy = "PlatformAdmin")]
    [ProducesResponseType<AuthResponse>(201)]
    public Task<ActionResult<AuthResponse>> Register([FromBody] RegisterInput input, CancellationToken cancellationToken) =>
        RegisterUser(input, cancellationToken);

    [HttpGet("me"), Authorize(Policy = "WORKSHOP")]
    [ProducesResponseType<AuthUser>(200)]
    public Task<AuthUser> Me(CancellationToken cancellationToken) => CurrentUser(cancellationToken);
}

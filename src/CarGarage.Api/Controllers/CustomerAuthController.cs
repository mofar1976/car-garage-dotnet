using CarGarage.Application.Models;
using CarGarage.Application.Services;
using CarGarage.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarGarage.Api.Controllers;

[Route("api/auth/customer")]
public sealed class CustomerAuthController(AuthService auth) : AuthControllerBase(auth)
{
    protected override AuthAudience Audience => AuthAudience.CUSTOMER;

    [HttpPost("register"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(201)]
    public Task<ActionResult<AuthResponse>> Register([FromBody] RegisterInput input, CancellationToken cancellationToken) =>
        RegisterUser(input, cancellationToken);

    [HttpGet("me"), Authorize(Policy = "CUSTOMER")]
    [ProducesResponseType<AuthUser>(200)]
    public Task<AuthUser> Me(CancellationToken cancellationToken) => CurrentUser(cancellationToken);
}

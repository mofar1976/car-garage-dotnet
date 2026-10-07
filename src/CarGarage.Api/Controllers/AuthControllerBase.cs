using CarGarage.Application.Models;
using CarGarage.Application.Services;
using CarGarage.Api.Contracts;
using CarGarage.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarGarage.Api.Controllers;

[ApiController]
public abstract class AuthControllerBase(AuthService auth) : ControllerBase
{
    protected abstract AuthAudience Audience { get; }

    [HttpPost("login"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(200)]
    public Task<AuthResponse> Login([FromBody] LoginRequest input, CancellationToken cancellationToken) =>
        auth.LoginAsync(Audience, input.Email, input.Password, Request.Headers.UserAgent.ToString(), cancellationToken);

    [HttpPost("refresh"), AllowAnonymous]
    [ProducesResponseType<AuthResponse>(200)]
    public Task<AuthResponse> Refresh([FromBody] RefreshRequest input, CancellationToken cancellationToken) =>
        auth.RefreshAsync(Audience, input.RefreshToken, cancellationToken);

    [HttpPost("logout"), AllowAnonymous]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest input, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(input.RefreshToken, cancellationToken);
        return NoContent();
    }

    protected async Task<ActionResult<AuthResponse>> RegisterUser(RegisterInput input, CancellationToken cancellationToken) =>
        StatusCode(201, await auth.RegisterAsync(Audience, input, cancellationToken));

    protected Task<AuthUser> CurrentUser(CancellationToken cancellationToken) =>
        auth.GetUserAsync(Guid.Parse(User.FindFirst("sub")!.Value), cancellationToken);
}

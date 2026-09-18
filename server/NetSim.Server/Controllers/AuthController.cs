using Microsoft.AspNetCore.Mvc;   // Provides [ApiController], [Route], [HttpPost], ControllerBase, ActionResult etc. - the whole ASP.NET Core Web API infrastructure
using NetSim.Server.Dtos;          // The DTOs (LoginRequest/RegisterRequest/AuthResponse) - the "shape" of data crossing HTTP, separate from the internal model (User)
using NetSim.Server.Services;      // AuthService - where all the register/login business logic lives

namespace NetSim.Server.Controllers;

// [ApiController] automatically enables: model-binding from the request's JSON, and automatic model validation
// (if the DTO had [Required] etc., an invalid request would be auto-rejected with 400 before my code even runs)
[ApiController]
// [Route("api/auth")] sets the base address for every action in this class: /api/auth/...
[Route("api/auth")]
// Inherits from ControllerBase (not the regular Controller) because this is a pure API with no Views/Razor -
// no need for MVC view support, so the lighter base class is preferred
public class AuthController : ControllerBase
{
    // readonly - set once in the constructor and never changes; prevents accidentally reassigning the dependency later
    private readonly AuthService _auth;

    // Constructor (Dependency) Injection: ASP.NET Core creates AuthController itself for each request,
    // and automatically injects an instance of AuthService (registered in Program.cs with AddScoped) - there is
    // no "new AuthService()" here on purpose, so the controller doesn't depend on how AuthService is built
    // (Inversion of Control - a core design principle)
    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    // [HttpPost("register")] -> POST /api/auth/register. POST (not GET) because the action changes state
    // (creates a user) and carries the password in the request body, not the URL (URLs end up in logs/history -
    // a password there is a data leak).
    // async Task<...> because the DB call (inside AuthService) is asynchronous I/O - it doesn't block the thread
    // while waiting on network/disk
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // All the logic (password policy check, email duplicate check, hashing, saving to DB) happens inside
        // AuthService, not here - the controller stays "thin" and is only responsible for HTTP: receiving the
        // request, calling the service, and picking a status code
        var result = await _auth.RegisterAsync(request);
        // If registration failed (e.g. password too weak / email taken) - 400 Bad Request: the problem is with
        // the input the client sent
        if (!result.Success)
            return BadRequest(result);
        // Success - 200 OK with the response body (AuthResponse) serialized as JSON (ASP.NET Core does this automatically)
        return Ok(result);
    }

    // POST /api/auth/login - also POST so the password travels in the request body, not as a visible URL parameter
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var result = await _auth.LoginAsync(request);
        // 401 Unauthorized (not 400/404!) when login fails - the semantically correct status code for a failed
        // authentication attempt, as opposed to 403 Forbidden (identity known but not allowed) or 404
        // (the user doesn't exist - exactly what we do NOT want to reveal!)
        if (!result.Success)
            return Unauthorized(result);
        return Ok(result);
    }
    [HttpPost("forgot-password")]
    public async Task<ActionResult<AuthResponse>> ForgotPassword(ForgotPasswordRequest request)
    {
        var result = await _auth.ForgotPasswordAsync(request);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<AuthResponse>> ResetPassword(ResetPasswordRequest request)
    {
        var result = await _auth.ResetPasswordAsync(request);
        if (!result.Success)
            return BadRequest(result);
        return Ok(result);
    }


}

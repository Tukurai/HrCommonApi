using AutoMapper;
using HrCommonApi.Controllers.Requests.User;
using HrCommonApi.Controllers.Responses.User;
using HrCommonApi.Database.Models;
using HrCommonApi.Enums;
using HrCommonApi.Extensions;
using HrCommonApi.Services.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace HrCommonApi.Controllers;

/// <summary>
/// The user controller. 
/// Handles all user-related requests. 
/// Requires no policy due to needing to be able to log in.
/// </summary>
[ApiController]
public abstract class UserController<TUser, TCreate, TSimple, TUpdate>(ILogger<UserController<TUser, TCreate, TSimple, TUpdate>> logger, IMapper mapper, IUserService<TUser> service)
    : CoreController<UserController<TUser, TCreate, TSimple, TUpdate>, IUserService<TUser>, TUser, TSimple, TCreate, TUpdate>(logger, mapper, service)
    where TUser : User
    where TCreate : CreateUserRequest
    where TSimple : SimpleUserResponse
    where TUpdate : UpdateUserRequest
{
    /// <summary>
    /// Logs in the <see cref="EntityType"/> if the provided password is correct for the provided username. Then creates a session for this user.
    /// </summary>
    /// <param name="loginRequest">The model for the registration request containing the details of the <see cref="EntityType"/> to be created.</param>
    /// <returns>Returns a JWT token that can be used to authenticate users with.</returns>
    [HttpPost("Login")]
    public virtual async Task<IActionResult> Login([FromBody] LoginUserRequest loginRequest) => await HandleRequestFlow(async () =>
    {
        var loginResult = await CoreService.Login(loginRequest.Username, loginRequest.Password);
        if (loginResult.Code != ServiceCode.Success)
            return new ServiceResponse<LoginResponse>(loginResult.Code, message: loginResult.Message, exception: loginResult.Exception);

        var now = DateTime.Now.ToUniversalTime();
        var session = loginResult.Result!.Sessions.First(q => q.AccessExpiresAt >= now);

        return new ServiceResponse<LoginResponse>(ServiceCode.Success, new LoginResponse()
        {
            Id = loginResult.Result.Id,
            FirstName = loginResult.Result.FirstName,
            LastName = loginResult.Result.LastName,
            Email = loginResult.Result.Email,
            Username = loginResult.Result.Username,
            Role = loginResult.Result.Role,
            SessionId = session.Id,
            AccessToken = session.AccessToken,
            RefreshToken = session.RefreshToken,
            AccessExpiresAt = session.AccessExpiresAt,
            RefreshExpiresAt = session.RefreshExpiresAt
        });
    });

    /// <summary>
    /// Get the active <see cref="Session"/>(s) of a the currently authenticated <see cref="EntityType"/>.
    /// </summary>
    /// <returns>Returns a <see cref="SessionStateResponse"/> containing a <see cref="EntityType"/> with its active <see cref="Session"/>(s).</returns>
    [HttpGet("Session")]
    public virtual async Task<IActionResult> Session() => await HandleRequestFlow(async () =>
    {
        var userId = new Guid(GetFromClaim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()));
        var activeSessionsResponse = await CoreService.GetUserSessions(userId);
        if (activeSessionsResponse.Code != ServiceCode.Success)
            return new ServiceResponse<SessionStateResponse>(activeSessionsResponse.Code, message: activeSessionsResponse.Message, exception: activeSessionsResponse.Exception);

        if (activeSessionsResponse.Result!.Count == 0)
            return new ServiceResponse<SessionStateResponse>(ServiceCode.NotFound, message: "No active sessions found for this user.");

        return new ServiceResponse<SessionStateResponse>(ServiceCode.Success, new SessionStateResponse()
        {
            Id = userId,
            HasSession = HttpContext.User?.Identity?.IsAuthenticated ?? false,
            Username = GetFromClaim(ClaimTypes.Name, "Anonymous"),
            Role = GetFromClaim(ClaimTypes.Role, Role.None.ToString()).TryGetAsRole(),
            ActiveSessions = Mapper.Map<List<UserSessionResponse>>(activeSessionsResponse.Result)
        });
    });

    /// <summary>
    /// Get the <see cref="Session"/>(s) of a specific <see cref="EntityType"/>.
    /// This request can only be done by admins.
    /// </summary>
    /// <param name="userId">The Guid representing the <see cref="EntityType"/>.</param>
    /// <returns>Returns a list of <see cref="SessionResponse"/> containing the user's <see cref="Session"/>(s).</returns>
    [HttpGet("{userId}/Session"), Authorize(Policy = "Admin")]
    public virtual async Task<IActionResult> GetUserSessions(Guid userId)
        => await HandleRequestFlow<List<UserSessionResponse>, List<Session>>(() => CoreService.GetUserSessions(userId));

    /// <summary>
    /// Returns the item that was created, if successfully.
    /// </summary>
    /// <param name="createRequest">The creation request related to this entity.</param>
    /// <returns>A single simple response.</returns>
    [HttpPost("Admin"), Authorize(Policy = "Admin")]
    public virtual async Task<IActionResult> Create(CreateAdminRequest createRequest)
        => await CreateToResponseModel<TSimple>(createRequest);

    /// <summary>
    /// Get the account details of specific <see cref="User"/>.
    /// This request can only be done by admins.
    /// </summary>
    /// <param name="userId">The Guid representing the <see cref="User"/>.</param>
    /// <returns>Returns an <see cref="ExtendedUserResponse"/> containing the <see cref="User"/>'s details and its the related objects.</returns>
    [HttpGet("{userId}/Details"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> GetUserDetails(Guid userId)
        => await HandleRequestFlow<ExtendedUserResponse, TUser>(() => CoreService.Get(userId));

    /// <summary>
    /// Set the role of a <see cref="User"/>.
    /// This request can only be done by admins.
    /// </summary>
    /// <param name="userId">The Guid representing the <see cref="User"/>.</param>
    /// <param name="role">The role to be set.</param>
    /// <returns>Returns an <see cref="ExtendedUserResponse"/> containing the <see cref="User"/>'s details and its the related objects.</returns>
    [HttpPost("{userId}/Role"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> SetUserRole(Guid userId, Role role)
        => await HandleRequestFlow<ExtendedUserResponse, TUser>(() => CoreService.UpdateRole(userId, role));
}

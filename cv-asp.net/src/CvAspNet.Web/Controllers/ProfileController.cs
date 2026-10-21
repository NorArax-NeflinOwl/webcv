using CvAspNet.Web.Models;
using CvAspNet.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace CvAspNet.Web.Controllers;

[ApiController]
public sealed class ProfileController(IProfileService profileService) : ControllerBase
{
    public const string BackendLabel = "C# + ASP.NET Core + Docker  cv-asp.net";

    [HttpGet("/api/hello")]
    public string Hello() => "Hello World from C# + ASP.NET Core! 🚀";

    /// <summary>Footer label of the shared CV page: tells which backend serves it.</summary>
    [HttpGet("/api/backend")]
    public BackendInfoDto Backend() => new(BackendLabel);

    [HttpGet("/api/profileinfo")]
    public Task<ProfileDto> Profile([FromQuery] string lang = ProfileService.DefaultLanguage, CancellationToken cancellationToken = default) =>
        profileService.GetProfileAsync(lang, cancellationToken);
}

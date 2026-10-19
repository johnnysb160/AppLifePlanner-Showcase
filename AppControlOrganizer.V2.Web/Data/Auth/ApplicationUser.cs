//-----------------------------------------------------------------------
// <copyright file="ApplicationUser.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Auth;

using Microsoft.AspNetCore.Identity;

/// <summary>
/// Custom Identity user for the Life Planner application.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Optional display name shown in the navbar and profile.
    /// Falls back to UserName when not set.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Profile photo stored as a base64 data URI (e.g. data:image/jpeg;base64,...).
    /// Kept in the database to avoid file-system dependencies.
    /// </summary>
    public string? ProfilePhotoData { get; set; }

    /// <summary>
    /// UI language preference: "en" (default) or "pt". Changeable via Profile settings.
    /// </summary>
    public string Language { get; set; } = "en";

    /// <summary>
    /// Gets or sets the IANA time-zone identifier used to interpret routine and planning dates.
    /// </summary>
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
}

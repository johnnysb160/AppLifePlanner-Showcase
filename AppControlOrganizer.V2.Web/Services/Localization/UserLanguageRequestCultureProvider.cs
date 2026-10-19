//-----------------------------------------------------------------------
// <copyright file="UserLanguageRequestCultureProvider.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Localization;

using AppControlOrganizer.V2.Web.Data.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

/// <summary>
/// Resolves the request culture from the authenticated user's stored language preference.
/// </summary>
public sealed class UserLanguageRequestCultureProvider : RequestCultureProvider
{
  private const string EnglishCultureName = "en-US";
  private const string PortugueseCultureName = "pt-BR";
  private const string CacheKeyPrefix = "user-culture:";
  private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

  /// <inheritdoc />
  public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
  {
    ArgumentNullException.ThrowIfNull(httpContext);

    if (httpContext.User.Identity?.IsAuthenticated != true)
    {
      return new ProviderCultureResult(EnglishCultureName, EnglishCultureName);
    }

    // This provider runs on every request that reaches the localization middleware (framework scripts,
    // negotiate, API calls), so the language is cached per user instead of costing a database round trip each time.
    string? cacheUserId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    IMemoryCache? cache = httpContext.RequestServices.GetService<IMemoryCache>();
    string cacheKey = CacheKeyPrefix + cacheUserId;
    if (cache != null
      && !string.IsNullOrEmpty(cacheUserId)
      && cache.TryGetValue(cacheKey, out string? cachedCulture)
      && !string.IsNullOrEmpty(cachedCulture))
    {
      return new ProviderCultureResult(cachedCulture, cachedCulture);
    }

    UserManager<ApplicationUser>? userManager =
      httpContext.RequestServices.GetService<UserManager<ApplicationUser>>();
    if (userManager == null)
    {
      return new ProviderCultureResult(EnglishCultureName, EnglishCultureName);
    }

    ApplicationUser? user = await userManager.GetUserAsync(httpContext.User);
    string cultureName = LocService.NormalizeLanguage(user?.Language) == "pt"
      ? PortugueseCultureName
      : EnglishCultureName;

    if (cache != null && !string.IsNullOrEmpty(cacheUserId))
    {
      cache.Set(cacheKey, cultureName, CacheDuration);
    }

    return new ProviderCultureResult(cultureName, cultureName);
  }

  /// <summary>
  /// Drops the cached culture of a user so the next request reads the language that was just saved.
  /// </summary>
  /// <param name="cache">The application memory cache.</param>
  /// <param name="userId">The identifier of the user whose language changed.</param>
  public static void Invalidate(IMemoryCache cache, string userId)
  {
    cache.Remove(CacheKeyPrefix + userId);
  }
}

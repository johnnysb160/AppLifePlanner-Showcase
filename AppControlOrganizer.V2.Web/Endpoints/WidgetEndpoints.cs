//-----------------------------------------------------------------------
// <copyright file="WidgetEndpoints.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Endpoints;

using System.Security.Claims;
using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Security;
using AppControlOrganizer.V2.Web.Services.Planning;
using AppControlOrganizer.V2.Web.Services.Widgets;

/// <summary>
/// Minimal API endpoints consumed by local AppLifePlanner widgets.
/// </summary>
public static class WidgetEndpoints
{
  private const int IosWidgetSchemaVersion = 2;
  private const int IosWidgetRefreshAfterSeconds = 21600;
  private const string IosWidgetRuntimeVersion = "2.0.0";
  private const string IosWidgetMinBootstrapVersion = "2.0.0";
  private const string DefaultIosWidgetKind = "lock:1";

  /// <summary>
  /// Maps local widget pairing and snapshot endpoints.
  /// </summary>
  /// <param name="app">Endpoint route builder.</param>
  public static void MapWidgetEndpoints(this IEndpointRouteBuilder app)
  {
    RouteGroupBuilder group = app.MapGroup("/api/widget")
      .WithTags("Widget")
      .RequireRateLimiting(RateLimitPolicies.Api);

    group.MapGet("/today", GetTodayAsync)
      .AllowAnonymous()
      .WithName("GetTodayWidgetSnapshot")
      .WithOpenApi();

    group.MapGet("/routine-lock-screen", GetRoutineLockScreenAsync)
      .AllowAnonymous()
      .WithName("GetRoutineLockScreenWidget")
      .WithOpenApi();

    group.MapGet("/ios/v2/catalog", GetIosV2Catalog)
      .AllowAnonymous()
      .WithName("GetIosWidgetV2Catalog")
      .WithOpenApi();

    group.MapGet("/ios/v2/snapshot", GetIosV2SnapshotAsync)
      .AllowAnonymous()
      .WithName("GetIosWidgetV2Snapshot")
      .WithOpenApi();

    group.MapGet("/ios/v2/routine-fast", GetIosV2RoutineFastAsync)
      .AllowAnonymous()
      .WithName("GetIosWidgetV2RoutineFast")
      .WithOpenApi();

    group.MapPost("/pairing-code", CreatePairingCodeAsync)
      .RequireAuthorization()
      .WithName("CreateWidgetPairingCode")
      .WithOpenApi();

    group.MapPost("/pairing/exchange", ExchangePairingCodeAsync)
      .AllowAnonymous()
      .RequireRateLimiting(RateLimitPolicies.Login)
      .WithName("ExchangeWidgetPairingCode")
      .WithOpenApi();

    group.MapDelete("/tokens", RevokeWidgetTokensAsync)
      .RequireAuthorization()
      .WithName("RevokeWidgetTokens")
      .WithOpenApi();
  }

  private static async Task<IResult> GetTodayAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService,
    ITodayWidgetSnapshotService snapshotService,
    int? tzOffsetMinutes,
    string? lang)
  {
    string? userId = await ResolveUserIdAsync(httpContext, pairingService);
    if (string.IsNullOrWhiteSpace(userId))
    {
      return Results.Unauthorized();
    }

    EnsureWidgetPrincipal(httpContext, userId);

    int offsetMinutes = Math.Clamp(tzOffsetMinutes ?? -180, -840, 840);
    TimeSpan offset = TimeSpan.FromMinutes(offsetMinutes);
    DateTime localNow = DateTime.UtcNow.Add(offset);
    string language = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "pt";
    TodayWidgetSnapshotDto snapshot = await snapshotService.BuildAsync(
      userId,
      localNow,
      offset,
      language,
      httpContext.RequestAborted);

    return Results.Ok(snapshot);
  }

  private static async Task<IResult> GetRoutineLockScreenAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService,
    ITodayWidgetSnapshotService snapshotService,
    int? tzOffsetMinutes,
    string? lang)
  {
    string? userId = await ResolveUserIdAsync(httpContext, pairingService);
    if (string.IsNullOrWhiteSpace(userId))
    {
      return Results.Unauthorized();
    }

    EnsureWidgetPrincipal(httpContext, userId);

    int offsetMinutes = Math.Clamp(tzOffsetMinutes ?? -180, -840, 840);
    TimeSpan offset = TimeSpan.FromMinutes(offsetMinutes);
    DateTime localNow = DateTime.UtcNow.Add(offset);
    string language = string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "pt";
    TodayWidgetSnapshotDto snapshot = await snapshotService.BuildAsync(
      userId,
      localNow,
      offset,
      language,
      httpContext.RequestAborted);

    return Results.Ok(CreateLockScreenRoutineDto(snapshot));
  }

  private static IResult GetIosV2Catalog(string? lang)
  {
    string language = NormalizeLanguage(lang);
    return Results.Ok(CreateIosCatalog(language));
  }

  private static async Task<IResult> GetIosV2SnapshotAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService,
    ITodayWidgetSnapshotService snapshotService,
    int? tzOffsetMinutes,
    string? lang,
    string? kind)
  {
    string? userId = await ResolveUserIdAsync(httpContext, pairingService);
    if (string.IsNullOrWhiteSpace(userId))
    {
      return Results.Unauthorized();
    }

    EnsureWidgetPrincipal(httpContext, userId);

    int offsetMinutes = Math.Clamp(tzOffsetMinutes ?? -180, -840, 840);
    TimeSpan offset = TimeSpan.FromMinutes(offsetMinutes);
    DateTime localNow = DateTime.UtcNow.Add(offset);
    string language = NormalizeLanguage(lang);
    string requestedKind = NormalizeIosWidgetKind(kind);
    TodayWidgetSnapshotDto snapshot = await snapshotService.BuildAsync(
      userId,
      localNow,
      offset,
      language,
      httpContext.RequestAborted);
    IosWidgetCatalogDto catalog = CreateIosCatalog(language);
    IosWidgetDefinitionDto selectedWidget = catalog.Widgets
      .First(item => string.Equals(item.Kind, requestedKind, StringComparison.Ordinal));

    return Results.Ok(new IosWidgetSnapshotDto
    {
      SchemaVersion = IosWidgetSchemaVersion,
      RuntimeVersion = IosWidgetRuntimeVersion,
      MinBootstrapVersion = IosWidgetMinBootstrapVersion,
      GeneratedAtUtc = DateTime.UtcNow,
      RefreshAfterSeconds = IosWidgetRefreshAfterSeconds,
      Kind = requestedKind,
      SelectedWidget = selectedWidget,
      Widgets = catalog.Widgets,
      Today = CreateIosTodaySnapshot(snapshot),
      EmptyTitle = language == "en" ? "Open day" : "Dia livre",
      EmptyDetail = language == "en"
        ? "No item to show right now"
        : "Nenhum item para mostrar agora",
    });
  }

  private static async Task<IResult> GetIosV2RoutineFastAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService,
    IPlanningExperienceService planningExperienceService,
    int? tzOffsetMinutes,
    string? lang)
  {
    string? userId = await ResolveUserIdAsync(httpContext, pairingService);
    if (string.IsNullOrWhiteSpace(userId))
    {
      return Results.Unauthorized();
    }

    EnsureWidgetPrincipal(httpContext, userId);

    int offsetMinutes = Math.Clamp(tzOffsetMinutes ?? -180, -840, 840);
    DateTime localNow = DateTime.UtcNow.Add(TimeSpan.FromMinutes(offsetMinutes));
    string language = NormalizeLanguage(lang);
    RoutineTemplateEntity? routine = await planningExperienceService.GetActiveRoutineAsync(
      userId,
      httpContext.RequestAborted);

    return Results.Ok(CreateFastRoutineDto(routine, localNow, language));
  }

  private static async Task<IResult> CreatePairingCodeAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService)
  {
    string? userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrWhiteSpace(userId))
    {
      return Results.Unauthorized();
    }

    WidgetPairingCodeResponseDto code = await pairingService.CreatePairingCodeAsync(
      userId,
      httpContext.RequestAborted);
    return Results.Ok(code);
  }

  private static async Task<IResult> ExchangePairingCodeAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService,
    WidgetPairingExchangeRequestDto request)
  {
    string? token = await pairingService.ExchangePairingCodeAsync(
      request.Code,
      request.DeviceName,
      httpContext.RequestAborted);
    if (string.IsNullOrWhiteSpace(token))
    {
      return Results.Unauthorized();
    }

    string baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
    return Results.Ok(new WidgetPairingExchangeResponseDto
    {
      Token = token,
      BaseUrl = baseUrl,
    });
  }

  private static async Task<IResult> RevokeWidgetTokensAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService)
  {
    string? userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (string.IsNullOrWhiteSpace(userId))
    {
      return Results.Unauthorized();
    }

    int removed = await pairingService.RevokeUserTokensAsync(userId, httpContext.RequestAborted);
    return Results.Ok(new { revoked = removed });
  }

  private static LockScreenRoutineWidgetDto CreateLockScreenRoutineDto(TodayWidgetSnapshotDto snapshot)
  {
    List<TodayWidgetItemDto> timeline = new List<TodayWidgetItemDto>();
    AddUnique(timeline, snapshot.Current);
    AddUnique(timeline, snapshot.Next);
    foreach (TodayWidgetItemDto item in snapshot.Timeline)
    {
      AddUnique(timeline, item);
    }

    TodayWidgetItemDto? current = CreateLockScreenItem(snapshot.Current);
    TodayWidgetItemDto? next = CreateLockScreenItem(snapshot.Next);

    return new LockScreenRoutineWidgetDto
    {
      ReferenceDate = snapshot.ReferenceDate,
      LocalNow = DateTime.SpecifyKind(snapshot.LocalNow, DateTimeKind.Unspecified),
      GeneratedAtUtc = snapshot.GeneratedAtUtc,
      Language = snapshot.Language,
      Current = current,
      Next = next,
      Timeline = timeline
        .OrderBy(item => item.StartLocal)
        .Select(item => CreateLockScreenItem(item)!)
        .ToList(),
      PlannedMomentCount = snapshot.Metadata.PlannedMomentCount,
      CompletedMomentCount = snapshot.Metadata.CompletedMomentCount,
      RefreshAfterSeconds = IosWidgetRefreshAfterSeconds,
      EmptyTitle = snapshot.Language == "en" ? "Open routine" : "Rotina livre",
      EmptyDetail = snapshot.Language == "en"
        ? "No active block right now"
        : "Nenhum bloco ativo agora",
    };
  }

  private static IosWidgetCatalogDto CreateIosCatalog(string language)
  {
    return new IosWidgetCatalogDto
    {
      SchemaVersion = IosWidgetSchemaVersion,
      RuntimeVersion = IosWidgetRuntimeVersion,
      MinBootstrapVersion = IosWidgetMinBootstrapVersion,
      GeneratedAtUtc = DateTime.UtcNow,
      RefreshAfterSeconds = IosWidgetRefreshAfterSeconds,
      Language = language,
      Widgets = CreateIosWidgetDefinitions(language),
    };
  }

  private static List<IosWidgetDefinitionDto> CreateIosWidgetDefinitions(string language)
  {
    List<IosWidgetDefinitionDto> widgets = new List<IosWidgetDefinitionDto>();
    string[] accents = new[] { "#22d3ee", "#f59e0b", "#a3e635", "#38bdf8", "#c084fc" };
    for (int index = 1; index <= 10; index++)
    {
      string accent = accents[(index - 1) % accents.Length];
      widgets.Add(CreateIosWidgetDefinition(
        $"lock:{index}",
        "lock",
        language == "en" ? $"Routine {index}" : $"Rotina {index}",
        GetRoutineWidgetDetail(language, "lock", index),
        accent,
        index <= 5 ? "compact" : "normal"));
      widgets.Add(CreateIosWidgetDefinition(
        $"home:{index}",
        "home",
        language == "en" ? $"Routine {index}" : $"Rotina {index}",
        GetRoutineWidgetDetail(language, "home", index),
        accent,
        "normal"));
    }

    return widgets;
  }

  private static string GetRoutineWidgetDetail(string language, string family, int index)
  {
    string[] ptLock =
    {
      "Circular: progresso do bloco ativo.",
      "Circular: tempo restante do bloco ativo ou próximo.",
      "Circular: contagem feita/planejada do dia.",
      "Circular: horário do próximo bloco.",
      "Circular: quantidade de blocos restantes.",
      "Largo: agora, próximo, restante, ritmo e contagem.",
      "Largo: divisão entre bloco atual e próximo.",
      "Largo: mini linha do tempo da rotina.",
      "Largo: contagem e ritmo do bloco ativo.",
      "Largo: mapa do dia com agora, restante e próximo.",
    };
    string[] enLock =
    {
      "Circular: active block progress.",
      "Circular: remaining time for the active or next block.",
      "Circular: completed/planned day count.",
      "Circular: next block start time.",
      "Circular: remaining block queue count.",
      "Wide: now, next, remaining, pace, and day count.",
      "Wide: split between current and next block.",
      "Wide: routine mini timeline.",
      "Wide: countdown and active block pace.",
      "Wide: day map with now, left, and next.",
    };
    string[] ptHome =
    {
      "Home: painel com atual, restante, próximo e contagem.",
      "Home: linha do tempo com próximos blocos.",
      "Home: divisão entre rotina atual e próxima.",
      "Home: contagem do bloco ativo ou próximo.",
      "Home: resumo com contagem, ritmo e fila.",
      "Home: linha do tempo maior da rotina.",
      "Home: quadro por bloco e duração.",
      "Home: quadro por categoria da rotina.",
      "Home: foco em ritmo e progresso.",
      "Home: cronograma compacto com horários da rotina.",
    };
    string[] enHome =
    {
      "Home: dashboard with current, remaining, next, and day count.",
      "Home: timeline with upcoming blocks.",
      "Home: split between current and next routine.",
      "Home: countdown for active or next block.",
      "Home: summary with count, pace, and queue.",
      "Home: larger routine timeline.",
      "Home: board by block and duration.",
      "Home: board by routine category.",
      "Home: pace and progress focus.",
      "Home: compact timeline with routine times.",
    };
    string[] values = family == "lock"
      ? (language == "en" ? enLock : ptLock)
      : (language == "en" ? enHome : ptHome);
    return values[Math.Clamp(index, 1, 10) - 1];
  }

  private static IosWidgetDefinitionDto CreateIosWidgetDefinition(
    string kind,
    string family,
    string title,
    string detail,
    string accent,
    string privacyLevel)
  {
    return new IosWidgetDefinitionDto
    {
      Kind = kind,
      Family = family,
      Title = title,
      Detail = detail,
      Accent = accent,
      PrivacyLevel = privacyLevel,
    };
  }

  private static TodayWidgetSnapshotDto CreateIosTodaySnapshot(TodayWidgetSnapshotDto snapshot)
  {
    return new TodayWidgetSnapshotDto
    {
      ReferenceDate = snapshot.ReferenceDate,
      LocalNow = DateTime.SpecifyKind(snapshot.LocalNow, DateTimeKind.Unspecified),
      GeneratedAtUtc = snapshot.GeneratedAtUtc,
      Language = snapshot.Language,
      Title = snapshot.Title,
      DateLabel = snapshot.DateLabel,
      Summary = snapshot.Summary,
      Current = CreateLockScreenItem(snapshot.Current),
      Next = CreateLockScreenItem(snapshot.Next),
      Timeline = snapshot.Timeline
        .Select(item => CreateLockScreenItem(item)!)
        .ToList(),
      Focuses = snapshot.Focuses,
      Actions = snapshot.Actions,
      Insights = snapshot.Insights,
      Metadata = new TodayWidgetMetadataDto
      {
        PlannedMomentCount = snapshot.Metadata.PlannedMomentCount,
        CompletedMomentCount = snapshot.Metadata.CompletedMomentCount,
        SimilarMomentCount = snapshot.Metadata.SimilarMomentCount,
        OpenActionCount = snapshot.Metadata.OpenActionCount,
        RefreshAfterSeconds = IosWidgetRefreshAfterSeconds,
      },
    };
  }

  private static LockScreenRoutineWidgetDto CreateFastRoutineDto(
    RoutineTemplateEntity? routine,
    DateTime localNow,
    string language)
  {
    List<TodayWidgetItemDto> todayItems = CreateFastRoutineItems(routine, localNow.Date, localNow.DayOfWeek, language);
    TodayWidgetItemDto? current = todayItems.FirstOrDefault(item => item.StartLocal <= localNow && localNow < item.EndLocal);
    TodayWidgetItemDto? next = todayItems.FirstOrDefault(item => item.StartLocal > localNow)
      ?? CreateNextFastRoutineItem(routine, localNow, language);
    int completedCount = todayItems.Count(item => item.EndLocal <= localNow);
    List<TodayWidgetItemDto> timeline = new List<TodayWidgetItemDto>();
    foreach (TodayWidgetItemDto item in todayItems)
    {
      AddUnique(timeline, item);
    }

    AddUnique(timeline, next);

    return new LockScreenRoutineWidgetDto
    {
      ReferenceDate = DateOnly.FromDateTime(localNow),
      LocalNow = DateTime.SpecifyKind(localNow, DateTimeKind.Unspecified),
      GeneratedAtUtc = DateTime.UtcNow,
      Language = language,
      Current = current,
      Next = next,
      Timeline = timeline.OrderBy(item => item.StartLocal).ToList(),
      PlannedMomentCount = todayItems.Count,
      CompletedMomentCount = completedCount,
      RefreshAfterSeconds = IosWidgetRefreshAfterSeconds,
      EmptyTitle = language == "en" ? "Open routine" : "Rotina livre",
      EmptyDetail = language == "en"
        ? "No active block right now"
        : "Nenhum bloco ativo agora",
    };
  }

  private static List<TodayWidgetItemDto> CreateFastRoutineItems(
    RoutineTemplateEntity? routine,
    DateTime date,
    DayOfWeek dayOfWeek,
    string language)
  {
    return routine?.Blocks
      .Where(item => item.DayOfWeek == dayOfWeek && !item.Deleted)
      .OrderBy(item => item.StartTime)
      .Select(item => CreateFastRoutineItem(item, date, language))
      .ToList() ?? new List<TodayWidgetItemDto>();
  }

  private static TodayWidgetItemDto? CreateNextFastRoutineItem(
    RoutineTemplateEntity? routine,
    DateTime localNow,
    string language)
  {
    if (routine == null)
    {
      return null;
    }

    for (int offset = 1; offset <= 7; offset++)
    {
      DateTime date = localNow.Date.AddDays(offset);
      RoutineBlockEntity? block = routine.Blocks
        .Where(item => item.DayOfWeek == date.DayOfWeek && !item.Deleted)
        .OrderBy(item => item.StartTime)
        .FirstOrDefault();
      if (block != null)
      {
        return CreateFastRoutineItem(block, date, language);
      }
    }

    return null;
  }

  private static TodayWidgetItemDto CreateFastRoutineItem(
    RoutineBlockEntity block,
    DateTime date,
    string language)
  {
    DateTime startLocal = DateTime.SpecifyKind(date.Add(block.StartTime.ToTimeSpan()), DateTimeKind.Unspecified);
    DateTime endLocal = DateTime.SpecifyKind(date.Add(block.EndTime.ToTimeSpan()), DateTimeKind.Unspecified);
    string category = string.IsNullOrWhiteSpace(block.Category)
      ? (language == "en" ? "Routine" : "Rotina")
      : block.Category;

    return new TodayWidgetItemDto
    {
      Id = block.Id,
      Title = block.Title,
      Source = language == "en" ? "Routine" : "Rotina",
      Category = category,
      StartLocal = startLocal,
      EndLocal = endLocal,
      Href = block.Id > 0 ? $"routine?blockId={block.Id}" : "routine",
      IconCss = string.IsNullOrWhiteSpace(block.IconCss) ? "bi bi-circle" : block.IconCss,
      ColorHex = string.IsNullOrWhiteSpace(block.ColorHex) ? "#64748b" : block.ColorHex,
      IsCurrent = false,
      IsCalendar = false,
      ProgressPercent = 0,
      DurationLabel = FormatFastDuration(endLocal - startLocal, language),
      RemainingLabel = string.Empty,
      NextLabel = language == "en" ? "Today" : "Hoje",
    };
  }

  private static string FormatFastDuration(TimeSpan duration, string language)
  {
    if (duration < TimeSpan.Zero)
    {
      duration = TimeSpan.Zero;
    }

    int totalMinutes = (int)Math.Ceiling(duration.TotalMinutes);
    if (totalMinutes >= 60)
    {
      return $"{totalMinutes / 60}h {totalMinutes % 60:D2}min";
    }

    return language == "en" ? $"{totalMinutes} min" : $"{totalMinutes} min";
  }

  private static TodayWidgetItemDto? CreateLockScreenItem(TodayWidgetItemDto? item)
  {
    if (item == null)
    {
      return null;
    }

    return new TodayWidgetItemDto
    {
      Id = item.Id,
      Title = item.Title,
      Source = item.Source,
      Category = item.Category,
      StartLocal = DateTime.SpecifyKind(item.StartLocal, DateTimeKind.Unspecified),
      EndLocal = DateTime.SpecifyKind(item.EndLocal, DateTimeKind.Unspecified),
      Href = item.Href,
      IconCss = item.IconCss,
      ColorHex = item.ColorHex,
      IsCurrent = item.IsCurrent,
      IsCalendar = item.IsCalendar,
      ProgressPercent = item.ProgressPercent,
      DurationLabel = item.DurationLabel,
      RemainingLabel = item.RemainingLabel,
      NextLabel = item.NextLabel,
    };
  }

  private static void AddUnique(List<TodayWidgetItemDto> items, TodayWidgetItemDto? item)
  {
    if (item == null)
    {
      return;
    }

    bool exists = items.Any(existing => existing.Id == item.Id
      && string.Equals(existing.Source, item.Source, StringComparison.Ordinal)
      && existing.StartLocal == item.StartLocal
      && existing.EndLocal == item.EndLocal);
    if (!exists)
    {
      items.Add(item);
    }
  }

  private static async Task<string?> ResolveUserIdAsync(
    HttpContext httpContext,
    IWidgetPairingService pairingService)
  {
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
      return httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    string token = GetBearerToken(httpContext);
    if (string.IsNullOrWhiteSpace(token))
    {
      token = httpContext.Request.Headers["X-AppLifePlanner-Widget-Token"].ToString();
    }

    if (string.IsNullOrWhiteSpace(token))
    {
      return null;
    }

    return await pairingService.ValidateTokenAsync(token, httpContext.RequestAborted);
  }

  private static void EnsureWidgetPrincipal(HttpContext httpContext, string userId)
  {
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
      return;
    }

    httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
      new[]
      {
        new Claim(ClaimTypes.NameIdentifier, userId),
      },
      "WidgetToken"));
  }

  private static string GetBearerToken(HttpContext httpContext)
  {
    string authorization = httpContext.Request.Headers.Authorization.ToString();
    const string bearerPrefix = "Bearer ";
    if (authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
    {
      return authorization[bearerPrefix.Length..].Trim();
    }

    return string.Empty;
  }

  private static string NormalizeLanguage(string? lang)
  {
    return string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "pt";
  }

  private static string NormalizeIosWidgetKind(string? kind)
  {
    string value = string.IsNullOrWhiteSpace(kind)
      ? DefaultIosWidgetKind
      : kind.Trim().ToLowerInvariant();
    List<IosWidgetDefinitionDto> definitions = CreateIosWidgetDefinitions("pt");
    return definitions.Any(item => string.Equals(item.Kind, value, StringComparison.Ordinal))
      ? value
      : DefaultIosWidgetKind;
  }
}

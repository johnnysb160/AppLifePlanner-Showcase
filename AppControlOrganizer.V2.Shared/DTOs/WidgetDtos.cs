//-----------------------------------------------------------------------
// <copyright file="WidgetDtos.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.DTOs;

/// <summary>
/// Snapshot consumed by local widgets that need the same Today orientation as the web app.
/// </summary>
public sealed class TodayWidgetSnapshotDto
{
  /// <summary>Gets or sets the local date represented by the snapshot.</summary>
  public DateOnly ReferenceDate { get; set; }

  /// <summary>Gets or sets the local time used to calculate current and next items.</summary>
  public DateTime LocalNow { get; set; }

  /// <summary>Gets or sets when the snapshot was generated on the server.</summary>
  public DateTime GeneratedAtUtc { get; set; }

  /// <summary>Gets or sets the language used for labels.</summary>
  public string Language { get; set; } = "pt";

  /// <summary>Gets or sets the localized Today title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized date label.</summary>
  public string DateLabel { get; set; } = string.Empty;

  /// <summary>Gets or sets the compact header summary.</summary>
  public string Summary { get; set; } = string.Empty;

  /// <summary>Gets or sets the current routine item.</summary>
  public TodayWidgetItemDto? Current { get; set; }

  /// <summary>Gets or sets the next routine item.</summary>
  public TodayWidgetItemDto? Next { get; set; }

  /// <summary>Gets or sets the visible timeline items.</summary>
  public List<TodayWidgetItemDto> Timeline { get; set; } = new List<TodayWidgetItemDto>();

  /// <summary>Gets or sets the visible weekly focus items.</summary>
  public List<TodayWidgetFocusDto> Focuses { get; set; } = new List<TodayWidgetFocusDto>();

  /// <summary>Gets or sets the visible planning action items.</summary>
  public List<TodayWidgetActionDto> Actions { get; set; } = new List<TodayWidgetActionDto>();

  /// <summary>Gets or sets optional rich insight cards for larger widgets.</summary>
  public List<TodayWidgetInsightDto> Insights { get; set; } = new List<TodayWidgetInsightDto>();

  /// <summary>Gets or sets small numeric facts useful for compact widgets.</summary>
  public TodayWidgetMetadataDto Metadata { get; set; } = new TodayWidgetMetadataDto();
}

/// <summary>
/// One routine item in the widget snapshot.
/// </summary>
public sealed class TodayWidgetItemDto
{
  /// <summary>Gets or sets the underlying item identifier.</summary>
  public int Id { get; set; }

  /// <summary>Gets or sets the item title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized source label.</summary>
  public string Source { get; set; } = string.Empty;

  /// <summary>Gets or sets the item category.</summary>
  public string Category { get; set; } = string.Empty;

  /// <summary>Gets or sets the local start time.</summary>
  public DateTime StartLocal { get; set; }

  /// <summary>Gets or sets the local end time.</summary>
  public DateTime EndLocal { get; set; }

  /// <summary>Gets or sets the destination route in the web app.</summary>
  public string Href { get; set; } = string.Empty;

  /// <summary>Gets or sets the Bootstrap icon CSS class.</summary>
  public string IconCss { get; set; } = "bi bi-circle";

  /// <summary>Gets or sets the accent color.</summary>
  public string ColorHex { get; set; } = "#64748b";

  /// <summary>Gets or sets a value indicating whether this item is active now.</summary>
  public bool IsCurrent { get; set; }

  /// <summary>Gets or sets a legacy compatibility flag. Routine-only snapshots set this to false.</summary>
  public bool IsCalendar { get; set; }

  /// <summary>Gets or sets the current progress percentage for active items.</summary>
  public int ProgressPercent { get; set; }

  /// <summary>Gets or sets the localized duration label.</summary>
  public string DurationLabel { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized remaining label.</summary>
  public string RemainingLabel { get; set; } = string.Empty;

  /// <summary>Gets or sets a next-item label suitable for compact display.</summary>
  public string NextLabel { get; set; } = string.Empty;
}

/// <summary>
/// One weekly focus item for widget display.
/// </summary>
public sealed class TodayWidgetFocusDto
{
  /// <summary>Gets or sets the focus text.</summary>
  public string Text { get; set; } = string.Empty;

  /// <summary>Gets or sets the destination route in the web app.</summary>
  public string Href { get; set; } = string.Empty;

  /// <summary>Gets or sets a value indicating whether the item is a suggestion.</summary>
  public bool Suggested { get; set; }
}

/// <summary>
/// One deterministic planning action for widget display.
/// </summary>
public sealed class TodayWidgetActionDto
{
  /// <summary>Gets or sets the action identifier.</summary>
  public int Id { get; set; }

  /// <summary>Gets or sets the localized title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized detail.</summary>
  public string Detail { get; set; } = string.Empty;

  /// <summary>Gets or sets the formatted evidence.</summary>
  public string Evidence { get; set; } = string.Empty;

  /// <summary>Gets or sets the destination route in the web app.</summary>
  public string Href { get; set; } = string.Empty;

  /// <summary>Gets or sets the Bootstrap icon CSS class.</summary>
  public string IconCss { get; set; } = string.Empty;

  /// <summary>Gets or sets the action tone.</summary>
  public string Tone { get; set; } = "execute";

  /// <summary>Gets or sets the localized primary action label.</summary>
  public string PrimaryAction { get; set; } = string.Empty;
}

/// <summary>
/// One compact insight card shown when the widget has enough space.
/// </summary>
public sealed class TodayWidgetInsightDto
{
  /// <summary>Gets or sets the localized title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the formatted value.</summary>
  public string Value { get; set; } = string.Empty;

  /// <summary>Gets or sets the short supporting detail.</summary>
  public string Detail { get; set; } = string.Empty;

  /// <summary>Gets or sets the destination route in the web app.</summary>
  public string Href { get; set; } = string.Empty;

  /// <summary>Gets or sets the display icon.</summary>
  public string Icon { get; set; } = string.Empty;

  /// <summary>Gets or sets the visual tone.</summary>
  public string Tone { get; set; } = "neutral";
}

/// <summary>
/// Compact numeric metadata for widgets.
/// </summary>
public sealed class TodayWidgetMetadataDto
{
  /// <summary>Gets or sets the total number of planned moments today.</summary>
  public int PlannedMomentCount { get; set; }

  /// <summary>Gets or sets the number of completed moments today.</summary>
  public int CompletedMomentCount { get; set; }

  /// <summary>Gets or sets the count of routine blocks with the same title this week.</summary>
  public int SimilarMomentCount { get; set; }

  /// <summary>Gets or sets the open planning action count.</summary>
  public int OpenActionCount { get; set; }

  /// <summary>Gets or sets the suggested refresh delay in seconds.</summary>
  public int RefreshAfterSeconds { get; set; } = 60;
}

/// <summary>
/// Compact response consumed by iOS Lock Screen widgets.
/// </summary>
public sealed class LockScreenRoutineWidgetDto
{
  /// <summary>Gets or sets the local date represented by the response.</summary>
  public DateOnly ReferenceDate { get; set; }

  /// <summary>Gets or sets the local time used to calculate current routine progress.</summary>
  public DateTime LocalNow { get; set; }

  /// <summary>Gets or sets when the response was generated on the server.</summary>
  public DateTime GeneratedAtUtc { get; set; }

  /// <summary>Gets or sets the language used for labels.</summary>
  public string Language { get; set; } = "pt";

  /// <summary>Gets or sets the item currently shown on the Lock Screen.</summary>
  public TodayWidgetItemDto? Current { get; set; }

  /// <summary>Gets or sets the next known routine item.</summary>
  public TodayWidgetItemDto? Next { get; set; }

  /// <summary>Gets or sets routine entries used to build the local WidgetKit timeline.</summary>
  public List<TodayWidgetItemDto> Timeline { get; set; } = new List<TodayWidgetItemDto>();

  /// <summary>Gets or sets the number of moments planned for the represented day.</summary>
  public int PlannedMomentCount { get; set; }

  /// <summary>Gets or sets the number of moments already completed for the represented day.</summary>
  public int CompletedMomentCount { get; set; }

  /// <summary>Gets or sets how often the iOS widget should validate remote changes when the system allows it.</summary>
  public int RefreshAfterSeconds { get; set; } = 21600;

  /// <summary>Gets or sets a localized fallback title for empty Lock Screen states.</summary>
  public string EmptyTitle { get; set; } = string.Empty;

  /// <summary>Gets or sets a localized fallback detail for empty Lock Screen states.</summary>
  public string EmptyDetail { get; set; } = string.Empty;
}

/// <summary>
/// Versioned iOS widget catalog used by the Scriptable V2 runtime.
/// </summary>
public sealed class IosWidgetCatalogDto
{
  /// <summary>Gets or sets the payload schema version.</summary>
  public int SchemaVersion { get; set; } = 2;

  /// <summary>Gets or sets the current remote runtime version.</summary>
  public string RuntimeVersion { get; set; } = string.Empty;

  /// <summary>Gets or sets the minimum bootstrap version that can load this runtime.</summary>
  public string MinBootstrapVersion { get; set; } = string.Empty;

  /// <summary>Gets or sets when the catalog was generated on the server.</summary>
  public DateTime GeneratedAtUtc { get; set; }

  /// <summary>Gets or sets the suggested widget refresh delay in seconds.</summary>
  public int RefreshAfterSeconds { get; set; } = 21600;

  /// <summary>Gets or sets the localized language used for catalog labels.</summary>
  public string Language { get; set; } = "pt";

  /// <summary>Gets or sets the available widget definitions.</summary>
  public List<IosWidgetDefinitionDto> Widgets { get; set; } = new List<IosWidgetDefinitionDto>();
}

/// <summary>
/// One selectable iOS widget experience in the V2 catalog.
/// </summary>
public sealed class IosWidgetDefinitionDto
{
  /// <summary>Gets or sets the stable widget kind parameter.</summary>
  public string Kind { get; set; } = string.Empty;

  /// <summary>Gets or sets the intended iOS widget surface.</summary>
  public string Family { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized widget title.</summary>
  public string Title { get; set; } = string.Empty;

  /// <summary>Gets or sets the localized widget description.</summary>
  public string Detail { get; set; } = string.Empty;

  /// <summary>Gets or sets the widget accent color.</summary>
  public string Accent { get; set; } = "#22d3ee";

  /// <summary>Gets or sets the privacy level for compact locked displays.</summary>
  public string PrivacyLevel { get; set; } = "normal";
}

/// <summary>
/// Versioned iOS widget snapshot consumed by the Scriptable V2 runtime.
/// </summary>
public sealed class IosWidgetSnapshotDto
{
  /// <summary>Gets or sets the payload schema version.</summary>
  public int SchemaVersion { get; set; } = 2;

  /// <summary>Gets or sets the current remote runtime version.</summary>
  public string RuntimeVersion { get; set; } = string.Empty;

  /// <summary>Gets or sets the minimum bootstrap version that can load this runtime.</summary>
  public string MinBootstrapVersion { get; set; } = string.Empty;

  /// <summary>Gets or sets when the snapshot was generated on the server.</summary>
  public DateTime GeneratedAtUtc { get; set; }

  /// <summary>Gets or sets the suggested widget refresh delay in seconds.</summary>
  public int RefreshAfterSeconds { get; set; } = 21600;

  /// <summary>Gets or sets the requested widget kind.</summary>
  public string Kind { get; set; } = string.Empty;

  /// <summary>Gets or sets the selected widget definition.</summary>
  public IosWidgetDefinitionDto SelectedWidget { get; set; } = new IosWidgetDefinitionDto();

  /// <summary>Gets or sets the available widget catalog.</summary>
  public List<IosWidgetDefinitionDto> Widgets { get; set; } = new List<IosWidgetDefinitionDto>();

  /// <summary>Gets or sets the Today data reused by every iOS widget variant.</summary>
  public TodayWidgetSnapshotDto Today { get; set; } = new TodayWidgetSnapshotDto();

  /// <summary>Gets or sets a localized fallback title for empty states.</summary>
  public string EmptyTitle { get; set; } = string.Empty;

  /// <summary>Gets or sets a localized fallback detail for empty states.</summary>
  public string EmptyDetail { get; set; } = string.Empty;
}

/// <summary>
/// Request used to exchange a one-time pairing code for a device token.
/// </summary>
public sealed class WidgetPairingExchangeRequestDto
{
  /// <summary>Gets or sets the one-time code shown by the web app.</summary>
  public string Code { get; set; } = string.Empty;

  /// <summary>Gets or sets a friendly local device name.</summary>
  public string DeviceName { get; set; } = string.Empty;
}

/// <summary>
/// Response returned when the web app creates a one-time pairing code.
/// </summary>
public sealed class WidgetPairingCodeResponseDto
{
  /// <summary>Gets or sets the one-time pairing code.</summary>
  public string Code { get; set; } = string.Empty;

  /// <summary>Gets or sets when the code expires.</summary>
  public DateTime ExpiresAtUtc { get; set; }
}

/// <summary>
/// Response returned when a widget is paired.
/// </summary>
public sealed class WidgetPairingExchangeResponseDto
{
  /// <summary>Gets or sets the bearer token stored by the local widget.</summary>
  public string Token { get; set; } = string.Empty;

  /// <summary>Gets or sets the server base URL used by the widget.</summary>
  public string BaseUrl { get; set; } = string.Empty;
}

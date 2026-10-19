//-----------------------------------------------------------------------
// <copyright file="TodayWidgetSnapshotService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Widgets;

using System.Globalization;
using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Services.Financial;
using AppControlOrganizer.V2.Web.Services.Localization;
using AppControlOrganizer.V2.Web.Services.Planning;

/// <summary>
/// Builds the compact Today widget snapshot from the same domain services used by the web experience.
/// </summary>
public sealed class TodayWidgetSnapshotService : ITodayWidgetSnapshotService
{
  private const int TimelinePreviewLimit = 10;

  private readonly IFinancialFactsService financialFactsService;
  private readonly LocService localizer;
  private readonly IPlanningActionService planningActionService;
  private readonly IPlanningExperienceService planningExperienceService;

  /// <summary>
  /// Initializes a new instance of the <see cref="TodayWidgetSnapshotService"/> class.
  /// </summary>
  public TodayWidgetSnapshotService(
    IPlanningExperienceService planningExperienceService,
    IPlanningActionService planningActionService,
    IFinancialFactsService financialFactsService,
    LocService localizer)
  {
    this.planningExperienceService = planningExperienceService;
    this.planningActionService = planningActionService;
    this.financialFactsService = financialFactsService;
    this.localizer = localizer;
  }

  /// <inheritdoc />
  public async Task<TodayWidgetSnapshotDto> BuildAsync(
    string userId,
    DateTime localNow,
    TimeSpan utcOffset,
    string language,
    CancellationToken cancellationToken = default)
  {
    this.localizer.Set(language);
    DateOnly today = DateOnly.FromDateTime(localNow);

    Task<FinancialFacts> seedFactsTask = this.financialFactsService.GetAsync(userId, today, cancellationToken: cancellationToken);
    Task<AnnualPlanEntity?> planTask = this.planningExperienceService.GetAnnualPlanAsync(
      userId,
      today.Year,
      cancellationToken);
    Task<RoutineTemplateEntity?> routineTask = this.planningExperienceService.GetActiveRoutineAsync(
      userId,
      cancellationToken);
    Task<List<WeeklyFocusEntity>> focusTask = this.planningExperienceService.GetWeeklyFocusesAsync(
      userId,
      today,
      cancellationToken);
    Task<List<WeeklyFocusEntity>> suggestionTask = this.planningExperienceService.GetWeeklyFocusSuggestionsAsync(
      userId,
      today,
      cancellationToken);
    Task<List<PlanningActionEntity>> actionTask = this.planningActionService.GetOpenActionsAsync(
      userId,
      6,
      cancellationToken);

    await Task.WhenAll(seedFactsTask, planTask, routineTask, focusTask, suggestionTask, actionTask);
    FinancialFacts seedFacts = await seedFactsTask;
    AnnualPlanEntity annualPlan = await planTask
      ?? await this.planningExperienceService.GetOrCreateAnnualPlanAsync(
        userId,
        today.Year,
        seedFacts,
        cancellationToken);
    FinancialFacts facts = await this.financialFactsService.GetWithAnnualPlanAsync(userId, today, cancellationToken);
    RoutineTemplateEntity? routine = await routineTask;
    List<TodayWidgetSourceItem> items = BuildTimeline(
      routine,
      localNow);
    TodayWidgetSourceItem? current = ResolveCurrent(items, localNow);
    TodayWidgetSourceItem? next = ResolveNext(items, routine, localNow);
    int completedCount = items.Count(item => item.EndLocal <= localNow);
    int similarCount = CountSimilar(routine, current);
    List<WeeklyFocusEntity> focuses = await focusTask;
    List<WeeklyFocusEntity> suggestions = MergeFocusSuggestions(await suggestionTask, facts.Signals);
    List<PlanningActionEntity> actions = await actionTask;

    return new TodayWidgetSnapshotDto
    {
      ReferenceDate = today,
      LocalNow = localNow,
      GeneratedAtUtc = DateTime.UtcNow,
      Language = this.localizer.Language,
      Title = this.localizer["Today"],
      DateLabel = localNow.ToString("dddd, dd MMMM", GetCulture()),
      Summary = BuildSummary(items.Count, next, localNow),
      Current = ToDto(current, localNow, similarCount),
      Next = ToDto(next, localNow, similarCount),
      Timeline = BuildTimelinePreview(items, localNow, next)
        .Select(item => ToDto(item, localNow, similarCount))
        .Where(item => item != null)
        .Cast<TodayWidgetItemDto>()
        .ToList(),
      Focuses = (focuses.Count > 0 ? focuses : suggestions)
        .Take(3)
        .Select(item => ToFocusDto(item, focuses.Count == 0))
        .ToList(),
      Actions = actions.Take(3).Select(ToActionDto).ToList(),
      Insights = this.BuildInsightCards(facts),
      Metadata = new TodayWidgetMetadataDto
      {
        PlannedMomentCount = items.Count,
        CompletedMomentCount = completedCount,
        SimilarMomentCount = similarCount,
        OpenActionCount = actions.Count,
        RefreshAfterSeconds = 60,
      },
    };
  }

  private static List<TodayWidgetSourceItem> BuildTimeline(
    RoutineTemplateEntity? routine,
    DateTime localNow)
  {
    List<TodayWidgetSourceItem> routineItems = routine?.Blocks
      .Where(item => item.DayOfWeek == localNow.DayOfWeek && !item.Deleted)
      .OrderBy(item => item.StartTime)
      .Select(item => new TodayWidgetSourceItem
      {
        Id = item.Id,
        Title = item.Title,
        SourceKey = "Routine",
        Category = string.IsNullOrWhiteSpace(item.Category) ? "Routine" : item.Category,
        IconCss = item.IconCss,
        ColorHex = item.ColorHex,
        StartLocal = localNow.Date.Add(item.StartTime.ToTimeSpan()),
        EndLocal = localNow.Date.Add(item.EndTime.ToTimeSpan()),
        IsCalendar = false,
      })
      .ToList() ?? new List<TodayWidgetSourceItem>();

    return routineItems
      .OrderBy(item => item.StartLocal)
      .ToList();
  }

  private static TodayWidgetSourceItem? ResolveCurrent(List<TodayWidgetSourceItem> items, DateTime localNow)
  {
    return items
      .Where(item => item.StartLocal <= localNow && localNow < item.EndLocal)
      .FirstOrDefault();
  }

  private static TodayWidgetSourceItem? ResolveNext(
    List<TodayWidgetSourceItem> items,
    RoutineTemplateEntity? routine,
    DateTime localNow)
  {
    TodayWidgetSourceItem? next = items.FirstOrDefault(item => item.StartLocal > localNow);
    if (next != null || routine == null)
    {
      return next;
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
        return new TodayWidgetSourceItem
        {
          Id = block.Id,
          Title = block.Title,
          SourceKey = "Routine",
          Category = string.IsNullOrWhiteSpace(block.Category) ? "Routine" : block.Category,
          IconCss = block.IconCss,
          ColorHex = block.ColorHex,
          StartLocal = date.Add(block.StartTime.ToTimeSpan()),
          EndLocal = date.Add(block.EndTime.ToTimeSpan()),
        };
      }
    }

    return null;
  }

  private static IReadOnlyList<TodayWidgetSourceItem> BuildTimelinePreview(
    List<TodayWidgetSourceItem> items,
    DateTime localNow,
    TodayWidgetSourceItem? next)
  {
    DateTime previewStart = next?.StartLocal ?? localNow;
    List<TodayWidgetSourceItem> upcoming = items
      .Where(item => item.StartLocal > previewStart)
      .Take(TimelinePreviewLimit)
      .ToList();
    if (upcoming.Count > 0)
    {
      return upcoming;
    }

    return items.TakeLast(TimelinePreviewLimit).ToList();
  }

  private static int CountSimilar(RoutineTemplateEntity? routine, TodayWidgetSourceItem? current)
  {
    if (routine == null || current == null)
    {
      return 0;
    }

    return Math.Max(
      1,
      routine.Blocks.Count(item => !item.Deleted
        && string.Equals(item.Title, current.Title, StringComparison.OrdinalIgnoreCase)));
  }

  private TodayWidgetItemDto? ToDto(TodayWidgetSourceItem? item, DateTime localNow, int similarCount)
  {
    if (item == null)
    {
      return null;
    }

    return new TodayWidgetItemDto
    {
      Id = item.Id,
      Title = item.Title,
      Source = this.localizer[item.SourceKey],
      Category = this.localizer[item.Category],
      StartLocal = item.StartLocal,
      EndLocal = item.EndLocal,
      Href = GetHref(item),
      IconCss = string.IsNullOrWhiteSpace(item.IconCss) ? "bi bi-circle" : item.IconCss,
      ColorHex = string.IsNullOrWhiteSpace(item.ColorHex) ? "#64748b" : item.ColorHex,
      IsCurrent = item.StartLocal <= localNow && localNow < item.EndLocal,
      IsCalendar = item.IsCalendar,
      ProgressPercent = GetProgressPercent(item, localNow),
      DurationLabel = FormatDuration(item.EndLocal - item.StartLocal),
      RemainingLabel = FormatDuration(item.EndLocal - localNow),
      NextLabel = GetNextLabel(item, localNow),
    };
  }

  private TodayWidgetFocusDto ToFocusDto(WeeklyFocusEntity focus, bool suggested)
  {
    return new TodayWidgetFocusDto
    {
      Text = this.localizer[focus.Text],
      Href = GetFocusHref(focus),
      Suggested = suggested,
    };
  }

  private TodayWidgetActionDto ToActionDto(PlanningActionEntity action)
  {
    return new TodayWidgetActionDto
    {
      Id = action.Id,
      Title = this.localizer[action.TitleKey],
      Detail = this.localizer[action.DetailKey],
      Evidence = FormatActionEvidence(action),
      Href = action.DestinationHref,
      IconCss = action.IconCss,
      Tone = GetActionTone(action.ActionType),
      PrimaryAction = this.localizer[action.PrimaryActionKey],
    };
  }

  private List<TodayWidgetInsightDto> BuildInsightCards(FinancialFacts facts)
  {
    decimal monthBalance = facts.MonthlyOperatingIncome - facts.MonthlyOperatingExpense;
    decimal reservePercent = facts.EmergencyReserveTarget <= 0m
      ? 0m
      : Math.Clamp(facts.EmergencyReserve / facts.EmergencyReserveTarget * 100m, 0m, 999m);
    int openSignals = facts.Signals.Count(item => item.RequiresAttention);

    return new List<TodayWidgetInsightDto>
    {
      CreateInsight(
        "↕",
        "Month balance",
        FormatBrl(monthBalance),
        "income minus expenses",
        "transactions",
        monthBalance >= 0m ? "positive" : "danger"),
      CreateInsight(
        "↗",
        "Income this month",
        FormatBrl(facts.MonthlyOperatingIncome),
        "operating income",
        "transactions?type=income",
        "positive"),
      CreateInsight(
        "↘",
        "Expenses this month",
        FormatBrl(facts.MonthlyOperatingExpense),
        "operating expenses",
        "transactions?type=expense",
        "danger"),
      CreateInsight(
        "▣",
        "Cash in accounts",
        FormatBrl(facts.AccountCash),
        "included account cash",
        "accounts",
        "blue"),
      CreateInsight(
        "⌁",
        "Active investments",
        FormatBrl(facts.AllocationCurrentValue),
        "current allocation value",
        "allocations",
        "teal"),
      CreateInsight(
        "◇",
        "Current net worth",
        FormatBrl(facts.NetWorth),
        "canonical financial facts",
        "plan",
        "purple"),
      CreateInsight(
        "＋",
        "Month contributions",
        FormatBrl(facts.MonthlyContributions),
        "classified contributions",
        "allocations",
        "green"),
      CreateInsight(
        "◔",
        "Reserve progress",
        $"{reservePercent:N0}%",
        $"{FormatBrl(facts.EmergencyReserve)} {this.localizer["of target"]}",
        "allocations",
        facts.EmergencyReserveTarget > 0m && facts.EmergencyReserve < facts.EmergencyReserveTarget ? "warning" : "green"),
      CreateInsight(
        "⚑",
        "Annual projection",
        FormatBrl(facts.PlannedYearEndNetWorth),
        "with current plan",
        "plan",
        "purple"),
      CreateInsight(
        "!",
        "Open signals",
        openSignals.ToString(CultureInfo.InvariantCulture),
        "attention required",
        "plan",
        openSignals > 0 ? "warning" : "green"),
    };
  }

  private TodayWidgetInsightDto CreateInsight(
    string icon,
    string titleKey,
    string value,
    string detailKey,
    string href,
    string tone)
  {
    return new TodayWidgetInsightDto
    {
      Icon = icon,
      Title = this.localizer[titleKey],
      Value = value,
      Detail = this.localizer[detailKey],
      Href = href,
      Tone = tone,
    };
  }

  private string BuildSummary(int itemCount, TodayWidgetSourceItem? next, DateTime localNow)
  {
    if (itemCount == 0)
    {
      return this.localizer["Your day is open right now."];
    }

    if (next != null && next.StartLocal.Date == localNow.Date)
    {
      return string.Format(
        CultureInfo.InvariantCulture,
        this.localizer["{0} moments today · next: {1} at {2}"],
        itemCount,
        next.Title,
        next.StartLocal.ToString("HH:mm", CultureInfo.InvariantCulture));
    }

    return string.Format(
      CultureInfo.InvariantCulture,
      this.localizer["{0} moments today · your schedule is clear from here"],
      itemCount);
  }

  private string FormatActionEvidence(PlanningActionEntity action)
  {
    if (action.SourceCode == "PLAN_REVIEW_DUE")
    {
      return string.IsNullOrWhiteSpace(action.EvidenceText)
        ? this.localizer["No review yet"]
        : string.Format(
          CultureInfo.InvariantCulture,
          this.localizer["{0} day(s) since last review"],
          action.EvidenceText);
    }

    if (action.SourceCode is "FIN_UNPRICED_POSITIONS" or "DATA_ALLOCATION_PURPOSE")
    {
      return string.Format(
        CultureInfo.InvariantCulture,
        this.localizer["{0} allocation(s)"],
        action.EvidenceText);
    }

    if (action.SourceCode == "DATA_TRANSACTION_IMPACT")
    {
      return string.Format(
        CultureInfo.InvariantCulture,
        this.localizer["{0} transaction(s)"],
        action.EvidenceText);
    }

    return action.EvidenceText;
  }

  private string FormatDuration(TimeSpan duration)
  {
    if (duration < TimeSpan.Zero)
    {
      duration = TimeSpan.Zero;
    }

    int totalMinutes = (int)Math.Ceiling(duration.TotalMinutes);
    if (totalMinutes >= 60)
    {
      return string.Format(
        CultureInfo.InvariantCulture,
        "{0}h {1:D2}min",
        totalMinutes / 60,
        totalMinutes % 60);
    }

    return string.Format(CultureInfo.InvariantCulture, this.localizer["{0} min"], totalMinutes);
  }

  private string FormatBrl(decimal value)
  {
    return value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
  }

  private string GetNextLabel(TodayWidgetSourceItem item, DateTime localNow)
  {
    if (item.StartLocal.Date == localNow.Date)
    {
      return this.localizer["Today"];
    }

    return GetDayName(item.StartLocal.DayOfWeek);
  }

  private string GetDayName(DayOfWeek day)
  {
    string key = day switch
    {
      DayOfWeek.Sunday => "Sunday",
      DayOfWeek.Monday => "Monday",
      DayOfWeek.Tuesday => "Tuesday",
      DayOfWeek.Wednesday => "Wednesday",
      DayOfWeek.Thursday => "Thursday",
      DayOfWeek.Friday => "Friday",
      _ => "Saturday",
    };
    return this.localizer[key];
  }

  private CultureInfo GetCulture()
  {
    if (this.localizer.Language == "pt")
    {
      return CultureInfo.GetCultureInfo("pt-BR");
    }

    return CultureInfo.GetCultureInfo("en-US");
  }

  private static int GetProgressPercent(TodayWidgetSourceItem item, DateTime localNow)
  {
    if (item.EndLocal <= item.StartLocal)
    {
      return 0;
    }

    double progress = (localNow - item.StartLocal).TotalMinutes
      / (item.EndLocal - item.StartLocal).TotalMinutes
      * 100d;
    return (int)Math.Clamp(progress, 0d, 100d);
  }

  private static string GetHref(TodayWidgetSourceItem item)
  {
    return item.Id > 0 ? $"routine?blockId={item.Id}" : "routine";
  }

  private static string GetFocusHref(WeeklyFocusEntity focus)
  {
    if (focus.RoutineBlockId.HasValue)
    {
      return $"routine?blockId={focus.RoutineBlockId.Value}";
    }

    if (focus.QuarterTaskId.HasValue)
    {
      return $"planning/quarters?taskId={focus.QuarterTaskId.Value}#quarter-task-{focus.QuarterTaskId.Value}";
    }

    if (focus.KeyResultId.HasValue)
    {
      return $"planning/okrs?keyResultId={focus.KeyResultId.Value}";
    }

    if (focus.ObjectiveId.HasValue)
    {
      return $"planning/okrs?objectiveId={focus.ObjectiveId.Value}";
    }

    return "plan#week";
  }

  private static string GetActionTone(PlanningActionType actionType)
  {
    return actionType switch
    {
      PlanningActionType.Apply => "apply",
      PlanningActionType.Correct => "correct",
      PlanningActionType.Decide => "decide",
      PlanningActionType.Register => "register",
      PlanningActionType.Review => "review",
      _ => "execute",
    };
  }

  private static List<WeeklyFocusEntity> MergeFocusSuggestions(
    List<WeeklyFocusEntity> planSuggestions,
    IReadOnlyCollection<FinancialSignal> signals)
  {
    List<WeeklyFocusEntity> merged = planSuggestions.Take(3).ToList();
    foreach (FinancialSignal signal in signals.Where(item => item.RequiresAttention))
    {
      if (merged.Count == 3)
      {
        break;
      }

      if (!merged.Any(item => string.Equals(item.Text, signal.TitleKey, StringComparison.OrdinalIgnoreCase)))
      {
        merged.Add(new WeeklyFocusEntity { Text = signal.TitleKey, SortOrder = merged.Count + 1 });
      }
    }

    return merged;
  }

  private sealed class TodayWidgetSourceItem
  {
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string SourceKey { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string IconCss { get; set; } = string.Empty;

    public string ColorHex { get; set; } = "#64748b";

    public DateTime StartLocal { get; set; }

    public DateTime EndLocal { get; set; }

    public bool IsCalendar { get; set; }
  }
}

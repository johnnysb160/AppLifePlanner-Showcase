// <copyright file="Habits.razor.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>

namespace AppControlOrganizer.V2.Web.Components.Pages.Planning;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using AppControlOrganizer.V2.Web.Services.Planning;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

/// <summary>
/// Code-behind for the Habits tracking page.
/// </summary>
public partial class Habits
{
  [Inject] private IPlanningService PlanningService { get; set; } = default!;
  [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

  private List<HabitEntity> habits = new();
  private List<PillarEntity> pillars = new();
  private List<HabitLogEntity> logs = new();
  private Dictionary<int, HabitMetrics> metricsMap = new();

  private DateTime currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
  private List<int> daysInMonth = new();
  private bool isLoading = true;
  private bool isSaving = false;
  private string statusMessage = string.Empty;

  // ── Habit modal ───────────────────────────────────────────────────────────
  private bool showHabitModal = false;
  private HabitEntity editingHabit = new();

  // Selected days of week for the frequency picker (0=Sun,1=Mon,...,6=Sat)
  private HashSet<int> editingHabitDays = new HashSet<int>();

  // ── Delete confirm ────────────────────────────────────────────────────────
  private string deleteMessage = string.Empty;
  private Func<Task>? pendingDeleteAction;

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    await LoadPage();
  }

  private async Task LoadPage()
  {
    isLoading = true;
    string userId = await GetUserIdAsync();
    habits = await PlanningService.GetHabitsAsync(userId);
    pillars = await PlanningService.GetPillarsAsync(userId);
    await LoadLogsAndMetrics(userId);
    RebuildDays();
    isLoading = false;
  }

  private async Task LoadLogsAndMetrics(string userId)
  {
    logs = await PlanningService.GetHabitLogsAsync(userId, currentMonth.Year, currentMonth.Month);
    metricsMap.Clear();
    foreach (HabitEntity h in habits)
    {
      HabitMetrics m = await PlanningService.GetHabitMetricsAsync(h.Id, userId);
      metricsMap[h.Id] = m;
    }
  }

  private void RebuildDays()
  {
    int days = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);
    daysInMonth = Enumerable.Range(1, days).ToList();
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private async Task<string> GetUserIdAsync()
  {
    AuthenticationState state = await AuthStateProvider.GetAuthenticationStateAsync();
    return state.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? LifePlannerContext.DemoUserId;
  }

  private async Task<bool> EnsureWriteAllowedAsync()
  {
    string currentUserId = await GetUserIdAsync();
    if (currentUserId != LifePlannerContext.DemoUserId)
    {
      return true;
    }

    statusMessage = Loc[DemoReadOnlyException.MessageKey];
    return false;
  }

  private bool IsDone(int habitId, int day)
  {
    DateTime date = new DateTime(currentMonth.Year, currentMonth.Month, day, 0, 0, 0, DateTimeKind.Utc);
    return logs.Any(l => l.HabitId == habitId && l.LogDate.Date == date.Date && l.Done);
  }

  private bool IsToday(int day)
  {
    return currentMonth.Year == DateTime.Today.Year
      && currentMonth.Month == DateTime.Today.Month
      && day == DateTime.Today.Day;
  }

  private bool IsFutureDay(int day)
  {
    DateTime date = new DateTime(currentMonth.Year, currentMonth.Month, day);
    return date.Date > DateTime.Today;
  }

  private string DayOfWeekLabel(int day)
  {
    DateTime date = new DateTime(currentMonth.Year, currentMonth.Month, day);

    // Abbreviations come from the active culture; hardcoding them pinned the calendar to Portuguese
    // regardless of the interface language.
    return Loc.Culture.DateTimeFormat.AbbreviatedDayNames[(int)date.DayOfWeek];
  }

  private HabitMetrics? GetMetrics(int habitId)
    => metricsMap.TryGetValue(habitId, out HabitMetrics? m) ? m : null;

  private static string PercentBadge(double pct) => pct switch
  {
    >= 80 => "bg-success",
    >= 50 => "bg-warning text-dark",
    _ => "bg-danger",
  };

  // ── Month navigation ──────────────────────────────────────────────────────

  private async Task PrevMonth()
  {
    currentMonth = currentMonth.AddMonths(-1);
    RebuildDays();
    string userId = await GetUserIdAsync();
    await LoadLogsAndMetrics(userId);
  }

  private async Task NextMonth()
  {
    currentMonth = currentMonth.AddMonths(1);
    RebuildDays();
    string userId = await GetUserIdAsync();
    await LoadLogsAndMetrics(userId);
  }

  // ── Toggle log ────────────────────────────────────────────────────────────

  private async Task ToggleHabit(int habitId, int day)
  {
    if (!await EnsureWriteAllowedAsync())
    {
      return;
    }

    DateTime logDate = new DateTime(currentMonth.Year, currentMonth.Month, day, 0, 0, 0, DateTimeKind.Utc);
    string userId = await GetUserIdAsync();
    HabitLogEntity updated = await PlanningService.ToggleHabitLogAsync(habitId, logDate, userId);

    HabitLogEntity? existing = logs.FirstOrDefault(l => l.HabitId == habitId && l.LogDate.Date == logDate.Date);
    if (existing != null)
    {
      existing.Done = updated.Done;
    }
    else
    {
      logs.Add(updated);
    }

    // Refresh metrics for this habit
    HabitMetrics m = await PlanningService.GetHabitMetricsAsync(habitId, userId);
    metricsMap[habitId] = m;
  }

  // ── Habit CRUD ────────────────────────────────────────────────────────────

  private void AddHabit()
  {
    editingHabit = new HabitEntity
    {
      ColorHex = "#0d6efd",
      IsActive = true,
    };
    editingHabitDays = new HashSet<int>();
    showHabitModal = true;
  }

  private void EditHabit(HabitEntity habit)
  {
    editingHabit = new HabitEntity
    {
      Id = habit.Id,
      Name = habit.Name,
      Description = habit.Description,
      ColorHex = habit.ColorHex,
      IconCss = habit.IconCss,
      PillarId = habit.PillarId,
      IsActive = habit.IsActive,
      FrequencyDays = habit.FrequencyDays,
    };
    editingHabitDays = ParseFrequencyDays(habit.FrequencyDays);
    showHabitModal = true;
  }

  private void ToggleFrequencyDay(int day)
  {
    if (!editingHabitDays.Add(day))
    {
      editingHabitDays.Remove(day);
    }
  }

  private static HashSet<int> ParseFrequencyDays(string? raw)
  {
    HashSet<int> result = new HashSet<int>();
    if (string.IsNullOrWhiteSpace(raw))
    {
      return result;
    }

    foreach (string part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
    {
      if (int.TryParse(part.Trim(), out int d))
      {
        result.Add(d);
      }
    }

    return result;
  }

  private static string? BuildFrequencyDays(HashSet<int> days)
  {
    if (days.Count == 0)
    {
      return null;
    }

    return string.Join(",", days.OrderBy(d => d));
  }

  private void CloseHabitModal() => showHabitModal = false;

  private async Task SaveHabit()
  {
    if (string.IsNullOrWhiteSpace(editingHabit.Name))
    {
      return;
    }

    if (!await EnsureWriteAllowedAsync())
    {
      return;
    }

    isSaving = true;
    try
    {
      // Sync frequency days from the checkbox set to the entity field
      editingHabit.FrequencyDays = BuildFrequencyDays(editingHabitDays);

      string userId = await GetUserIdAsync();
      HabitEntity saved = await PlanningService.SaveHabitAsync(editingHabit, userId);
      saved.Pillar = saved.PillarId.HasValue ? pillars.FirstOrDefault(p => p.Id == saved.PillarId.Value) : null;

      HabitEntity? existing = habits.FirstOrDefault(h => h.Id == saved.Id);
      if (existing != null)
      {
        int idx = habits.IndexOf(existing);
        saved.Pillar = saved.PillarId.HasValue ? pillars.FirstOrDefault(p => p.Id == saved.PillarId.Value) : null;
        habits[idx] = saved;
      }
      else
      {
        saved.Pillar = saved.PillarId.HasValue ? pillars.FirstOrDefault(p => p.Id == saved.PillarId.Value) : null;
        habits.Add(saved);
        // Initialize metrics for new habit
        metricsMap[saved.Id] = new HabitMetrics { HabitId = saved.Id };
      }

      showHabitModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private void ConfirmDeleteHabit(HabitEntity habit)
  {
    deleteMessage = Loc.Format("Delete habit \"{0}\"?", habit.Name);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteHabitAsync(habit.Id, userId);
      habits.Remove(habits.First(h => h.Id == habit.Id));
      metricsMap.Remove(habit.Id);
      logs.RemoveAll(l => l.HabitId == habit.Id);
    };
  }

  private async Task ExecuteDelete()
  {
    if (pendingDeleteAction == null)
    {
      return;
    }

    if (!await EnsureWriteAllowedAsync())
    {
      deleteMessage = string.Empty;
      pendingDeleteAction = null;
      return;
    }

    isSaving = true;
    try
    {
      await pendingDeleteAction.Invoke();
      deleteMessage = string.Empty;
      pendingDeleteAction = null;
    }
    finally
    {
      isSaving = false;
    }
  }
}

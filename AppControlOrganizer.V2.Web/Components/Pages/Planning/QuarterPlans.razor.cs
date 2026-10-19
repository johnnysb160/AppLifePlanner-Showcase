// <copyright file="QuarterPlans.razor.cs" company="AppLifePlanner">
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
/// Code-behind for the Quarter Plans page.
/// </summary>
public partial class QuarterPlans
{
  /// <summary>Gets or sets the quarter task requested by contextual navigation.</summary>
  [Parameter]
  [SupplyParameterFromQuery(Name = "taskId")]
  public int? RequestedTaskId { get; set; }

  [Inject] private IPlanningService PlanningService { get; set; } = default!;
  [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

  private List<QuarterPlanEntity> plans = new();
  private bool isLoading = true;
  private bool isSaving = false;
  private string statusMessage = string.Empty;

  // ── Plan modal ────────────────────────────────────────────────────────────
  private bool showPlanModal = false;
  private QuarterPlanEntity editingPlan = new();

  // ── Task modal ────────────────────────────────────────────────────────────
  private bool showTaskModal = false;
  private string newTaskText = string.Empty;
  private int taskTargetPlanId = 0;

  // ── Delete confirm ────────────────────────────────────────────────────────
  private string deleteMessage = string.Empty;
  private Func<Task>? pendingDeleteAction;

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    string userId = await GetUserIdAsync();
    plans = await PlanningService.GetQuarterPlansAsync(userId);

    // Load tasks for each plan
    foreach (QuarterPlanEntity plan in plans)
    {
      QuarterPlanEntity? full = await PlanningService.GetQuarterPlanWithTasksAsync(plan.Id, userId);
      if (full != null)
      {
        plan.Tasks = full.Tasks;
      }
    }

    isLoading = false;
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

  private static string RagBadge(RagStatus status) => status switch
  {
    RagStatus.Green => "bg-success",
    RagStatus.Amber => "bg-warning text-dark",
    RagStatus.Red => "bg-danger",
    _ => "bg-secondary",
  };

  private static string RagLabel(RagStatus status) => status switch
  {
    RagStatus.Green => "Verde",
    RagStatus.Amber => "Amarelo",
    RagStatus.Red => "Vermelho",
    _ => "?",
  };

  private static string QuarterHeaderClass(RagStatus status) => status switch
  {
    RagStatus.Green => "quarter-header-green",
    RagStatus.Amber => "quarter-header-amber",
    RagStatus.Red => "quarter-header-red",
    _ => string.Empty,
  };

  // ── Plan CRUD ─────────────────────────────────────────────────────────────

  private void AddQuarterPlan()
  {
    int currentYear = DateTime.Today.Year;
    int currentQ = (DateTime.Today.Month - 1) / 3 + 1;
    editingPlan = new QuarterPlanEntity
    {
      Year = currentYear,
      Quarter = currentQ,
      Status = RagStatus.Green,
    };
    showPlanModal = true;
  }

  private void EditPlan(QuarterPlanEntity plan)
  {
    editingPlan = new QuarterPlanEntity
    {
      Id = plan.Id,
      Year = plan.Year,
      Quarter = plan.Quarter,
      Focus = plan.Focus,
      Delivery = plan.Delivery,
      TargetDate = plan.TargetDate,
      WeeklyCadence = plan.WeeklyCadence,
      KpiDescription = plan.KpiDescription,
      Status = plan.Status,
    };
    showPlanModal = true;
  }

  private void ClosePlanModal() => showPlanModal = false;

  private async Task SavePlan()
  {
    if (string.IsNullOrWhiteSpace(editingPlan.Focus))
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
      string userId = await GetUserIdAsync();
      QuarterPlanEntity saved = await PlanningService.SaveQuarterPlanAsync(editingPlan, userId);

      QuarterPlanEntity? existing = plans.FirstOrDefault(p => p.Id == saved.Id);
      if (existing != null)
      {
        existing.Year = saved.Year;
        existing.Quarter = saved.Quarter;
        existing.Focus = saved.Focus;
        existing.Delivery = saved.Delivery;
        existing.TargetDate = saved.TargetDate;
        existing.WeeklyCadence = saved.WeeklyCadence;
        existing.KpiDescription = saved.KpiDescription;
        existing.Status = saved.Status;
      }
      else
      {
        saved.Tasks = new List<QuarterTaskEntity>();
        plans.Add(saved);
        plans = plans.OrderByDescending(p => p.Year).ThenBy(p => p.Quarter).ToList();
      }

      showPlanModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private void ConfirmDeletePlan(QuarterPlanEntity plan)
  {
    deleteMessage = Loc.Format("Delete plan \"Q{0}/{1}\"?", plan.Quarter, plan.Year);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteQuarterPlanAsync(plan.Id, userId);
      plans.Remove(plans.First(p => p.Id == plan.Id));
    };
  }

  // ── Task CRUD ─────────────────────────────────────────────────────────────

  private void AddTask(QuarterPlanEntity plan)
  {
    taskTargetPlanId = plan.Id;
    newTaskText = string.Empty;
    showTaskModal = true;
  }

  private void CloseTaskModal() => showTaskModal = false;

  private async Task SaveTask()
  {
    if (string.IsNullOrWhiteSpace(newTaskText))
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
      string userId = await GetUserIdAsync();
      QuarterPlanEntity? plan = plans.FirstOrDefault(p => p.Id == taskTargetPlanId);
      int nextOrder = plan?.Tasks.Count > 0 ? plan.Tasks.Max(t => t.SortOrder) + 1 : 1;

      QuarterTaskEntity task = new()
      {
        QuarterPlanId = taskTargetPlanId,
        Text = newTaskText.Trim(),
        SortOrder = nextOrder,
        Done = false,
      };

      QuarterTaskEntity saved = await PlanningService.SaveQuarterTaskAsync(task, userId);
      plan?.Tasks.Add(saved);
      showTaskModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private async Task ToggleTask(QuarterTaskEntity task)
  {
    if (!await EnsureWriteAllowedAsync())
    {
      return;
    }

    string userId = await GetUserIdAsync();
    await PlanningService.ToggleQuarterTaskAsync(task.Id, userId);
    task.Done = !task.Done;
  }

  private void ConfirmDeleteTask(QuarterTaskEntity task)
  {
    deleteMessage = Loc.Format("Delete task \"{0}\"?", task.Text);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteQuarterTaskAsync(task.Id, userId);
      QuarterPlanEntity? plan = plans.FirstOrDefault(p => p.Id == task.QuarterPlanId);
      plan?.Tasks.Remove(plan.Tasks.First(t => t.Id == task.Id));
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

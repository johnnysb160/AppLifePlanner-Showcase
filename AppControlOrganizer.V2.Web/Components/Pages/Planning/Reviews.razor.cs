// <copyright file="Reviews.razor.cs" company="AppLifePlanner">
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
/// Code-behind for the Reviews page.
/// </summary>
public partial class Reviews
{
  [Inject] private IPlanningService PlanningService { get; set; } = default!;
  [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

  private string activeTab = "runs";
  private bool isLoading = true;
  private bool isSaving = false;
  private string statusMessage = string.Empty;

  // ── Templates ─────────────────────────────────────────────────────────────
  private List<ReviewTemplateEntity> templates = new();
  private bool showTemplateModal = false;
  private ReviewTemplateEntity editingTemplate = new();
  private List<string> templateItemTexts = new();

  // ── Active run ────────────────────────────────────────────────────────────
  private ReviewRunEntity? activeRun;

  // ── History ───────────────────────────────────────────────────────────────
  private List<ReviewRunEntity> historyRuns = new();
  private string filterType = string.Empty;
  private DateTime? filterFrom;
  private DateTime? filterTo;

  // ── Delete confirm ────────────────────────────────────────────────────────
  private string deleteMessage = string.Empty;
  private Func<Task>? pendingDeleteAction;

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    string userId = await GetUserIdAsync();
    if (userId != LifePlannerContext.DemoUserId)
    {
      await PlanningService.EnsureDefaultTemplatesAsync(userId);
    }

    await LoadTemplates(userId);
    isLoading = false;
  }

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

  private async Task LoadTemplates(string userId)
  {
    templates = await PlanningService.GetReviewTemplatesAsync(userId);
    // Load items for each template
    foreach (ReviewTemplateEntity tpl in templates)
    {
      ReviewTemplateEntity? full = await PlanningService.GetReviewTemplateWithItemsAsync(tpl.Id, userId);
      if (full != null)
      {
        tpl.Items = full.Items;
      }
    }
  }

  private async Task SwitchToHistory()
  {
    activeTab = "history";
    await LoadHistory();
  }

  private async Task SwitchToTemplates()
  {
    activeTab = "templates";
    string userId = await GetUserIdAsync();
    await LoadTemplates(userId);
  }

  // ── Run execution ─────────────────────────────────────────────────────────

  private async Task StartRun(ReviewTemplateEntity template)
  {
    if (!await EnsureWriteAllowedAsync())
    {
      return;
    }

    string userId = await GetUserIdAsync();
    activeRun = await PlanningService.StartReviewRunAsync(template.Id, DateTime.UtcNow, userId);

    // Attach template for display
    activeRun.Template = template;
  }

  private void CancelRun()
  {
    activeRun = null;
  }

  private async Task SaveRun()
  {
    if (activeRun == null)
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
      await PlanningService.SaveReviewRunAsync(activeRun, userId);
      activeRun = null;
    }
    finally
    {
      isSaving = false;
    }
  }

  // ── History ───────────────────────────────────────────────────────────────

  private async Task LoadHistory()
  {
    string userId = await GetUserIdAsync();
    ReviewType? type = filterType switch
    {
      "Weekly" => ReviewType.Weekly,
      "Monthly" => ReviewType.Monthly,
      _ => null,
    };
    historyRuns = await PlanningService.GetReviewRunsAsync(userId, type, filterFrom, filterTo);

    // Load items for each run
    foreach (ReviewRunEntity run in historyRuns)
    {
      ReviewRunEntity? full = await PlanningService.GetReviewRunWithItemsAsync(run.Id, userId);
      if (full != null)
      {
        run.Items = full.Items;
        run.Template = full.Template;
      }
    }
  }

  private void ConfirmDeleteRun(ReviewRunEntity run)
  {
    deleteMessage = Loc.Format("Delete review from {0}?", run.RunDate.ToString("d", Loc.Culture));
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteReviewRunAsync(run.Id, userId);
      historyRuns.Remove(historyRuns.First(r => r.Id == run.Id));
    };
  }

  // ── Template CRUD ─────────────────────────────────────────────────────────

  private void AddTemplate()
  {
    editingTemplate = new ReviewTemplateEntity { ReviewType = ReviewType.Weekly };
    templateItemTexts = new List<string>();
    showTemplateModal = true;
  }

  private void EditTemplate(ReviewTemplateEntity tpl)
  {
    editingTemplate = new ReviewTemplateEntity
    {
      Id = tpl.Id,
      Name = tpl.Name,
      ReviewType = tpl.ReviewType,
      IsDefault = tpl.IsDefault,
    };
    templateItemTexts = tpl.Items
      .OrderBy(i => i.SortOrder)
      .Select(i => i.Text)
      .ToList();
    showTemplateModal = true;
  }

  private void CloseTemplateModal() => showTemplateModal = false;

  private void AddTemplateItem() => templateItemTexts.Add(string.Empty);

  private void RemoveTemplateItem(int index)
  {
    if (index >= 0 && index < templateItemTexts.Count)
    {
      templateItemTexts.RemoveAt(index);
    }
  }

  private async Task SaveTemplate()
  {
    if (string.IsNullOrWhiteSpace(editingTemplate.Name))
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

      editingTemplate.Items = templateItemTexts
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .Select((text, i) => new ReviewTemplateItemEntity
        {
          TemplateId = editingTemplate.Id,
          Text = text.Trim(),
          SortOrder = i + 1,
        })
        .ToList();

      ReviewTemplateEntity saved = await PlanningService.SaveReviewTemplateAsync(editingTemplate, userId);
      await LoadTemplates(userId);
      showTemplateModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private void ConfirmDeleteTemplate(ReviewTemplateEntity tpl)
  {
    deleteMessage = Loc.Format("Delete template \"{0}\"?", tpl.Name);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteReviewTemplateAsync(tpl.Id, userId);
      templates.Remove(templates.First(t => t.Id == tpl.Id));
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

// <copyright file="Risks.razor.cs" company="AppLifePlanner">
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
/// Code-behind for the Risks (pre-mortem) page.
/// </summary>
public partial class Risks
{
  [Inject] private IPlanningService PlanningService { get; set; } = default!;
  [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

  private List<RiskEntity> risks = new();
  private List<PillarEntity> pillars = new();
  private bool isLoading = true;
  private bool isSaving = false;
  private string statusMessage = string.Empty;

  // ── Risk modal ────────────────────────────────────────────────────────────
  private bool showRiskModal = false;
  private RiskEntity editingRisk = new();

  // ── Delete confirm ────────────────────────────────────────────────────────
  private string deleteMessage = string.Empty;
  private Func<Task>? pendingDeleteAction;

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    string userId = await GetUserIdAsync();
    risks = await PlanningService.GetRisksAsync(userId);
    pillars = await PlanningService.GetPillarsAsync(userId);
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

  // ── Badge helpers ─────────────────────────────────────────────────────────

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

  private static string ProbabilityBadge(RiskProbability p) => p switch
  {
    RiskProbability.High => "bg-danger",
    RiskProbability.Medium => "bg-warning text-dark",
    _ => "bg-success",
  };

  private static string ProbabilityLabel(RiskProbability p) => p switch
  {
    RiskProbability.High => "Alta",
    RiskProbability.Medium => "Media",
    _ => "Baixa",
  };

  private static string ImpactBadge(RiskImpact imp) => imp switch
  {
    RiskImpact.High => "bg-danger",
    RiskImpact.Medium => "bg-warning text-dark",
    _ => "bg-success",
  };

  private static string ImpactLabel(RiskImpact imp) => imp switch
  {
    RiskImpact.High => "Alto",
    RiskImpact.Medium => "Medio",
    _ => "Baixo",
  };

  // ── Risk CRUD ─────────────────────────────────────────────────────────────

  private void AddRisk()
  {
    editingRisk = new RiskEntity
    {
      Probability = RiskProbability.Medium,
      Impact = RiskImpact.Medium,
      Status = RagStatus.Amber,
    };
    showRiskModal = true;
  }

  private void EditRisk(RiskEntity risk)
  {
    editingRisk = new RiskEntity
    {
      Id = risk.Id,
      Title = risk.Title,
      Description = risk.Description,
      Probability = risk.Probability,
      Impact = risk.Impact,
      EarlyWarning = risk.EarlyWarning,
      Contingency = risk.Contingency,
      PlanB = risk.PlanB,
      PillarId = risk.PillarId,
      Status = risk.Status,
    };
    showRiskModal = true;
  }

  private void CloseRiskModal() => showRiskModal = false;

  private async Task SaveRisk()
  {
    if (string.IsNullOrWhiteSpace(editingRisk.Title))
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
      RiskEntity saved = await PlanningService.SaveRiskAsync(editingRisk, userId);

      // Attach pillar navigation for display
      saved.Pillar = saved.PillarId.HasValue
        ? pillars.FirstOrDefault(p => p.Id == saved.PillarId.Value)
        : null;

      RiskEntity? existing = risks.FirstOrDefault(r => r.Id == saved.Id);
      if (existing != null)
      {
        int idx = risks.IndexOf(existing);
        risks[idx] = saved;
      }
      else
      {
        risks.Add(saved);
      }

      showRiskModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private void ConfirmDeleteRisk(RiskEntity risk)
  {
    deleteMessage = Loc.Format("Delete risk \"{0}\"?", risk.Title);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteRiskAsync(risk.Id, userId);
      risks.Remove(risks.First(r => r.Id == risk.Id));
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

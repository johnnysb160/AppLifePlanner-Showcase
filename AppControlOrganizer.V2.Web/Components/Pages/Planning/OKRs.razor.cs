// <copyright file="OKRs.razor.cs" company="AppLifePlanner">
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
/// Code-behind for the OKRs planning page.
/// </summary>
public partial class OKRs : IDisposable
{
  /// <summary>Gets or sets the objective requested by contextual navigation.</summary>
  [Parameter]
  [SupplyParameterFromQuery(Name = "objectiveId")]
  public int? RequestedObjectiveId { get; set; }

  /// <summary>Gets or sets the key result requested by contextual navigation.</summary>
  [Parameter]
  [SupplyParameterFromQuery(Name = "keyResultId")]
  public int? RequestedKeyResultId { get; set; }

  [Inject] private IPlanningService PlanningService { get; set; } = default!;
  [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

  /// <summary>Gets or sets the service backing the header show/hide values toggle.</summary>
  [Inject] private AppControlOrganizer.V2.Web.Services.UI.ValuesVisibilityService ValuesVis { get; set; } = default!;

  private List<PillarEntity> pillars = new();
  private bool isLoading = true;
  private bool isSaving = false;
  private string statusMessage = string.Empty;

  // ── Pillar modal ──────────────────────────────────────────────────────────
  private bool showPillarModal = false;
  private PillarEntity editingPillar = new();

  // ── Objective modal ───────────────────────────────────────────────────────
  private bool showObjectiveModal = false;
  private ObjectiveEntity editingObjective = new();

  // ── Key Result modal ──────────────────────────────────────────────────────
  private bool showKrModal = false;
  private KeyResultEntity editingKr = new();

  // ── Delete confirm ────────────────────────────────────────────────────────
  private string deleteMessage = string.Empty;
  private Func<Task>? pendingDeleteAction;

  /// <inheritdoc />
  protected override async Task OnInitializedAsync()
  {
    this.ValuesVis.OnChange += this.OnValuesVisibilityChanged;
    string userId = await GetUserIdAsync();
    pillars = await PlanningService.GetFullOkrTreeAsync(userId);
    isLoading = false;
    OpenRequestedItemIfAvailable();
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

  private static string ProgressBarColor(double pct) => pct switch
  {
    >= 70 => "bg-success",
    >= 40 => "bg-warning",
    _ => "bg-danger",
  };

  private void OpenRequestedItemIfAvailable()
  {
    if (RequestedKeyResultId.HasValue)
    {
      KeyResultEntity? keyResult = pillars
        .SelectMany(pillar => pillar.Objectives)
        .SelectMany(objective => objective.KeyResults)
        .FirstOrDefault(result => result.Id == RequestedKeyResultId.Value);
      if (keyResult != null)
      {
        EditKeyResult(keyResult);
        return;
      }
    }

    if (!RequestedObjectiveId.HasValue)
    {
      return;
    }

    ObjectiveEntity? objective = pillars
      .SelectMany(pillar => pillar.Objectives)
      .FirstOrDefault(item => item.Id == RequestedObjectiveId.Value);
    if (objective != null)
    {
      EditObjective(objective);
    }
  }

  // ── Pillar CRUD ───────────────────────────────────────────────────────────

  private void AddPillar()
  {
    editingPillar = new PillarEntity { ColorHex = "#0d6efd" };
    showPillarModal = true;
  }

  private void EditPillar(PillarEntity pillar)
  {
    editingPillar = new PillarEntity
    {
      Id = pillar.Id,
      Name = pillar.Name,
      Description = pillar.Description,
      ColorHex = pillar.ColorHex,
      IconCss = pillar.IconCss,
      SortOrder = pillar.SortOrder,
    };
    showPillarModal = true;
  }

  private void ClosePillarModal() => showPillarModal = false;

  private async Task SavePillar()
  {
    if (string.IsNullOrWhiteSpace(editingPillar.Name))
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
      PillarEntity saved = await PlanningService.SavePillarAsync(editingPillar, userId);

      PillarEntity? existing = pillars.FirstOrDefault(p => p.Id == saved.Id);
      if (existing != null)
      {
        existing.Name = saved.Name;
        existing.Description = saved.Description;
        existing.ColorHex = saved.ColorHex;
        existing.IconCss = saved.IconCss;
        existing.SortOrder = saved.SortOrder;
      }
      else
      {
        pillars.Add(saved);
        pillars = pillars.OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToList();
      }

      showPillarModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private void ConfirmDeletePillar(PillarEntity pillar)
  {
    deleteMessage = Loc.Format("Delete pillar \"{0}\" and all its objectives and KRs?", pillar.Name);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeletePillarAsync(pillar.Id, userId);
      pillars.Remove(pillars.First(p => p.Id == pillar.Id));
    };
  }

  // ── Objective CRUD ────────────────────────────────────────────────────────

  private void AddObjective(int pillarId)
  {
    editingObjective = new ObjectiveEntity { PillarId = pillarId, Status = RagStatus.Green };
    showObjectiveModal = true;
  }

  private void EditObjective(ObjectiveEntity obj)
  {
    editingObjective = new ObjectiveEntity
    {
      Id = obj.Id,
      PillarId = obj.PillarId,
      Title = obj.Title,
      Description = obj.Description,
      Deadline = obj.Deadline,
      Status = obj.Status,
      SortOrder = obj.SortOrder,
    };
    showObjectiveModal = true;
  }

  private void CloseObjectiveModal() => showObjectiveModal = false;

  private async Task SaveObjective()
  {
    if (string.IsNullOrWhiteSpace(editingObjective.Title))
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
      ObjectiveEntity saved = await PlanningService.SaveObjectiveAsync(editingObjective, userId);

      PillarEntity? pillar = pillars.FirstOrDefault(p => p.Id == saved.PillarId);
      if (pillar != null)
      {
        ObjectiveEntity? existing = pillar.Objectives.FirstOrDefault(o => o.Id == saved.Id);
        if (existing != null)
        {
          existing.Title = saved.Title;
          existing.Description = saved.Description;
          existing.Deadline = saved.Deadline;
          existing.Status = saved.Status;
          existing.SortOrder = saved.SortOrder;
        }
        else
        {
          saved.KeyResults = new List<KeyResultEntity>();
          pillar.Objectives.Add(saved);
          pillar.Objectives = pillar.Objectives.OrderBy(o => o.SortOrder).ThenBy(o => o.Title).ToList();
        }
      }

      showObjectiveModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private void ConfirmDeleteObjective(ObjectiveEntity obj)
  {
    deleteMessage = Loc.Format("Delete objective \"{0}\" and all its KRs?", obj.Title);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteObjectiveAsync(obj.Id, userId);
      PillarEntity? pillar = pillars.FirstOrDefault(p => p.Id == obj.PillarId);
      pillar?.Objectives.Remove(pillar.Objectives.First(o => o.Id == obj.Id));
    };
  }

  // ── Key Result CRUD ───────────────────────────────────────────────────────

  private void AddKeyResult(int objectiveId)
  {
    editingKr = new KeyResultEntity { ObjectiveId = objectiveId, Status = RagStatus.Green };
    showKrModal = true;
  }

  private void EditKeyResult(KeyResultEntity kr)
  {
    editingKr = new KeyResultEntity
    {
      Id = kr.Id,
      ObjectiveId = kr.ObjectiveId,
      Title = kr.Title,
      MetricUnit = kr.MetricUnit,
      Baseline = kr.Baseline,
      Current = kr.Current,
      Target = kr.Target,
      WeeklyNote = kr.WeeklyNote,
      Deadline = kr.Deadline,
      Status = kr.Status,
      SortOrder = kr.SortOrder,
    };
    showKrModal = true;
  }

  private void CloseKrModal() => showKrModal = false;

  private async Task SaveKeyResult()
  {
    if (string.IsNullOrWhiteSpace(editingKr.Title))
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
      KeyResultEntity saved = await PlanningService.SaveKeyResultAsync(editingKr, userId);

      ObjectiveEntity? obj = pillars
        .SelectMany(p => p.Objectives)
        .FirstOrDefault(o => o.Id == saved.ObjectiveId);

      if (obj != null)
      {
        KeyResultEntity? existing = obj.KeyResults.FirstOrDefault(k => k.Id == saved.Id);
        if (existing != null)
        {
          existing.Title = saved.Title;
          existing.MetricUnit = saved.MetricUnit;
          existing.Baseline = saved.Baseline;
          existing.Current = saved.Current;
          existing.Target = saved.Target;
          existing.WeeklyNote = saved.WeeklyNote;
          existing.Deadline = saved.Deadline;
          existing.Status = saved.Status;
          existing.SortOrder = saved.SortOrder;
        }
        else
        {
          obj.KeyResults.Add(saved);
          obj.KeyResults = obj.KeyResults.OrderBy(k => k.SortOrder).ThenBy(k => k.Title).ToList();
        }
      }

      showKrModal = false;
    }
    finally
    {
      isSaving = false;
    }
  }

  private void ConfirmDeleteKeyResult(KeyResultEntity kr)
  {
    deleteMessage = Loc.Format("Delete Key Result \"{0}\"?", kr.Title);
    pendingDeleteAction = async () =>
    {
      string userId = await GetUserIdAsync();
      await PlanningService.DeleteKeyResultAsync(kr.Id, userId);
      ObjectiveEntity? obj = pillars
        .SelectMany(p => p.Objectives)
        .FirstOrDefault(o => o.Id == kr.ObjectiveId);
      obj?.KeyResults.Remove(obj.KeyResults.First(k => k.Id == kr.Id));
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

  /// <summary>Tokens that mark a key-result metric as monetary.</summary>
  /// <remarks>
  /// MetricUnit is free text ("R$/month", "clients", "%", "hours"), so the only way to tell money
  /// from a plain count is to look for a currency marker. Counts stay visible: hiding "3 clients"
  /// protects nothing and makes the board unreadable.
  /// </remarks>
  private static readonly string[] CurrencyMarkers =
  {
    "R$", "$", "€", "£", "¥", "USD", "BRL", "EUR", "GBP", "real", "reais", "dolar", "dollar",
  };

  /// <summary>Renders a key-result amount, masking it only when the metric is monetary.</summary>
  /// <param name="keyResult">Key result the amount belongs to.</param>
  /// <param name="value">Amount to render.</param>
  /// <returns>The formatted amount, masked when the metric carries a currency unit.</returns>
  private string KeyResultValue(KeyResultEntity keyResult, decimal value)
  {
    // Stored with four decimals: show money with two and other units without trailing zeros.
    bool isCurrency = IsCurrencyMetric(keyResult.MetricUnit);
    string text = value.ToString(isCurrency ? "N2" : "#,0.##", this.Loc.Culture);
    return isCurrency ? this.ValuesVis.Mask(text) : text;
  }

  /// <summary>Reports whether a metric unit denotes money.</summary>
  /// <param name="metricUnit">Free-text unit stored on the key result.</param>
  /// <returns><see langword="true"/> when the unit carries a currency marker.</returns>
  private static bool IsCurrencyMetric(string? metricUnit)
  {
    if (string.IsNullOrWhiteSpace(metricUnit))
    {
      return false;
    }

    foreach (string candidate in CurrencyMarkers)
    {
      if (metricUnit.Contains(candidate, StringComparison.OrdinalIgnoreCase))
      {
        return true;
      }
    }

    return false;
  }

  /// <inheritdoc />
  public void Dispose()
  {
    this.ValuesVis.OnChange -= this.OnValuesVisibilityChanged;
  }

  private void OnValuesVisibilityChanged() => this.InvokeAsync(this.StateHasChanged);
}

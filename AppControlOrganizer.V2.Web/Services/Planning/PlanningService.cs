//-----------------------------------------------------------------------
// <copyright file="PlanningService.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Services.Planning;

using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Shared.Enums;
using AppControlOrganizer.V2.Web.Data;
using Microsoft.EntityFrameworkCore;

/// <inheritdoc />
public sealed class PlanningService : IPlanningService
{
  private readonly IDbContextFactory<LifePlannerContext> contextFactory;

  /// <summary>Initialises a new instance of <see cref="PlanningService"/>.</summary>
  /// <param name="contextFactory">EF context factory for direct database access.</param>
  public PlanningService(IDbContextFactory<LifePlannerContext> contextFactory)
  {
    this.contextFactory = contextFactory;
  }

  // ── Pillars ──────────────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<PillarEntity>> GetPillarsAsync(string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.Pillars
      .Where(p => p.OwnerUserId == userId && !p.Deleted)
      .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
      .ToListAsync();
  }

  /// <inheritdoc />
  public async Task<PillarEntity> SavePillarAsync(PillarEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      db.Pillars.Add(entity);
    }
    else
    {
      PillarEntity? existing = await db.Pillars.FirstOrDefaultAsync(p => p.Id == entity.Id && p.OwnerUserId == userId);
      if (existing is null) { return entity; }
      existing.Name = entity.Name;
      existing.Description = entity.Description;
      existing.SortOrder = entity.SortOrder;
      existing.ColorHex = entity.ColorHex;
      existing.IconCss = entity.IconCss;
    }
    await db.SaveChangesAsync();
    return entity;
  }

  /// <inheritdoc />
  public async Task DeletePillarAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    PillarEntity? entity = await db.Pillars.FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  // ── Objectives ────────────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<ObjectiveEntity>> GetObjectivesAsync(int pillarId, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.Objectives
      .Where(o => o.PillarId == pillarId && o.OwnerUserId == userId && !o.Deleted)
      .OrderBy(o => o.SortOrder).ThenBy(o => o.Title)
      .ToListAsync();
  }

  /// <inheritdoc />
  public async Task<ObjectiveEntity> SaveObjectiveAsync(ObjectiveEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      db.Objectives.Add(entity);
    }
    else
    {
      ObjectiveEntity? existing = await db.Objectives.FirstOrDefaultAsync(o => o.Id == entity.Id && o.OwnerUserId == userId);
      if (existing is null) { return entity; }
      existing.Title = entity.Title;
      existing.Description = entity.Description;
      existing.Deadline = entity.Deadline;
      existing.Status = entity.Status;
      existing.SortOrder = entity.SortOrder;
      existing.PillarId = entity.PillarId;
    }
    await db.SaveChangesAsync();
    return entity;
  }

  /// <inheritdoc />
  public async Task DeleteObjectiveAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    ObjectiveEntity? entity = await db.Objectives.FirstOrDefaultAsync(o => o.Id == id && o.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  // ── Key Results ───────────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<KeyResultEntity>> GetKeyResultsAsync(int objectiveId, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.KeyResults
      .Where(kr => kr.ObjectiveId == objectiveId && kr.OwnerUserId == userId && !kr.Deleted)
      .OrderBy(kr => kr.SortOrder).ThenBy(kr => kr.Title)
      .ToListAsync();
  }

  /// <inheritdoc />
  public async Task<KeyResultEntity> SaveKeyResultAsync(KeyResultEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    entity.UpdatedAt = DateTime.UtcNow;
    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      db.KeyResults.Add(entity);
    }
    else
    {
      KeyResultEntity? existing = await db.KeyResults.FirstOrDefaultAsync(kr => kr.Id == entity.Id && kr.OwnerUserId == userId);
      if (existing is null) { return entity; }
      existing.Title = entity.Title;
      existing.MetricUnit = entity.MetricUnit;
      existing.Baseline = entity.Baseline;
      existing.Target = entity.Target;
      existing.Current = entity.Current;
      existing.WeeklyNote = entity.WeeklyNote;
      existing.Deadline = entity.Deadline;
      existing.Status = entity.Status;
      existing.SortOrder = entity.SortOrder;
      existing.UpdatedAt = entity.UpdatedAt;
    }
    await db.SaveChangesAsync();
    return entity;
  }

  /// <inheritdoc />
  public async Task DeleteKeyResultAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    KeyResultEntity? entity = await db.KeyResults.FirstOrDefaultAsync(kr => kr.Id == id && kr.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  /// <inheritdoc />
  public async Task<List<PillarEntity>> GetFullOkrTreeAsync(string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.Pillars
      .Where(p => p.OwnerUserId == userId && !p.Deleted)
      .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
      .Include(p => p.Objectives.Where(o => !o.Deleted).OrderBy(o => o.SortOrder))
        .ThenInclude(o => o.KeyResults.Where(kr => !kr.Deleted).OrderBy(kr => kr.SortOrder))
      .ToListAsync();
  }

  // ── Quarter Plans ─────────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<QuarterPlanEntity>> GetQuarterPlansAsync(string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.QuarterPlans
      .Where(q => q.OwnerUserId == userId && !q.Deleted)
      .OrderByDescending(q => q.Year).ThenBy(q => q.Quarter)
      .ToListAsync();
  }

  /// <inheritdoc />
  public async Task<QuarterPlanEntity?> GetQuarterPlanWithTasksAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.QuarterPlans
      .Include(q => q.Tasks.Where(t => !t.Deleted).OrderBy(t => t.SortOrder))
      .FirstOrDefaultAsync(q => q.Id == id && q.OwnerUserId == userId && !q.Deleted);
  }

  /// <inheritdoc />
  public async Task<QuarterPlanEntity> SaveQuarterPlanAsync(QuarterPlanEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    entity.UpdatedAt = DateTime.UtcNow;
    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      db.QuarterPlans.Add(entity);
    }
    else
    {
      QuarterPlanEntity? existing = await db.QuarterPlans.FirstOrDefaultAsync(q => q.Id == entity.Id && q.OwnerUserId == userId);
      if (existing is null) { return entity; }
      existing.Year = entity.Year;
      existing.Quarter = entity.Quarter;
      existing.Focus = entity.Focus;
      existing.Delivery = entity.Delivery;
      existing.TargetDate = entity.TargetDate;
      existing.WeeklyCadence = entity.WeeklyCadence;
      existing.KpiDescription = entity.KpiDescription;
      existing.Status = entity.Status;
      existing.UpdatedAt = entity.UpdatedAt;
    }
    await db.SaveChangesAsync();
    return entity;
  }

  /// <inheritdoc />
  public async Task DeleteQuarterPlanAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    QuarterPlanEntity? entity = await db.QuarterPlans.FirstOrDefaultAsync(q => q.Id == id && q.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  /// <inheritdoc />
  public async Task<QuarterTaskEntity> SaveQuarterTaskAsync(QuarterTaskEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      db.QuarterTasks.Add(entity);
    }
    else
    {
      QuarterTaskEntity? existing = await db.QuarterTasks.FirstOrDefaultAsync(t => t.Id == entity.Id && t.OwnerUserId == userId);
      if (existing is null) { return entity; }
      existing.Text = entity.Text;
      existing.SortOrder = entity.SortOrder;
      existing.Done = entity.Done;
    }
    await db.SaveChangesAsync();
    return entity;
  }

  /// <inheritdoc />
  public async Task ToggleQuarterTaskAsync(int taskId, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    QuarterTaskEntity? entity = await db.QuarterTasks.FirstOrDefaultAsync(t => t.Id == taskId && t.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Done = !entity.Done;
    await db.SaveChangesAsync();
  }

  /// <inheritdoc />
  public async Task DeleteQuarterTaskAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    QuarterTaskEntity? entity = await db.QuarterTasks.FirstOrDefaultAsync(t => t.Id == id && t.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  // ── Review Templates ──────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<ReviewTemplateEntity>> GetReviewTemplatesAsync(string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.ReviewTemplates
      .Where(t => t.OwnerUserId == userId && !t.Deleted)
      .OrderBy(t => t.ReviewType).ThenBy(t => t.Name)
      .ToListAsync();
  }

  /// <inheritdoc />
  public async Task<ReviewTemplateEntity?> GetReviewTemplateWithItemsAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.ReviewTemplates
      .Include(t => t.Items.Where(i => !i.Deleted).OrderBy(i => i.SortOrder))
      .FirstOrDefaultAsync(t => t.Id == id && t.OwnerUserId == userId && !t.Deleted);
  }

  /// <inheritdoc />
  public async Task<ReviewTemplateEntity> SaveReviewTemplateAsync(ReviewTemplateEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    entity.UpdatedAt = DateTime.UtcNow;
    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      foreach (ReviewTemplateItemEntity item in entity.Items)
      {
        item.CreationDate = DateTime.UtcNow;
        item.RefId = Guid.NewGuid();
      }
      db.ReviewTemplates.Add(entity);
    }
    else
    {
      ReviewTemplateEntity? existing = await db.ReviewTemplates
        .Include(t => t.Items)
        .FirstOrDefaultAsync(t => t.Id == entity.Id && t.OwnerUserId == userId);
      if (existing is null) { return entity; }
      existing.Name = entity.Name;
      existing.ReviewType = entity.ReviewType;
      existing.IsDefault = entity.IsDefault;
      existing.UpdatedAt = entity.UpdatedAt;

      RebuildTemplateItems(db, existing, entity.Items);
    }

    await db.SaveChangesAsync();
    return entity;
  }

  /// <summary>
  /// Rebuilds the item collection of an existing review template by soft-deleting removed items
  /// and adding or updating the items present in the incoming list.
  /// </summary>
  /// <param name="db">Database context used to track new item entities.</param>
  /// <param name="existing">The persisted template whose item collection will be modified in place.</param>
  /// <param name="incomingItems">The new ordered list of items from the update request.</param>
  private static void RebuildTemplateItems(
    LifePlannerContext db,
    ReviewTemplateEntity existing,
    List<ReviewTemplateItemEntity> incomingItems)
  {
    HashSet<int> incomingIds = incomingItems.Where(i => i.Id != 0).Select(i => i.Id).ToHashSet();

    foreach (ReviewTemplateItemEntity old in existing.Items)
    {
      if (!incomingIds.Contains(old.Id))
      {
        old.Deleted = true;
      }
    }

    int sortOrder = 0;
    foreach (ReviewTemplateItemEntity incoming in incomingItems)
    {
      incoming.SortOrder = sortOrder++;

      if (incoming.Id == 0)
      {
        incoming.TemplateId = existing.Id;
        incoming.CreationDate = DateTime.UtcNow;
        incoming.RefId = Guid.NewGuid();
        db.ReviewTemplateItems.Add(incoming);
      }
      else
      {
        ReviewTemplateItemEntity? existingItem = existing.Items.FirstOrDefault(i => i.Id == incoming.Id);
        if (existingItem is not null)
        {
          existingItem.Text = incoming.Text;
          existingItem.SortOrder = incoming.SortOrder;
          existingItem.Deleted = false;
        }
      }
    }
  }

  /// <inheritdoc />
  public async Task DeleteReviewTemplateAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    ReviewTemplateEntity? entity = await db.ReviewTemplates.FirstOrDefaultAsync(t => t.Id == id && t.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  /// <inheritdoc />
  public async Task EnsureDefaultTemplatesAsync(string userId)
  {
    // The showcase tenant receives its templates from the demonstration seeder; never create on read.
    if (userId == LifePlannerContext.DemoUserId)
    {
      return;
    }

    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    bool hasWeekly = await db.ReviewTemplates.AnyAsync(t => t.OwnerUserId == userId && t.ReviewType == ReviewType.Weekly && !t.Deleted);
    bool hasMonthly = await db.ReviewTemplates.AnyAsync(t => t.OwnerUserId == userId && t.ReviewType == ReviewType.Monthly && !t.Deleted);

    DateTime now = DateTime.UtcNow;

    if (!hasWeekly)
    {
      ReviewTemplateEntity weekly = new ReviewTemplateEntity
      {
        OwnerUserId = userId, Name = "Revisao Semanal", ReviewType = ReviewType.Weekly,
        IsDefault = true, CreationDate = now, UpdatedAt = now,
        Items = new List<ReviewTemplateItemEntity>
        {
          new() { Text = "Revisao das metas da semana", SortOrder = 0, CreationDate = now },
          new() { Text = "Top 3 conquistas desta semana", SortOrder = 1, CreationDate = now },
          new() { Text = "O que nao funcionou? O que aprender?", SortOrder = 2, CreationDate = now },
          new() { Text = "Verificar progresso dos Key Results", SortOrder = 3, CreationDate = now },
          new() { Text = "Planejar blocos da proxima semana", SortOrder = 4, CreationDate = now },
          new() { Text = "Verificar habitos da semana", SortOrder = 5, CreationDate = now },
          new() { Text = "Energia, foco e bem-estar ok?", SortOrder = 6, CreationDate = now },
        }
      };
      db.ReviewTemplates.Add(weekly);
    }

    if (!hasMonthly)
    {
      ReviewTemplateEntity monthly = new ReviewTemplateEntity
      {
        OwnerUserId = userId, Name = "Revisao Mensal", ReviewType = ReviewType.Monthly,
        IsDefault = true, CreationDate = now, UpdatedAt = now,
        Items = new List<ReviewTemplateItemEntity>
        {
          new() { Text = "Avaliacao geral do mes (RAG em cada pilar)", SortOrder = 0, CreationDate = now },
          new() { Text = "Progresso dos OKRs: atualizar Current de cada KR", SortOrder = 1, CreationDate = now },
          new() { Text = "Revisao do cronograma trimestral", SortOrder = 2, CreationDate = now },
          new() { Text = "Habitos: % mensal e streak", SortOrder = 3, CreationDate = now },
          new() { Text = "Pre-mortem: algum risco se materializou?", SortOrder = 4, CreationDate = now },
          new() { Text = "Financas: receita x despesa x alocacoes", SortOrder = 5, CreationDate = now },
          new() { Text = "Planejamento do proximo mes", SortOrder = 6, CreationDate = now },
          new() { Text = "Uma acao concreta para o trimestre com base nesta revisao", SortOrder = 7, CreationDate = now },
        }
      };
      db.ReviewTemplates.Add(monthly);
    }

    await db.SaveChangesAsync();
  }

  // ── Review Runs ───────────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<ReviewRunEntity>> GetReviewRunsAsync(string userId, ReviewType? type = null, DateTime? from = null, DateTime? to = null)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    IQueryable<ReviewRunEntity> q = db.ReviewRuns
      .Include(r => r.Template)
      .Where(r => r.OwnerUserId == userId && !r.Deleted);
    if (type.HasValue) { q = q.Where(r => r.ReviewType == type.Value); }
    if (from.HasValue) { q = q.Where(r => r.RunDate >= from.Value); }
    if (to.HasValue) { q = q.Where(r => r.RunDate <= to.Value); }
    return await q.OrderByDescending(r => r.RunDate).ToListAsync();
  }

  /// <inheritdoc />
  public async Task<ReviewRunEntity?> GetReviewRunWithItemsAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.ReviewRuns
      .Include(r => r.Template)
      .Include(r => r.Items.OrderBy(i => i.SortOrder))
      .FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == userId && !r.Deleted);
  }

  /// <inheritdoc />
  public async Task<ReviewRunEntity> StartReviewRunAsync(int templateId, DateTime runDate, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    ReviewTemplateEntity? template = await db.ReviewTemplates
      .Include(t => t.Items.Where(i => !i.Deleted).OrderBy(i => i.SortOrder))
      .FirstOrDefaultAsync(t => t.Id == templateId && t.OwnerUserId == userId && !t.Deleted);

    if (template is null) { throw new InvalidOperationException("Template not found."); }

    DateTime now = DateTime.UtcNow;
    ReviewRunEntity run = new ReviewRunEntity
    {
      OwnerUserId = userId,
      TemplateId = templateId,
      ReviewType = template.ReviewType,
      RunDate = runDate.Date,
      CreationDate = now,
      Items = template.Items.Select((item, idx) => new ReviewRunItemEntity
      {
        TemplateItemId = item.Id,
        Text = item.Text,
        SortOrder = idx,
        Done = false,
        CreationDate = now
      }).ToList()
    };
    db.ReviewRuns.Add(run);
    await db.SaveChangesAsync();
    return run;
  }

  /// <inheritdoc />
  public async Task SaveReviewRunAsync(ReviewRunEntity run, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    ReviewRunEntity? existing = await db.ReviewRuns
      .Include(r => r.Items)
      .FirstOrDefaultAsync(r => r.Id == run.Id && r.OwnerUserId == userId && !r.Deleted);
    if (existing is null) { return; }

    existing.Notes = run.Notes;
    existing.ScoreEnergy = run.ScoreEnergy;
    existing.ScoreFocus = run.ScoreFocus;
    existing.ScoreFinances = run.ScoreFinances;
    existing.ScoreRelations = run.ScoreRelations;

    foreach (ReviewRunItemEntity incomingItem in run.Items)
    {
      ReviewRunItemEntity? existingItem = existing.Items.FirstOrDefault(i => i.Id == incomingItem.Id);
      if (existingItem is not null) { existingItem.Done = incomingItem.Done; }
    }
    await db.SaveChangesAsync();
  }

  /// <inheritdoc />
  public async Task DeleteReviewRunAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    ReviewRunEntity? entity = await db.ReviewRuns.FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  // ── Risks ──────────────────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<RiskEntity>> GetRisksAsync(string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.Risks
      .Include(r => r.Pillar)
      .Where(r => r.OwnerUserId == userId && !r.Deleted)
      .OrderByDescending(r => r.Impact).ThenByDescending(r => r.Probability)
      .ToListAsync();
  }

  /// <inheritdoc />
  public async Task<RiskEntity> SaveRiskAsync(RiskEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    entity.UpdatedAt = DateTime.UtcNow;
    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      db.Risks.Add(entity);
    }
    else
    {
      RiskEntity? existing = await db.Risks.FirstOrDefaultAsync(r => r.Id == entity.Id && r.OwnerUserId == userId);
      if (existing is null) { return entity; }
      existing.Title = entity.Title;
      existing.Description = entity.Description;
      existing.Probability = entity.Probability;
      existing.Impact = entity.Impact;
      existing.EarlyWarning = entity.EarlyWarning;
      existing.Contingency = entity.Contingency;
      existing.PlanB = entity.PlanB;
      existing.PillarId = entity.PillarId;
      existing.Status = entity.Status;
      existing.UpdatedAt = entity.UpdatedAt;
    }
    await db.SaveChangesAsync();
    return entity;
  }

  /// <inheritdoc />
  public async Task DeleteRiskAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    RiskEntity? entity = await db.Risks.FirstOrDefaultAsync(r => r.Id == id && r.OwnerUserId == userId);
    if (entity is null) { return; }
    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  // ── Habits ─────────────────────────────────────────────────────────────────

  /// <inheritdoc />
  public async Task<List<HabitEntity>> GetHabitsAsync(string userId, bool includeInactive = false)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    IQueryable<HabitEntity> q = db.Habits
      .Include(h => h.Pillar)
      .Where(h => h.OwnerUserId == userId && !h.Deleted);
    if (!includeInactive) { q = q.Where(h => h.IsActive); }
    return await q.OrderBy(h => h.Name).ToListAsync();
  }

  /// <inheritdoc />
  public async Task<HabitEntity> SaveHabitAsync(HabitEntity entity, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    entity.OwnerUserId = userId;
    entity.UpdatedAt = DateTime.UtcNow;

    if (entity.Id == 0)
    {
      entity.CreationDate = DateTime.UtcNow;
      db.Habits.Add(entity);
      await db.SaveChangesAsync();
    }
    else
    {
      HabitEntity? existing = await db.Habits
        .FirstOrDefaultAsync(h => h.Id == entity.Id && h.OwnerUserId == userId);
      if (existing is null)
      {
        return entity;
      }

      existing.Name = entity.Name;
      existing.Description = entity.Description;
      existing.ColorHex = entity.ColorHex;
      existing.IconCss = entity.IconCss;
      existing.PillarId = entity.PillarId;
      existing.IsActive = entity.IsActive;
      existing.FrequencyDays = entity.FrequencyDays;
      existing.UpdatedAt = entity.UpdatedAt;

      await db.SaveChangesAsync();
    }

    return entity;
  }

  /// <inheritdoc />
  public async Task DeleteHabitAsync(int id, string userId)
  {
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    HabitEntity? entity = await db.Habits.FirstOrDefaultAsync(h => h.Id == id && h.OwnerUserId == userId);
    if (entity is null)
    {
      return;
    }

    entity.Deleted = true;
    await db.SaveChangesAsync();
  }

  /// <inheritdoc />
  public async Task<List<HabitLogEntity>> GetHabitLogsAsync(string userId, int year, int month)
  {
    DateTime start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    DateTime end = start.AddMonths(1);
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    return await db.HabitLogs
      .Where(hl => hl.OwnerUserId == userId && hl.LogDate >= start && hl.LogDate < end)
      .ToListAsync();
  }

  /// <inheritdoc />
  public async Task<HabitLogEntity> ToggleHabitLogAsync(int habitId, DateTime logDate, string userId)
  {
    DateTime dateOnly = logDate.Date.ToUniversalTime();
    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    HabitLogEntity? existing = await db.HabitLogs
      .FirstOrDefaultAsync(hl => hl.HabitId == habitId && hl.OwnerUserId == userId && hl.LogDate == dateOnly);

    if (existing is not null)
    {
      existing.Done = !existing.Done;
    }
    else
    {
      existing = new HabitLogEntity
      {
        OwnerUserId = userId, HabitId = habitId, LogDate = dateOnly,
        Done = true, CreationDate = DateTime.UtcNow
      };
      db.HabitLogs.Add(existing);
    }
    await db.SaveChangesAsync();
    return existing;
  }

  /// <inheritdoc />
  public async Task<HabitMetrics> GetHabitMetricsAsync(int habitId, string userId)
  {
    DateTime today = DateTime.UtcNow.Date;

    // Weekly window (Mon–Sun ISO week)
    int daysSinceMon = ((int)today.DayOfWeek + 6) % 7;
    DateTime weekStart = today.AddDays(-daysSinceMon);
    DateTime weekEnd = weekStart.AddDays(7);

    // Monthly window
    DateTime monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    DateTime monthEnd = monthStart.AddMonths(1);

    await using LifePlannerContext db = await contextFactory.CreateDbContextAsync();
    List<HabitLogEntity> logs = await db.HabitLogs
      .Where(hl => hl.HabitId == habitId && hl.OwnerUserId == userId && hl.LogDate >= monthStart)
      .ToListAsync();

    // Weekly %
    int weekDays = (int)(weekEnd > today.AddDays(1) ? today.AddDays(1) : weekEnd).Subtract(weekStart).TotalDays;
    int weekDone = logs.Count(l => l.Done && l.LogDate >= weekStart && l.LogDate < weekEnd);
    double weekPct = weekDays > 0 ? Math.Round(weekDone * 100.0 / weekDays, 1) : 0;

    // Monthly %
    int monthDays = (int)Math.Min((today - monthStart).TotalDays + 1, (monthEnd - monthStart).TotalDays);
    int monthDone = logs.Count(l => l.Done && l.LogDate >= monthStart && l.LogDate < monthEnd);
    double monthPct = monthDays > 0 ? Math.Round(monthDone * 100.0 / monthDays, 1) : 0;

    // Streak: count consecutive done days ending today
    // Load full history for streak (up to 365 days)
    DateTime streakFrom = today.AddDays(-365);
    List<DateTime> doneDates = await db.HabitLogs
      .Where(hl => hl.HabitId == habitId && hl.OwnerUserId == userId && hl.Done && hl.LogDate >= streakFrom)
      .Select(hl => hl.LogDate)
      .ToListAsync();
    HashSet<DateTime> doneSet = doneDates.ToHashSet();

    int streak = 0;
    DateTime cursor = today;
    while (doneSet.Contains(cursor))
    {
      streak++;
      cursor = cursor.AddDays(-1);
    }

    return new HabitMetrics { HabitId = habitId, WeeklyPercent = weekPct, MonthlyPercent = monthPct, CurrentStreak = streak };
  }
}

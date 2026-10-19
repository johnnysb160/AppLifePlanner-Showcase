//-----------------------------------------------------------------------
// <copyright file="HabitEntity.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Entities
{
  /// <summary>
  /// Represents a daily habit tracked as a 0/1 (done / not done) each day.
  /// </summary>
  public sealed class HabitEntity : BaseEntity
  {
    /// <summary>Gets or sets the owning user identifier.</summary>
    public string OwnerUserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the habit name (e.g. "Meditar 10 min").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets an optional short description or motivation note.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the hex colour used to represent this habit in the grid.</summary>
    public string ColorHex { get; set; } = "#198754";

    /// <summary>Gets or sets an optional Bootstrap-Icons CSS class.</summary>
    public string? IconCss { get; set; }

    /// <summary>Gets or sets an optional link to a pillar (for OKR alignment).</summary>
    public int? PillarId { get; set; }

    /// <summary>Navigation property to the associated pillar.</summary>
    public PillarEntity? Pillar { get; set; }

    /// <summary>Gets or sets a value indicating whether this habit is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the scheduled days of the week for this habit as comma-separated
    /// <see cref="System.DayOfWeek"/> integer values (0 = Sunday … 6 = Saturday).
    /// Example: "1,3,5" = Monday, Wednesday, Friday.
    /// </summary>
    public string? FrequencyDays { get; set; }

    /// <summary>Gets or sets the UTC timestamp of the last update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property — daily log entries for this habit.</summary>
    public List<HabitLogEntity> Logs { get; set; } = new();
  }
}

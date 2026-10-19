//-----------------------------------------------------------------------
// <copyright file="IAppSettingRepository.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;

using AppControlOrganizer.V2.Shared.Entities;

/// <summary>
/// Repository for application key/value settings.
/// </summary>
public interface IAppSettingRepository
{
  /// <summary>
  /// Retrieves an app setting by its key.
  /// </summary>
  Task<AppSetting?> SelectAsync(string key, CancellationToken cancellationToken = default);

  /// <summary>
  /// Inserts a new app setting.
  /// </summary>
  Task<AppSetting> InsertAsync(AppSetting item);

  /// <summary>
  /// Updates an existing app setting.
  /// </summary>
  Task<AppSetting> UpdateAsync(AppSetting item);
}

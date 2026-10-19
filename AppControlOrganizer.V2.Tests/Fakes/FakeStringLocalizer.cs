//-----------------------------------------------------------------------
// <copyright file="FakeStringLocalizer.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Tests.Fakes;

using System.Collections;
using Microsoft.Extensions.Localization;

/// <summary>
/// String localizer that returns keys unchanged.
/// </summary>
/// <typeparam name="T">The resource type.</typeparam>
internal sealed class FakeStringLocalizer<T> : IStringLocalizer<T>
{
  /// <inheritdoc />
  public LocalizedString this[string name]
  {
    get
    {
      return new LocalizedString(name, name, resourceNotFound: true);
    }
  }

  /// <inheritdoc />
  public LocalizedString this[string name, params object[] arguments]
  {
    get
    {
      return new LocalizedString(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments), resourceNotFound: true);
    }
  }

  /// <inheritdoc />
  public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
  {
    return Enumerable.Empty<LocalizedString>();
  }
}

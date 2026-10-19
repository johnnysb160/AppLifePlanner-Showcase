//-----------------------------------------------------------------------
// <copyright file="DropdownItemExtensions.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace AppControlOrganizer.V2.Web.Components.Shared.Common;

/// <summary>
/// Extension methods to convert enum collections to dropdown item arrays.
/// </summary>
public static class DropdownItemExtensions
{
    /// <summary>
    /// Converts the provided enum values into an array of <see cref="DropdownItem{TEnum}"/> using the supplied text factory.
    /// </summary>
    /// <typeparam name="TEnum">The enum type.</typeparam>
    /// <param name="values">The enum values to convert.</param>
    /// <param name="textFactory">Function that produces the display text for each enum value.</param>
    /// <returns>An array of dropdown items for the provided enum values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> or <paramref name="textFactory"/> is null.</exception>
    public static DropdownItem<TEnum>[] ToDropdownItemsArray<TEnum>(this IEnumerable<TEnum> values, Func<TEnum, string> textFactory)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (textFactory == null)
        {
            throw new ArgumentNullException(nameof(textFactory));
        }

        return values.Select(v => new DropdownItem<TEnum>(v, textFactory(v))).ToArray();
    }

    /// <summary>
    /// Converts the provided enum values into an array of nullable <see cref="DropdownItem{TEnum?}"/> using the supplied text factory.
    /// </summary>
    /// <typeparam name="TEnum">The enum value type (struct).</typeparam>
    /// <param name="values">The enum values to convert.</param>
    /// <param name="textFactory">Function that produces the display text for each enum value.</param>
    /// <returns>An array of nullable dropdown items for the provided enum values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> or <paramref name="textFactory"/> is null.</exception>
    public static DropdownItem<TEnum?>[] ToNullableDropdownItemsArray<TEnum>(this IEnumerable<TEnum> values, Func<TEnum, string> textFactory)
        where TEnum : struct
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        if (textFactory == null)
        {
            throw new ArgumentNullException(nameof(textFactory));
        }

        return values.Select(v => new DropdownItem<TEnum?>((TEnum?)v, textFactory(v))).ToArray();
    }
}

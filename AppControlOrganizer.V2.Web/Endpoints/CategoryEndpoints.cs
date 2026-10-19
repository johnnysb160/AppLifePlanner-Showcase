//-----------------------------------------------------------------------
// <copyright file="CategoryEndpoints.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Endpoints;

using AppControlOrganizer.V2.Shared.DTOs;
using AppControlOrganizer.V2.Shared.Entities;
using AppControlOrganizer.V2.Web.Data.Repositories.Interfaces;
using AppControlOrganizer.V2.Web.Services;
public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        // Categories are shared across every tenant, so the whole group requires an authenticated
        // caller: an anonymous (demonstration) session must never mutate data seen by real profiles.
        RouteGroupBuilder group = app.MapGroup("/api/categories")
          .WithTags("Categories")
          .RequireAuthorization()
          .RequireRateLimiting("api");

        group.MapGet("/", GetAllCategories).WithName("GetAllCategories").WithOpenApi();

        group.MapGet("/{id:int}", GetCategoryById).WithName("GetCategoryById").WithOpenApi();

        group.MapGet("/predefined", GetPredefinedCategories).WithName("GetPredefinedCategories").WithOpenApi();

        group.MapGet("/income", GetIncomeCategories).WithName("GetIncomeCategories").WithOpenApi();

        group.MapGet("/by-code/{code}", GetCategoryByCode).WithName("GetCategoryByCode").WithOpenApi();

        group.MapPost("/CreateCategory", CreateCategory).WithName("CreateCategory").WithOpenApi();

        group.MapPut("/UpdateCategory/{id:int}", UpdateCategory).WithName("UpdateCategory").WithOpenApi();

        group.MapDelete("/DeleteCategory/{id:int}", DeleteCategory).WithName("DeleteCategory").WithOpenApi();
    }

    private static async Task<IResult> GetAllCategories(CategoryService categoryService)
    {
        List<CategoryDisplayDto> list = await categoryService.GetAllAsync();
        return Results.Ok(list);
    }

    private static async Task<IResult> GetCategoryById(int id, ICategoryRepository categoryRepository)
    {
        CategoryEntity? entity = await categoryRepository.SelectAsync(id);
        return EndpointHelper.OkOrNotFound(entity, "Category not found");
    }

    private static async Task<IResult> GetPredefinedCategories(ICategoryRepository categoryRepository)
    {
        List<CategoryEntity>? list = await categoryRepository.SelectPredefinedAsync();
        return Results.Ok(list ?? new List<CategoryEntity>());
    }

    private static async Task<IResult> GetIncomeCategories(ICategoryRepository categoryRepository)
    {
        List<CategoryEntity>? list = await categoryRepository.SelectIncomeCategoriesAsync();
        return Results.Ok(list ?? new List<CategoryEntity>());
    }

    private static async Task<IResult> GetCategoryByCode(string code, ICategoryRepository categoryRepository)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Results.BadRequest(new { message = "Invalid code" });
        }

        CategoryEntity? entity = await categoryRepository.SelectByCodeAsync(code);
        return EndpointHelper.OkOrNotFound(entity, "Category not found");
    }

    private static async Task<IResult> CreateCategory(CategoryDto dto, CategoryService categoryService)
    {
        try
        {
            CategoryDisplayDto created = await categoryService.CreateAsync(dto);
            return Results.Created($"/api/categories/{created.Id}", created);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> UpdateCategory(int id, CategoryDto dto, CategoryService categoryService)
    {
        try
        {
            CategoryDisplayDto updated = await categoryService.UpdateAsync(id, dto);
            return Results.Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message?.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Results.NotFound(new { message = ex.Message });
            }

            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> DeleteCategory(int id, CategoryService categoryService)
    {
        try
        {
            await categoryService.DeleteAsync(id);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message?.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Results.NotFound(new { message = ex.Message });
            }

            return Results.BadRequest(new { message = ex.Message });
        }
    }
}

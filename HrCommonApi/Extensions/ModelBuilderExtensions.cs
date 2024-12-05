﻿using HrCommonApi.Database.Models.Base;
using HrCommonApi.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace HrCommonApi.Extensions;

/// <summary>
/// My advice is to not look at this extension in any way or shape.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds the entity models to the ModelBuilder.
    /// </summary>
    public static ModelBuilder AddEntityModels(this ModelBuilder modelBuilder, IConfiguration configuration)
    {
        var targetNamespace = configuration?["HrCommonApi:Namespaces:Models"] ?? null;
        if (string.IsNullOrEmpty(targetNamespace))
            throw new InvalidOperationException("The target namespace for the Models is not set. Expected configuration key: \"HrCommonApi:Namespaces:Models\"");

        modelBuilder.AddEntityModelsFromNamespace(targetNamespace);

        // Add this libraries models
        modelBuilder.AddEntityModelsFromNamespace("HrCommonApi.Database.Models");

        return modelBuilder;
    }

    /// <summary>
    /// Adds the entity models in the target namespace to the ModelBuilder.
    /// </summary>
    public static ModelBuilder AddEntityModelsFromNamespace(this ModelBuilder modelBuilder, string targetNamespace)
    {
        foreach (var entityType in ReflectionUtils.GetTypesInNamespaceImplementing<DbEntity>(Assembly.GetExecutingAssembly(), targetNamespace))
        {
            if (typeof(IMappedEntity).IsAssignableFrom(entityType))
            {
                entityType.GetMethod(nameof(IMappedEntity.MapEntity), BindingFlags.Static | BindingFlags.Public)!.Invoke(null, [modelBuilder]);
            }
        }

        return modelBuilder;
    }
}
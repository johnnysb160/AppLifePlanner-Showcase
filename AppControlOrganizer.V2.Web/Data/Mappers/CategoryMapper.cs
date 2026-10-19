//-----------------------------------------------------------------------
// <copyright file="CategoryMapper.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Web.Data.Mappers;

using AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Maps invoice categories from CSV to system Category enum.
/// </summary>
public static class CategoryMapper
{
  // Card invoice category keywords (category column from CSV)
  private static readonly Dictionary<string, Category> CardCategoryMappings = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
  {
    // Food
    { "restaurante", Category.Food },
    { "supermercado", Category.Food },
    { "mercearia", Category.Food },
    { "padaria", Category.Food },
    { "lanchonete", Category.Food },
    { "bar", Category.Food },
    { "conveniência", Category.Food },
    { "conveniencia", Category.Food },
    { "alimentação", Category.Food },
    { "alimentacao", Category.Food },

    // Internet/Telecom
    { "telecomunicações", Category.Internet },
    { "telecomunicacoes", Category.Internet },
    { "telecom", Category.Internet },

    // Health
    { "assistência médica", Category.Health },
    { "assistencia medica", Category.Health },
    { "odontológica", Category.Health },
    { "odontologica", Category.Health },
    { "drogaria", Category.Health },
    { "farmácia", Category.Health },
    { "farmacia", Category.Health },
    { "saúde", Category.Health },
    { "saude", Category.Health },

    // Vehicles
    { "automotivo", Category.Vehicles },
    { "ituran", Category.Vehicles },
    { "combustível", Category.Vehicles },
    { "combustivel", Category.Vehicles },
    { "estacionamento", Category.Vehicles },
    { "pedágio", Category.Vehicles },
    { "pedagio", Category.Vehicles },

    // Leisure
    { "companhia aérea", Category.Leisure },
    { "companhia aerea", Category.Leisure },
    { "smiles", Category.Leisure },
    { "viagem", Category.Leisure },
    { "lazer", Category.Leisure },
    { "entretenimento", Category.Leisure },
    { "hospedagem", Category.Leisure },

    // Company costs
    { "serviços profissionais", Category.CompanyCosts },
    { "servicos profissionais", Category.CompanyCosts },
    { "marketing direto", Category.CompanyCosts },
    { "contabilidade", Category.CompanyCosts },
    { "assinatura", Category.CompanyCosts },
    { "software", Category.CompanyCosts },

    // Utilities (Light)
    { "energia", Category.Light },
    { "água", Category.Light },
    { "agua", Category.Light },
    { "gás", Category.Light },
    { "gas", Category.Light },
    { "saneamento", Category.Light },

    // Home
    { "lojas de materiais", Category.Home },
    { "móveis", Category.Home },
    { "moveis", Category.Home },
    { "decoração", Category.Home },
    { "decoracao", Category.Home }
  };

  // Generic issuer categories applied only after description keywords fail: the issuer files unrelated
  // merchants under them (e.g. travel agencies under "Transporte"), so they must not override a keyword match.
  private static readonly Dictionary<string, Category> CardGenericCategoryMappings = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase)
  {
    { "transporte", Category.Vehicles }
  };

  // Card invoice description keywords for fallback matching
  private static readonly (string Keyword, Category Category)[] CardDescriptionMappings =
  {
    // Food
    ("CARREFOUR", Category.Food), ("COOP", Category.Food), ("PAO DE ACUCAR", Category.Food),
    ("EXTRA", Category.Food), ("IFOOD", Category.Food), ("RAPPI", Category.Food), ("UBER EATS", Category.Food),
    ("MCDONALDS", Category.Food), ("BURGER KING", Category.Food), ("SUBWAY", Category.Food),
    ("STARBUCKS", Category.Food), ("CHURRASCARI", Category.Food), ("PIZZARIA", Category.Food),
    ("PADARIA", Category.Food), ("MERCADO", Category.Food),

    // Internet/Telecom
    ("VIVO", Category.Internet), ("CLARO", Category.Internet), ("TIM", Category.Internet),
    ("OI ", Category.Internet), ("NET ", Category.Internet), ("NETFLIX", Category.Internet),
    ("SPOTIFY", Category.Internet), ("AMAZON PRIME", Category.Internet), ("DISNEY", Category.Internet),
    ("HBO", Category.Internet),

    // Health
    ("DROGASIL", Category.Health), ("DROGARIA", Category.Health), ("FARMACIA", Category.Health),
    ("HOSPITAL", Category.Health), ("CLINICA", Category.Health), ("LABORATORIO", Category.Health),
    ("UNIMED", Category.Health), ("AMIL", Category.Health), ("SULAMERICA", Category.Health),

    // Vehicles
    ("ITURAN", Category.Vehicles), ("POSTO", Category.Vehicles), ("SHELL", Category.Vehicles),
    ("IPIRANGA", Category.Vehicles), ("PETROBRAS", Category.Vehicles), ("UBER ", Category.Vehicles),
    ("99 ", Category.Vehicles), ("ESTAPAR", Category.Vehicles), ("SEM PARAR", Category.Vehicles),
    ("CONECTCAR", Category.Vehicles),

    // Leisure
    ("SMILES", Category.Leisure), ("LATAM", Category.Leisure), ("GOL ", Category.Leisure),
    ("AZUL ", Category.Leisure), ("BOOKING", Category.Leisure), ("AIRBNB", Category.Leisure),
    ("HOTEL", Category.Leisure), ("CINEMA", Category.Leisure), ("TEATRO", Category.Leisure),
    ("PARQUE", Category.Leisure),

    // Company costs
    ("GITHUB", Category.CompanyCosts), ("MICROSOFT", Category.CompanyCosts), ("GOOGLE", Category.CompanyCosts),
    ("AWS", Category.CompanyCosts), ("AZURE", Category.CompanyCosts), ("DIGITAL OCEAN", Category.CompanyCosts),
    ("HEROKU", Category.CompanyCosts), ("CONTABIL", Category.CompanyCosts),

    // Utilities (Light)
    ("ENEL", Category.Light), ("SABESP", Category.Light), ("CPFL", Category.Light), ("CEMIG", Category.Light),
    ("COMGAS", Category.Light), ("ELETROPAULO", Category.Light), ("COPEL", Category.Light),
    ("LIGHT ", Category.Light),

    // Home
    ("LEROY MERLIN", Category.Home), ("TELHA NORTE", Category.Home), ("CASA SHOW", Category.Home),
    ("ETNA", Category.Home), ("TOK STOK", Category.Home)
  };

  // Bank statement description keywords
  private static readonly (string Keyword, Category Category, bool? IsIncome)[] BankDescriptionMappings =
  {
    // Income
    ("SALARIO", Category.Salary, true), ("SALARY", Category.Salary, true), ("PAYROLL", Category.Salary, true),
    ("FOLHA", Category.Salary, true), ("PROLABORE", Category.Salary, true),

    // Utilities (Light) - expenses
    ("ENEL", Category.Light, false), ("SABESP", Category.Light, false), ("CPFL", Category.Light, false),
    ("CEMIG", Category.Light, false), ("COMGAS", Category.Light, false), ("ELETROPAULO", Category.Light, false),
    ("COPEL", Category.Light, false),

    // Food - expenses
    ("CARREFOUR", Category.Food, false), ("COOP", Category.Food, false), ("PAO DE ACUCAR", Category.Food, false),
    ("EXTRA HIPER", Category.Food, false), ("IFOOD", Category.Food, false), ("RAPPI", Category.Food, false),
    ("UBER EATS", Category.Food, false), ("SUPERMERCADO", Category.Food, false), ("MERCADO", Category.Food, false),

    // Internet - expenses
    ("VIVO", Category.Internet, false), ("CLARO", Category.Internet, false), ("TIM", Category.Internet, false),
    ("NETFLIX", Category.Internet, false), ("SPOTIFY", Category.Internet, false),
    ("AMAZON PRIME", Category.Internet, false),

    // Health - expenses
    ("DROGASIL", Category.Health, false), ("DROGARIA", Category.Health, false),
    ("FARMACIA", Category.Health, false), ("HOSPITAL", Category.Health, false), ("UNIMED", Category.Health, false),
    ("AMIL", Category.Health, false),

    // Vehicles - expenses
    ("POSTO", Category.Vehicles, false), ("SHELL", Category.Vehicles, false),
    ("IPIRANGA", Category.Vehicles, false), ("UBER ", Category.Vehicles, false),
    ("SEM PARAR", Category.Vehicles, false), ("ITURAN", Category.Vehicles, false),

    // Company costs - expenses
    ("GITHUB", Category.CompanyCosts, false), ("MICROSOFT", Category.CompanyCosts, false),
    ("GOOGLE CLOUD", Category.CompanyCosts, false), ("AWS ", Category.CompanyCosts, false),
    ("CONTABIL", Category.CompanyCosts, false),

    // Leisure - expenses
    ("SMILES", Category.Leisure, false), ("LATAM", Category.Leisure, false), ("GOL ", Category.Leisure, false),
    ("AZUL ", Category.Leisure, false), ("BOOKING", Category.Leisure, false),

    // Home - expenses
    ("LEROY MERLIN", Category.Home, false), ("TELHA NORTE", Category.Home, false)
  };

  /// <summary>
  /// Maps card invoice category and description to the corresponding Category enum.
  /// </summary>
  public static Category MapFromInvoiceCategory(string categoryText, string description = "")
  {
    // Try category text first
    if (!string.IsNullOrWhiteSpace(categoryText))
    {
      string normalizedCategory = categoryText.Trim().ToLowerInvariant();

      foreach (KeyValuePair<string, Category> mapping in CardCategoryMappings)
      {
        if (normalizedCategory.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
        {
          return mapping.Value;
        }
      }
    }

    // Try description keywords for better categorization
    if (!string.IsNullOrWhiteSpace(description))
    {
      string upperDesc = description.ToUpperInvariant();

      foreach ((string Keyword, Category Category) mapping in CardDescriptionMappings)
      {
        if (upperDesc.Contains(mapping.Keyword, StringComparison.OrdinalIgnoreCase))
        {
          return mapping.Category;
        }
      }
    }

    if (!string.IsNullOrWhiteSpace(categoryText))
    {
      string normalizedGenericCategory = categoryText.Trim().ToLowerInvariant();

      foreach (KeyValuePair<string, Category> mapping in CardGenericCategoryMappings)
      {
        if (normalizedGenericCategory.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
        {
          return mapping.Value;
        }
      }
    }

    return Category.ExtraExpenses;
  }

  /// <summary>
  /// Legacy method for backward compatibility - uses only category text.
  /// </summary>
  public static Category MapFromInvoiceCategory(string categoryText)
  {
    return MapFromInvoiceCategory(categoryText, string.Empty);
  }

  /// <summary>
  /// Returns category for bank statement entries based on description and income flag.
  /// </summary>
  public static Category GetBankStatementCategory(string description, bool isIncome)
  {
    if (!string.IsNullOrWhiteSpace(description))
    {
      string upperDesc = description.ToUpperInvariant();

      foreach ((string Keyword, Category Category, bool? IsIncome) mapping in BankDescriptionMappings)
      {
        // Check if keyword matches
        if (!upperDesc.Contains(mapping.Keyword, StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        // If mapping has income constraint, check it matches
        if (mapping.IsIncome.HasValue && mapping.IsIncome.Value != isIncome)
        {
          continue;
        }

        return mapping.Category;
      }
    }

    // Default fallback
    return isIncome ? Category.OthersIncome : Category.ExtraExpenses;
  }

  /// <summary>
  /// Returns default category for bank statement entries (legacy).
  /// </summary>
  public static Category GetDefaultBankStatementCategory(bool isIncome)
  {
    return isIncome ? Category.OthersIncome : Category.ExtraExpenses;
  }
}

//-----------------------------------------------------------------------
// <copyright file="AiProviderKind.cs" company="AppLifePlanner">
// Copyright (c) App Life Planner.
// </copyright>
//-----------------------------------------------------------------------

namespace AppControlOrganizer.V2.Shared.Enums;

/// <summary>
/// Identifies the AI provider backend used to serve language model requests.
/// </summary>
public enum AiProviderKind
{
  /// <summary>Any provider that exposes an OpenAI-compatible REST API (e.g. LM Studio, Ollama, Together.ai).</summary>
  OpenAICompatible = 0,

  /// <summary>OpenAI hosted API (api.openai.com).</summary>
  OpenAI = 1,

  /// <summary>Anthropic hosted API (api.anthropic.com).</summary>
  Anthropic = 2,

  /// <summary>Google Gemini hosted API.</summary>
  Gemini = 3,

  /// <summary>Locally hosted model (e.g. llama.cpp server on localhost).</summary>
  Local = 4,
}

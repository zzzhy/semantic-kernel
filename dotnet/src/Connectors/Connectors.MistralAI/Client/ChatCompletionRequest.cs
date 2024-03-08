﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel.Connectors.MistralAI.Client;

/// <summary>
/// Request for chat completion.
/// </summary>
internal sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; }

    [JsonPropertyName("messages")]
    public IList<MistralChatMessage> Messages { get; set; } = new List<MistralChatMessage>();

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; } = 0.7;

    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 1;

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = false;

    [JsonPropertyName("safe_prompt")]
    public bool SafePrompt { get; set; } = false;

    [JsonPropertyName("random_seed")]
    public int? RandomSeed { get; set; }

    /// <summary>
    /// Construct an instance of <see cref="ChatCompletionRequest"/>.
    /// </summary>
    /// <param name="model">ID of the model to use.</param>
    internal ChatCompletionRequest(string model)
    {
        this.Model = model;
    }
}
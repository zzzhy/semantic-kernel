﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Http;

namespace Microsoft.SemanticKernel.Connectors.MistralAI.Client;

/// <summary>
/// The Mistral client.
/// </summary>
internal sealed class MistralClient
{
    internal MistralClient(
        string modelId,
        HttpClient httpClient,
        string apiKey,
        Uri? endpoint = null,
        ILogger? logger = null)
    {
        Verify.NotNullOrWhiteSpace(modelId);
        Verify.NotNullOrWhiteSpace(apiKey);
        Verify.NotNull(httpClient);

        endpoint ??= new Uri("https://api.mistral.ai/v1");
        this._separator = endpoint.AbsolutePath.EndsWith("/", StringComparison.InvariantCulture) ? string.Empty : "/";
        this._endpoint = endpoint;
        this._modelId = modelId;
        this._apiKey = apiKey;
        this._httpClient = httpClient;
        this._logger = logger ?? NullLogger.Instance;
        this._streamJsonParser = new ChatCompletionStreamJsonParser();
    }

    internal async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, CancellationToken cancellationToken, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null)
    {
        string modelId = executionSettings?.ModelId ?? this._modelId;
        var mistralExecutionSettings = MistralAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
        var request = this.CreateChatCompletionRequest(modelId, false, chatHistory, mistralExecutionSettings);
        var endpoint = new Uri($"{this._endpoint}{this._separator}chat/completions");

        using var httpRequestMessage = this.CreatePost(request, endpoint, this._apiKey, stream: false);

        var response = await this.SendRequestAsync<ChatCompletionResponse>(httpRequestMessage, cancellationToken).ConfigureAwait(false);

        return response.Choices.Select(chatChoice => new ChatMessageContent(new AuthorRole(chatChoice.Message!.Role), chatChoice.Message!.Content, this._modelId, chatChoice, Encoding.UTF8, GetChatChoiceMetadata(response, chatChoice))).ToList();
    }

    internal async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, CancellationToken cancellationToken, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null)
    {
        string modelId = executionSettings?.ModelId ?? this._modelId;
        var mistralExecutionSettings = MistralAIPromptExecutionSettings.FromExecutionSettings(executionSettings);
        var request = this.CreateChatCompletionRequest(modelId, false, chatHistory, mistralExecutionSettings);
        var endpoint = new Uri($"{this._endpoint}{this._separator}chat/completions");

        using var httpRequestMessage = this.CreatePost(request, endpoint, this._apiKey, stream: true);

        using var response = await this.SendStreamingRequestAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);

        using var responseStream = await response.Content.ReadAsStreamAndTranslateExceptionAsync().ConfigureAwait(false);
        foreach (var streamingTextContent in this.ProcessResponseStream(responseStream, modelId))
        {
            yield return streamingTextContent;
        }
    }

    internal async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(IList<string> data, CancellationToken cancellationToken, Kernel? kernel = null)
    {
        var request = new TextEmbeddingRequest(this._modelId, data);
        var endpoint = new Uri($"{this._endpoint}{this._separator}embeddings");
        using var httpRequestMessage = this.CreatePost(request, endpoint, this._apiKey, false);

        var response = await this.SendRequestAsync<TextEmbeddingResponse>(httpRequestMessage, cancellationToken).ConfigureAwait(false);

        return response.Data.Select(item => new ReadOnlyMemory<float>(item.Embedding.ToArray())).ToList();
    }

    #region private
    private readonly string _modelId;
    private readonly string _apiKey;
    private readonly Uri _endpoint;
    private readonly string _separator;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly IStreamJsonParser _streamJsonParser;

    private ChatCompletionRequest CreateChatCompletionRequest(string modelId, bool stream, ChatHistory chatHistory, MistralAIPromptExecutionSettings? executionSettings)
    {
        var request = new ChatCompletionRequest(modelId)
        {
            Stream = stream,
            Messages = chatHistory.Select(chatMessage => new MistralChatMessage(chatMessage.Role.ToString(), chatMessage.Content!)).ToList(),
        };

        if (executionSettings is not null)
        {
            request.Temperature = executionSettings.Temperature;
            request.TopP = executionSettings.TopP;
            request.MaxTokens = executionSettings.MaxTokens;
            request.SafePrompt = executionSettings.SafePrompt;
            request.RandomSeed = executionSettings.RandomSeed;
        }

        return request;
    }

    private HttpRequestMessage CreatePost(object requestData, Uri endpoint, string apiKey, bool stream)
    {
        var httpRequestMessage = HttpRequest.CreatePostRequest(endpoint, requestData);
        this.SetRequestHeaders(httpRequestMessage, apiKey, stream);

        return httpRequestMessage;
    }

    private void SetRequestHeaders(HttpRequestMessage request, string apiKey, bool stream)
    {
        request.Headers.Add("User-Agent", HttpHeaderConstant.Values.UserAgent);
        request.Headers.Add(HttpHeaderConstant.Names.SemanticKernelVersion, HttpHeaderConstant.Values.GetAssemblyVersion(this.GetType()));
        request.Headers.Add("Accept", stream ? "text/event-stream" : "application/json");
        request.Headers.Add("Authorization", $"Bearer {this._apiKey}");
        request.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }

    private async Task<T> SendRequestAsync<T>(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        using var response = await this._httpClient.SendWithSuccessCheckAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringWithExceptionMappingAsync().ConfigureAwait(false);

        return DeserializeResponse<T>(body);
    }

    private async Task<HttpResponseMessage> SendStreamingRequestAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
    {
        return await this._httpClient.SendWithSuccessCheckAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
    }

    private IEnumerable<StreamingChatMessageContent> ProcessResponseStream(Stream stream, string modelId)
        => from chatCompletionChunk in this.ParseTextResponseStream(stream)
           from chatContent in this.GetStreamingChatMessageContentFromResponse(chatCompletionChunk, modelId)
           select chatContent;

    private IEnumerable<MistralChatCompletionChunk> ParseTextResponseStream(Stream responseStream)
        => this._streamJsonParser.Parse(responseStream).Select(DeserializeResponse<MistralChatCompletionChunk>);

    private List<StreamingChatMessageContent> GetStreamingChatMessageContentFromResponse(MistralChatCompletionChunk chunk, string modelId)
    {
        return new List<StreamingChatMessageContent>
        {
            new(role: chunk.GetRole(),
                content: chunk.GetContent(),
                choiceIndex: chunk.GetChoiceIndex(),
                modelId: modelId,
                encoding: chunk.GetEncoding(),
                innerContent: chunk,
                metadata: chunk.GetMetadata())
        };
    }

    private static T DeserializeResponse<T>(string body)
    {
        try
        {
            T? deserializedResponse = JsonSerializer.Deserialize<T>(body);
            if (deserializedResponse is null)
            {
                throw new JsonException("Response is null");
            }

            return deserializedResponse;
        }
        catch (JsonException exc)
        {
            throw new KernelException("Unexpected response from model", exc)
            {
                Data = { { "ResponseData", body } },
            };
        }
    }

    private static Dictionary<string, object?> GetChatChoiceMetadata(ChatCompletionResponse completionResponse, MistralChatChoice chatChoice)
    {
        return new Dictionary<string, object?>(6)
        {
            { nameof(completionResponse.Id), completionResponse.Id },
            { nameof(completionResponse.Object), completionResponse.Object },
            { nameof(completionResponse.Model), completionResponse.Model },
            { nameof(completionResponse.Usage), completionResponse.Usage },
            { nameof(completionResponse.Created), completionResponse.Created },
            { nameof(chatChoice.Index), chatChoice.Index },
            { nameof(chatChoice.FinishReason), chatChoice.FinishReason },
        };
    }
    #endregion
}
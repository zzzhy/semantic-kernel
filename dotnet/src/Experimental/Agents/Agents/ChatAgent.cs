﻿// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

#pragma warning disable IDE0290 // Use primary constructor

namespace Microsoft.SemanticKernel.Experimental.Agents.Agents;
/// <summary>
/// A <see cref="KernelAgent"/> specialization based on <see cref="IChatCompletionService"/>.
/// </summary>
public sealed class ChatAgent : KernelAgent
{
    private readonly PromptExecutionSettings? _executionSettings;

    /// <inheritdoc/>
    public override string? Description { get; }

    /// <inheritdoc/>
    public override string Id { get; }

    /// <summary>
    /// The instructions of the agent (optional)
    /// </summary>
    public string? Instructions { get; }

    /// <inheritdoc/>
    public override string? Name { get; }

    /// <inheritdoc/>
    protected internal override Type ChannelType => typeof(LocalChannel<ChatAgent>);

    /// <inheritdoc/>
    protected internal override Task<AgentChannel> CreateChannelAsync(AgentNexus nexus, CancellationToken cancellationToken)
    {
        return Task.FromResult<AgentChannel>(new LocalChannel<ChatAgent>(nexus, InvokeAsync));
    }

    private static async IAsyncEnumerable<ChatMessageContent> InvokeAsync(ChatAgent agent, ChatHistory chat, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(agent.Instructions))
        {
            chat.AddMessage(AuthorRole.System, agent.Instructions!, name: agent.Name);
        }

        var chatCompletionService = agent.Kernel.GetRequiredService<IChatCompletionService>();

        var messages =
            await chatCompletionService.GetChatMessageContentsAsync(
                chat,
                agent._executionSettings,
                agent.Kernel,
                cancellationToken).ConfigureAwait(false);

        foreach (var message in messages)
        {
            message.Source = new AgentMessageSource(agent.Id).ToJson();

            yield return message;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatAgent"/> class.
    /// </summary>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="instructions">The agent instructions</param>
    /// <param name="description">The agent description (optional)</param>
    /// <param name="name">The agent name</param>
    /// <param name="executionSettings">The execution settings for the agent.</param>
    /// <remarks>
    /// Enable <see cref="OpenAIPromptExecutionSettings.ToolCallBehavior"/> for agent plugins.
    /// </remarks>
    public ChatAgent(
        Kernel kernel,
        string? instructions,
        string? description,
        string? name,
        PromptExecutionSettings? executionSettings = null)
       : base(kernel)
    {
        this.Id = Guid.NewGuid().ToString();
        this.Description = description;
        this.Instructions = instructions;
        this.Name = name;
        this._executionSettings = executionSettings;
    }
}
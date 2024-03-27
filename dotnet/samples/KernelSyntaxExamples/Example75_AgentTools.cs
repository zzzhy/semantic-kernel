﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Experimental.Agents;
using Resources;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Showcase usage of code_interpreter and retrieval tools.
/// </summary>
public sealed class Example75_AgentTools : BaseTest
{
    /// <summary>
    /// Specific model is required that supports agents and parallel function calling.
    /// Currently this is limited to Open AI hosted services.
    /// </summary>
    private const string OpenAIFunctionEnabledModel = "gpt-4-1106-preview";

    /// <summary>
    /// Flag to force usage of OpenAI configuration if both <see cref="TestConfiguration.OpenAI"/>
    /// and <see cref="TestConfiguration.AzureOpenAI"/> are defined.
    /// If 'false', Azure takes precedence.
    /// </summary>
    /// <remarks>
    /// NOTE: Retrieval tools is not currently available on Azure.
    /// </remarks>
    private const bool ForceOpenAI = true;

    // Track agents for clean-up
    private readonly List<IAgent> _agents = new();

    /// <summary>
    /// Show how to utilize code_interpreter tool.
    /// </summary>
    [Fact]
    public async Task RunCodeInterpreterToolAsync()
    {
        this.WriteLine("======== Using CodeInterpreter tool ========");

        var builder = CreateAgentBuilder().WithInstructions("Write only code to solve the given problem without comment.");

        try
        {
            var defaultAgent = Track(await builder.BuildAsync());

            var codeInterpreterAgent = Track(await builder.WithCodeInterpreter().BuildAsync());

            await ChatAsync(
                defaultAgent,
                codeInterpreterAgent,
                fileId: null,
                "What is the solution to `3x + 2 = 14`?",
                "What is the fibinacci sequence until 101?");
        }
        finally
        {
            await Task.WhenAll(this._agents.Select(a => a.DeleteAsync()));
        }
    }

    /// <summary>
    /// Show how to utilize retrieval tool.
    /// </summary>
    [Fact]
    public async Task RunRetrievalToolAsync()
    {
        // Set to "true" to pass fileId via thread invocation.
        // Set to "false" to associate fileId with agent definition.
        const bool PassFileOnRequest = false;

        this.WriteLine("======== Using Retrieval tool ========");

        if (TestConfiguration.OpenAI.ApiKey == null)
        {
            this.WriteLine("OpenAI apiKey not found. Skipping example.");
            return;
        }

        Kernel kernel = CreateFileEnabledKernel();
        var fileService = kernel.GetRequiredService<OpenAIFileService>();
        var result =
            await fileService.UploadContentAsync(
                new BinaryContent(() => Task.FromResult(EmbeddedResource.ReadStream("travelinfo.txt")!)),
                new OpenAIFileUploadExecutionSettings("travelinfo.txt", OpenAIFilePurpose.Assistants));

        var fileId = result.Id;
        this.WriteLine($"! {fileId}");

        var defaultAgent = Track(await CreateAgentBuilder().BuildAsync());

        var retrievalAgent = Track(await CreateAgentBuilder().WithRetrieval().BuildAsync());

        if (!PassFileOnRequest)
        {
            await retrievalAgent.AddFileAsync(fileId);
        }

        try
        {
            await ChatAsync(
                defaultAgent,
                retrievalAgent,
                PassFileOnRequest ? fileId : null,
                "Where did sam go?",
                "When does the flight leave Seattle?",
                "What is the hotel contact info at the destination?");
        }
        finally
        {
            await Task.WhenAll(this._agents.Select(a => a.DeleteAsync()).Append(fileService.DeleteFileAsync(fileId)));
        }
    }

    /// <summary>
    /// Common chat loop used for: RunCodeInterpreterToolAsync and RunRetrievalToolAsync.
    /// Processes each question for both "default" and "enabled" agents.
    /// </summary>
    private async Task ChatAsync(
        IAgent defaultAgent,
        IAgent enabledAgent,
        string? fileId = null,
        params string[] questions)
    {
        string[]? fileIds = null;
        if (fileId != null)
        {
            fileIds = new string[] { fileId };
        }

        foreach (var question in questions)
        {
            this.WriteLine("\nDEFAULT AGENT:");
            await InvokeAgentAsync(defaultAgent, question);

            this.WriteLine("\nTOOL ENABLED AGENT:");
            await InvokeAgentAsync(enabledAgent, question);
        }

        async Task InvokeAgentAsync(IAgent agent, string question)
        {
            await foreach (var message in agent.InvokeAsync(question, null, fileIds))
            {
                string content = message.Content;
                foreach (var annotation in message.Annotations)
                {
                    content = content.Replace(annotation.Label, string.Empty, StringComparison.Ordinal);
                }

                this.WriteLine($"# {message.Role}: {content}");

                if (message.Annotations.Count > 0)
                {
                    this.WriteLine("\n# files:");
                    foreach (var annotation in message.Annotations)
                    {
                        this.WriteLine($"* {annotation.FileId}");
                    }
                }
            }

            this.WriteLine();
        }
    }

    private static Kernel CreateFileEnabledKernel()
    {
        return
            ForceOpenAI || string.IsNullOrEmpty(TestConfiguration.AzureOpenAI.Endpoint) ?
                Kernel.CreateBuilder().AddOpenAIFiles(TestConfiguration.OpenAI.ApiKey).Build() :
                Kernel.CreateBuilder().AddAzureOpenAIFiles(TestConfiguration.AzureOpenAI.Endpoint, TestConfiguration.AzureOpenAI.ApiKey).Build();
    }

    private static AgentBuilder CreateAgentBuilder()
    {
        return
            ForceOpenAI || string.IsNullOrEmpty(TestConfiguration.AzureOpenAI.Endpoint) ?
                new AgentBuilder().WithOpenAIChatCompletion(OpenAIFunctionEnabledModel, TestConfiguration.OpenAI.ApiKey) :
                new AgentBuilder().WithAzureOpenAIChatCompletion(TestConfiguration.AzureOpenAI.Endpoint, TestConfiguration.AzureOpenAI.ChatDeploymentName, TestConfiguration.AzureOpenAI.ApiKey);
    }

    private IAgent Track(IAgent agent)
    {
        this._agents.Add(agent);

        return agent;
    }

    public Example75_AgentTools(ITestOutputHelper output) : base(output) { }
}

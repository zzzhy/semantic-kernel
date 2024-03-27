﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AudioToText;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.OpenAI;

public sealed class OpenAIAudioToTextTests
{
    private readonly IConfigurationRoot _configuration;

    public OpenAIAudioToTextTests(ITestOutputHelper output)
    {
        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<OpenAIAudioToTextTests>()
            .Build();
    }

    [Fact(Skip = "OpenAI will often throttle requests. This test is for manual verification.")]
    public async Task OpenAIAudioToTextTestAsync()
    {
        // Arrange
        const string Filename = "test_audio.wav";

        OpenAIConfiguration? openAIConfiguration = this._configuration.GetSection("OpenAIAudioToText").Get<OpenAIConfiguration>();
        Assert.NotNull(openAIConfiguration);

        var kernel = Kernel.CreateBuilder()
            .AddOpenAIAudioToText(openAIConfiguration.ModelId, openAIConfiguration.ApiKey)
            .Build();

        var service = kernel.GetRequiredService<IAudioToTextService>();

        await using Stream audio = File.OpenRead($"./TestData/{Filename}");
        var audioData = await BinaryData.FromStreamAsync(audio);

        // Act
        var result = await service.GetTextContentAsync(new AudioContent(audioData), new OpenAIAudioToTextExecutionSettings(Filename));

        // Assert
        Assert.Contains("The sun rises in the east and sets in the west.", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AzureOpenAIAudioToTextTestAsync()
    {
        // Arrange
        const string Filename = "test_audio.wav";

        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAIAudioToText").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIAudioToText(
                azureOpenAIConfiguration.DeploymentName,
                azureOpenAIConfiguration.Endpoint,
                azureOpenAIConfiguration.ApiKey)
            .Build();

        var service = kernel.GetRequiredService<IAudioToTextService>();

        await using Stream audio = File.OpenRead($"./TestData/{Filename}");
        var audioData = await BinaryData.FromStreamAsync(audio);

        // Act
        var result = await service.GetTextContentAsync(new AudioContent(audioData), new OpenAIAudioToTextExecutionSettings(Filename));

        // Assert
        Assert.Contains("The sun rises in the east and sets in the west.", result.Text, StringComparison.OrdinalIgnoreCase);
    }
}

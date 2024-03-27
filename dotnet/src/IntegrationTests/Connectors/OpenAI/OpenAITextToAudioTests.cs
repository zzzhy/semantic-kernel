﻿// Copyright (c) Microsoft. All rights reserved.

using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextToAudio;
using SemanticKernel.IntegrationTests.TestSettings;
using Xunit;
using Xunit.Abstractions;

namespace SemanticKernel.IntegrationTests.Connectors.OpenAI;

public sealed class OpenAITextToAudioTests
{
    private readonly IConfigurationRoot _configuration;

    public OpenAITextToAudioTests(ITestOutputHelper output)
    {
        // Load configuration
        this._configuration = new ConfigurationBuilder()
            .AddJsonFile(path: "testsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile(path: "testsettings.development.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<OpenAITextToAudioTests>()
            .Build();
    }

    [Fact(Skip = "OpenAI will often throttle requests. This test is for manual verification.")]
    public async Task OpenAITextToAudioTestAsync()
    {
        // Arrange
        OpenAIConfiguration? openAIConfiguration = this._configuration.GetSection("OpenAITextToAudio").Get<OpenAIConfiguration>();
        Assert.NotNull(openAIConfiguration);

        var kernel = Kernel.CreateBuilder()
            .AddOpenAITextToAudio(openAIConfiguration.ModelId, openAIConfiguration.ApiKey)
            .Build();

        var service = kernel.GetRequiredService<ITextToAudioService>();

        // Act
        var result = await service.GetAudioContentAsync("The sun rises in the east and sets in the west.");

        // Assert
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.Value.IsEmpty);
    }

    [Fact]
    public async Task AzureOpenAITextToAudioTestAsync()
    {
        // Arrange
        AzureOpenAIConfiguration? azureOpenAIConfiguration = this._configuration.GetSection("AzureOpenAITextToAudio").Get<AzureOpenAIConfiguration>();
        Assert.NotNull(azureOpenAIConfiguration);

        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAITextToAudio(
                azureOpenAIConfiguration.DeploymentName,
                azureOpenAIConfiguration.Endpoint,
                azureOpenAIConfiguration.ApiKey)
            .Build();

        var service = kernel.GetRequiredService<ITextToAudioService>();

        // Act
        var result = await service.GetAudioContentAsync("The sun rises in the east and sets in the west.");

        // Assert
        Assert.NotNull(result.Data);
        Assert.False(result.Data!.Value.IsEmpty);
    }
}

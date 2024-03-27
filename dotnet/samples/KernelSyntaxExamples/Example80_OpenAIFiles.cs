﻿// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Resources;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

// ReSharper disable once InconsistentNaming
/// <summary>
/// Showcase usage of Open AI file-service.
/// </summary>
public sealed class Example79_OpenAIFiles : BaseTest
{
    private const string ResourceFileName = "30-user-context.txt";

    /// <summary>
    /// Flag to force usage of OpenAI configuration if both <see cref="TestConfiguration.OpenAI"/>
    /// and <see cref="TestConfiguration.AzureOpenAI"/> are defined.
    /// If 'false', Azure takes precedence.
    /// </summary>
    private const bool ForceOpenAI = false;

    /// <summary>
    /// Show how to utilize OpenAI file-service.
    /// </summary>
    [Fact]
    public async Task RunFileLifecycleAsync()
    {
        this.WriteLine("======== OpenAI File-Service ========");

        // Initialize file-service
        var kernel =
            ForceOpenAI || string.IsNullOrEmpty(TestConfiguration.AzureOpenAI.Endpoint) ?
                Kernel.CreateBuilder().AddOpenAIFiles(TestConfiguration.OpenAI.ApiKey).Build() :
                Kernel.CreateBuilder().AddAzureOpenAIFiles(TestConfiguration.AzureOpenAI.Endpoint, TestConfiguration.AzureOpenAI.ApiKey).Build();

        var fileService = kernel.GetRequiredService<OpenAIFileService>();

        // Upload file
        var fileContent = new BinaryContent(() => Task.FromResult(EmbeddedResource.ReadStream(ResourceFileName)!));
        var fileReference =
            await fileService.UploadContentAsync(
                fileContent,
                new OpenAIFileUploadExecutionSettings(ResourceFileName, OpenAIFilePurpose.Assistants));

        WriteLine("SOURCE:");
        WriteLine($"# Name: {fileReference.FileName}");
        WriteLine("# Content:");
        WriteLine(Encoding.UTF8.GetString((await fileContent.GetContentAsync()).Span));

        try
        {
            // Retrieve file metadata for validation.
            var copyReference = await fileService.GetFileAsync(fileReference.Id);
            Assert.Equal(fileReference.Id, copyReference.Id);
            WriteLine("REFERENCE:");
            WriteLine($"# ID: {fileReference.Id}");
            WriteLine($"# Name: {fileReference.FileName}");
            WriteLine($"# Purpose: {fileReference.Purpose}");
        }
        finally
        {
            // Remove file
            await fileService.DeleteFileAsync(fileReference.Id);
        }
    }

    public Example79_OpenAIFiles(ITestOutputHelper output) : base(output) { }
}

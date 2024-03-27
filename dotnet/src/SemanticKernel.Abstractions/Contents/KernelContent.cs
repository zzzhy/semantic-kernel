﻿// Copyright (c) Microsoft. All rights reserved.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.SemanticKernel;

/// <summary>
/// Base class for all AI non-streaming results
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(TextContent), typeDiscriminator: nameof(TextContent))]
[JsonDerivedType(typeof(ImageContent), typeDiscriminator: nameof(ImageContent))]
#pragma warning disable SKEXP0010
[JsonDerivedType(typeof(BinaryContent), typeDiscriminator: nameof(BinaryContent))]
#pragma warning restore SKEXP0010
#pragma warning disable SKEXP0001
[JsonDerivedType(typeof(AudioContent), typeDiscriminator: nameof(AudioContent))]
#pragma warning restore SKEXP0001
public abstract class KernelContent
{
    /// <summary>
    /// The inner content representation. Use this to bypass the current abstraction.
    /// </summary>
    /// <remarks>
    /// The usage of this property is considered "unsafe". Use it only if strictly necessary.
    /// </remarks>
    [JsonIgnore]
    public object? InnerContent { get; set; }

    /// <summary>
    /// The model ID used to generate the content.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ModelId { get; set; }

    /// <summary>
    /// The metadata associated with the content.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? Metadata { get; set; }

    /// <summary>
    /// MIME type of the content.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MimeType { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelContent"/> class.
    /// </summary>
    protected KernelContent()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KernelContent"/> class.
    /// </summary>
    /// <param name="innerContent">The inner content representation</param>
    /// <param name="modelId">The model ID used to generate the content</param>
    /// <param name="metadata">Metadata associated with the content</param>
    protected KernelContent(object? innerContent, string? modelId = null, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        this.ModelId = modelId;
        this.InnerContent = innerContent;
        this.Metadata = metadata;
    }
}

﻿// Copyright © 2023-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Hylo;
using Hylo.Properties;
using System.Net;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace CloudStreams.Core.Infrastructure;

/// <summary>
/// Represents an object used to describe the result of an operation
/// </summary>
[DataContract]
public record OperationResult
    : ProblemDetails
{

    /// <summary>
    /// Initializes a new <see cref="OperationResult"/>
    /// </summary>
    public OperationResult() { }

    /// <summary>
    /// Initialize a new successfull <see cref="OperationResult"/>
    /// </summary>
    /// <param name="status">The response's status code</param>
    /// <param name="content">The response's content</param>
    public OperationResult(int status, object? content = null)
    {
        if (!status.IsSuccessStatusCode()) throw new NotSupportedException("This constructor can only be used for successfull responses");
        this.Status = status;
        this.Content = content;
    }

    /// <inheritdoc/>
    public OperationResult(Uri type, string title, int status, string? detail = null, Uri? instance = null, IDictionary<string, string[]>? errors = null, IDictionary<string, object>? extensionData = null)
    : base(type, title, status, detail, instance, errors, extensionData)
    {

    }

    /// <inheritdoc/>
    [DataMember(Name = "content", Order = 10), JsonPropertyOrder(10), JsonPropertyName("content"), YamlMember(Order = 10, Alias = "content")]
    public virtual object? Content { get; set; }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe a successfull operation
    /// </summary>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult Ok() => new((int)HttpStatusCode.OK);

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe a successfull operation
    /// </summary>
    /// <typeparam name="TContent">The type of content</typeparam>
    /// <param name="content">The content to wrap</param>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult<TContent> Ok<TContent>(TContent content) => new((int)HttpStatusCode.OK, content: content);

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe a successfull operation
    /// </summary>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult Accepted() => new((int)HttpStatusCode.Accepted);

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe the successfull creation of the specified content
    /// </summary>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult Created() => new((int)HttpStatusCode.Created);

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe the successfull creation of the specified content
    /// </summary>
    /// <typeparam name="TContent">The type of content</typeparam>
    /// <param name="content">The content to wrap</param>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult<TContent> Created<TContent>(TContent content) => new((int)HttpStatusCode.Created, content: content);

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe failure to modify a resource
    /// </summary>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult NotModified()
    {
        return new(ProblemTypes.NotModified, ProblemTitles.NotModified, (int)HttpStatusCode.NotModified, Properties.ProblemDetails.NotModified);
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe failure to modify a resource
    /// </summary>
    /// <typeparam name="TContent">The type of content</typeparam>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult<TContent> NotModified<TContent>()
    {
        return new(ProblemTypes.NotModified, ProblemTitles.NotModified, (int)HttpStatusCode.NotModified, Properties.ProblemDetails.NotModified);
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to describe failure due to a forbidden operation
    /// </summary>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult Forbidden() => new((int)HttpStatusCode.Forbidden); //todo: add title and message

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to inform about a validation failure
    /// </summary>
    /// <param name="detail">Describes the validation error</param>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult ValidationFailed(string? detail = null)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest, detail);
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> to inform about a validation failure
    /// </summary>
    /// <param name="evaluationResults">An object that represents the validation results</param>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult ValidationFailed(EvaluationResults evaluationResults)
    {
        var errors = evaluationResults.ToErrorList()?.ToDictionary(e => e.Key, e => e.Value);
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = errors == null ? null : new(errors)
        };
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> that describes failure due to validation problems
    /// </summary>
    /// <param name="errors">The errors that have occured during validation</param>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult ValidationFailed(params KeyValuePair<string, string[]>[] errors)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = new(errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
        };
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <param name="errors">The errors that have occured during validation</param>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult<TContent> ValidationFailed<TContent>(params KeyValuePair<string, string[]>[] errors)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = new(errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value))
        };
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <param name="evaluationResults">The <see cref="EvaluationResults"/> that describes the validation errors that have occured</param>
    /// <returns>A new <see cref="OperationResult"/></returns>
    public static OperationResult<TContent> ValidationFailed<TContent>(EvaluationResults evaluationResults)
    {
        var errors = evaluationResults.ToErrorList()?.ToDictionary(e => e.Key, e => e.Value);
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = errors == null ? null : new(errors)
        };
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type
    /// </summary>
    /// <typeparam name="TResource">The expected type of result</typeparam>
    /// <param name="name">The name of the resource that could not be found</param>
    /// <param name="namespace">The namespace the resource that could not be found belongs to</param>
    /// <returns>A new <see cref="OperationResult{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static OperationResult<TResource> ResourceNotFound<TResource>(string name, string? @namespace = null)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        if (string.IsNullOrWhiteSpace(@namespace)) return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ClusterResourceNotFound, resource.GetGroup(), resource.GetVersion(), resource.Kind, name));
        else return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.NamespacedResourceNotFound, resource.GetGroup(), resource.GetVersion(), resource.Kind, resource.GetNamespace()!, name));
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type
    /// </summary>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <param name="reference">A reference to the resource that could not be found</param>
    /// <returns>A new <see cref="OperationResult{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static OperationResult<TContent> ResourceNotFound<TContent>(IResourceReference reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.Namespace)) return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.NamespacedResourceNotFound, reference.Definition.Group, reference.Definition.Version, reference.Definition.Plural, reference.Name));
        else return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ClusterResourceNotFound, reference.Definition.Group, reference.Definition.Version, reference.Definition.Plural, reference.Name));
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult"/> that describes the failure to find an <see cref="IResourceDefinition"/> for the specified type <see cref="IResource"/> type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> the <see cref="IResourceDefinition"/> of could not be found</typeparam>
    /// <returns>A new <see cref="OperationResult"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static OperationResult ResourceDefinitionNotFound<TResource>()
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        return new(ProblemTypes.ResourceDefinitionNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ResourceDefinitionNotFound, resource.GetGroup(), resource.GetVersion(), resource.Definition.Plural));
    }

    /// <summary>
    /// Creates a new <see cref="OperationResult{TContent}"/> that describes the failure to find an <see cref="IResourceDefinition"/> for the specified type <see cref="IResource"/> type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> the <see cref="IResourceDefinition"/> of could not be found</typeparam>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <returns>A new <see cref="OperationResult{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static OperationResult<TContent> ResourceDefinitionNotFound<TResource, TContent>()
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        return new(ProblemTypes.ResourceDefinitionNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ResourceDefinitionNotFound, resource.GetGroup(), resource.GetVersion(), resource.Definition.Plural));
    }

}

/// <summary>
/// Describes a response produced to a request
/// </summary>
/// <typeparam name="TContent">The type of content wrapped by the <see cref="OperationResult{TContent}"/></typeparam>
public record OperationResult<TContent>
    : OperationResult
{

    /// <inheritdoc/>
    public OperationResult() { }

    /// <inheritdoc/>
    public OperationResult(int status, object? content = null) : base(status, content) { }

    /// <inheritdoc/>
    public OperationResult(Uri type, string title, int status, string? detail = null, Uri? instance = null, IDictionary<string, string[]>? errors = null, IDictionary<string, object>? extensionData = null)
        : base(type, title, status, detail, instance, errors, extensionData)
    {

    }

    /// <inheritdoc/>
    [IgnoreDataMember, JsonIgnore, YamlIgnore]
    public new TContent? Content
    {
        get
        {
            return (TContent?)base.Content;
        }
        set
        {
            base.Content = value;
        }
    }

}

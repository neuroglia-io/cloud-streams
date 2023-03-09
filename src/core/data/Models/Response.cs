using Json.Pointer;
using Json.Schema;
using System.Runtime.Versioning;
using System.Xml.Schema;

namespace CloudStreams.Core.Data.Models;

/// <summary>
/// Represents an object used to describe a response to a request
/// </summary>
[DataContract]
public class Response
    : ProblemDetails
{

    /// <summary>
    /// Initializes a new <see cref="Response"/>
    /// </summary>
    public Response() { }

    /// <summary>
    /// Initialize a new successfull <see cref="Response"/>
    /// </summary>
    /// <param name="status">The response's status code</param>
    /// <param name="content">The response's content</param>
    public Response(int status, object? content = null)
    {
        if (!status.IsSuccessStatusCode()) throw new NotSupportedException("This constructor can only be used for successfull responses");
        this.Status = status;
        this.Content = content;
    }

    /// <inheritdoc/>
    public Response(Uri type, string title, int status, string? detail = null, Uri? instance = null, IDictionary<string, string[]>? errors = null, IDictionary<string, object>? extensionData = null)
        : base(type, title, status, detail, instance, errors, extensionData)
    {

    }

    /// <inheritdoc/>
    [DataMember(Name = "content", Order = 1), JsonPropertyName("content"), YamlMember(Alias = "content")]
    public virtual object? Content { get; set; }

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe a successfull operation
    /// </summary>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response Ok() => new((int)HttpStatusCode.OK);

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe a successfull operation
    /// </summary>
    /// <typeparam name="TContent">The type of content</typeparam>
    /// <param name="content">The content to wrap</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TContent> Ok<TContent>(TContent content) => new((int)HttpStatusCode.OK, content: content);

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe a successfull operation
    /// </summary>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response Accepted() => new((int)HttpStatusCode.Accepted);

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe the successfull creation of the specified content
    /// </summary>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response Created() => new((int)HttpStatusCode.Created);

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe the successfull creation of the specified content
    /// </summary>
    /// <typeparam name="TContent">The type of content</typeparam>
    /// <param name="content">The content to wrap</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TContent> Created<TContent>(TContent content) => new((int)HttpStatusCode.Created, content: content);

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe failure to modify a resource
    /// </summary>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response NotModified()
    {
        return new(ProblemTypes.NotModified, ProblemTitles.NotModified, (int)HttpStatusCode.NotModified, Properties.ProblemDetails.NotModified);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe failure to modify a resource
    /// </summary>
    /// <typeparam name="TContent">The type of content</typeparam>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TContent> NotModified<TContent>()
    {
        return new(ProblemTypes.NotModified, ProblemTitles.NotModified, (int)HttpStatusCode.NotModified, Properties.ProblemDetails.NotModified);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> to describe failure due to a forbidden operation
    /// </summary>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response Forbidden() => new((int)HttpStatusCode.Forbidden); //todo: add title and message

    /// <summary>
    /// Creates a new <see cref="Response"/> to inform about a validation failure
    /// </summary>
    /// <param name="detail">Describes the validation error</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed(string? detail = null)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest, detail);
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> to inform about a validation failure
    /// </summary>
    /// <param name="evaluationResults">An object that represents the validation results</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed(EvaluationResults evaluationResults)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = evaluationResults.ToErrorList()?.ToDictionary(e => e.Key, e => e.Value)
        };
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to validation problems
    /// </summary>
    /// <param name="errors">The errors that have occured during validation</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed(params KeyValuePair<string, string[]>[] errors)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <param name="errors">The errors that have occured during validation</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TContent> ValidationFailed<TContent>(params KeyValuePair<string, string[]>[] errors)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes failure due to validation problems
    /// </summary>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <param name="evaluationResults">The <see cref="EvaluationResults"/> that describes the validation errors that have occured</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response<TContent> ValidationFailed<TContent>(EvaluationResults evaluationResults)
    {
        return new(ProblemTypes.ValidationFailed, ProblemTitles.ValidationFailed, (int)HttpStatusCode.BadRequest)
        {
            Errors = evaluationResults.ToErrorList()?.ToDictionary(e => e.Key, e => e.Value)
        };
    }

    /// <summary>
    /// Creates a new <see cref="Response{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type
    /// </summary>
    /// <typeparam name="TResource">The expected type of result</typeparam>
    /// <param name="name">The name of the resource that could not be found</param>
    /// <param name="namespace">The namespace the resource that could not be found belongs to</param>
    /// <returns>A new <see cref="Response{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static Response<TResource> ResourceNotFound<TResource>(string name, string? @namespace = null)
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        if (string.IsNullOrWhiteSpace(@namespace)) return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ClusterResourceNotFound, resource.GetGroup(), resource.GetVersion(), resource.Kind, name));
        else return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.NamespacedResourceNotFound, resource.GetGroup(), resource.GetVersion(), resource.Kind, resource.GetNamespace()!, name));
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes the failure to find an <see cref="IResource"/> of the specified type
    /// </summary>
    /// <param name="reference">A reference to the resource that could not be found</param>
    /// <returns>A new <see cref="Response"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static Response ResourceNotFound(IResourceReference reference)
    {
        if(reference.IsNamespaced()) return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.NamespacedResourceNotFound, reference.GetGroup(), reference.GetVersion(), reference.Kind, reference.Namespace!, reference.Name));
        else return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ClusterResourceNotFound, reference.GetGroup(), reference.GetVersion(), reference.Kind, reference.Name));
    }

    /// <summary>
    /// Creates a new <see cref="Response{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type
    /// </summary>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <param name="reference">A reference to the resource that could not be found</param>
    /// <returns>A new <see cref="Response{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static Response<TContent> ResourceNotFound<TContent>(IResourceReference reference)
    {
        if (reference.IsNamespaced()) return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.NamespacedResourceNotFound, reference.GetGroup(), reference.GetVersion(), reference.Kind, reference.Name));
        else return new(ProblemTypes.ResourceNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ClusterResourceNotFound, reference.GetGroup(), reference.GetVersion(), reference.Kind, reference.Name));
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> that describes the failure to find an <see cref="IResourceDefinition"/> for the specified type <see cref="IResource"/> type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> the <see cref="IResourceDefinition"/> of could not be found</typeparam>
    /// <returns>A new <see cref="Response"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static Response ResourceDefinitionNotFound<TResource>()
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        return new(ProblemTypes.ResourceDefinitionNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ResourceDefinitionNotFound, resource.GetGroup(), resource.GetVersion(), resource.Type.Plural));
    }

    /// <summary>
    /// Creates a new <see cref="Response{TContent}"/> that describes the failure to find an <see cref="IResourceDefinition"/> for the specified type <see cref="IResource"/> type
    /// </summary>
    /// <typeparam name="TResource">The type of <see cref="IResource"/> the <see cref="IResourceDefinition"/> of could not be found</typeparam>
    /// <typeparam name="TContent">The expected type of result</typeparam>
    /// <returns>A new <see cref="Response{TContent}"/> that describes the failure to find an <see cref="IResource"/> of the specified type</returns>
    public static Response<TContent> ResourceDefinitionNotFound<TResource, TContent>()
        where TResource : class, IResource, new()
    {
        var resource = new TResource();
        return new(ProblemTypes.ResourceDefinitionNotFound, ProblemTitles.NotFound, (int)HttpStatusCode.NotFound, StringExtensions.Format(Properties.ProblemDetails.ResourceDefinitionNotFound, resource.GetGroup(), resource.GetVersion(), resource.Type.Plural));
    }

}

/// <summary>
/// Describes a response produced to a request
/// </summary>
/// <typeparam name="TContent">The type of content wrapped by the <see cref="Response"/></typeparam>
public class Response<TContent>
    : Response
{

    /// <inheritdoc/>
    public Response() { }

    /// <inheritdoc/>
    public Response(int status, object? content = null) : base(status, content) { }

    /// <inheritdoc/>
    public Response(Uri type, string title, int status, string? detail = null, Uri? instance = null, IDictionary<string, string[]>? errors = null, IDictionary<string, object>? extensionData = null)
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

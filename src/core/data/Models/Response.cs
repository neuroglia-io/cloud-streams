using Json.Schema;
using System.Xml.Schema;

namespace CloudStreams.Data.Models;

/// <summary>
/// Represents an object used to describe a response to a request
/// </summary>
[DataContract]
public class Response
    : IExtensible
{

    /// <summary>
    /// Initializes a new <see cref="Response"/>
    /// </summary>
    public Response() { }

    /// <summary>
    /// Initializes a new <see cref="Response"/>
    /// </summary>
    /// <param name="status">The <see cref="Response"/>'s status code</param>
    /// <param name="type">An URI referencing a document that provides human-readable documentation for the response type</param>
    /// <param name="title">A short, human-readable summary of the response type</param>
    /// <param name="detail">A human-readable explanation specific to this occurrence of the response</param>
    /// <param name="instance">An URI reference that identifies the specific occurrence of the response. It may or may not yield further information if dereferenced.</param>
    /// <param name="content">The <see cref="Response"/>'s content, if any</param>
    /// <param name="errors">An <see cref="IDictionary{TKey, TValue}"/> containing they code/message mappings of the errors that have occured during the request's execution</param>
    public Response(int status, Uri? type = null, string? title = null, string? detail = null, Uri? instance = null, object? content = null, IDictionary<string, string[]>? errors = null)
    {
        this.Status = status;
        this.Type = type;
        this.Title = title;
        this.Detail = detail;
        this.Instance = instance;
        this.Content = content;
        this.Errors = errors;
    }

    /// <inheritdoc/>
    [DataMember(Name = "status", Order = 1), JsonPropertyName("status"), YamlMember(Alias = "status")]
    public virtual int Status { get; set; }

    /// <inheritdoc/>
    [DataMember(Name = "type", Order = 2), JsonPropertyName("type"), YamlMember(Alias = "type")]
    public virtual Uri? Type { get; set; }

    /// <inheritdoc/>
    [DataMember(Name = "title", Order = 3), JsonPropertyName("title"), YamlMember(Alias = "title")]
    public virtual string? Title { get; set; }

    /// <inheritdoc/>
    [DataMember(Name = "details", Order = 4), JsonPropertyName("details"), YamlMember(Alias = "details")]
    public virtual string? Detail { get; set; }

    /// <inheritdoc/>
    [DataMember(Name = "instance", Order = 5), JsonPropertyName("instance"), YamlMember(Alias = "instance")]
    public virtual Uri? Instance { get; set; }

    /// <inheritdoc/>
    [DataMember(Name = "content", Order = 6), JsonPropertyName("content"), YamlMember(Alias = "content")]
    public virtual object? Content { get; set; }

    /// <inheritdoc/>
    [DataMember(Name = "errors", Order = 7), JsonPropertyName("errors"), YamlMember(Alias = "errors")]
    public virtual IDictionary<string, string[]>? Errors { get; set; }

    /// <inheritdoc/>
    [DataMember(Name = "extensions", Order = 8), JsonExtensionData]
    public virtual IDictionary<string, object>? ExtensionData { get; set; }

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
        return new((int)HttpStatusCode.BadRequest)
        {
            Title = ProblemTitles.ValidationFailed,
            Detail = detail
        };
    }

    /// <summary>
    /// Creates a new <see cref="Response"/> to inform about a validation failure
    /// </summary>
    /// <param name="validationResults">An object that represents the validation results</param>
    /// <returns>A new <see cref="Response"/></returns>
    public static Response ValidationFailed(ValidationResults validationResults)
    {
        return new((int)HttpStatusCode.BadRequest)
        {
            Title = ProblemTitles.ValidationFailed,
            Detail = $"{validationResults?.InstanceLocation}: {validationResults?.Message}",
            Errors = validationResults?.NestedResults?.ToDictionary(r => r.InstanceLocation.ToString(), r => r.GetErrorMessages().ToArray())
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
        return new((int)HttpStatusCode.BadRequest)
        {
            Title = ProblemTitles.ValidationFailed,
            Errors = errors.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };
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
    public Response(int status, Uri? type = null, string? title = null, string? detail = null, Uri? instance = null, object? content = null, IDictionary<string, string[]>? errors = null) : base(status, type, title, detail, instance, content, errors) { }

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

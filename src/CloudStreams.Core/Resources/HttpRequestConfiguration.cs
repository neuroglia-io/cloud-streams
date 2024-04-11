namespace CloudStreams.Core.Resources;

/// <summary>
/// Represents an object used to configure an HTTP request
/// </summary>
[DataContract]
public record HttpRequestConfiguration
{

    /// <summary>
    /// Initializes a new <see cref="HttpRequestConfiguration"/>
    /// </summary>
    public HttpRequestConfiguration() { }

    /// <summary>
    /// Initializes a new <see cref="HttpRequestConfiguration"/>
    /// </summary>
    /// <param name="method">The method of the HTTP request to perform</param>
    /// <param name="path">The path of the HTTP request to perform</param>
    /// <param name="headers">The headers of the HTTP request to perform</param>
    /// <param name="body">A name/value mapping of the properties of the request's body, if any</param>
    public HttpRequestConfiguration(string method, string path, IDictionary<string, string>? headers = null, IDictionary<string, object>? body = null)
    {
        if (string.IsNullOrWhiteSpace(method)) throw new ArgumentNullException(nameof(method));
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
        this.Method = method;
        this.Path = path;
        this.Headers = headers;
        this.Body = body;
    }

    /// <summary>
    /// Gets/sets the method of the HTTP request to perform
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 1, Name = "method", IsRequired = true), JsonPropertyOrder(1), JsonPropertyName("method"), YamlMember(Order = 1, Alias = "method")]
    public virtual string Method { get; set; } = null!;

    /// <summary>
    /// Gets/sets the path of the HTTP request to perform
    /// </summary>
    [Required, JsonRequired]
    [DataMember(Order = 2, Name = "path", IsRequired = true), JsonPropertyOrder(2), JsonPropertyName("path"), YamlMember(Order = 2, Alias = "path")]
    public virtual string Path { get; set; } = null!;

    /// <summary>
    /// Gets/sets the headers of the HTTP request to perform
    /// </summary>
    [DataMember(Order = 3, Name = "headers"), JsonPropertyOrder(3), JsonPropertyName("headers"), YamlMember(Order = 3, Alias = "headers")]
    public virtual IDictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Gets/sets a name/value mapping of the properties of the request's body, if any
    /// </summary>
    [DataMember(Order = 4, Name = "body"), JsonPropertyOrder(4), JsonPropertyName("body"), YamlMember(Order = 4, Alias = "body")]
    public virtual IDictionary<string, object>? Body { get; set; }

}
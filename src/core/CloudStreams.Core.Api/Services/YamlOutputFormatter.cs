using Microsoft.AspNetCore.Mvc.Formatters;
using Neuroglia.Serialization;
using System.Text;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the <see cref="TextOutputFormatter"/> used to serialize YAML
/// </summary>
public class YamlOutputFormatter
    : TextOutputFormatter
{

    /// <summary>
    /// Initializes a new <see cref="YamlOutputFormatter"/>
    /// </summary>
    /// <param name="serializer">The service used to serialize/deserialize objects to/from YAML</param>
    public YamlOutputFormatter(IYamlSerializer serializer)
    {
        this.Serializer = serializer;
        this.SupportedEncodings.Add(Encoding.UTF8);
        this.SupportedEncodings.Add(Encoding.Unicode);
        this.SupportedMediaTypes.Add("application/x-yaml");
        this.SupportedMediaTypes.Add("text/yaml");
    }

    /// <summary>
    /// Gets the service used to serialize/deserialize objects to/from YAML
    /// </summary>
    protected IYamlSerializer Serializer { get; }

    /// <inheritdoc/>
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(selectedEncoding);
        var response = context.HttpContext.Response;
        using var stream = new MemoryStream();
        this.Serializer.Serialize(context.Object, stream);
        await stream.FlushAsync().ConfigureAwait(false);
        stream.Position = 0;
        await stream.CopyToAsync(response.Body);
    }

}
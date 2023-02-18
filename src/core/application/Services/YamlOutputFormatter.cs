using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text;

namespace CloudStreams.Core.Application.Services;

/// <summary>
/// Represents the <see cref="TextOutputFormatter"/> used to serialize YAML
/// </summary>
public class YamlOutputFormatter 
    : TextOutputFormatter
{

    /// <summary>
    /// Initializes a new <see cref="YamlOutputFormatter"/>
    /// </summary>
    public YamlOutputFormatter()
    {
        this.SupportedEncodings.Add(Encoding.UTF8);
        this.SupportedEncodings.Add(Encoding.Unicode);
        this.SupportedMediaTypes.Add("application/x-yaml");
        this.SupportedMediaTypes.Add("text/yaml");
        this.SupportedMediaTypes.Add(CloudEventMediaTypeNames.CloudEventsYaml);
    }

    /// <inheritdoc/>
    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (selectedEncoding == null) throw new ArgumentNullException(nameof(selectedEncoding));
        var response = context.HttpContext.Response;
        using var stream = new MemoryStream();
        using var streamWriter = new StreamWriter(stream);
        Serializer.Yaml.Serialize(streamWriter, context.Object);
        await streamWriter.FlushAsync();
        stream.Position = 0;
        await stream.CopyToAsync(response.Body);
    }

}

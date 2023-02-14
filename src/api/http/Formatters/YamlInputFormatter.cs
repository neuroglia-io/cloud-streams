using Microsoft.AspNetCore.Mvc.Formatters;
using System.Text;

namespace CloudStreams.Api.Http.Formatters;

/// <summary>
/// Represents the <see cref="TextInputFormatter"/> used to deserialize YAML
/// </summary>
public class YamlInputFormatter 
    : TextInputFormatter
{

    /// <summary>
    /// Initializes a new <see cref="YamlInputFormatter"/>
    /// </summary>
    public YamlInputFormatter()
    {
        this.SupportedEncodings.Add(Encoding.UTF8);
        this.SupportedEncodings.Add(Encoding.Unicode);
        this.SupportedMediaTypes.Add("application/x-yaml");
        this.SupportedMediaTypes.Add("text/yaml");
        this.SupportedMediaTypes.Add(CloudEventMediaTypeNames.CloudEventsYaml);
    }

    /// <inheritdoc/>
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (encoding == null) throw new ArgumentNullException(nameof(encoding));
        var request = context.HttpContext.Request;
        using var stream = new MemoryStream();
        await request.Body.CopyToAsync(stream);
        await stream.FlushAsync();
        stream.Position = 0;
        using var streamReader = new StreamReader(stream);
        try
        {
            var model = Serializer.Yaml.Deserialize(streamReader, context.ModelType);
            return await InputFormatterResult.SuccessAsync(model);
        }
        catch (Exception)
        {
            return await InputFormatterResult.FailureAsync();
        }
    }
}

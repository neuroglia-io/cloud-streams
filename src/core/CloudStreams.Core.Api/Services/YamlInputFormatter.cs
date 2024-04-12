using Microsoft.AspNetCore.Mvc.Formatters;
using Neuroglia.Serialization;
using System.Text;

namespace CloudStreams.Core.Api.Services;

/// <summary>
/// Represents the <see cref="TextInputFormatter"/> used to deserialize YAML
/// </summary>
public class YamlInputFormatter
    : TextInputFormatter
{

    /// <summary>
    /// Initializes a new <see cref="YamlInputFormatter"/>
    /// </summary>
    /// <param name="serializer">The service used to serialize/deserialize objects to/from YAML</param>
    public YamlInputFormatter(IYamlSerializer serializer)
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
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (encoding == null) throw new ArgumentNullException(nameof(encoding));
        var request = context.HttpContext.Request;
        using var stream = new MemoryStream();
        await request.Body.CopyToAsync(stream);
        await stream.FlushAsync();
        stream.Position = 0;
        try
        {
            var model = this.Serializer.Deserialize(stream, context.ModelType);
            return await InputFormatterResult.SuccessAsync(model);
        }
        catch (Exception)
        {
            return await InputFormatterResult.FailureAsync();
        }
    }
}

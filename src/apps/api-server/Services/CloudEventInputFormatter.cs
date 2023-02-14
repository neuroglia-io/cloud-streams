using CloudNative.CloudEvents.AspNetCore;
using CloudNative.CloudEvents.Core;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace CloudStreams.Api.Server.Services;

/// <summary>
/// A <see cref="TextInputFormatter"/> that parses HTTP requests into CloudEvents.
/// </summary>
public class CloudEventInputFormatter 
    : TextInputFormatter
{

    /// <summary>
    /// Initializes a new <see cref="CloudEventInputFormatter"/>
    /// </summary>
    /// <param name="formatter">The service used to format <see cref="CloudEvent"/>s</param>
    public CloudEventInputFormatter(CloudEventFormatter formatter)
    {
        this.Formatter = Validation.CheckNotNull(formatter, nameof(formatter));
        this.SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse("application/cloudevents+json"));
        this.SupportedEncodings.Add(Encoding.UTF8);
        this.SupportedEncodings.Add(Encoding.Unicode);
    }

    /// <summary>
    /// Gets the service used to format <see cref="CloudEvent"/>s
    /// </summary>
    protected CloudEventFormatter Formatter { get; }

    /// <inheritdoc />
    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context, Encoding encoding)
    {
        Validation.CheckNotNull(context, nameof(context));
        Validation.CheckNotNull(encoding, nameof(encoding));
        var request = context.HttpContext.Request;
        try
        {
            var cloudEvent = await request.ToCloudEventAsync(this.Formatter);
            return await InputFormatterResult.SuccessAsync(cloudEvent);
        }
        catch (Exception)
        {
            return await InputFormatterResult.FailureAsync();
        }
    }

    /// <inheritdoc />
    protected override bool CanReadType(Type type) => type == typeof(CloudEvent) && base.CanReadType(type);

}
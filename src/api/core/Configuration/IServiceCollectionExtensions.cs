namespace CloudStreams.Api.Configuration;

/// <summary>
/// Defines extensions for <see cref="WebApplicationBuilder"/>s
/// </summary>
public static class IWebApplicationBuilderExtensions
{

    /// <summary>
    /// Configures the <see cref="WebApplicationBuilder"/> to use the Cloud Streams API
    /// </summary>
    /// <param name="app">The <see cref="WebApplicationBuilder"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the <see cref="ICloudStreamsApiBuilder"/></param>
    /// <returns>The configured <see cref="WebApplicationBuilder"/></returns>
    public static WebApplicationBuilder AddCloudStreamsApi(this WebApplicationBuilder app, Action<ICloudStreamsApiBuilder> setup)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));
        if (setup == null) throw new ArgumentNullException(nameof(setup));
        var builder = new CloudStreamsApiBuilder(app.Configuration, app.Environment, app.Services);
        setup(builder);
        builder.Build();
        return app;
    }

}

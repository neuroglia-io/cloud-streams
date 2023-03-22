namespace CloudStreams.Core.Application.Configuration;

/// <summary>
/// Defines extensions for <see cref="WebApplicationBuilder"/>s
/// </summary>
public static class IWebApplicationBuilderExtensions
{

    /// <summary>
    /// Configures the <see cref="WebApplicationBuilder"/> to use Cloud Streams
    /// </summary>
    /// <param name="app">The <see cref="WebApplicationBuilder"/> to configure</param>
    /// <param name="setup">An <see cref="Action{T}"/> used to setup the <see cref="ICloudStreamsApplicationBuilder"/></param>
    /// <returns>The configured <see cref="WebApplicationBuilder"/></returns>
    public static WebApplicationBuilder UseCloudStreams(this WebApplicationBuilder app, Action<ICloudStreamsApplicationBuilder> setup)
    {
        if (app == null) throw new ArgumentNullException(nameof(app));
        if (setup == null) throw new ArgumentNullException(nameof(setup));
        var builder = new CloudStreamsApplicationBuilder(app.Configuration, app.Environment, app.Services, app.Logging);
        setup(builder);
        builder.Build();
        return app;
    }

}

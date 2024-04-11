using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neuroglia.Data.Infrastructure.ResourceOriented;
using Neuroglia.Data.Infrastructure.ResourceOriented.Services;

namespace CloudStreams.Core.Infrastructure.Services;

/// <summary>
/// Represents the service used to initialize the Cloud Streams resource database
/// </summary>
/// <inheritdoc/>
public class DatabaseInitializer(ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
    : Neuroglia.Data.Infrastructure.ResourceOriented.Services.DatabaseInitializer(loggerFactory, serviceProvider)
{

    /// <inheritdoc/>
    protected override async Task SeedAsync(CancellationToken cancellationToken)
    {
        var database = this.ServiceProvider.GetRequiredService<IDatabase>();
        foreach (var definition in CloudStreamsDefaults.Resources.Definitions.AsEnumerable())
        {
            await database.CreateResourceAsync(definition, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

}

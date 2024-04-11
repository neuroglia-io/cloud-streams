using CloudStreams.Core.Resources;
using System.Diagnostics;

namespace CloudStreams.Core;

/// <summary>
/// Exposes defaults about Cloud Streams
/// </summary>
public static class CloudStreamsDefaults
{

    /// <summary>
    /// Gets the default group for Cloud Streams resources
    /// </summary>
    public const string ResourceGroup = "cloud-streams.io";

    /// <summary>
    /// Exposes Cloud Streams default resources
    /// </summary>
    public static class Resources
    {

        /// <summary>
        /// Exposes Cloud Streams resource definitions
        /// </summary>
        public static class Definitions
        {

            /// <summary>
            /// Gets the definition of Broker resources
            /// </summary>
            public static ResourceDefinition Broker { get; } = new BrokerResourceDefinition();

            /// <summary>
            /// Gets the definition of Gateway resources
            /// </summary>
            public static ResourceDefinition Gateway { get; } = new GatewayResourceDefinition();

            /// <summary>
            /// Gets the definition of Subscription resources
            /// </summary>
            public static ResourceDefinition Subscription { get; } = new SubscriptionResourceDefinition();

            /// <summary>
            /// Gets a new <see cref="IEnumerable{T}"/> containing Cloud Streams default resource definitions
            /// </summary>
            /// <returns></returns>
            public static IEnumerable<ResourceDefinition> AsEnumerable()
            {
                yield return Broker;
                yield return Gateway;
                yield return Subscription;
            }

        }

    }

    /// <summary>
    /// Exposes constants about Cloud Streams application telemetry
    /// </summary>
    public static class Telemetry
    {

        /// <summary>
        /// Exposes the Cloud Streams application's <see cref="System.Diagnostics.ActivitySource"/>
        /// </summary>
        public static ActivitySource ActivitySource { get; set; } = null!;

    }

}

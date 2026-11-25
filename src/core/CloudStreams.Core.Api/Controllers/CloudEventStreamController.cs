// Copyright © 2024-Present The Cloud Streams Authors
//
// Licensed under the Apache License, Version 2.0 (the "License"),
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using CloudStreams.Core.Application.Queries.Streams;

namespace CloudStreams.Core.Api.Controllers;

/// <summary>
/// Represents the API controller used to manage the cloud event streams
/// </summary>
/// <inheritdoc/>
[Route("api/core/v1/cloud-events/stream")]
public class CloudEventStreamController(IMediator mediator)
    : ApiController(mediator)
{

    /// <summary>
    /// Gets the cloud event stream's metadata
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StreamMetadata>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetStreamMetadata(CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new GetEventStreamMetadataQuery(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Describes the recent activity of the cloud event stream: how many cloud events have been recorded over a period ending now, and the most active sources and types
    /// </summary>
    /// <param name="period">The period, ending now, to describe, in ISO 8601 duration format (e.g. <c>PT24H</c>). Defaults to 24 hours, and may not exceed 31 days</param>
    /// <param name="buckets">The amount of consecutive slices to split the period into, between 1 and 96. Defaults to 24</param>
    /// <param name="sampleSize">The amount of most recent cloud events to compute the most active sources and types from, between 0 and 1000. Defaults to 500</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    /// <remarks>See #225</remarks>
    [HttpGet("activity")]
    [ProducesResponseType(typeof(StreamActivity), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> GetStreamActivity([FromQuery] string? period = null, [FromQuery] int buckets = GetEventStreamActivityQuery.DefaultBuckets, [FromQuery] int sampleSize = GetEventStreamActivityQuery.DefaultSampleSize, CancellationToken cancellationToken = default)
    {
        TimeSpan duration;
        try { duration = string.IsNullOrWhiteSpace(period) ? GetEventStreamActivityQuery.DefaultPeriod : System.Xml.XmlConvert.ToTimeSpan(period); }
        catch (FormatException) { return this.ValidationProblem(detail: $"The period '{period}' is not a valid ISO 8601 duration"); }
        return this.Process(await this.Mediator.ExecuteAsync(new GetEventStreamActivityQuery(duration, buckets, sampleSize), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Reads the cloud event stream
    /// </summary>
    /// <param name="options">The object used to configure the query to perform</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <returns>A new <see cref="IActionResult"/></returns>
    [HttpGet("read")]
    [ProducesResponseType(typeof(IEnumerable<object>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(Neuroglia.ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public virtual async Task<IActionResult> ReadStream([FromQuery] StreamReadOptions options, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid) return this.ValidationProblem(this.ModelState);
        return this.Process(await this.Mediator.ExecuteAsync(new ReadEventStreamQuery(options), cancellationToken).ConfigureAwait(false));
    }

}

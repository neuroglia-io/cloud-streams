// Copyright © 2023-Present The Cloud Streams Authors
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

using Hylo;
using Microsoft.AspNetCore.Mvc.Formatters;
using System.Net.NetworkInformation;
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

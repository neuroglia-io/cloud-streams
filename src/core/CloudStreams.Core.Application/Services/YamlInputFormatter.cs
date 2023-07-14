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
using System.Text;

namespace CloudStreams.Core.Application.Services;

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

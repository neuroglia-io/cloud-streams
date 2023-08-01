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

using CloudStreams.Core.Data;
using System.Text.Json.Nodes;

namespace CloudStreams.Core;

/// <summary>
/// Defines extensions for <see cref="CloudEventDescriptor"/>s
/// </summary>
public static class CloudEventDescriptorExtensions
{

    /// <summary>
    /// Converts the <see cref="CloudEventDescriptor"/> into the <see cref="CloudEvent"/> it describes
    /// </summary>
    /// <param name="descriptor">The <see cref="CloudEventDescriptor"/> to convert</param>
    /// <returns>The <see cref="CloudEvent"/> described by the converted <see cref="CloudEventDescriptor"/></returns>
    public static CloudEvent ToCloudEvent(this CloudEventDescriptor descriptor)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        if(descriptor.Data is byte[] byteArray)
        {
            //lets assume the data is actually JSON

        }
        var e = (JsonObject)Hylo.Serializer.Json.SerializeToNode(descriptor.Metadata.ContextAttributes)!;
        var data = Hylo.Serializer.Json.SerializeToNode(descriptor.Data);
        e[CloudEventAttributes.Data] = data;
        return Hylo.Serializer.Json.Deserialize<CloudEvent>(e)!;
    }

}

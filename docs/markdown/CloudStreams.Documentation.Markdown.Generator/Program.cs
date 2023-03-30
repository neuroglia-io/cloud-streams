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

using CloudStreams.Core;
using CloudStreams.Core.Data.Models;
using CloudStreams.Documentation.Markdown.Generator;
using System.Collections;
using System.Text;
using System.Text.Json.Serialization;

var modelTypes = TypeCacheUtil.FindFilteredTypes("csdg:resources", t => t.IsClass && !t.IsInterface && !t.IsAbstract && !t.IsGenericTypeDefinition && t.IsPublic && t.Namespace == typeof(Subscription).Namespace, typeof(Broker).Assembly).OrderBy(t => t.Name);
GenerateTableOfContents(modelTypes);
foreach (var modelType in modelTypes)
{
    GenerateMarkdownDocumentationFor(modelType);
}

void GenerateTableOfContents(IEnumerable<Type> modelTypes)
{
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine(
$"""
## Data Models Index

""");
    foreach(var modelType in modelTypes)
    {
        stringBuilder.AppendLine(
$"""
- {GenerateTypeReferenceFor(modelType)}
""");
    }
    File.WriteAllText($"README.md", stringBuilder.ToString());
}

void GenerateMarkdownDocumentationFor(Type type)
{
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine(
$"""
## {type.Name}

### Description

{ConvertLineBreaksToMarkdown(XmlDocumentationHelper.SummaryOf(type))}

```c#
public class {type.Name}
```

### Properties

{GeneratePropertiesMarkdownTableFor(type)}

### Examples

{GenerateExamplesFor(type)}

""");

    File.WriteAllText($"{type.Name.ToHyphenCase()}.md", stringBuilder.ToString());
}

string GeneratePropertiesMarkdownTableFor(Type type)
{
    var stringBuilder = new StringBuilder();
    stringBuilder.AppendLine(
$"""
| Name | Type | Required | Description |
|------|:----:|:--------:|-------------|
""");
    foreach(var property in type.GetProperties().Where(p => p.CanRead && p.CanWrite && !p.TryGetCustomAttribute<JsonIgnoreAttribute>(out _)).OrderBy(p =>
    {
        if (p.TryGetCustomAttribute(out JsonPropertyOrderAttribute? orderAttribute) && orderAttribute != null) return orderAttribute.Order; else return 0;
    }))
    {
        var name = property.Name.ToCamelCase();
        var propertyType = GenerateTypeReferenceFor(property.PropertyType);
        var required = property.TryGetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>(out _) ? "`yes`" : "`no`";
        var summary = ConvertLineBreaksToMarkdown(XmlDocumentationHelper.SummaryOf(property));
        if (property.PropertyType.IsEnum) summary += GenerateSupportedEnumValuesSummary(property.PropertyType);
        stringBuilder.AppendLine(
$"""
| {name} | {propertyType} | {required} | {summary} |
""");
    }
    return stringBuilder.ToString();
}

string GetTypeName(Type type)
{
    if (type.IsEnum || type == typeof(string) || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan) || type == typeof(Guid)) return "string";
    if (type == typeof(short) || type == typeof(int) || type == typeof(long)) return "integer";
    if (type == typeof(double) || type == typeof(decimal) || type == typeof(float)) return "number";
    if (type.GetGenericType(typeof(IDictionary<,>)) != null) return "object";
    if (type.IsEnumerable()) return $"{GetTypeName(type.GetEnumerableElementType())}[]";
    return type.Name;
}

string GenerateTypeReferenceFor(Type type)
{
    var typeName = GetTypeName(type);
    if (type.IsPrimitiveType() || typeName == "object") return $"`{typeName}`";
    return 
$"""
[`{typeName}`](/{typeName.ToHyphenCase()}.md)
""";
}

string GenerateExamplesFor(Type type)
{
    var sample = type.GetSample();
    return
$$"""

<table>
<tr>
<th>JSON</th>
<th>YAML</th>
</tr>
<tr>
<td valign="top">

```json

{{Serializer.Json.Serialize(sample, true)}}

```

</td>
<td valign="top">

```yaml

{{Serializer.Yaml.Serialize(sample)}}

```

</td>
</tr>
</table>

""";
}

string? ConvertLineBreaksToMarkdown(string? input)
{
    return input?
        .Replace(Environment.NewLine, "<br>")
        .Replace("\r\n", "<br>")
        .Replace("\n", "<br>")
        .Replace("\t", "<br>")
        .Replace("<para/>", "<br>")
        .Replace("<para />", "<br>")
        .Replace("<para></para>", "<br>");
}

string GenerateSupportedEnumValuesSummary(Type type)
{
    var stringBuilder = new StringBuilder();
    stringBuilder.Append("<br><u>Supported values</u>:<br>");
    foreach(var value in Enum.GetValues(type))
    {
        stringBuilder.Append(
$"""
- `{EnumHelper.Stringify((Enum)value, type)}`: {XmlDocumentationHelper.SummaryOf(EnumHelper.GetField((Enum)value, type)!)}<br>
""");
    }
    return stringBuilder.ToString();
}
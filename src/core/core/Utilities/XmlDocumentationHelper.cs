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

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml;

namespace CloudStreams.Core;

/// <summary>
/// Defines helper members to handle Xml code documentation
/// </summary>
/// <remarks>Code adapted from https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/october/csharp-accessing-xml-documentation-via-reflection</remarks>
public static class XmlDocumentationHelper
{

    private static readonly ConcurrentDictionary<string, AssemblyXmlDocumentationContainer> LoadedAssemblyXmlDocumentation = new();

    /// <summary>
    /// Gets the specified type's XML code documentation
    /// </summary>
    /// <param name="type">The type to get the XML code documentation for</param>
    /// <returns>The XML code documentation of the specified type</returns>
    public static string? DocumentationOf(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (!LoadedAssemblyXmlDocumentation.TryGetValue(type.Assembly.FullName!, out var container))
        {
            container = AssemblyXmlDocumentationContainer.BuildFor(type.Assembly);
            LoadedAssemblyXmlDocumentation[type.Assembly.FullName!] = container;
        }
        return container.GetDocumentationFor(type);
    }

    /// <summary>
    /// Gets the specified type's XML code documentation
    /// </summary>
    /// <param name="type">The type to get the XML code documentation for</param>
    /// <returns>The XML code documentation of the specified type</returns>
    public static XElement? DocumentationXElementOf(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        var xml = DocumentationOf(type);
        if (string.IsNullOrWhiteSpace(xml)) return null;
        return XElement.Parse(xml);
    }

    /// <summary>
    /// Gets the specified type's XML code documentation summary
    /// </summary>
    /// <param name="type">The type to get the XML code documentation summary for</param>
    /// <returns>The XML code documentation summary of the specified type</returns>
    public static string? SummaryOf(Type type)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        var docElement = DocumentationXElementOf(type);
        if (docElement == null) return null;
        using var reader = docElement.Element("summary")?.CreateReader();
        reader?.MoveToContent();
        return reader?.ReadInnerXml().Trim();
    }

    /// <summary>
    /// Gets the specified member's XML code documentation
    /// </summary>
    /// <param name="member">The member to get the XML code documentation for</param>
    /// <returns>The XML code documentation of the specified member</returns>
    public static string DocumentationOf(MemberInfo member)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        if (!LoadedAssemblyXmlDocumentation.TryGetValue(member.DeclaringType!.Assembly.FullName!, out var container))
        {
            container = AssemblyXmlDocumentationContainer.BuildFor(member.DeclaringType.Assembly);
            LoadedAssemblyXmlDocumentation[member.DeclaringType!.Assembly.FullName!] = container;
        }
        return container.GetDocumentationFor((dynamic)member);
    }

    /// <summary>
    /// Gets the specified member's XML code documentation
    /// </summary>
    /// <param name="member">The member to get the XML code documentation for</param>
    /// <returns>The XML code documentation of the specified member</returns>
    public static XElement? DocumentationXElementOf(MemberInfo member)
    {
        if (member == null) throw new ArgumentNullException(nameof(member));
        var xml = DocumentationOf(member);
        if (string.IsNullOrWhiteSpace(xml))return null;
        return XElement.Parse(xml);
    }

    /// <summary>
    /// Gets the specified member's XML code documentation summary
    /// </summary>
    /// <param name="member">The member to get the XML code documentation summary for</param>
    /// <returns>The XML code documentation summary of the specified member</returns>
    public static string? SummaryOf(MemberInfo member)
    {
        if (member == null) return null;
        var docElement = DocumentationXElementOf(member);
        if (docElement == null) return null;
        if (member.DeclaringType != member.ReflectedType || docElement.Element("inheritdoc") != null) return SummaryOf(member.GetOverridenMember()!);
        using var reader = docElement.Element("summary")?.CreateReader();
        reader?.MoveToContent();
        return reader?.ReadInnerXml().Trim();
    }

    /// <summary>
    /// Gets the specified parameter's XML code documentation
    /// </summary>
    /// <param name="parameter">The parameter to get the XML code documentation for</param>
    /// <returns>The XML code documentation of the specified parameter</returns>
    public static string DocumentationOf(ParameterInfo parameter)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));
        if (!LoadedAssemblyXmlDocumentation.TryGetValue(parameter.Member.DeclaringType!.Assembly.FullName!, out var container))
        {
            container = AssemblyXmlDocumentationContainer.BuildFor(parameter.Member.DeclaringType.Assembly);
            LoadedAssemblyXmlDocumentation[parameter.Member.DeclaringType!.Assembly.FullName!] = container;
        }
        return container.GetDocumentationFor((dynamic)parameter);
    }

    /// <summary>
    /// Gets the specified parameter's XML code documentation summary
    /// </summary>
    /// <param name="parameter">The parameter to get the XML code documentation summary for</param>
    /// <returns>The XML code documentation summary of the specified parameter</returns>
    public static string SummaryOf(ParameterInfo parameter)
    {
        if (parameter == null)
            throw new ArgumentNullException(nameof(parameter));
        return DocumentationOf(parameter);
    }

    private class AssemblyXmlDocumentationContainer
    {

        private AssemblyXmlDocumentationContainer()
        {

        }

        public Dictionary<string, string> XmlDocumentation { get; } = new();

        public string? GetDocumentationFor(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            var key = "T:" + BuildXmlKey(type.FullName!, null);
            this.XmlDocumentation.TryGetValue(key, out var documentation);
            return documentation?.Trim();
        }

        public string? GetDocumentationFor(MethodBase methodInfo)
        {
            if (methodInfo == null) throw new ArgumentNullException(nameof(methodInfo));
            var key = "M:" + BuildXmlKeyFor(methodInfo);
            this.XmlDocumentation.TryGetValue(key, out var documentation);
            return documentation?.Trim();
        }

        public string? GetDocumentationFor(FieldInfo fieldInfo)
        {
            if (fieldInfo == null) throw new ArgumentNullException(nameof(fieldInfo));
            var key = "F:" + BuildXmlKey(fieldInfo.DeclaringType!.FullName!, fieldInfo.Name);
            this.XmlDocumentation.TryGetValue(key, out var documentation);
            return documentation?.Trim();
        }

        public string? GetDocumentationFor(EventInfo eventInfo)
        {
            if (eventInfo == null) throw new ArgumentNullException(nameof(eventInfo));
            var key = "M:" + BuildXmlKey(eventInfo.DeclaringType!.FullName!, eventInfo.Name);
            this.XmlDocumentation.TryGetValue(key, out var documentation);
            return documentation?.Trim();
        }

        public string? GetDocumentationFor(PropertyInfo propertyInfo)
        {
            if (propertyInfo == null) throw new ArgumentNullException(nameof(propertyInfo));
            var key = "P:" + BuildXmlKey(propertyInfo.DeclaringType!.FullName!, propertyInfo.Name);
            this.XmlDocumentation.TryGetValue(key, out var documentation);
            return documentation?.Trim();
        }

        public string? GetDocumentationFor(ParameterInfo parameterInfo)
        {
            if (parameterInfo == null) throw new ArgumentNullException(nameof(parameterInfo));
            var memberDocumentation = this.GetDocumentationFor((dynamic)parameterInfo.Member);
            if (memberDocumentation != null)
            {
                var regexPattern = Regex.Escape(@"<param name=" + "\"" + parameterInfo.Name + "\"" + @">") + ".*?" + Regex.Escape(@"</param>");
                Match match = Regex.Match(memberDocumentation, regexPattern);
                if (match.Success) return match.Value?.Trim();
            }
            return null;
        }

        private static string BuildXmlKey(string fullTypeName, string? memberName = null)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName)) throw new ArgumentNullException(nameof(fullTypeName));
            var key = Regex.Replace(fullTypeName, @"\[.*\]", string.Empty).Replace('+', '.');
            if (memberName != null) key += "." + memberName;
            return key;
        }

        private static string BuildXmlKeyFor(MethodBase methodBase)
        {
            var key = BuildXmlKey(methodBase.DeclaringType!.FullName!, methodBase.Name);
            if (methodBase.IsGenericMethod) key += $"`{methodBase.GetGenericArguments().Length}";
            if (methodBase is ConstructorInfo) key = key.Replace("..ctor", ".#ctor");
            key += $"({string.Join(",", methodBase.GetParameters().Select(p => BuildXmlKey(p.ParameterType.FullName!)))})";
            return key;
        }

        public static AssemblyXmlDocumentationContainer BuildFor(Assembly assembly)
        {
            if (assembly == null)throw new ArgumentNullException(nameof(assembly));
            var container = new AssemblyXmlDocumentationContainer();
            var file = $"{new Uri(assembly.Location).LocalPath[..^3]}xml";
            if (!File.Exists(file)) return container;
            var xmlDocumentation = File.ReadAllText(file);
            using (XmlReader xmlReader = XmlReader.Create(new StringReader(xmlDocumentation)))
            {
                while (xmlReader.Read())
                {
                    if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "member")
                    {
                        string xmlName = xmlReader["name"]!;
                        container.XmlDocumentation[xmlName] = xmlReader.ReadOuterXml();
                    }
                }
            }
            return container;
        }

    }

}
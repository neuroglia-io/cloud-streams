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

using System.Reflection;

namespace CloudStreams.Core;

/// <summary>
/// Acts as a helper class for locating <see cref="Assembly"/> instances
/// </summary>
public static class AssemblyLocator
{

    private static readonly object _Lock = new();

    private static readonly List<Assembly> _LoadedAssemblies = new();

    static AssemblyLocator()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            _LoadedAssemblies.Add(assembly);
        }
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
    }

    /// <summary>
    /// Get all loaded assemlies
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> of all loaded assemblies</returns>
    public static IEnumerable<Assembly> GetAssemblies() => _LoadedAssemblies.AsEnumerable();

    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs e)
    {
        lock (_Lock)
        {
            _LoadedAssemblies.Add(e.LoadedAssembly);
        }
    }

}
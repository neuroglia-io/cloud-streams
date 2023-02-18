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
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CloudStreams.Core;


/// <summary>
/// Defines extension methods for <see cref="Type"/>s
/// </summary>
public static class TypeExtensions
{

    /// <summary>
    /// Gets the type's <see cref="IEnumerable{T}"/> element type
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <returns>The element type</returns>
    public static Type GetEnumerableElementType(this Type extended)
    {
        var enumerableType = extended.GetEnumerableType();
        if (enumerableType == null) return extended;
        return enumerableType.GetGenericArguments()[0];
    }

    /// <summary>
    /// Gets the generic type that derives from the <see cref="IEnumerable"/> type
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <returns>The generic type that derives from the <see cref="IEnumerable"/> type</returns>
    public static Type? GetEnumerableType(this Type extended)
    {
        if (extended == null || extended == typeof(string)) return null;
        if (extended.IsArray) return typeof(IEnumerable<>).MakeGenericType(extended.GetElementType()!);
        if (extended.IsGenericType)
        {
            foreach (Type arg in extended.GetGenericArguments())
            {
                var enumerableType = typeof(IEnumerable<>).MakeGenericType(arg);
                if (enumerableType.IsAssignableFrom(extended)) return enumerableType;
            }
        }
        var interfaces = extended.GetInterfaces();
        if (interfaces != null
            && interfaces.Length > 0)
        {
            foreach (Type @interface in interfaces)
            {
                var enumerableType = @interface.GetEnumerableType();
                if (enumerableType != null) return enumerableType;
            }
        }
        if (extended.BaseType != null && extended.BaseType != typeof(object)) extended.BaseType.GetEnumerableType();
        return null;
    }

    /// <summary>
    /// Gets a boolean indicating whether or not the type is a primitive type (includes value types, <see cref="Guid"/>, <see cref="string"/>, <see cref="DateTime"/> and array types)
    /// </summary>
    /// <param name="extended">The type to check</param>
    /// <returns>A boolean indicating whether or not the type is a primitive type</returns>
    public static bool IsPrimitiveType(this Type extended)
    {
        if (extended.IsValueType) return true;
        if (extended == typeof(Guid) || extended == typeof(DateTime) || extended == typeof(string)
            || extended == typeof(char[]) || extended == typeof(string[]) || extended == typeof(byte[]) || extended == typeof(object[]))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a boolean indicating whether or not the type is an <see cref="IEnumerable"/> type
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <returns>A boolean indicating whether or not the type is an <see cref="IEnumerable"/> type</returns>
    public static bool IsEnumerable(this Type extended) => typeof(IEnumerable).IsAssignableFrom(extended);

    /// <summary>
    /// Gets a boolean indicating whether or not the type is a nullable type
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <returns>A boolean indicating whether or not the type is a nullable type</returns>
    public static bool IsNullable(this Type extended)
    {
        var type = extended;
        do
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) return true;
            type = type.BaseType;
        }
        while (type != null);
        return false;
    }

    /// <summary>
    /// Determines whether or not the type is an anonymous type
    /// </summary>
    /// <param name="type">The extended type</param>
    /// <returns>A boolean indicating whether or not the type is an anonymous type</returns>
    public static bool IsAnonymousType(this Type type)
    {
        return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute), false)
            && type.IsGenericType && type.Name.Contains("AnonymousType")
            && (type.Name.StartsWith("<>", StringComparison.OrdinalIgnoreCase) || type.Name.StartsWith("VB$", StringComparison.OrdinalIgnoreCase))
            && (type.Attributes & TypeAttributes.NotPublic) == TypeAttributes.NotPublic;

    }

    /// <summary>
    /// Gets the nullable type the type inherits from, if any
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <returns>The nullable type the type inherits from, if any</returns>
    public static Type? GetNullableType(this Type extended)
    {
        var type = extended;
        do
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) break;
            type = type.BaseType;
        }
        while (type != null);
        if (type == null) return null;
        return type.GetGenericArguments()[0];
    }

    /// <summary>
    /// Gets the type's default value
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <returns>The type's default value</returns>
    public static object? GetDefaultValue(this Type extended)
    {
        if (extended.IsValueType) return Activator.CreateInstance(extended);
        else return null;
    }

    /// <summary>
    /// Gets the type's generic type of the specified generic type definition
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <param name="genericTypeDefinition">The generic type definition to get the generic type of</param>
    /// <returns>The type's generic type of the specified generic type definition</returns>
    public static Type? GetGenericType(this Type extended, Type genericTypeDefinition)
    {
        Type? baseType, result;
        if (genericTypeDefinition == null) throw new ArgumentNullException(nameof(genericTypeDefinition));
        if (!genericTypeDefinition.IsGenericTypeDefinition) throw new ArgumentException("The specified type is not a generic type definition", nameof(genericTypeDefinition));
        baseType = extended;
        while (baseType != null)
        {
            if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == genericTypeDefinition) return baseType;
            result = baseType.GetInterfaces().Select(i => i.GetGenericType(genericTypeDefinition)).Where(t => t != null).FirstOrDefault();
            if (result != null) return result;
            baseType = baseType.BaseType;
        }
        return null;
    }

    /// <summary>
    /// Gets the type's generic types of the specified generic type definition
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <param name="genericTypeDefinition">The generic type definition to get the generic types of</param>
    /// <returns>A new <see cref="IEnumerable"/> containing the type's generic types of the specified generic type definition</returns>
    public static IEnumerable<Type> GetGenericTypes(this Type extended, Type genericTypeDefinition)
    {
        var results = new List<Type>();
        if (genericTypeDefinition == null) throw new ArgumentNullException(nameof(genericTypeDefinition));
        if (!genericTypeDefinition.IsGenericTypeDefinition) throw new ArgumentException("The specified type is not a generic type definition", nameof(genericTypeDefinition));
        var baseType = extended;
        while (baseType != null)
        {
            if (baseType.IsGenericType
                && baseType.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                results.Add(baseType);
                continue;
            }
            results.AddRange(baseType.GetInterfaces().Select(i => i.GetGenericType(genericTypeDefinition)).Where(t => t != null)!);
            baseType = baseType.BaseType;
        }
        return results;
    }

    /// <summary>
    /// Gets a boolean indicating whether or not the type is a generic implementation of the specified generic type definition
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <param name="genericTypeDefinition">The generic type definition to check</param>
    /// <returns>A boolean indicating whether or not the type is a generic implementation of the specified generic type definition</returns>
    public static bool IsGenericImplementationOf(this Type extended, Type genericTypeDefinition)
    {
        return extended.GetGenericType(genericTypeDefinition) != null;
    }

    /// <summary>
    /// Attempts to get a custom attribute of the specified type
    /// </summary>
    /// <typeparam name="TAttribute">The type of the custom attribute to get</typeparam>
    /// <param name="extended">The extended type</param>
    /// <param name="attribute">The resulting custom attribute</param>
    /// <returns>A boolean indicating whether or not the custom attribute of the specified type could be found</returns>
    public static bool TryGetCustomAttribute<TAttribute>(this Type extended, out TAttribute? attribute)
        where TAttribute : Attribute
    {
        attribute = extended.GetCustomAttribute<TAttribute>();
        return attribute != null;
    }

    /// <summary>
    /// Determines whether or not the type declares the specified <see cref="MemberInfo"/>
    /// </summary>
    /// <param name="extended">The extended type</param>
    /// <param name="member">The <see cref="MemberInfo"/> to check</param>
    /// <returns>A boolean indicating whether or not the type declares the specified <see cref="MemberInfo"/></returns>
    public static bool DeclaresMember(this Type extended, MemberInfo member)
    {
        if (extended == null)throw new ArgumentNullException(nameof(extended));
        if (member == null) throw new ArgumentNullException(nameof(member));
        return member switch
        {
            FieldInfo field => extended.GetField(field.Name) != null,
            PropertyInfo property => extended.GetProperty(property.Name) != null,
            MethodInfo method => extended.GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray()) != null,
            _ => false,
        };
    }

}

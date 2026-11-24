using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DogUtils;

#nullable enable
public static class TypeExts
{
    public static object? InvokeMethodBySig(
        this Type type,
        string name,
        object[]? parameters = null
    )
    {
        return type.InvokeMethodBySig<object>(name, null, parameters);
    }

    public static object? InvokeMethodBySig<T>(
        this Type type,
        string name,
        T? instance = null,
        object[]? parameters = null
    )
        where T : class
    {
        parameters ??= [];

        Type?[] paramTypes = [.. parameters.Select(p => p?.GetType())];

        MethodInfo method =
            type.GetMethodBySig(name, instance == null, paramTypes)
            ?? throw new MissingMethodException(type.FullName, name);

        ParameterInfo[] methodParams = method.GetParameters();
        object?[] args = new object?[methodParams.Length];
        for (int i = 0; i < methodParams.Length; i++)
        {
            if (i < parameters.Length)
                args[i] = parameters[i];
            else if (methodParams[i].HasDefaultValue)
                args[i] = methodParams[i].DefaultValue;
        }

        return method.Invoke(instance, args);
    }

    public static MethodInfo? GetMethodBySig(
        this Type type,
        string name,
        bool isStatic = true,
        Type?[]? parameterTypes = null
    )
    {
        parameterTypes ??= [];

        BindingFlags flags =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.FlattenHierarchy
            | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

        if (parameterTypes.All(t => t is not null))
        {
            MethodInfo? method = type.GetMethod(
                name,
                0,
                flags,
                null,
                [.. parameterTypes.Select(t => t!)],
                []
            );
            if (method is not null)
                return method;
        }

        IEnumerable<MethodInfo> candidates = [];

        foreach (MethodInfo candidate in type.GetMethods(flags))
        {
            if (candidate.Name != name)
                continue;

            ParameterInfo[] candidateParams = candidate.GetParameters();
            if (candidateParams.Length < parameterTypes.Length)
                continue;

            bool isValid = true;
            for (int i = 0; i < candidateParams.Length; i++)
            {
                if (i >= parameterTypes.Length)
                {
                    if (!candidateParams[i].HasDefaultValue)
                    {
                        isValid = false;
                        break;
                    }
                    continue;
                }

                Type candidateType = candidateParams[i].ParameterType;
                if (parameterTypes[i] is null)
                {
                    if (
                        candidateType.IsValueType
                        && Nullable.GetUnderlyingType(candidateType) is null
                    )
                    {
                        isValid = false;
                        break;
                    }
                    continue;
                }

                if (candidateType != parameterTypes[i])
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid)
                candidates = candidates.Append(candidate);
        }

        if (candidates.Count() > 1)
            throw new AmbiguousMatchException();
        if (candidates.Count() == 1)
            return candidates.First();
        return null;
    }
}

public static class DogUtils
{
    public static Assembly? GetLoadedAssembly(string assemblyName)
    {
        return AppDomain
            .CurrentDomain.GetAssemblies()
            .SingleOrDefault(assembly => assembly.GetName().Name == assemblyName);
    }
}

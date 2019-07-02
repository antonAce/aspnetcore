// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.AspNetCore.Components.Reflection
{
    /// <summary>
    /// Resolves components for an application.
    /// </summary>
    public sealed class ComponentResolver
    {
        private Assembly _cachedAssembly;
        private IReadOnlyList<Type> _resolvedComponents;

        public IReadOnlyList<Type> ResolveComponents(Assembly assembly)
        {
            if (_cachedAssembly == assembly)
            {
                return _resolvedComponents;
            }

            var resolvedComponents = ResolveComponentsCore(assembly);

            if (_cachedAssembly is null)
            {
                _resolvedComponents = resolvedComponents;
                _cachedAssembly = assembly;
            }

            return _resolvedComponents;
        }

        private IReadOnlyList<Type> ResolveComponentsCore(Assembly assembly)
        {
            var componentsAssembly = typeof(IComponent).Assembly;

            return EnumerateAssemblies(assembly.GetName(), componentsAssembly, new HashSet<Assembly>())
                .SelectMany(a => a.ExportedTypes)
                .Where(t => typeof(IComponent).IsAssignableFrom(t))
                .ToList();
        }

        private IEnumerable<Assembly> EnumerateAssemblies(
            AssemblyName assemblyName,
            Assembly componentAssembly,
            HashSet<Assembly> visited)
        {
            var assembly = Assembly.Load(assemblyName);
            if (visited.Contains(assembly))
            {
                // Avoid traversing visited assemblies.
                yield break;
            }
            visited.Add(assembly);
            var references = assembly.GetReferencedAssemblies();
            if (!references.Any(r => string.Equals(r.FullName, componentAssembly.FullName, StringComparison.Ordinal)))
            {
                // Avoid traversing references that don't point to Components (like netstandard2.0)
                yield break;
            }
            else
            {
                yield return assembly;

                // Look at the list of transitive dependencies for more components.
                foreach (var reference in references.SelectMany(r => EnumerateAssemblies(r, componentAssembly, visited)))
                {
                    yield return reference;
                }
            }
        }

        private class AssemblyComparer : IEqualityComparer<Assembly>
        {
            public bool Equals(Assembly x, Assembly y)
            {
                return string.Equals(x?.FullName, y?.FullName, StringComparison.Ordinal);
            }

            public int GetHashCode(Assembly obj)
            {
                return obj.FullName.GetHashCode();
            }
        }
    }
}

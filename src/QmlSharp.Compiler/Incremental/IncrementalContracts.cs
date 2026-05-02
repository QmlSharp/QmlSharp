#pragma warning disable MA0048

namespace QmlSharp.Compiler
{
    /// <summary>
    /// Provides incremental compilation with dirty-file and dependency tracking.
    /// </summary>
    public interface IIncrementalCompiler
    {
        /// <summary>Compiles dirty files and returns cached plus fresh units.</summary>
        CompilationResult CompileIncremental(ProjectContext context, CompilerOptions options);

        /// <summary>Gets dirty file paths for the current context.</summary>
        ImmutableArray<string> GetDirtyFiles(ProjectContext context);

        /// <summary>Gets the current dependency graph.</summary>
        DependencyGraph GetDependencyGraph();

        /// <summary>Invalidates file paths and dependent files.</summary>
        void Invalidate(ImmutableArray<string> filePaths);

        /// <summary>Clears all cached state.</summary>
        void ClearCache();

        /// <summary>Saves cache state to disk.</summary>
        void SaveCache(string cacheDir);

        /// <summary>Loads cache state from disk.</summary>
        void LoadCache(string cacheDir);
    }

    /// <summary>
    /// Tracks View to ViewModel dependencies and file content hashes.
    /// </summary>
    public sealed class DependencyGraph
    {
        private readonly Dictionary<string, SortedSet<string>> dependentsByViewModel = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SortedSet<string>> dependenciesByViewFile = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> contentHashesByFile = new(StringComparer.Ordinal);

        /// <summary>Gets files depending on a ViewModel class.</summary>
        public ImmutableArray<string> GetDependentsOf(string viewModelClassName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(viewModelClassName);

            if (!dependentsByViewModel.TryGetValue(viewModelClassName, out SortedSet<string>? dependents))
            {
                return ImmutableArray<string>.Empty;
            }

            return dependents.ToImmutableArray();
        }

        /// <summary>Gets ViewModel class names a View file depends on.</summary>
        public ImmutableArray<string> GetDependenciesOf(string viewFilePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(viewFilePath);

            if (!dependenciesByViewFile.TryGetValue(viewFilePath, out SortedSet<string>? dependencies))
            {
                return ImmutableArray<string>.Empty;
            }

            return dependencies.ToImmutableArray();
        }

        /// <summary>Registers a View file to ViewModel dependency.</summary>
        public void AddDependency(string viewFilePath, string viewModelClassName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(viewFilePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(viewModelClassName);

            if (!dependenciesByViewFile.TryGetValue(viewFilePath, out SortedSet<string>? dependencies))
            {
                dependencies = new SortedSet<string>(StringComparer.Ordinal);
                dependenciesByViewFile.Add(viewFilePath, dependencies);
            }

            if (!dependentsByViewModel.TryGetValue(viewModelClassName, out SortedSet<string>? dependents))
            {
                dependents = new SortedSet<string>(StringComparer.Ordinal);
                dependentsByViewModel.Add(viewModelClassName, dependents);
            }

            _ = dependencies.Add(viewModelClassName);
            _ = dependents.Add(viewFilePath);
        }

        /// <summary>Gets all tracked View file paths in deterministic order.</summary>
        public ImmutableArray<string> GetViewFiles()
        {
            return dependenciesByViewFile.Keys
                .Order(StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <summary>Gets all tracked file content hashes in deterministic order.</summary>
        public ImmutableArray<KeyValuePair<string, string>> GetContentHashes()
        {
            return contentHashesByFile
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        /// <summary>Gets a cached content hash for a file.</summary>
        public string? GetContentHash(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            return contentHashesByFile.TryGetValue(filePath, out string? hash) ? hash : null;
        }

        /// <summary>Sets a cached content hash for a file.</summary>
        public void SetContentHash(string filePath, string hash)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(hash);
            contentHashesByFile[filePath] = hash;
        }

        /// <summary>Removes a cached content hash for a file.</summary>
        public void RemoveContentHash(string filePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
            _ = contentHashesByFile.Remove(filePath);
        }

        /// <summary>Removes all dependency and hash state.</summary>
        public void Clear()
        {
            dependentsByViewModel.Clear();
            dependenciesByViewFile.Clear();
            contentHashesByFile.Clear();
        }

        /// <summary>Removes all View to ViewModel dependency edges.</summary>
        public void ClearDependencies()
        {
            dependentsByViewModel.Clear();
            dependenciesByViewFile.Clear();
        }
    }
}

#pragma warning restore MA0048

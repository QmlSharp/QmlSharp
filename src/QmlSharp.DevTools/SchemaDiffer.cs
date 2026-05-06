using QmlSharp.Compiler;

namespace QmlSharp.DevTools
{
    /// <summary>
    /// Compares compiler schemas and identifies changes that require a full dev-server restart.
    /// </summary>
    internal sealed class SchemaDiffer
    {
        private static readonly SchemaDiffResult NoChanges = new(
            HasStructuralChanges: false,
            AffectedViewModels: ImmutableArray<string>.Empty,
            Reasons: ImmutableArray<string>.Empty);

        public SchemaDiffResult Compare(CompilationResult? previous, CompilationResult current)
        {
            ArgumentNullException.ThrowIfNull(current);

            if (previous is null)
            {
                return NoChanges;
            }

            ImmutableArray<ViewModelSchema> previousSchemas = ExtractSchemas(previous);
            return previousSchemas.IsEmpty
                ? NoChanges
                : Compare(previousSchemas, ExtractSchemas(current));
        }

        public SchemaDiffResult Compare(
            IReadOnlyList<ViewModelSchema> previous,
            IReadOnlyList<ViewModelSchema> current)
        {
            ArgumentNullException.ThrowIfNull(previous);
            ArgumentNullException.ThrowIfNull(current);

            Dictionary<string, ViewModelSchema> previousByKey = IndexSchemas(previous);
            Dictionary<string, ViewModelSchema> currentByKey = IndexSchemas(current);
            ImmutableSortedSet<string>.Builder affected =
                ImmutableSortedSet.CreateBuilder<string>(StringComparer.Ordinal);
            ImmutableArray<string>.Builder reasons = ImmutableArray.CreateBuilder<string>();

            foreach (string key in previousByKey.Keys.OrderBy(static value => value, StringComparer.Ordinal))
            {
                if (!currentByKey.TryGetValue(key, out ViewModelSchema? currentSchema))
                {
                    ViewModelSchema previousSchema = previousByKey[key];
                    AddReason(affected, reasons, previousSchema.ClassName, "ViewModel removed: " + previousSchema.ClassName);
                    continue;
                }

                CompareSchema(previousByKey[key], currentSchema, affected, reasons);
            }

            foreach (string key in currentByKey.Keys.OrderBy(static value => value, StringComparer.Ordinal))
            {
                if (previousByKey.ContainsKey(key))
                {
                    continue;
                }

                ViewModelSchema currentSchema = currentByKey[key];
                AddReason(affected, reasons, currentSchema.ClassName, "ViewModel added: " + currentSchema.ClassName);
            }

            if (reasons.Count == 0)
            {
                return NoChanges;
            }

            return new SchemaDiffResult(
                HasStructuralChanges: true,
                affected.ToImmutableArray(),
                reasons.ToImmutable());
        }

        private static ImmutableArray<ViewModelSchema> ExtractSchemas(CompilationResult result)
        {
            return result.Units
                .Where(static unit => unit.Schema is not null)
                .Select(static unit => unit.Schema!)
                .OrderBy(static schema => SchemaKey(schema), StringComparer.Ordinal)
                .ThenBy(static schema => schema.ClassName, StringComparer.Ordinal)
                .ToImmutableArray();
        }

        private static Dictionary<string, ViewModelSchema> IndexSchemas(IReadOnlyList<ViewModelSchema> schemas)
        {
            Dictionary<string, ViewModelSchema> indexed = new(StringComparer.Ordinal);
            foreach (ViewModelSchema schema in schemas.OrderBy(static value => SchemaKey(value), StringComparer.Ordinal))
            {
                indexed[SchemaKey(schema)] = schema;
            }

            return indexed;
        }

        private static void CompareSchema(
            ViewModelSchema previous,
            ViewModelSchema current,
            ImmutableSortedSet<string>.Builder affected,
            ImmutableArray<string>.Builder reasons)
        {
            string viewModel = !string.IsNullOrWhiteSpace(current.ClassName)
                ? current.ClassName
                : previous.ClassName;

            CompareScalar(previous.SchemaVersion, current.SchemaVersion, viewModel, "schema version", affected, reasons);
            CompareScalar(previous.ClassName, current.ClassName, viewModel, "class name", affected, reasons);
            CompareScalar(previous.ModuleName, current.ModuleName, viewModel, "module name", affected, reasons);
            CompareScalar(previous.ModuleUri, current.ModuleUri, viewModel, "module URI", affected, reasons);
            if (previous.ModuleVersion != current.ModuleVersion)
            {
                AddReason(
                    affected,
                    reasons,
                    viewModel,
                    viewModel + " module version changed: " + previous.ModuleVersion + " -> " + current.ModuleVersion);
            }

            CompareStateEntries(previous, current, affected, reasons);
            CompareCommandEntries(previous, current, affected, reasons);
            CompareEffectEntries(previous, current, affected, reasons);
        }

        private static void CompareStateEntries(
            ViewModelSchema previous,
            ViewModelSchema current,
            ImmutableSortedSet<string>.Builder affected,
            ImmutableArray<string>.Builder reasons)
        {
            Dictionary<string, StateEntry> previousByName = previous.Properties
                .ToDictionary(static property => property.Name, StringComparer.Ordinal);
            Dictionary<string, StateEntry> currentByName = current.Properties
                .ToDictionary(static property => property.Name, StringComparer.Ordinal);

            CompareMembers(
                previous.ClassName,
                current.ClassName,
                previousByName.Keys,
                currentByName.Keys,
                "state property",
                affected,
                reasons);

            foreach (string name in previousByName.Keys.Intersect(currentByName.Keys, StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal))
            {
                StateEntry previousEntry = previousByName[name];
                StateEntry currentEntry = currentByName[name];
                if (!StringComparer.Ordinal.Equals(previousEntry.Type, currentEntry.Type))
                {
                    AddReason(
                        affected,
                        reasons,
                        current.ClassName,
                        current.ClassName + " state property type changed: " + name);
                }

                if (previousEntry.ReadOnly != currentEntry.ReadOnly)
                {
                    AddReason(
                        affected,
                        reasons,
                        current.ClassName,
                        current.ClassName + " state property writability changed: " + name);
                }
            }
        }

        private static void CompareCommandEntries(
            ViewModelSchema previous,
            ViewModelSchema current,
            ImmutableSortedSet<string>.Builder affected,
            ImmutableArray<string>.Builder reasons)
        {
            Dictionary<string, CommandEntry> previousByName = previous.Commands
                .ToDictionary(static command => command.Name, StringComparer.Ordinal);
            Dictionary<string, CommandEntry> currentByName = current.Commands
                .ToDictionary(static command => command.Name, StringComparer.Ordinal);

            CompareMembers(
                previous.ClassName,
                current.ClassName,
                previousByName.Keys,
                currentByName.Keys,
                "command",
                affected,
                reasons);

            foreach (string name in previousByName.Keys.Intersect(currentByName.Keys, StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal)
                .Where(name => !ParametersEqual(previousByName[name].Parameters, currentByName[name].Parameters)))
            {
                AddReason(
                    affected,
                    reasons,
                    current.ClassName,
                    current.ClassName + " command parameters changed: " + name);
            }
        }

        private static void CompareEffectEntries(
            ViewModelSchema previous,
            ViewModelSchema current,
            ImmutableSortedSet<string>.Builder affected,
            ImmutableArray<string>.Builder reasons)
        {
            Dictionary<string, EffectEntry> previousByName = previous.Effects
                .ToDictionary(static effect => effect.Name, StringComparer.Ordinal);
            Dictionary<string, EffectEntry> currentByName = current.Effects
                .ToDictionary(static effect => effect.Name, StringComparer.Ordinal);

            CompareMembers(
                previous.ClassName,
                current.ClassName,
                previousByName.Keys,
                currentByName.Keys,
                "effect",
                affected,
                reasons);

            foreach (string name in previousByName.Keys.Intersect(currentByName.Keys, StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal))
            {
                EffectEntry previousEntry = previousByName[name];
                EffectEntry currentEntry = currentByName[name];
                if (!StringComparer.Ordinal.Equals(previousEntry.PayloadType, currentEntry.PayloadType))
                {
                    AddReason(
                        affected,
                        reasons,
                        current.ClassName,
                        current.ClassName + " effect payload changed: " + name);
                }

                if (!ParametersEqual(previousEntry.Parameters, currentEntry.Parameters))
                {
                    AddReason(
                        affected,
                        reasons,
                        current.ClassName,
                        current.ClassName + " effect parameters changed: " + name);
                }
            }
        }

        private static void CompareMembers(
            string previousViewModel,
            string currentViewModel,
            IEnumerable<string> previousNames,
            IEnumerable<string> currentNames,
            string memberKind,
            ImmutableSortedSet<string>.Builder affected,
            ImmutableArray<string>.Builder reasons)
        {
            ImmutableSortedSet<string> previousSet = previousNames.ToImmutableSortedSet(StringComparer.Ordinal);
            ImmutableSortedSet<string> currentSet = currentNames.ToImmutableSortedSet(StringComparer.Ordinal);

            foreach (string removed in previousSet.Except(currentSet))
            {
                AddReason(
                    affected,
                    reasons,
                    previousViewModel,
                    previousViewModel + " " + memberKind + " removed: " + removed);
            }

            foreach (string added in currentSet.Except(previousSet))
            {
                AddReason(
                    affected,
                    reasons,
                    currentViewModel,
                    currentViewModel + " " + memberKind + " added: " + added);
            }
        }

        private static void CompareScalar(
            string previous,
            string current,
            string viewModel,
            string label,
            ImmutableSortedSet<string>.Builder affected,
            ImmutableArray<string>.Builder reasons)
        {
            if (StringComparer.Ordinal.Equals(previous, current))
            {
                return;
            }

            AddReason(affected, reasons, viewModel, viewModel + " " + label + " changed: " + previous + " -> " + current);
        }

        private static bool ParametersEqual(
            ImmutableArray<ParameterEntry> previous,
            ImmutableArray<ParameterEntry> current)
        {
            ImmutableArray<ParameterEntry> normalizedPrevious = previous.IsDefault
                ? ImmutableArray<ParameterEntry>.Empty
                : previous;
            ImmutableArray<ParameterEntry> normalizedCurrent = current.IsDefault
                ? ImmutableArray<ParameterEntry>.Empty
                : current;
            if (normalizedPrevious.Length != normalizedCurrent.Length)
            {
                return false;
            }

            for (int index = 0; index < normalizedPrevious.Length; index++)
            {
                ParameterEntry previousEntry = normalizedPrevious[index];
                ParameterEntry currentEntry = normalizedCurrent[index];
                if (!StringComparer.Ordinal.Equals(previousEntry.Name, currentEntry.Name) ||
                    !StringComparer.Ordinal.Equals(previousEntry.Type, currentEntry.Type))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddReason(
            ImmutableSortedSet<string>.Builder affected,
            ImmutableArray<string>.Builder reasons,
            string viewModel,
            string reason)
        {
            _ = affected.Add(viewModel);
            reasons.Add(reason);
        }

        private static string SchemaKey(ViewModelSchema schema)
        {
            return string.IsNullOrWhiteSpace(schema.CompilerSlotKey)
                ? schema.ClassName
                : schema.CompilerSlotKey;
        }
    }
}

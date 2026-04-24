using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace QmlSharp.Registry.Tests.Contracts
{
    public sealed class RegistryModuleClosureTests
    {
        private static readonly ImmutableArray<(string Prefix, int FirstId, int LastId)> ExpectedScenarioRanges =
        [
            ("SCN", 1, 12),
            ("QTP", 1, 30),
            ("QDP", 1, 20),
            ("MTP", 1, 20),
            ("TNM", 1, 31),
            ("NRM", 1, 15),
            ("RQY", 1, 30),
            ("SNP", 1, 9),
            ("BLD", 1, 9),
        ];

        [Fact]
        public void Scenario_prefixed_tests_cover_every_registry_test_spec_id()
        {
            Dictionary<string, ImmutableArray<string>> scenarioIdsByPrefix = GetScenarioIdsByPrefix();

            foreach ((string prefix, int firstId, int lastId) in ExpectedScenarioRanges)
            {
                Assert.True(
                    scenarioIdsByPrefix.TryGetValue(prefix, out ImmutableArray<string> actualIds),
                    $"No scenario-prefixed tests were discovered for suite '{prefix}'.");

                string[] expectedIds = Enumerable.Range(firstId, (lastId - firstId) + 1)
                    .Select(index => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{prefix}-{index:00}"))
                    .ToArray();

                Assert.Equal(expectedIds, actualIds.ToArray());
            }

            int totalScenarioCount = scenarioIdsByPrefix.Values.Sum(ids => ids.Length);
            Assert.Equal(176, totalScenarioCount);
        }

        [Fact]
        public void Public_contract_namespaces_match_the_registry_architecture_layout()
        {
            string[] expectedNamespaces =
            [
                "QmlSharp.Registry",
                "QmlSharp.Registry.Diagnostics",
                "QmlSharp.Registry.Normalization",
                "QmlSharp.Registry.Parsing",
                "QmlSharp.Registry.Querying",
                "QmlSharp.Registry.Scanning",
                "QmlSharp.Registry.Snapshots",
            ];

            string[] actualNamespaces = typeof(QmlRegistry).Assembly
                .GetExportedTypes()
                .Select(type => type.Namespace)
                .Where(@namespace => !string.IsNullOrWhiteSpace(@namespace))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(@namespace => @namespace, StringComparer.Ordinal)
                .ToArray()!;

            Assert.Equal(expectedNamespaces, actualNamespaces);
        }

        [Fact]
        public void Public_contract_properties_are_init_only_or_read_only_and_avoid_mutable_collection_shapes()
        {
            Type[] exportedTypes = typeof(QmlRegistry).Assembly
                .GetExportedTypes()
                .Where(type => type.IsClass && !type.IsInterface && !type.IsEnum)
                .ToArray();

            foreach (Type type in exportedTypes)
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (property.SetMethod is not null)
                    {
                        Assert.True(
                            IsInitOnly(property.SetMethod),
                            $"{type.FullName}.{property.Name} must be init-only or read-only.");
                    }

                    Assert.False(
                        UsesMutableCollectionShape(property.PropertyType),
                        $"{type.FullName}.{property.Name} uses mutable collection type '{property.PropertyType}'.");
                }
            }
        }

        private static Dictionary<string, ImmutableArray<string>> GetScenarioIdsByPrefix()
        {
            Regex scenarioPattern = new Regex(
                @"^(?<prefix>[A-Z]{3})_(?<id>\d{2})_",
                RegexOptions.CultureInvariant | RegexOptions.Compiled);

            return typeof(RegistryModuleClosureTests).Assembly
                .GetTypes()
                .Where(type => string.Equals(type.Namespace, "QmlSharp.Registry.Tests", StringComparison.Ordinal)
                    || type.Namespace?.StartsWith("QmlSharp.Registry.Tests.", StringComparison.Ordinal) == true)
                .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
                .Where(IsTestMethod)
                .Select(method => scenarioPattern.Match(method.Name))
                .Where(match => match.Success)
                .Select(match => new
                {
                    Prefix = match.Groups["prefix"].Value,
                    ScenarioId = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{match.Groups["prefix"].Value}-{match.Groups["id"].Value}"),
                })
                .GroupBy(item => item.Prefix, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .Select(item => item.ScenarioId)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToImmutableArray(),
                    StringComparer.Ordinal);
        }

        private static bool IsInitOnly(MethodInfo setMethod)
        {
            return setMethod.ReturnParameter
                .GetRequiredCustomModifiers()
                .Contains(typeof(IsExternalInit));
        }

        private static bool IsTestMethod(MethodInfo method)
        {
            return method.GetCustomAttributes(inherit: true)
                .Any(attribute => attribute is FactAttribute);
        }

        private static bool UsesMutableCollectionShape(Type propertyType)
        {
            if (propertyType == typeof(string))
            {
                return false;
            }

            if (propertyType.IsArray)
            {
                return true;
            }

            if (!propertyType.IsGenericType)
            {
                return false;
            }

            Type genericTypeDefinition = propertyType.GetGenericTypeDefinition();

            return genericTypeDefinition == typeof(List<>)
                || genericTypeDefinition == typeof(Dictionary<,>)
                || genericTypeDefinition == typeof(HashSet<>)
                || genericTypeDefinition == typeof(ICollection<>)
                || genericTypeDefinition == typeof(IList<>)
                || genericTypeDefinition == typeof(IDictionary<,>);
        }
    }
}

using System.Collections.Immutable;
using QmlSharp.Dsl.Tests.Fixtures;
using QmlSharp.Qml.Ast;

#pragma warning disable IDE0058

namespace QmlSharp.Dsl.Tests.Runtime
{
    public sealed class PropertyCollectorTests
    {
        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void SetProperty_RecordsValueEntriesInInsertionOrder()
        {
            PropertyCollector collector = new();

            IPropertyCollector result = collector.SetProperty("width", 2).SetProperty("color", "black");

            Assert.Same(collector, result);
            Assert.Collection(
                collector.Entries,
                entry =>
                {
                    Assert.Equal("width", entry.PropertyName);
                    Assert.IsType<NumberLiteral>(entry.Value);
                },
                entry =>
                {
                    Assert.Equal("color", entry.PropertyName);
                    Assert.IsType<StringLiteral>(entry.Value);
                });
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void SetBinding_RecordsExpressionEntry()
        {
            PropertyCollector collector = new();

            collector.SetBinding("width", "parent.borderWidth");

            PropertyCollectionEntry entry = Assert.Single(collector.Entries);
            Assert.Equal("width", entry.PropertyName);
            ScriptExpression expression = Assert.IsType<ScriptExpression>(entry.Value);
            Assert.Equal("parent.borderWidth", expression.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void HandleSignal_RecordsSignalExpressionEntry()
        {
            PropertyCollector collector = new();

            collector.HandleSignal("onPressed", "event.accepted = true");

            PropertyCollectionEntry entry = Assert.Single(collector.Entries);
            Assert.Equal("onPressed", entry.PropertyName);
            ScriptExpression expression = Assert.IsType<ScriptExpression>(entry.Value);
            Assert.Equal("event.accepted = true", expression.Code);
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedCollectorProxy_MapsGeneratedPropertyAndBindMethods()
        {
            PropertyCollectorMetadata metadata = new(
                ImmutableArray.Create(
                    new PropertyMethodMetadata("Width", "width"),
                    new PropertyMethodMetadata("Color", "color")),
                ImmutableArray<SignalMethodMetadata>.Empty);
            IBorderCollector collector = PropertyCollectorFactory.Create<IBorderCollector>(metadata);

            IBorderCollector result = collector.Width(2).ColorBind("theme.borderColor");

            Assert.Same(collector, result);
            Assert.Collection(
                collector.Entries,
                entry =>
                {
                    Assert.Equal("width", entry.PropertyName);
                    Assert.IsType<NumberLiteral>(entry.Value);
                },
                entry =>
                {
                    Assert.Equal("color", entry.PropertyName);
                    Assert.IsType<ScriptExpression>(entry.Value);
                });
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedCollectorProxy_ThrowsForUnknownMethodWhenMetadataIsRestricted()
        {
            PropertyCollectorMetadata metadata = new(
                ImmutableArray.Create(new PropertyMethodMetadata("Width", "width")),
                ImmutableArray<SignalMethodMetadata>.Empty);
            IBorderCollector collector = PropertyCollectorFactory.Create<IBorderCollector>(metadata);

            Assert.Throws<MissingMethodException>(() => collector.Color("black"));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedCollectorProxy_SupportsSignalHandlersOnAttachedSurfaces()
        {
            PropertyCollectorMetadata metadata = new(
                ImmutableArray.Create(new PropertyMethodMetadata("Enabled", "enabled")),
                ImmutableArray.Create(new SignalMethodMetadata("OnPressed", "onPressed")));
            IKeysCollector collector = PropertyCollectorFactory.Create<IKeysCollector>(metadata);

            collector.Enabled(true).OnPressed("event.accepted = true");

            Assert.Collection(
                collector.Entries,
                entry => Assert.Equal("enabled", entry.PropertyName),
                entry => Assert.Equal("onPressed", entry.PropertyName));
        }

        [Fact]
        [Trait("Category", TestCategories.Runtime)]
        public void GeneratedCollectorProxy_TreatsOnPrefixedStringPropertyAsPropertyWhenMetadataMatches()
        {
            PropertyCollectorMetadata metadata = new(
                ImmutableArray.Create(new PropertyMethodMetadata("OnboardingText", "onboardingText")),
                ImmutableArray<SignalMethodMetadata>.Empty);
            IOnPrefixedCollector collector = PropertyCollectorFactory.Create<IOnPrefixedCollector>(metadata);

            collector.OnboardingText("Welcome");

            PropertyCollectionEntry entry = Assert.Single(collector.Entries);
            Assert.Equal("onboardingText", entry.PropertyName);
            Assert.Equal("Welcome", Assert.IsType<StringLiteral>(entry.Value).Value);
        }

        private interface IBorderCollector : IPropertyCollector
        {
            IBorderCollector Width(int value);

            IBorderCollector Color(string value);

            IBorderCollector ColorBind(string expression);
        }

        private interface IKeysCollector : IPropertyCollector
        {
            IKeysCollector Enabled(bool value);

            IKeysCollector OnPressed(string body);
        }

        private interface IOnPrefixedCollector : IPropertyCollector
        {
            IOnPrefixedCollector OnboardingText(string value);
        }
    }
}

#pragma warning restore IDE0058

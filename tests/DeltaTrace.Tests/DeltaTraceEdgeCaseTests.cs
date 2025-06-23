using DeltaTrace;
using TUnit.Assertions;
using TUnit.Core;

namespace DeltaTrace.Tests;

public class DeltaTraceEdgeCaseTests
{
    private readonly DeltaTrace _generator = new();

    [Test]
    public async Task Generator_HandlesComplexPropertyTypes()
    {
        const string source = @"
using DeltaTrace;
using System;
using System.Collections.Generic;

namespace TestNamespace
{
    [DeltaTrace]
    public class ComplexModel
    {
        public Dictionary<string, int> Mappings { get; set; }
        public List<string> Tags { get; set; }
        public int[] Numbers { get; set; }
        public Guid? NullableGuid { get; set; }
        public DateTime? NullableDate { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ComplexModelDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();

        var generatedSource = deltaFile!.GetText().ToString();
        await Assert.That(generatedSource).Contains("PropertyDelta<Dictionary<string, int>> Mappings");
        await Assert.That(generatedSource).Contains("PropertyDelta<List<string>> Tags");
        await Assert.That(generatedSource).Contains("PropertyDelta<int[]> Numbers");
        await Assert.That(generatedSource).Contains("PropertyDelta<Guid?> NullableGuid");
    }

    [Test]
    public async Task Generator_HandlesPrivateAndProtectedProperties()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class AccessibilityTest
    {
        public string PublicProp { get; set; }
        internal string InternalProp { get; set; }
        protected string ProtectedProp { get; set; }
        private string PrivateProp { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("AccessibilityTestDelta.g.cs"));
        var generatedSource = deltaFile!.GetText().ToString();

        // Only public properties should be included
        await Assert.That(generatedSource).Contains("PublicProp");
        await Assert.That(generatedSource).DoesNotContain("InternalProp");
        await Assert.That(generatedSource).DoesNotContain("ProtectedProp");
        await Assert.That(generatedSource).DoesNotContain("PrivateProp");
    }

    [Test]
    public async Task Generator_HandlesStaticAndIndexerProperties()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class SpecialProperties
    {
        public string Name { get; set; }
        public static string StaticProp { get; set; }
        public string this[int index] => Name;
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("SpecialPropertiesDelta.g.cs"));
        var generatedSource = deltaFile!.GetText().ToString();

        // Only instance, non-indexer properties should be included
        await Assert.That(generatedSource).Contains("Name");
        await Assert.That(generatedSource).DoesNotContain("StaticProp");
        await Assert.That(generatedSource).DoesNotContain("this[");
    }

    [Test]
    public async Task Generator_HandlesNestedTypes()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    public class OuterClass
    {
        [DeltaTrace]
        public class NestedModel
        {
            public string Value { get; set; }
        }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("NestedModelDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();
    }

    [Test]
    public async Task Generator_HandlesGenericTypes()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class GenericModel<T>
    {
        public T Value { get; set; }
        public string Name { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        // Generic types should be handled
        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.Contains("GenericModel"));
        await Assert.That(deltaFile).IsNotNull();
    }

    [Test]
    public async Task Generator_HandlesReadOnlyProperties()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class ReadOnlyModel
    {
        public string Name { get; }
        public int ComputedValue => 42;
        public string FullName { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ReadOnlyModelDelta.g.cs"));
        var generatedSource = deltaFile!.GetText().ToString();

        // Properties with getters should be included
        await Assert.That(generatedSource).Contains("Name");
        await Assert.That(generatedSource).Contains("ComputedValue");
        await Assert.That(generatedSource).Contains("FullName");
    }

    [Test]
    public async Task Generator_HandlesCircularReferences()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class Parent
    {
        public string Name { get; set; }
        public Child Child { get; set; }
    }

    [DeltaTrace]
    public class Child
    {
        public string Name { get; set; }
        public Parent Parent { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        // Should handle circular references without infinite loops
        var parentDelta = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ParentDelta.g.cs"));
        var childDelta = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ChildDelta.g.cs"));

        await Assert.That(parentDelta).IsNotNull();
        await Assert.That(childDelta).IsNotNull();
    }
}
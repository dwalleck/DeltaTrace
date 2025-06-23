using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Core;
using DeltaTrace;

namespace DeltaTrace.Tests;

public class DeltaTraceTests
{
    private readonly DeltaTraceGenerator _generator = new();

    [Test]
    public async Task Generator_WhenNoDeltaTraceAttribute_GeneratesNothing()
    {
        const string source = @"
namespace TestNamespace
{
    public class TestClass
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(_generator, source);

        // With IIncrementalGenerator, base classes are always generated
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        
        // But no delta classes should be generated
        var deltaFiles = result.GeneratedTrees.Where(t => t.FilePath.EndsWith("Delta.g.cs")).ToList();
        await Assert.That(deltaFiles.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Generator_WithDeltaTraceAttribute_GeneratesDelta()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class TestModel
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(2);
        
        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();
        
        var generatedSource = deltaFile!.GetText().ToString();
        await Assert.That(generatedSource).Contains("public sealed class TestModelDelta");
        await Assert.That(generatedSource).Contains("public PropertyDelta<string> Name =>");
        await Assert.That(generatedSource).Contains("public PropertyDelta<int> Age =>");
    }

    [Test]
    public async Task Generator_WithIgnoreDeltaAttribute_ExcludesProperty()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class TestModel
    {
        public string Name { get; set; }
        
        [IgnoreDelta]
        public int InternalId { get; set; }
        
        public DateTime LastModified { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();
        
        var generatedSource = deltaFile!.GetText().ToString();
        await Assert.That(generatedSource).Contains("public PropertyDelta<string> Name =>");
        await Assert.That(generatedSource).Contains("public PropertyDelta<DateTime> LastModified =>");
        await Assert.That(generatedSource).DoesNotContain("InternalId");
    }

    [Test]
    public async Task Generator_WithCustomDeltaSuffix_UsesCustomSuffix()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace(DeltaSuffix = ""Diff"")]
    public class TestModel
    {
        public string Name { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var diffFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        await Assert.That(diffFile).IsNotNull();
        
        var generatedSource = diffFile!.GetText().ToString();
        await Assert.That(generatedSource).Contains("public sealed class TestModelDiff");
    }

    [Test]
    public async Task Generator_WithDeepTracking_GeneratesNestedTrackers()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class Person
    {
        public string Name { get; set; }
        public Address Address { get; set; }
    }

    [DeltaTrace]
    public class Address
    {
        public string Street { get; set; }
        public string City { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var personDeltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("PersonDelta.g.cs"));
        await Assert.That(personDeltaFile).IsNotNull();
        
        var generatedSource = personDeltaFile!.GetText().ToString();
        await Assert.That(generatedSource).Contains("public AddressDelta Address =>");
        await Assert.That(generatedSource).Contains("foreach (var delta in Address.GetAllDeltas())");
    }

    [Test]
    public async Task Generator_WithExtensions_GeneratesExtensionMethods()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace(GenerateExtensions = true)]
    public class TestModel
    {
        public string Name { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        var generatedSource = deltaFile!.GetText().ToString();
        
        await Assert.That(generatedSource).Contains("public static class TestModelDeltaExtensions");
        await Assert.That(generatedSource).Contains("public static TestModelDelta GetDelta(this TestModel? previous, TestModel? current)");
        await Assert.That(generatedSource).Contains("public static TestModelDelta GetDeltaFrom(this TestModel? current, TestModel? previous)");
    }

    [Test]
    public async Task Generator_ProducesValidCSharpCode()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        // Check for compilation errors
        var diagnostics = result.Diagnostics;
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        
        await Assert.That(errors.Length).IsEqualTo(0);
    }
}
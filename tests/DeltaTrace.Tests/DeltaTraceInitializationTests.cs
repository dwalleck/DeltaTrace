using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Core;
using DeltaTrace;

namespace DeltaTrace.Tests;

public class DeltaTraceInitializationTests
{
    private readonly DeltaTraceGenerator _generator = new();

    [Test]
    public async Task Generator_InitializesWithCorrectReceiverType()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class TestModel
    {
        public string Name { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        // The generator should produce output when the attribute is present
        await Assert.That(result.GeneratedTrees.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Generator_HandlesEmptyCompilation()
    {
        const string source = "";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(_generator, source);

        // Should not throw and should generate only base classes
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
        await Assert.That(result.Diagnostics.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Generator_HandlesNullSyntaxReceiver()
    {
        const string source = @"
namespace TestNamespace
{
    public class TestClass { }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(_generator, source);

        // Should handle gracefully and generate only base classes
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(1);
    }

    [Test]
    public async Task Generator_CreatesBaseClassesOnlyOnce()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class Model1 { public string Name { get; set; } }
    
    [DeltaTrace]
    public class Model2 { public int Value { get; set; } }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        // Should have base classes + 2 model delta files = 3 total
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(3);
        
        var baseClassFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("DeltaTraceBase.g.cs"));
        await Assert.That(baseClassFile).IsNotNull();
    }

}
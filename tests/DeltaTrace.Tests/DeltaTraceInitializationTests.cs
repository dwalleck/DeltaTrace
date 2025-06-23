using DeltaTrace;
using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Core;

namespace DeltaTrace.Tests;

public class DeltaTraceInitializationTests
{
    private readonly DeltaTrace _generator = new();

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

        // Should not throw and should generate no output
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
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

        // Should handle gracefully
        await Assert.That(result.GeneratedTrees.Length).IsEqualTo(0);
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

    [Test]
    public async Task SyntaxReceiver_CollectsOnlyTypesWithAttributes()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    public class NoAttributeClass { }
    
    [DeltaTrace]
    public class WithAttributeClass { }
    
    public struct NoAttributeStruct { }
    
    [DeltaTrace]
    public struct WithAttributeStruct { }
}";

        var receiver = new DeltaTraceSyntaxReceiver();
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source);
        var root = syntaxTree.GetRoot();

        foreach (var node in root.DescendantNodes())
        {
            receiver.OnVisitSyntaxNode(node);
        }

        // Should collect only types with attribute lists
        await Assert.That(receiver.CandidateTypes.Count).IsEqualTo(2);
        await Assert.That(receiver.CandidateTypes.All(t => t.AttributeLists.Count > 0)).IsTrue();
    }
}
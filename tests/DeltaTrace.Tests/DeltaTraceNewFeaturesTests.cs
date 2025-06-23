using DeltaTrace;
using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Core;

namespace DeltaTrace.Tests;

public class DeltaTraceNewFeaturesTests
{
    private readonly DeltaTrace _generator = new();

    [Test]
    public async Task Generator_CreatesGetDeltasMethod_ReturnsPropertyChangeObjects()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class TestModel
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public bool IsActive { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();
        
        var generatedSource = deltaFile!.GetText().ToString();
        
        // Check that GetDeltas method exists
        await Assert.That(generatedSource).Contains("public IEnumerable<PropertyChange> GetDeltas()");
        
        // Check that it uses ToPropertyChange()
        await Assert.That(generatedSource).Contains("yield return Name.ToPropertyChange();");
        await Assert.That(generatedSource).Contains("yield return Value.ToPropertyChange();");
        await Assert.That(generatedSource).Contains("yield return IsActive.ToPropertyChange();");
    }

    [Test]
    public async Task Generator_CreatesHasDeltaInMethod_ForTypeSafePropertyAccess()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class TestModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public int Age { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();
        
        var generatedSource = deltaFile!.GetText().ToString();
        
        // Check that HasDeltaIn methods exist
        await Assert.That(generatedSource).Contains("public bool HasDeltaIn<TProp>(Func<TestModelDelta, PropertyDelta<TProp>> selector)");
        await Assert.That(generatedSource).Contains("public bool HasDeltaIn(Func<TestModelDelta, IDeltaTracker> selector)");
    }

    [Test]
    public async Task Generator_WithDeepTracking_UsesWithPrefixForNestedDeltas()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace(DeepTracking = true)]
    public class ParentModel
    {
        public string Name { get; set; }
        public ChildModel Child { get; set; }
    }
    
    [DeltaTrace]
    public class ChildModel
    {
        public string Value { get; set; }
        public int Count { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var parentDeltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ParentModelDelta.g.cs"));
        await Assert.That(parentDeltaFile).IsNotNull();
        
        var generatedSource = parentDeltaFile!.GetText().ToString();
        
        // Check for WithPrefix usage in GetDeltas
        await Assert.That(generatedSource).Contains("foreach (var delta in Child.GetDeltas())");
        await Assert.That(generatedSource).Contains("yield return delta.WithPrefix(\"Child\");");
    }

    [Test]
    public async Task Generator_GetRootDeltasMethod_ReturnsOnlyTopLevelChanges()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace(DeepTracking = true)]
    public class ComplexModel
    {
        public string Name { get; set; }
        public NestedModel Nested { get; set; }
        public int Value { get; set; }
    }
    
    [DeltaTrace]
    public class NestedModel
    {
        public string InnerValue { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ComplexModelDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();
        
        var generatedSource = deltaFile!.GetText().ToString();
        
        // Check that GetRootDeltas exists
        await Assert.That(generatedSource).Contains("public IEnumerable<DeltaInfo> GetRootDeltas()");
        
        // Should check HasAnyDeltas for nested types but return the property itself
        await Assert.That(generatedSource).Contains("if (Nested.HasAnyDeltas)");
        await Assert.That(generatedSource).Contains("yield return new DeltaInfo(\"Nested\", _previous?.Nested, _current?.Nested, typeof(NestedModel));");
    }

    [Test]
    public async Task Generator_PropertyDeltaClass_IncludesToPropertyChangeMethod()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class SimpleModel
    {
        public string Text { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var baseClassFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("DeltaTraceBase.g.cs"));
        await Assert.That(baseClassFile).IsNotNull();
        
        var generatedSource = baseClassFile!.GetText().ToString();
        
        // Check PropertyDelta class has ToPropertyChange method
        await Assert.That(generatedSource).Contains("public sealed class PropertyDelta<T>");
        await Assert.That(generatedSource).Contains("public PropertyChange ToPropertyChange()");
    }

    [Test]
    public async Task Generator_PropertyChangeClass_SupportsWithPrefix()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class SimpleModel
    {
        public string Text { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator, 
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var baseClassFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("DeltaTraceBase.g.cs"));
        await Assert.That(baseClassFile).IsNotNull();
        
        var generatedSource = baseClassFile!.GetText().ToString();
        
        // Check PropertyChange class has WithPrefix method
        await Assert.That(generatedSource).Contains("public sealed class PropertyChange");
        await Assert.That(generatedSource).Contains("public PropertyChange WithPrefix(string prefix)");
    }

    [Test]
    public async Task Generator_ImplementsIDeltaTrackerInterface()
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

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        var generatedSource = deltaFile!.GetText().ToString();
        
        // Check that the delta class implements IDeltaTracker
        await Assert.That(generatedSource).Contains("public sealed class TestModelDelta : IDeltaTracker");
        
        // Verify interface members
        await Assert.That(generatedSource).Contains("bool HasAnyDeltas");
        await Assert.That(generatedSource).Contains("IEnumerable<DeltaInfo> GetAllDeltas()");
    }
}
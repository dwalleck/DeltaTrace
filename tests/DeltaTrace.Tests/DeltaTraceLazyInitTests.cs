using DeltaTrace;
using TUnit.Assertions;
using TUnit.Core;

namespace DeltaTrace.Tests;

public class DeltaTraceLazyInitTests
{
    private readonly DeltaTrace _generator = new();

    [Test]
    public async Task Generator_GeneratesLazyInitialization()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class Product
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("ProductDelta.g.cs"));
        await Assert.That(deltaFile).IsNotNull();

        var generatedSource = deltaFile!.GetText().ToString();
        
        // Check for lazy field declarations
        await Assert.That(generatedSource).Contains("private readonly Lazy<PropertyDelta<string>> _nameDelta;");
        await Assert.That(generatedSource).Contains("private readonly Lazy<PropertyDelta<decimal>> _priceDelta;");
        await Assert.That(generatedSource).Contains("private readonly Lazy<PropertyDelta<int>> _stockDelta;");
        
        // Check for lazy initialization in constructor
        await Assert.That(generatedSource).Contains("_nameDelta = new Lazy<PropertyDelta<string>>(() =>");
        await Assert.That(generatedSource).Contains("_priceDelta = new Lazy<PropertyDelta<decimal>>(() =>");
        await Assert.That(generatedSource).Contains("_stockDelta = new Lazy<PropertyDelta<int>>(() =>");
        
        // Check for property accessors using .Value
        await Assert.That(generatedSource).Contains("public PropertyDelta<string> Name => _nameDelta.Value;");
        await Assert.That(generatedSource).Contains("public PropertyDelta<decimal> Price => _priceDelta.Value;");
        await Assert.That(generatedSource).Contains("public PropertyDelta<int> Stock => _stockDelta.Value;");
    }

    [Test]
    public async Task Generator_GeneratesLazyInitializationForNestedTypes()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class Order
    {
        public int Id { get; set; }
        public Customer Customer { get; set; }
    }

    [DeltaTrace]
    public class Customer
    {
        public string Name { get; set; }
        public string Email { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var orderDeltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("OrderDelta.g.cs"));
        await Assert.That(orderDeltaFile).IsNotNull();

        var generatedSource = orderDeltaFile!.GetText().ToString();
        
        // Check for lazy initialization of nested delta tracker
        await Assert.That(generatedSource).Contains("private readonly Lazy<CustomerDelta> _customerDelta;");
        await Assert.That(generatedSource).Contains("_customerDelta = new Lazy<CustomerDelta>(() =>");
        await Assert.That(generatedSource).Contains("new CustomerDelta(_previous?.Customer, _current?.Customer)");
        await Assert.That(generatedSource).Contains("public CustomerDelta Customer => _customerDelta.Value;");
    }

    [Test]
    public async Task Generator_LazyFieldNamesHandlePropertyCasing()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class TestModel
    {
        public string FirstName { get; set; }
        public string lastName { get; set; }
        public string ALLCAPS { get; set; }
        public string _underscore { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("TestModelDelta.g.cs"));
        var generatedSource = deltaFile!.GetText().ToString();
        
        // Check field naming conventions
        await Assert.That(generatedSource).Contains("_firstNameDelta");
        await Assert.That(generatedSource).Contains("_lastNameDelta");
        await Assert.That(generatedSource).Contains("_aLLCAPSDelta");
        await Assert.That(generatedSource).Contains("__underscoreDelta");
    }

    [Test]
    public async Task Generator_LazyInitializationHandlesNullValues()
    {
        const string source = @"
using DeltaTrace;

namespace TestNamespace
{
    [DeltaTrace]
    public class NullableModel
    {
        public string Name { get; set; }
        public int? OptionalValue { get; set; }
    }
}";

        var result = await SourceGeneratorTestHelper.RunGeneratorAsync(
            _generator,
            source,
            typeof(DeltaTrace.DeltaTraceAttribute).Assembly);

        var deltaFile = result.GeneratedTrees.FirstOrDefault(t => t.FilePath.EndsWith("NullableModelDelta.g.cs"));
        var generatedSource = deltaFile!.GetText().ToString();
        
        // Check null handling in lazy initialization
        await Assert.That(generatedSource).Contains("_previous != null ? _previous.Name! : default(string)!");
        await Assert.That(generatedSource).Contains("_current != null ? _current.Name! : default(string)!");
        await Assert.That(generatedSource).Contains("_previous != null ? _previous.OptionalValue! : default(int?)!");
        await Assert.That(generatedSource).Contains("_current != null ? _current.OptionalValue! : default(int?)!");
    }
}
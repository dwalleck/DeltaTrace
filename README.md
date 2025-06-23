# DeltaTrace

A powerful C# source generator that automatically creates change tracking (delta) classes for your data models. Track property changes between object instances with zero runtime reflection and full type safety.

## Features

- 🚀 **Zero Runtime Overhead** - All code is generated at compile time
- 🔍 **Deep Change Tracking** - Automatically tracks changes in nested objects
- 💪 **Type Safe** - Full IntelliSense support and compile-time type checking
- 🎯 **Selective Tracking** - Use attributes to control what gets tracked
- 📦 **NuGet Ready** - Easy to install and use in any .NET project
- ⚡ **Incremental Generation** - Built on modern IIncrementalGenerator for optimal IDE performance

## Quick Start

### 1. Install the Package

```bash
dotnet add package DeltaTrace
```

### 2. Mark Your Classes

```csharp
using DeltaTrace;

[DeltaTrace]
public class Customer
{
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime LastOrderDate { get; set; }
    public Address Address { get; set; }
}

[DeltaTrace]
public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string PostalCode { get; set; }
}
```

### 3. Track Changes

```csharp
// Create two instances to compare
var originalCustomer = GetCustomerFromDatabase();
var modifiedCustomer = GetModifiedCustomer();

// Create a delta tracker
var delta = new CustomerDelta(originalCustomer, modifiedCustomer);

// Check if anything changed
if (delta.HasAnyDeltas)
{
    // Get all changes
    foreach (var change in delta.GetAllDeltas())
    {
        Console.WriteLine($"{change.PropertyPath}: {change.PreviousValue} → {change.CurrentValue}");
    }
}

// Check specific properties
if (delta.Email.HasChanged)
{
    Console.WriteLine($"Email changed from {delta.Email.PreviousValue} to {delta.Email.CurrentValue}");
}

// Check nested properties with deep tracking
if (delta.Address.HasAnyDeltas)
{
    Console.WriteLine("Address has changes");
}
```

## How It Works

DeltaTrace uses C# source generators to create specialized delta tracking classes at compile time. For each class marked with `[DeltaTrace]`, it generates:

1. A `{ClassName}Delta` class that compares two instances
2. Property accessors that return `PropertyDelta<T>` objects
3. Methods to retrieve all changes in various formats
4. Extension methods for easier usage (optional)

## Configuration Options

### DeltaTrace Attribute

Control the generation behavior with attribute properties:

```csharp
[DeltaTrace(
    DeepTracking = true,           // Enable tracking of nested [DeltaTrace] objects (default: true)
    GenerateExtensions = true,      // Generate extension methods (default: true)
    DeltaSuffix = "Delta"          // Suffix for generated class names (default: "Delta")
)]
public class MyModel { }
```

### Ignore Properties

Use `[IgnoreDelta]` to exclude specific properties from tracking:

```csharp
[DeltaTrace]
public class User
{
    public string Username { get; set; }
    
    [IgnoreDelta]
    public string PasswordHash { get; set; }  // This won't be tracked
}
```

## Advanced Usage

### Working with Collections

```csharp
[DeltaTrace]
public class Order
{
    public List<OrderItem> Items { get; set; }
    public Dictionary<string, decimal> Discounts { get; set; }
}

// Collections are tracked as whole values
var delta = new OrderDelta(original, modified);
if (delta.Items.HasChanged)
{
    // The entire list reference changed
    var oldItems = delta.Items.PreviousValue;
    var newItems = delta.Items.CurrentValue;
}
```

### Extension Methods

When `GenerateExtensions = true`, you get convenient extension methods:

```csharp
// Create delta using extension method
var delta = originalCustomer.GetDelta(modifiedCustomer);

// Or fluent style
var delta = modifiedCustomer.GetDeltaFrom(originalCustomer);
```

### Querying Specific Properties

```csharp
// Check if a specific property changed using lambda
if (delta.HasDeltaIn(d => d.Email))
{
    // Email changed
}

// Check nested properties
if (delta.HasDeltaIn(d => d.Address))
{
    // Any address property changed
}
```

### Getting Change Details

```csharp
// Get all changes including nested objects
IEnumerable<DeltaInfo> allChanges = delta.GetAllDeltas();

// Get only root-level changes
IEnumerable<DeltaInfo> rootChanges = delta.GetRootDeltas();

// Get changes as PropertyChange objects (includes path information)
IEnumerable<PropertyChange> changes = delta.GetDeltas();
```

## Real-World Examples

### Audit Logging

```csharp
[DeltaTrace]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int StockLevel { get; set; }
}

public void UpdateProduct(Product original, Product updated, string userId)
{
    var delta = new ProductDelta(original, updated);
    
    if (delta.HasAnyDeltas)
    {
        foreach (var change in delta.GetAllDeltas())
        {
            _auditLog.LogChange(new AuditEntry
            {
                EntityType = "Product",
                EntityId = updated.Id,
                PropertyName = change.PropertyPath,
                OldValue = change.PreviousValue?.ToString(),
                NewValue = change.CurrentValue?.ToString(),
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow
            });
        }
    }
}
```

### Optimistic Concurrency

```csharp
public async Task<bool> TryUpdateCustomer(Customer customer)
{
    var original = await _repository.GetCustomer(customer.Id);
    var delta = new CustomerDelta(original, customer);
    
    if (!delta.HasAnyDeltas)
    {
        return true; // No changes needed
    }
    
    // Only update changed fields
    var updateQuery = BuildPartialUpdate(customer.Id, delta);
    return await _repository.ExecuteUpdate(updateQuery);
}
```

### API Patch Operations

```csharp
[HttpPatch("{id}")]
public async Task<IActionResult> PatchUser(int id, UserDto updates)
{
    var existing = await _userService.GetUser(id);
    var updated = _mapper.Map(updates, existing);
    
    var delta = new UserDelta(existing, updated);
    
    // Validate only changed fields
    if (delta.Email.HasChanged)
    {
        if (!IsValidEmail(delta.Email.CurrentValue))
            return BadRequest("Invalid email format");
    }
    
    await _userService.UpdateUser(updated);
    return Ok();
}
```

## Performance Considerations

DeltaTrace is designed for maximum performance:

- **Compile-Time Generation**: No runtime reflection or expression trees
- **Lazy Evaluation**: Property deltas are only computed when accessed
- **Minimal Allocations**: Reuses comparison logic and minimizes object creation
- **Incremental Compilation**: Changes to unrelated code don't trigger regeneration

## Building from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/DeltaTrace.git

# Build the solution
dotnet build

# Run tests
dotnet test

# Run benchmarks
dotnet run -c Release --project benchmarks/DeltaTrace.Benchmarks
```

## Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## Troubleshooting

### Common Issues

1. **Generated code not appearing**: Ensure your IDE supports source generators. Visual Studio 2019 16.9+ or VS Code with OmniSharp are recommended.

2. **Types not found**: The DeltaTrace attributes must be defined in your project or referenced assembly.

3. **Performance in large solutions**: While initial compilation may be slower with IIncrementalGenerator, subsequent builds and IDE performance are significantly improved.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built on [Roslyn Source Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md)
- Inspired by change tracking patterns in Entity Framework and other ORMs
- Performance testing with [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet)

## Roadmap

- [ ] Collection diff support with detailed change information
- [ ] JSON serialization support for delta objects
- [ ] Async change tracking for large object graphs
- [ ] Visual Studio analyzer for common usage patterns
- [ ] Source generator analyzer to suggest optimizations
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeltaTrace.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class GeneratorBenchmarks
{
    private CSharpCompilation _smallCompilation = null!;
    private CSharpCompilation _mediumCompilation = null!;
    private CSharpCompilation _largeCompilation = null!;
    
    private DeltaTraceGenerator _incrementalGenerator = null!;
    private DeltaTraceGeneratorOld _sourceGenerator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _incrementalGenerator = new DeltaTraceGenerator();
        _sourceGenerator = new DeltaTraceGeneratorOld();

        // Create different sized test scenarios
        _smallCompilation = CreateCompilation(GenerateTestCode(5, 10)); // 5 types, 10 properties each
        _mediumCompilation = CreateCompilation(GenerateTestCode(20, 15)); // 20 types, 15 properties each
        _largeCompilation = CreateCompilation(GenerateTestCode(50, 20)); // 50 types, 20 properties each
    }

    #region Small Compilation (5 types, 10 properties each)
    
    [Benchmark(Baseline = true, Description = "Old ISourceGenerator - Small")]
    public GeneratorDriverRunResult OldGenerator_Small()
    {
        var driver = CSharpGeneratorDriver.Create(_sourceGenerator);
        return driver.RunGeneratorsAndUpdateCompilation(_smallCompilation, out _, out _).GetRunResult();
    }

    [Benchmark(Description = "New IIncrementalGenerator - Small")]
    public GeneratorDriverRunResult IncrementalGenerator_Small()
    {
        var driver = CSharpGeneratorDriver.Create(_incrementalGenerator);
        return driver.RunGeneratorsAndUpdateCompilation(_smallCompilation, out _, out _).GetRunResult();
    }

    #endregion

    #region Medium Compilation (20 types, 15 properties each)

    [Benchmark(Description = "Old ISourceGenerator - Medium")]
    public GeneratorDriverRunResult OldGenerator_Medium()
    {
        var driver = CSharpGeneratorDriver.Create(_sourceGenerator);
        return driver.RunGeneratorsAndUpdateCompilation(_mediumCompilation, out _, out _).GetRunResult();
    }

    [Benchmark(Description = "New IIncrementalGenerator - Medium")]
    public GeneratorDriverRunResult IncrementalGenerator_Medium()
    {
        var driver = CSharpGeneratorDriver.Create(_incrementalGenerator);
        return driver.RunGeneratorsAndUpdateCompilation(_mediumCompilation, out _, out _).GetRunResult();
    }

    #endregion

    #region Large Compilation (50 types, 20 properties each)

    [Benchmark(Description = "Old ISourceGenerator - Large")]
    public GeneratorDriverRunResult OldGenerator_Large()
    {
        var driver = CSharpGeneratorDriver.Create(_sourceGenerator);
        return driver.RunGeneratorsAndUpdateCompilation(_largeCompilation, out _, out _).GetRunResult();
    }

    [Benchmark(Description = "New IIncrementalGenerator - Large")]
    public GeneratorDriverRunResult IncrementalGenerator_Large()
    {
        var driver = CSharpGeneratorDriver.Create(_incrementalGenerator);
        return driver.RunGeneratorsAndUpdateCompilation(_largeCompilation, out _, out _).GetRunResult();
    }

    #endregion

    #region Incremental Change Benchmarks

    [Benchmark(Description = "Old Generator - Incremental Change")]
    public GeneratorDriverRunResult OldGenerator_IncrementalChange()
    {
        var driver = CSharpGeneratorDriver.Create(_sourceGenerator);
        var result = driver.RunGeneratorsAndUpdateCompilation(_mediumCompilation, out var outputCompilation, out _);
        
        // Simulate a small change to one file
        var modifiedTree = _mediumCompilation.SyntaxTrees.First();
        var modifiedRoot = modifiedTree.GetRoot().WithLeadingTrivia(SyntaxFactory.Comment("// Modified"));
        var modifiedCompilation = outputCompilation.ReplaceSyntaxTree(modifiedTree, modifiedTree.WithRootAndOptions(modifiedRoot, modifiedTree.Options));
        
        return result.RunGeneratorsAndUpdateCompilation(modifiedCompilation, out _, out _).GetRunResult();
    }

    [Benchmark(Description = "Incremental Generator - Incremental Change")]
    public GeneratorDriverRunResult IncrementalGenerator_IncrementalChange()
    {
        var driver = CSharpGeneratorDriver.Create(_incrementalGenerator);
        var result = driver.RunGeneratorsAndUpdateCompilation(_mediumCompilation, out var outputCompilation, out _);
        
        // Simulate a small change to one file
        var modifiedTree = _mediumCompilation.SyntaxTrees.First();
        var modifiedRoot = modifiedTree.GetRoot().WithLeadingTrivia(SyntaxFactory.Comment("// Modified"));
        var modifiedCompilation = outputCompilation.ReplaceSyntaxTree(modifiedTree, modifiedTree.WithRootAndOptions(modifiedRoot, modifiedTree.Options));
        
        return result.RunGeneratorsAndUpdateCompilation(modifiedCompilation, out _, out _).GetRunResult();
    }

    #endregion

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
        };

        return CSharpCompilation.Create(
            assemblyName: "BenchmarkAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string GenerateTestCode(int typeCount, int propertiesPerType)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"
using System;
using System.Collections.Generic;

namespace DeltaTrace
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class DeltaTraceAttribute : Attribute
    {
        public bool DeepTracking { get; set; } = true;
        public bool GenerateExtensions { get; set; } = true;
        public string DeltaSuffix { get; set; } = ""Delta"";
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class IgnoreDeltaAttribute : Attribute { }
}

namespace TestNamespace
{");

        for (int i = 0; i < typeCount; i++)
        {
            sb.AppendLine($@"
    [DeltaTrace.DeltaTrace]
    public class TestModel{i}
    {{");
            
            for (int j = 0; j < propertiesPerType; j++)
            {
                var propertyType = (j % 4) switch
                {
                    0 => "string",
                    1 => "int",
                    2 => "DateTime",
                    _ => "Guid?"
                };
                
                sb.AppendLine($"        public {propertyType} Property{j} {{ get; set; }}");
            }
            
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
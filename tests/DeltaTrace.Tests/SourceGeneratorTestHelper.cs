using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;

namespace DeltaTrace.Tests;

public static class SourceGeneratorTestHelper
{
    public static Task<GeneratorDriverRunResult> RunGeneratorAsync(
        IIncrementalGenerator generator,
        string source,
        params Assembly[] additionalAssemblies)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
        };

        foreach (var assembly in additionalAssemblies)
        {
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(generator);
        return Task.FromResult(driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _).GetRunResult());
    }

    public static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    public static void AssertGeneratedSource(GeneratorDriverRunResult runResult, string expectedSource, string fileName)
    {
        var generatedSource = runResult.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith(fileName))
            ?.GetText()
            .ToString();

        if (generatedSource == null)
        {
            throw new Exception($"No generated file found with name '{fileName}'");
        }

        var normalizedGenerated = NormalizeLineEndings(generatedSource.Trim());
        var normalizedExpected = NormalizeLineEndings(expectedSource.Trim());

        if (normalizedGenerated != normalizedExpected)
        {
            var message = new StringBuilder();
            message.AppendLine($"Generated source for '{fileName}' does not match expected.");
            message.AppendLine("Expected:");
            message.AppendLine(normalizedExpected);
            message.AppendLine();
            message.AppendLine("Actual:");
            message.AppendLine(normalizedGenerated);
            throw new Exception(message.ToString());
        }
    }

    public static void AssertDiagnostics(GeneratorDriverRunResult runResult, params DiagnosticDescriptor[] expectedDiagnostics)
    {
        var actualDiagnostics = runResult.Diagnostics;

        foreach (var expected in expectedDiagnostics)
        {
            if (!actualDiagnostics.Any(d => d.Id == expected.Id))
            {
                throw new Exception($"Expected diagnostic '{expected.Id}' was not found");
            }
        }

        var unexpectedDiagnostics = actualDiagnostics
            .Where(d => !expectedDiagnostics.Any(e => e.Id == d.Id))
            .ToList();

        if (unexpectedDiagnostics.Any())
        {
            var message = new StringBuilder("Unexpected diagnostics found:");
            foreach (var diagnostic in unexpectedDiagnostics)
            {
                message.AppendLine($" - {diagnostic.Id}: {diagnostic.GetMessage()}");
            }
            throw new Exception(message.ToString());
        }
    }
}

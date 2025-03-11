using Microsoft.CodeAnalysis.Testing;
using System.IO;

namespace TimeProviderAnalyzers.Tests;

internal static class TargetReferenceAssemblies
{
    public static readonly ReferenceAssemblies Net9 = new(
        targetFramework: "net9.0",
        referenceAssemblyPackage: new PackageIdentity("Microsoft.NETCore.App.Ref", "9.0.0-preview.3.24172.9"),
        referenceAssemblyPath: Path.Combine("ref", "net9.0"));
}

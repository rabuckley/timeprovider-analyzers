using Microsoft.CodeAnalysis;

namespace TimeProviderAnalyzers;

public static class Rules
{
    private const string UsageCategory = "Usage";

    public static readonly DiagnosticDescriptor UseOfStaticTimeDescriptor =
        CreateDescriptor("TPA0001", UsageCategory, DiagnosticSeverity.Info);

    public static readonly DiagnosticDescriptor UseOfStaticTimeWithTimeProviderInScopeDescriptor =
        CreateDescriptor("TPA0002", UsageCategory, DiagnosticSeverity.Warning);

    public static readonly DiagnosticDescriptor PassTimeProviderDescriptor =
        CreateDescriptor("TPA0003", UsageCategory, DiagnosticSeverity.Warning);

    private static DiagnosticDescriptor CreateDescriptor(
        string diagnosticId,
        string category,
        DiagnosticSeverity severity)
    {
        return new DiagnosticDescriptor(
            id: diagnosticId,
            title: GetLocalizableResourceString($"{diagnosticId}Title"),
            messageFormat: GetLocalizableResourceString($"{diagnosticId}MessageFormat"),
            category: category,
            defaultSeverity: severity,
            isEnabledByDefault: true,
            description: GetLocalizableResourceString($"{diagnosticId}Description")
        );

        static LocalizableResourceString GetLocalizableResourceString(string name) =>
            new(name, Resources.ResourceManager, typeof(Resources));
    }
}

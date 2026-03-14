using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;
using VerifyCS = TimeProviderAnalyzers.Test.CSharpCodeFixVerifier<
    TimeProviderAnalyzers.TimeProviderAnalyzersAnalyzer,
    TimeProviderAnalyzers.AddTimeProviderParameterCodeFixProvider>;

namespace TimeProviderAnalyzers.Tests;

public sealed class TPA0001CodeFixTests
{
    [Theory]
    [MemberData(nameof(TimeProviderAnalyzersUnitTest.DateTypePropertyData), MemberType = typeof(TimeProviderAnalyzersUnitTest))]
    public async Task CodeFix_MethodWithNoParams_AddsParameterAndReplacesExpression(
        DateType dateType,
        DateTypeProperty dateTypeProperty)
    {
        var dateTypeString = DateTypeFormatter.Format(dateType);
        var accessorName = DateTypePropertyFormatter.Format(dateTypeProperty);
        var replacement = GetExpectedReplacement("timeProvider", dateType, dateTypeProperty);

        /* lang=c# */
        var test =
            $$"""
              using System;

              class MyClass
              {
                  public {{dateTypeString}} GetNow()
                  {
                      return {|TPA0001:{{dateTypeString}}.{{accessorName}}|};
                  }
              }
              """;

        /* lang=c# */
        var fixedSource =
            $$"""
              using System;

              class MyClass
              {
                  public {{dateTypeString}} GetNow(TimeProvider timeProvider)
                  {
                      return {{replacement}};
                  }
              }
              """;

        var context = new VerifyCS.Test
        {
            TestCode = test,
            FixedCode = fixedSource,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9,
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task CodeFix_MethodWithExistingParams_AddsAsLastParam()
    {
        /* lang=c# */
        const string test = """
            using System;

            class MyClass
            {
                public DateTime GetNow(string label, int offset)
                {
                    return {|TPA0001:DateTime.Now|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class MyClass
            {
                public DateTime GetNow(string label, int offset, TimeProvider timeProvider)
                {
                    return timeProvider.GetLocalNow().DateTime;
                }
            }
            """;

        var context = new VerifyCS.Test
        {
            TestCode = test,
            FixedCode = fixedSource,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9,
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task CodeFix_ConstructorBody_AddsParameterAndReplacesExpression()
    {
        /* lang=c# */
        const string test = """
            using System;

            class MyClass
            {
                private readonly DateTime _created;

                public MyClass()
                {
                    _created = {|TPA0001:DateTime.UtcNow|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class MyClass
            {
                private readonly DateTime _created;

                public MyClass(TimeProvider timeProvider)
                {
                    _created = timeProvider.GetUtcNow().DateTime;
                }
            }
            """;

        var context = new VerifyCS.Test
        {
            TestCode = test,
            FixedCode = fixedSource,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9,
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task NoCodeFix_FieldInitializer()
    {
        // TPA0001 fires for the static access, but no code fix should be offered
        // because there is no enclosing method to add a parameter to.
        /* lang=c# */
        const string test = """
            using System;

            class MyClass
            {
                private readonly DateTime _created = {|TPA0001:DateTime.UtcNow|};

                public void DoWork() { }
            }
            """;

        var context = new VerifyCS.Test
        {
            TestCode = test,
            FixedCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9,
        };

        // No code fix should be applied — the fixed code should be identical to the test code.
        context.NumberOfFixAllInDocumentIterations = 0;
        context.NumberOfFixAllInProjectIterations = 0;
        context.NumberOfFixAllIterations = 0;

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Maps a (DateType, DateTypeProperty) pair to the expected TimeProvider replacement expression.
    /// </summary>
    private static string GetExpectedReplacement(string timeProviderName, DateType dateType, DateTypeProperty property)
    {
        return (dateType, property) switch
        {
            (DateType.DateTime, DateTypeProperty.Now) => $"{timeProviderName}.GetLocalNow().DateTime",
            (DateType.DateTime, DateTypeProperty.UtcNow) => $"{timeProviderName}.GetUtcNow().DateTime",
            (DateType.DateTime, DateTypeProperty.Today) => $"{timeProviderName}.GetLocalNow().Date",
            (DateType.DateTimeOffset, DateTypeProperty.Now) => $"{timeProviderName}.GetLocalNow()",
            (DateType.DateTimeOffset, DateTypeProperty.UtcNow) => $"{timeProviderName}.GetUtcNow()",
            _ => throw new System.ArgumentException($"Unsupported combination: {dateType}.{property}")
        };
    }
}

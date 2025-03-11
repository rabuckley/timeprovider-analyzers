using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using VerifyCS = TimeProviderAnalyzers.Test.CSharpCodeFixVerifier<
    TimeProviderAnalyzers.TimeProviderAnalyzersAnalyzer,
    TimeProviderAnalyzers.TimeProviderAnalyzersCodeFixProvider>;

namespace TimeProviderAnalyzers.Tests;

public enum DateType
{
    DateTime,
    DateTimeOffset
}

public static class DateTypeFormatter
{
    public static string Format(DateType dateType, bool fullyQualified = false)
    {
        return dateType switch
        {
            DateType.DateTime => fullyQualified ? "System.DateTime" : "DateTime",
            DateType.DateTimeOffset => fullyQualified ? "System.DateTimeOffset" : "DateTimeOffset",
            _ => throw new ArgumentException(nameof(dateType))
        };
    }
}

public enum DateTypeProperty
{
    Now,
    UtcNow,

    // DateTime.Today,
    Today,
}

public static class DateTypePropertyFormatter
{
    public static string Format(DateTypeProperty dateTypeProperty)
    {
        return dateTypeProperty switch
        {
            DateTypeProperty.Now => "Now",
            DateTypeProperty.UtcNow => "UtcNow",
            DateTypeProperty.Today => "Today",
            _ => throw new ArgumentException(nameof(dateTypeProperty))
        };
    }
}

public sealed class TimeProviderAnalyzersUnitTest
{
    [Fact]
    public async Task Analyzer_NoDiagnosticsForEmptyString()
    {
        const string test = "";
        await VerifyCS.VerifyAnalyzerAsync(test).ConfigureAwait(true);
    }

    // Matrix:
    // Platform
    // Scope: Parameters, Local Scope, Field, Property
    // Type: DateTime, DateTimeOffset
    // Method: Now, UtcNow
    // TimeProvider: System, Custom

    public static IEnumerable<TheoryDataRow<DateType, DateTypeProperty>> DateTypePropertyData()
    {
        yield return (DateType.DateTime, DateTypeProperty.Now);
        yield return (DateType.DateTime, DateTypeProperty.UtcNow);
        yield return (DateType.DateTime, DateTypeProperty.Today);
        yield return (DateType.DateTimeOffset, DateTypeProperty.Now);
        yield return (DateType.DateTimeOffset, DateTypeProperty.UtcNow);
    }

    [Theory]
    [MemberData(nameof(DateTypePropertyData))]
    public async Task Analyzer_WithTimeProviderParameter(
        DateType dateType,
        DateTypeProperty dateTypeProperty)
    {
        var expectedId = Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        var dateTypeString = DateTypeFormatter.Format(dateType);
        var accessorNameString = DateTypePropertyFormatter.Format(dateTypeProperty);

        /* lang=c# */
        var test =
            $$"""
              using System;

              namespace ConsoleApplication1
              {
                  class SomeClass
                  {
                      public static {{dateTypeString}} GetNow(TimeProvider timeProvider)
                      {
                          return {{dateTypeString}}.{{accessorNameString}};
                      }
                  }
              }
              """;

        context.TestCode = test;

        var expectedDiagnostic = VerifyCS.Diagnostic(expectedId)
            .WithLocation(9, 20)
            .WithArguments("timeProvider", $"{dateTypeString}.{accessorNameString}");

        context.ExpectedDiagnostics.Add(expectedDiagnostic);

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(DateTypePropertyData))]
    public async Task Analyzer_WithLocalVariable(
        DateType dateType,
        DateTypeProperty dateTypeProperty)
    {
        var expectedId = Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        var dateTypeString = DateTypeFormatter.Format(dateType);
        var accessorNameString = DateTypePropertyFormatter.Format(dateTypeProperty);

        // Some crude randomization of variable names
        var variableName = Random.Shared.Next(0, 3) switch
        {
            0 => "tp",
            1 => "timeProvider",
            _ => "provider"
        };

        /* lang=c# */
        var test =
            $$"""
              using System;

              namespace ConsoleApplication1
              {
                  class SomeClass
                  {
                      public static {{dateTypeString}}  GetNow()
                      {
                          var {{variableName}} = TimeProvider.System;
                          return {{dateTypeString}}.{{accessorNameString}};
                      }
                  }
              }
              """;

        context.TestCode = test;

        var expectedDiagnostic = VerifyCS.Diagnostic(expectedId)
            .WithLocation(10, 20)
            .WithArguments(variableName, $"{dateTypeString}.{accessorNameString}");

        context.ExpectedDiagnostics.Add(expectedDiagnostic);

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(DateTypePropertyData))]
    public async Task Analyzer_WithStaticField(
        DateType dateType,
        DateTypeProperty dateTypeProperty)
    {
        var expectedId = Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        var dateTypeString = DateTypeFormatter.Format(dateType);
        var accessorNameString = DateTypePropertyFormatter.Format(dateTypeProperty);
        var accessString = $"{dateTypeString}.{accessorNameString}";

        var fieldName = Random.Shared.Next(0, 3) switch
        {
            0 => "tp",
            1 => "timeProvider",
            _ => "provider"
        };

        /* lang=c# */
        var test =
            $$"""
              using System;

              namespace ConsoleApplication1
              {
                  class SomeClass
                  {
                      private static TimeProvider {{fieldName}} = TimeProvider.System;
              
                      public static {{dateTypeString}} GetNow()
                      {
                          return {{accessString}};
                      }
                  }
              }
              """;

        context.TestCode = test;

        var expectedDiagnostic = VerifyCS.Diagnostic(expectedId)
            .WithSpan(11, 20, 11, 20 + accessString.Length)
            .WithArguments(fieldName, accessString);

        context.ExpectedDiagnostics.Add(expectedDiagnostic);

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(DateTypePropertyData))]
    public async Task Analyzer_WithInstanceField(
        DateType dateType,
        DateTypeProperty dateTypeProperty)
    {
        var expectedId = Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        var dateTypeString = DateTypeFormatter.Format(dateType);
        var accessorNameString = DateTypePropertyFormatter.Format(dateTypeProperty);
        var accessString = $"{dateTypeString}.{accessorNameString}";

        var fieldName = Random.Shared.Next(0, 3) switch
        {
            0 => "tp",
            1 => "timeProvider",
            _ => "provider"
        };

        /* lang=c# */
        var test =
            $$"""
              using System;

              namespace ConsoleApplication1
              {
                  class SomeClass
                  {
                      private TimeProvider {{fieldName}} = TimeProvider.System;
              
                      public {{dateTypeString}} GetNow()
                      {
                          return {{accessString}};
                      }
                  }
              }
              """;

        context.TestCode = test;

        var expectedDiagnostic = VerifyCS.Diagnostic(expectedId)
            .WithSpan(11, 20, 11, 20 + accessString.Length)
            .WithArguments(fieldName, accessString);

        context.ExpectedDiagnostics.Add(expectedDiagnostic);

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(DateTypePropertyData))]
    public async Task Analyzer_WithNestedClass_OuterScopeOnly(
        DateType dateType,
        DateTypeProperty dateTypeProperty)
    {
        var expectedId = Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        var dateTypeString = DateTypeFormatter.Format(dateType);
        var accessorNameString = DateTypePropertyFormatter.Format(dateTypeProperty);
        var accessString = $"{dateTypeString}.{accessorNameString}";

        var fieldName = Random.Shared.Next(0, 3) switch
        {
            0 => "tp",
            1 => "timeProvider",
            _ => "provider"
        };

        /* lang=c# */
        var test =
            $$"""
              using System;

              namespace ConsoleApplication1
              {
                  class SomeClass
                  {
                      private TimeProvider {{fieldName}} = TimeProvider.System;
              
                      public {{dateTypeString}} GetNow()
                      {
                          return {{accessString}};
                      }
              
                      class NestedClass
                      {
                          public {{dateTypeString}} GetNow()
                          {
                              return {{accessString}};
                          }
                      }
                  }
              }
              """;

        context.TestCode = test;

        context.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(expectedId)
            .WithSpan(11, 20, 11, 20 + accessString.Length)
            .WithArguments(fieldName, accessString));
        
        context.ExpectedDiagnostics.Add(VerifyCS.Diagnostic(Rules.UseOfStaticTimeDescriptor.Id)
            .WithSpan(18, 24, 18, 24 + accessString.Length)
            .WithArguments(accessString));

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(DateTypePropertyData))]
    public async Task Analyzer_WithNestedClass_InnerBindsTighter(
        DateType dateType,
        DateTypeProperty dateTypeProperty)
    {
        var expectedId = Rules.UseOfStaticTimeWithTimeProviderInScopeDescriptor.Id;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        var dateTypeString = DateTypeFormatter.Format(dateType);
        var accessorNameString = DateTypePropertyFormatter.Format(dateTypeProperty);
        var accessString = $"{dateTypeString}.{accessorNameString}";

        var fieldName = Random.Shared.Next(0, 3) switch
        {
            0 => "tp",
            1 => "timeProvider",
            _ => "provider"
        };

        const string innerFieldName = "nestedProvider";

        /* lang=c# */
        var test =
            $$"""
              using System;

              namespace ConsoleApplication1
              {
                  class SomeClass
                  {
                      private TimeProvider {{fieldName}} = TimeProvider.System;
              
                      class NestedClass
                      {
                          private TimeProvider {{innerFieldName}} = TimeProvider.System;
              
                          public {{dateTypeString}} GetNow()
                          {
                              return {{accessString}};
                          }
                      }
                  }
              }
              """;

        context.TestCode = test;

        const int startColumn = 24;
        const int line = 15;

        var expectedDiagnostic = VerifyCS.Diagnostic(expectedId)
            .WithSpan(line, startColumn, line, startColumn + accessString.Length)
            .WithArguments(innerFieldName, accessString);

        context.ExpectedDiagnostics.Add(expectedDiagnostic);

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}

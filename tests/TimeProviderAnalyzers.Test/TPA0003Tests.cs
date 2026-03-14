using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Threading.Tasks;
using Xunit;
using VerifyCS = TimeProviderAnalyzers.Test.CSharpCodeFixVerifier<
    TimeProviderAnalyzers.TimeProviderAnalyzersAnalyzer,
    TimeProviderAnalyzers.PassTimeProviderCodeFixProvider>;

namespace TimeProviderAnalyzers.Tests;

public sealed class TPA0003Tests
{
    private const string ExpectedId = "TPA0003";

    [Fact]
    public async Task TaskDelay_WithTimeProviderParameter_Reports()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading.Tasks;

            class MyClass
            {
                public async Task DoWork(TimeProvider timeProvider)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        context.ExpectedDiagnostics.Add(new DiagnosticResult(ExpectedId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(8, 15)
            .WithArguments("timeProvider", "Task.Delay"));

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task TaskDelay_WithTimeSpanAndCancellationToken_WithTimeProviderField_Reports()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            class MyClass
            {
                private readonly TimeProvider _tp = TimeProvider.System;

                public async Task DoWork(CancellationToken ct)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        context.ExpectedDiagnostics.Add(new DiagnosticResult(ExpectedId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(11, 15)
            .WithArguments("_tp", "Task.Delay"));

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task CancellationTokenSourceCtor_WithTimeProviderLocalVar_Reports()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading;

            class MyClass
            {
                public void DoWork()
                {
                    var tp = TimeProvider.System;
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        context.ExpectedDiagnostics.Add(new DiagnosticResult(ExpectedId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(9, 19)
            .WithArguments("tp", "CancellationTokenSource"));

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task PeriodicTimerCtor_WithTimeProviderParameter_Reports()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading;

            class MyClass
            {
                public void DoWork(TimeProvider timeProvider)
                {
                    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        context.ExpectedDiagnostics.Add(new DiagnosticResult(ExpectedId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(8, 27)
            .WithArguments("timeProvider", "PeriodicTimer"));

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task TimeProviderAsProperty_Reports()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading.Tasks;

            class MyClass
            {
                public TimeProvider Clock { get; set; } = TimeProvider.System;

                public async Task DoWork()
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        context.ExpectedDiagnostics.Add(new DiagnosticResult(ExpectedId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(10, 15)
            .WithArguments("Clock", "Task.Delay"));

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task UserDefinedMethod_WithTimeProviderOverload_Reports()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(int value, TimeProvider timeProvider) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    Utilities.Foo(42);
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        context.ExpectedDiagnostics.Add(new DiagnosticResult(ExpectedId, Microsoft.CodeAnalysis.DiagnosticSeverity.Warning)
            .WithLocation(13, 9)
            .WithArguments("tp", "Utilities.Foo"));

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    // --- Negative cases ---

    [Fact]
    public async Task TaskDelay_NoTimeProviderInScope_NoDiagnostic()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading.Tasks;

            class MyClass
            {
                public async Task DoWork()
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task TaskDelay_AlreadyPassingTimeProvider_NoDiagnostic()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading.Tasks;

            class MyClass
            {
                public async Task DoWork(TimeProvider timeProvider)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), timeProvider);
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task UserDefinedMethod_NoTimeProviderOverload_NoDiagnostic()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Delay(TimeSpan duration) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    Utilities.Delay(TimeSpan.FromSeconds(1));
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task ParameterlessCancellationTokenSourceCtor_NoDiagnostic()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading;

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    var cts = new CancellationTokenSource();
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task MethodAlreadyReceivesTimeProviderParameter_NoDiagnostic()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Process(int value, TimeProvider tp) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    Utilities.Process(42, tp);
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    [Fact]
    public async Task TaskDelayMilliseconds_NoTimeProviderOverload_NoDiagnostic()
    {
        // Task.Delay(int) has no TimeProvider overload — only TimeSpan-based ones do.
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading.Tasks;

            class MyClass
            {
                public async Task DoWork(TimeProvider tp)
                {
                    await Task.Delay(1000);
                }
            }
            """;

        var context = new CSharpAnalyzerTest<TimeProviderAnalyzersAnalyzer, DefaultVerifier>
        {
            TestCode = test,
            ReferenceAssemblies = TargetReferenceAssemblies.Net9
        };

        await context.RunAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    // --- Code fix tests ---

    [Fact]
    public async Task CodeFix_TaskDelay_AddsTimeProviderArgument()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading.Tasks;

            class MyClass
            {
                public async Task DoWork(TimeProvider timeProvider)
                {
                    await {|TPA0003:Task.Delay(TimeSpan.FromSeconds(1))|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;
            using System.Threading.Tasks;

            class MyClass
            {
                public async Task DoWork(TimeProvider timeProvider)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), timeProvider);
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
    public async Task CodeFix_CancellationTokenSourceCtor_AddsTimeProviderArgument()
    {
        /* lang=c# */
        const string test = """
            using System;
            using System.Threading;

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    var cts = {|TPA0003:new CancellationTokenSource(TimeSpan.FromSeconds(5))|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;
            using System.Threading;

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5), tp);
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
    public async Task CodeFix_UserDefinedMethod_AddsTimeProviderArgument()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(int value, TimeProvider timeProvider) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    {|TPA0003:Utilities.Foo(42)|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(int value, TimeProvider timeProvider) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    Utilities.Foo(42, tp);
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

    // --- Position-aware insertion tests ---

    [Fact]
    public async Task CodeFix_TimeProviderAsFirstParam_InsertsAtCorrectPosition()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(TimeProvider timeProvider, int value) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    {|TPA0003:Utilities.Foo(42)|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(TimeProvider timeProvider, int value) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    Utilities.Foo(tp, 42);
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
    public async Task CodeFix_TimeProviderInMiddle_InsertsAtCorrectPosition()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Foo(int a, int b) { }
                public static void Foo(int a, TimeProvider tp, int b) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider myTp)
                {
                    {|TPA0003:Utilities.Foo(1, 2)|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class Utilities
            {
                public static void Foo(int a, int b) { }
                public static void Foo(int a, TimeProvider tp, int b) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider myTp)
                {
                    Utilities.Foo(1, myTp, 2);
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

    // --- Named argument tests ---

    [Fact]
    public async Task CodeFix_NamedArguments_InsertsNamedTimeProviderArgument()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(int value, TimeProvider timeProvider) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    {|TPA0003:Utilities.Foo(value: 42)|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(int value, TimeProvider timeProvider) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    Utilities.Foo(value: 42, timeProvider: tp);
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
    public async Task CodeFix_MixedPositionalAndNamedArgs_InsertsNamedTimeProviderArgument()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Foo(int a, string name) { }
                public static void Foo(int a, string name, TimeProvider timeProvider) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    {|TPA0003:Utilities.Foo(42, name: "hello")|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class Utilities
            {
                public static void Foo(int a, string name) { }
                public static void Foo(int a, string name, TimeProvider timeProvider) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider tp)
                {
                    Utilities.Foo(42, name: "hello", timeProvider: tp);
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
    public async Task CodeFix_NamedArgsWithTimeProviderFirst_InsertsNamedAtCorrectPosition()
    {
        /* lang=c# */
        const string test = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(TimeProvider tp, int value) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider myTp)
                {
                    {|TPA0003:Utilities.Foo(value: 42)|};
                }
            }
            """;

        /* lang=c# */
        const string fixedSource = """
            using System;

            class Utilities
            {
                public static void Foo(int value) { }
                public static void Foo(TimeProvider tp, int value) { }
            }

            class MyClass
            {
                public void DoWork(TimeProvider myTp)
                {
                    Utilities.Foo(tp: myTp, value: 42);
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
}

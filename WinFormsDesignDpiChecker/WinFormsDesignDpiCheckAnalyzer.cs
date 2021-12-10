using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace WinFormsDesignDpiChecker
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class WinFormsDesignDpiCheckAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The form design has been saved with settings for environments where the DPI scale is not 100%.
        /// </summary>
        internal static DiagnosticDescriptor s_diagnosticDescriptor_WinFormsDesignDpiChecker0001 = new DiagnosticDescriptor(
            "WinFormsDesignDpiChecker0001",
            "The form design has been saved with settings for environments where the DPI scale is not 100%",
            "The form design has been saved with settings for environments where the DPI scale is not 100%",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// AutoScaleMode does not match the WindowsFormsAutoScaleMode setting in MSBuild property.
        /// </summary>
        internal static DiagnosticDescriptor s_diagnosticDescriptor_WinFormsDesignDpiChecker0002 = new DiagnosticDescriptor(
            "WinFormsDesignDpiChecker0002",
            "AutoScaleMode does not match the WindowsFormsAutoScaleMode setting in MSBuild property",
            "The AutoScaleMode of this is different from the value specified in the project. The project's default value is \"{0}\".",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            s_diagnosticDescriptor_WinFormsDesignDpiChecker0001,
            s_diagnosticDescriptor_WinFormsDesignDpiChecker0002
            );

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(AnalyzeNamedTypeSymbol, SymbolKind.NamedType);

        }
        private static void AnalyzeNamedTypeSymbol(SymbolAnalysisContext context)
        {
            if (context.Symbol is not INamedTypeSymbol namedTypeSymbol)
            {
                return;
            }


            while (true)
            {
                var baseType = namedTypeSymbol.BaseType;

                if (baseType is null) return;

                if (baseType.SpecialType == SpecialType.System_Object) return;

                if (baseType.ContainingNamespace.Name == "Forms"
                    && baseType.ContainingNamespace.ContainingNamespace.Name == "Windows"
                    && baseType.ContainingNamespace.ContainingNamespace.ContainingNamespace.Name == "System"
                    && baseType.ContainingNamespace.ContainingNamespace.ContainingNamespace.ContainingNamespace.IsGlobalNamespace
                    )
                {
                    if (baseType.Name == "Form" || baseType.Name == "UserControl")
                    {
                        break;
                    }
                }
            }

            var locations = namedTypeSymbol.GetMembers("InitializeComponent").SelectMany(v => v.Locations);

            foreach (var location in locations)
            {
                if (location.SourceTree is null)
                {
                    continue;
                }

                var node = location.SourceTree.GetRoot().FindNode(location.SourceSpan);

                if (node is not MethodDeclarationSyntax methodDeclarationSyntax)
                {
                    continue;
                }

                if (methodDeclarationSyntax.Body is null)
                {
                    continue;
                }

                var assignmentExpressions = methodDeclarationSyntax.Body.Statements.OfType<ExpressionStatementSyntax>().Select(v => v.Expression).OfType<AssignmentExpressionSyntax>();

                string? autoScaleMode = null;
                (float arg1, float arg2)? autoScaleDimensions = null;

                foreach (var assignmentExpression in assignmentExpressions)
                {
                    if (assignmentExpression.Left is MemberAccessExpressionSyntax leftExpression)
                    {
                        if (leftExpression.Name.Identifier.ValueText == "AutoScaleMode")
                        {
                            if (assignmentExpression.Right is MemberAccessExpressionSyntax rightExpression)
                            {
                                autoScaleMode = rightExpression.Name.Identifier.ValueText;
                            }
                        }

                        if (leftExpression.Name.Identifier.ValueText == "AutoScaleDimensions")
                        {
                            if (assignmentExpression.Right is ObjectCreationExpressionSyntax rightExpression)
                            {
                                if (rightExpression.ArgumentList?.Arguments.Count == 2)
                                {
                                    var args = rightExpression.ArgumentList.Arguments;

                                    if (args[0].Expression is LiteralExpressionSyntax literalExpression1 && args[1].Expression is LiteralExpressionSyntax literalExpression2)
                                    {
                                        var arg1 = literalExpression1.Token.Value switch
                                        {
                                            float floatValue => (float)floatValue,
                                            double doubleValue => (float)doubleValue,
                                            int intValue => (float)intValue,
                                            _ => -1
                                        };

                                        var arg2 = literalExpression2.Token.Value switch
                                        {
                                            float floatValue => (float)floatValue,
                                            double doubleValue => (float)doubleValue,
                                            int intValue => (float)intValue,
                                            _ => -1
                                        };

                                        if (arg1 >= 0 && arg2 >= 0)
                                        {
                                            autoScaleDimensions = (arg1, arg2);
                                        }
                                    }
                                }

                            }
                        }
                    }
                }

                if (autoScaleDimensions.HasValue)
                {
                    if (autoScaleMode == "Font")
                    {
                        if (autoScaleDimensions.Value.arg1 != 6F || autoScaleDimensions.Value.arg2 != 12F)
                        {
                            foreach (var warningLocation in namedTypeSymbol.Locations)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(s_diagnosticDescriptor_WinFormsDesignDpiChecker0001, warningLocation));
                            }
                        }
                    }
                    else if (autoScaleMode == "Dpi")
                    {
                        if (autoScaleDimensions.Value.arg1 != 96F || autoScaleDimensions.Value.arg2 != 96F)
                        {
                            foreach (var warningLocation in namedTypeSymbol.Locations)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(s_diagnosticDescriptor_WinFormsDesignDpiChecker0001, warningLocation));
                            }
                        }
                    }
                }

                if (context.Options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.WindowsFormsAutoScaleMode", out var projectSetAutoScaleMode))
                {
                    if (autoScaleMode is not null && string.Equals(projectSetAutoScaleMode.Trim(), autoScaleMode, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var warningLocation in namedTypeSymbol.Locations)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(s_diagnosticDescriptor_WinFormsDesignDpiChecker0002, warningLocation, projectSetAutoScaleMode));
                        }
                    }
                }
            }
        }
    }
}

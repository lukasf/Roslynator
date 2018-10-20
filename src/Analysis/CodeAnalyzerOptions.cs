﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Roslynator.Analysis
{
    public class CodeAnalyzerOptions
    {
        public static CodeAnalyzerOptions Default { get; } = new CodeAnalyzerOptions();

        public CodeAnalyzerOptions(
            bool ignoreAnalyzerReferences = false,
            bool ignoreCompilerDiagnostics = false,
            bool reportFadeDiagnostics = false,
            bool reportSuppressedDiagnostics = false,
            bool executionTime = false,
            DiagnosticSeverity minimalSeverity = DiagnosticSeverity.Info,
            IEnumerable<string> ignoredDiagnosticIds = null,
            IEnumerable<string> ignoredProjectNames = null,
            string language = null,
            string cultureName = null)
        {
            IgnoreAnalyzerReferences = ignoreAnalyzerReferences;
            IgnoreCompilerDiagnostics = ignoreCompilerDiagnostics;
            ReportFadeDiagnostics = reportFadeDiagnostics;
            ReportSuppressedDiagnostics = reportSuppressedDiagnostics;
            ExecutionTime = executionTime;
            MinimalSeverity = minimalSeverity;
            IgnoredDiagnosticIds = ignoredDiagnosticIds?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty;
            IgnoredProjectNames = ignoredProjectNames?.ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty;
            Language = language;
            CultureName = cultureName;
        }

        public bool IgnoreAnalyzerReferences { get; }

        public bool IgnoreCompilerDiagnostics { get; }

        public bool ReportFadeDiagnostics { get; }

        public bool ReportSuppressedDiagnostics { get; }

        public bool ExecutionTime { get; }

        public DiagnosticSeverity MinimalSeverity { get; }

        public ImmutableHashSet<string> IgnoredDiagnosticIds { get; }

        public ImmutableHashSet<string> IgnoredProjectNames { get; }

        public string Language { get; }

        public string CultureName { get; }
    }
}

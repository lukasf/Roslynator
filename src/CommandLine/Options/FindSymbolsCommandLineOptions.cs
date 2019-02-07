﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using CommandLine;

namespace Roslynator.CommandLine
{
#if DEBUG
    [Verb("find-symbols", HelpText = "Finds symbols in the specified project or solution.")]
#endif
    public class FindSymbolsCommandLineOptions : MSBuildCommandLineOptions
    {
        [Option(longName: "ignored-attributes")]
        public IEnumerable<string> IgnoredAttributes { get; set; }

        [Option(longName: "ignore-obsolete")]
        public bool IgnoreObsolete { get; set; }

        [Option(longName: "ignored-symbols")]
        public IEnumerable<string> IgnoredSymbols { get; set; }

        [Option(longName: "include-generated-code")]
        public bool IncludeGeneratedCode { get; set; }

        [Option(longName: ParameterNames.SymbolKinds)]
        public IEnumerable<string> SymbolKinds { get; set; }

        [Option(longName: "unused-only")]
        public bool UnusedOnly { get; set; }

        [Option(longName: ParameterNames.Visibility)]
        public IEnumerable<string> Visibility { get; set; }
    }
}

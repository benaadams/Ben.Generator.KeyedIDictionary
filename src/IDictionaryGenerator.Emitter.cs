// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Generators
{
    public partial class IDictionaryGenerator
    {
        private sealed class Emitter
        {
            private readonly StringBuilder _builder = new StringBuilder(1024);
            private readonly GeneratorExecutionContext _context;

            public Emitter(GeneratorExecutionContext context) => _context = context;

            public void Emit(IDictionaryInterface[]? classSources, CancellationToken cancellationToken)
            {
                if ((classSources?.Length ?? 0) == 0) return;

                foreach (IDictionaryInterface? ec in classSources)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // stop any additional work
                        break;
                    }

                    _builder.AppendLine(@$"using System;
using {ec.ReturnTypeNamespace};
using {ec.BaseNamespace};

{(string.IsNullOrEmpty(ec.ClassNamespace) ? "" : $@"namespace {ec.ClassNamespace}
{{")}
    public interface {ec.InterfaceName} : {ec.BaseInterface}
    {{
");
                    foreach (var key in ec.Keys)
                    {
                        _builder.AppendLine($@"
        {ec.ReturnType} {key} {{ get => this[HeaderNames.{key}]; set => this[HeaderNames.{key}] = value; }}");

                    }

                    _builder.AppendLine(@$"    }}
{(string.IsNullOrEmpty(ec.ClassNamespace) ? "" : @"
}")}");
                    _context.AddSource($"{ec.InterfaceName}.Generated", SourceText.From(_builder.ToString(), Encoding.UTF8));

                    _builder.Clear();
                }
            }
        }
    }
}

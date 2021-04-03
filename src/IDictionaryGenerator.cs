// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generators
{
    [Generator]
    public partial class IDictionaryGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
        private const string attributeText = @"
#nullable enable

namespace System.Collections
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class KeyedIDictionaryAttribute : Attribute
    {
        public KeyedIDictionaryAttribute(string InterfaceName, Type BaseInterface)
        {
        }
    }
}
";
        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("KeyedIDictionaryAttribute", SourceText.From(attributeText, Encoding.UTF8));

            if (context.SyntaxReceiver is not SyntaxReceiver receiver) return;

            CSharpParseOptions options = (CSharpParseOptions)((CSharpCompilation)context.Compilation).SyntaxTrees[0].Options;

            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));

            if ((receiver?.CandidateClasses?.Count ?? 0) == 0)
            {
                return;
            }

            Parser? p = new Parser(compilation, context.ReportDiagnostic, context.CancellationToken);
            IDictionaryInterface[]? interfaceSources = p.GetEventSourceClasses(receiver.CandidateClasses);
            Emitter? e = new Emitter(context);
            e.Emit(interfaceSources, context.CancellationToken);
        }

        private sealed class SyntaxReceiver : ISyntaxReceiver
        {
            private List<ClassDeclarationSyntax>? _candidateClasses;

            public List<ClassDeclarationSyntax>? CandidateClasses => _candidateClasses;

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // Only add classes annotated [KeyedIDictionary] to reduce busy work.
                const string KeyedIDictionaryAttribute = "KeyedIDictionaryAttribute";
                const string KeyedIDictionaryAttributeShort = "KeyedIDictionary";

                // Only clasess
                if (syntaxNode is ClassDeclarationSyntax classDeclaration)
                {
                    // Check if has EventSource attribute before adding to candidates
                    // as we don't want to add every class in the project
                    foreach (AttributeListSyntax? cal in classDeclaration.AttributeLists)
                    {
                        foreach (AttributeSyntax? ca in cal.Attributes)
                        {
                            // Check if Span length matches before allocating the string to check more
                            int length = ca.Name.Span.Length;
                            if (length != KeyedIDictionaryAttribute.Length && length != KeyedIDictionaryAttributeShort.Length)
                            {
                                continue;
                            }

                            // Possible match, now check the string value
                            string attrName = ca.Name.ToString();
                            if (attrName == KeyedIDictionaryAttribute || attrName == KeyedIDictionaryAttributeShort)
                            {
                                // Match add to candidates
                                _candidateClasses ??= new List<ClassDeclarationSyntax>();
                                _candidateClasses.Add(classDeclaration);
                                return;
                            }
                        }
                    }
                }
            }
        }

        private sealed class IDictionaryInterface
        {
            public string InterfaceName = string.Empty;
            public string ClassNamespace = string.Empty;
            public string BaseNamespace = string.Empty;
            public string BaseInterface = string.Empty;
            public string ReturnType = string.Empty;
            public string ReturnTypeNamespace = string.Empty;

            public List<string> Keys { get; } = new List<string>();
        }
    }
}

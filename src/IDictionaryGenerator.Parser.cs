// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
//using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Generators
{
    public partial class IDictionaryGenerator
    {
        private sealed class Parser
        {
            private readonly CancellationToken _cancellationToken;
            private readonly Compilation _compilation;
            private readonly Action<Diagnostic> _reportDiagnostic;

            public Parser(Compilation compilation, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
            {
                _compilation = compilation;
                _cancellationToken = cancellationToken;
                _reportDiagnostic = reportDiagnostic;
            }

            public IDictionaryInterface[] GetEventSourceClasses(List<ClassDeclarationSyntax> classDeclarations)
            {
                INamedTypeSymbol? keyedIDictionaryAttribute = _compilation.GetTypeByMetadataName("System.Collections.KeyedIDictionaryAttribute");
                if (keyedIDictionaryAttribute is null)
                {
                    // No EventSourceAutoGenerateAttribute
                    return Array.Empty<IDictionaryInterface>();
                }

                List<IDictionaryInterface>? results = null;
                // we enumerate by syntax tree, to minimize the need to instantiate semantic models (since they're expensive)
                foreach (IGrouping<SyntaxTree, ClassDeclarationSyntax>? group in classDeclarations.GroupBy(x => x.SyntaxTree))
                {
                    SemanticModel? sm = null;
                    foreach (ClassDeclarationSyntax? classDef in group)
                    {
                        List<string> existing = new List<string>();
                        IDictionaryInterface? sourceClass = null;
                        if (_cancellationToken.IsCancellationRequested)
                        {
                            // be nice and stop if we're asked to
                            return results?.ToArray() ?? Array.Empty<IDictionaryInterface>();
                        }

                        bool generate = false;
                        foreach (AttributeListSyntax? cal in classDef.AttributeLists)
                        {
                            foreach (AttributeSyntax? ca in cal.Attributes)
                            {
                                // need a semantic model for this tree
                                sm ??= _compilation.GetSemanticModel(classDef.SyntaxTree);

                                if (sm.GetSymbolInfo(ca, _cancellationToken).Symbol is not IMethodSymbol caSymbol)
                                {
                                    // badly formed attribute definition, or not the right attribute
                                    continue;
                                }

                                if (keyedIDictionaryAttribute.Equals(caSymbol.ContainingType, SymbolEqualityComparer.Default))
                                {
                                    string baseNamespace = string.Empty;
                                    string baseInterface = string.Empty;
                                    string classNamespace = string.Empty;
                                    string interfaceName = string.Empty;
                                    string returnType = string.Empty;
                                    string returnTypeNamespace = string.Empty;

                                    SeparatedSyntaxList<AttributeArgumentSyntax>? args = ca.ArgumentList?.Arguments;
                                    if (args is not null)
                                    {
                                        foreach (AttributeArgumentSyntax? arg in args)
                                        {
                                            string? argName = arg.NameEquals?.Name.Identifier.ToString() ?? arg.NameColon!.Name.ToString();

                                            switch (argName)
                                            {
                                                case "InterfaceName":
                                                    interfaceName = sm.GetConstantValue(arg.Expression, _cancellationToken).ToString();
                                                    break;
                                                case "BaseInterface":
                                                    ITypeSymbol type = sm.GetTypeInfo((arg.Expression as TypeOfExpressionSyntax).Type).Type;
                                                    baseNamespace = type.ContainingNamespace.ToString();
                                                    baseInterface = type.Name;
                                                    existing.AddRange(type.GetMembers().Select(m => m.Name));
                                                    var symbol = type.GetMembers().First(m => m.Name == "this[]");
                                                    var returntype = (symbol as IPropertySymbol).Type;
                                                    returnType = returntype.Name;
                                                    returnTypeNamespace = returntype.ContainingNamespace.ToString();
                                                    break;
                                            }
                                        }
                                    }

                                    if (string.IsNullOrEmpty(baseNamespace) ||
                                        string.IsNullOrEmpty(interfaceName) ||
                                        string.IsNullOrEmpty(baseInterface) ||
                                        string.IsNullOrEmpty(returnType) ||
                                        string.IsNullOrEmpty(returnTypeNamespace))
                                    {
                                        continue;
                                    }

                                    classNamespace = classDef.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? string.Empty;

                                    sourceClass = new IDictionaryInterface
                                    {
                                        BaseNamespace = baseNamespace,
                                        BaseInterface = baseInterface,
                                        InterfaceName = interfaceName,
                                        ClassNamespace = classNamespace,
                                        ReturnType = returnType,
                                        ReturnTypeNamespace = returnTypeNamespace
                                    };

                                    generate = true;
                                }
                            }
                        }

                        if (!generate || sourceClass is null)
                        {
                            continue;
                        }

                        var classSymbol = sm.GetDeclaredSymbol(classDef);
                        foreach (var member in classSymbol.GetMembers())
                        {
                            if (member is IFieldSymbol field && 
                                field.DeclaredAccessibility == Accessibility.Public && 
                                field.IsStatic &&
                                field.Type.SpecialType == SpecialType.System_String)
                            {
                                var name = field.Name;
                                if (existing.Contains(name))
                                {
                                    continue;
                                }

                                sourceClass.Keys.Add(name);
                            }
                        }

                        results ??= new List<IDictionaryInterface>();
                        results.Add(sourceClass);
                    }
                }

                return results?.ToArray() ?? Array.Empty<IDictionaryInterface>();
            }
        }
    }
}

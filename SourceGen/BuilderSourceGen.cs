using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace PolarizedIons.SimpleBuilderGen
{
    [Generator]
    public class BuilderSourceGen : ISourceGenerator
    {
        private const string AttributeText = @"using System;

namespace PolarizedIons.SimpleBuilderGen {
    public class GenerateBuilderAttribute : Attribute {}
}
";
        
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            {
                return;
            }
            var options = (context.Compilation as CSharpCompilation)?.SyntaxTrees[0].Options as CSharpParseOptions;
            var compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(AttributeText, Encoding.UTF8), options));
            
            
            
            var sourceBuilder = new StringBuilder(@"
namespace PolarizedIons.SimpleBuilderGen.Generated
{
");

            var attributeSymbol = compilation.GetTypeByMetadataName("PolarizedIons.SimpleBuilderGen.GenerateBuilderAttribute");

            var classSymbols = new List<ISymbol>();
            foreach (var cls in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(cls.SyntaxTree);
                var classSymbol = ModelExtensions.GetDeclaredSymbol(model, cls);
                if (classSymbol?.GetAttributes().Any(ad => attributeSymbol != null && ad.AttributeClass != null && ad.AttributeClass.Name == attributeSymbol.Name) ?? false) // todo, weird that  ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default) always returns null - see https://github.com/dotnet/roslyn/issues/30248 maybe?
                {
                    classSymbols.Add(classSymbol);
                }
            }

            foreach (var classSymbol in classSymbols)
            {
                var properties = compilation.GetTypeByMetadataName(classSymbol.ToDisplayString())
                    ?.GetMembers()
                    .Where(x => x.Kind == SymbolKind.Property && x.DeclaredAccessibility == Accessibility.Public)
                    .Select(x => (IPropertySymbol)x)
                    .Where(x => x != null)
                    .ToArray();

                if (properties == null)
                {
                    continue;
                }

                sourceBuilder.Append(@$"
    public class {classSymbol.Name}Builder {{
");

                foreach (var prop in properties)
                {
                    sourceBuilder.Append(@$"
        private {prop.Type} {GetPrivateName(prop)} = default;
        public {classSymbol.Name}Builder With{prop.Name}({prop.Type} value) {{
            {GetPrivateName(prop)} = value;
            return this;
        }}
");
                }

                sourceBuilder.Append($@"
        public {classSymbol} Build() {{
            return new {classSymbol} 
            {{
");
                foreach (var prop in properties)
                {
                    sourceBuilder.Append($"{prop.Name} = {GetPrivateName(prop)},\n");
                }              
                sourceBuilder.Append(@"
            };
        }
    }
");
            }


            sourceBuilder.Append(@"
}");
            
            context.AddSource(nameof(BuilderSourceGen) + "_Attribute.cs", SourceText.From(AttributeText, Encoding.UTF8));
            context.AddSource(nameof(BuilderSourceGen) + "_Generated.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }

        private static string GetPrivateName(IPropertySymbol prop)
        {
            return "_" + prop.Name[..1].ToLower() + prop.Name[1..];
        }
    }

    internal class SyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

        /// <summary>
        /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
        /// </summary>
        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // any field with at least one attribute is a candidate for property generation
            if (syntaxNode is ClassDeclarationSyntax {AttributeLists: {Count: > 0}} classDeclarationSyntax)
            {
                CandidateClasses.Add(classDeclarationSyntax);
            }
        }
    }
}

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;

using System;
using System.Linq;
using System.Threading;
using System.Composition;
using System.Threading.Tasks;
using System.Collections.Immutable;

namespace MembersSort
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MembersSortCodeFixProvider)), Shared]
    public class MembersSortCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MembersSortAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }
        
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            foreach (var diagnostic in context.Diagnostics.Where(d => FixableDiagnosticIds.Contains(d.Id)))
            {
                context.RegisterCodeFix(CodeAction.Create("Arrange members by accessibility",
                    token => GetTransformedDocumentAsync(context.Document, diagnostic, token)), diagnostic);
            }

            await Task.FromResult(Task.CompletedTask);
        }


        private static async Task<Document> GetTransformedDocumentAsync(Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            try
            {
                SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
                var members = root.DescendantNodes().Where(r =>
                    r.IsKind(SyntaxKind.PropertyDeclaration) || r.IsKind(SyntaxKind.MethodDeclaration)).ToList();
                var sortedMembers = members.OrderByDescending(m => RetrieveAccessibility(m, semanticModel)).ToList();
                var docEditor = await DocumentEditor.CreateAsync(document, cancellationToken);
                for (int i = 0; i < members.Count; i++)
                {
                    docEditor.ReplaceNode(members[i], sortedMembers[i]);
                }
                return docEditor.GetChangedDocument();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return document;
            }
        }
        private static Accessibility RetrieveAccessibility(SyntaxNode m, SemanticModel semanticModel)
        {
            var model = semanticModel.Compilation.GetSemanticModel(m.SyntaxTree);
            var accessibility = model.GetDeclaredSymbol(m).DeclaredAccessibility;
            return accessibility;
        }
    }
}

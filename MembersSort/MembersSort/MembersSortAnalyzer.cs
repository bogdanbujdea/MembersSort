using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using System.Linq;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace MembersSort
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MembersSortAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "MembersSort";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        private static DiagnosticDescriptor _rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(_rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var classSymbol = context.Symbol as INamedTypeSymbol;
            if (classSymbol?.TypeKind != TypeKind.Class)
                return; //we only need classes, not other types like enum, struct, etc.
            var members = GetMembersFromClass(classSymbol); //retrieve only methods and properties
            var unorderedMember = GetUnorderedMember(members); //then find the first member that is out of order
            if (unorderedMember != null)
            {
                var rule = new DiagnosticDescriptor(DiagnosticId, Title, $"{unorderedMember.Name} should be moved", Category,
                    DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
                //now we're creating a rule that tells the user to move the member lower in the file
                var diagnostic = Diagnostic.Create(rule, unorderedMember.Locations[0], classSymbol);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static ISymbol GetUnorderedMember(List<ISymbol> members)
        {
            //I'm just sorting the members by DeclaredAccessibility, then comparing them with the order of
            //the initial members
            var orderedByAccessibility = members.OrderByDescending(m => m.DeclaredAccessibility).ToList();
            for (var i = 0; i < members.Count; i++)
            {
                if (members[i].DeclaredAccessibility != orderedByAccessibility[i].DeclaredAccessibility)
                {
                    if (orderedByAccessibility[i].DeclaredAccessibility < members[i].DeclaredAccessibility)
                    {
                        return members[i];
                    }
                }
            }

            return null;
        }

        private static List<ISymbol> GetMembersFromClass(INamedTypeSymbol classSymbol)
        {
            var members = new List<ISymbol>();
            foreach (var member in classSymbol.GetMembers())
            {
                if (member.Kind == SymbolKind.Method)
                {
                    //I'm not interested in the constructor because it could be private for singletons and on top of the file
                    //also a method may be public but it's getter or setter may be private so I'm only interested in the property access
                    var methodMember = member as IMethodSymbol;
                    if (methodMember.MethodKind == MethodKind.Constructor ||
                        methodMember.MethodKind == MethodKind.PropertyGet || methodMember.MethodKind == MethodKind.PropertySet)
                        continue;
                }

                //same with fields, they usually go on top of the file so we shouldn't ask the
                //user to move them down
                if (member.Kind == SymbolKind.Field)
                {
                    continue;
                }
                //there are other cases probably, but as I said before this is just for demo purposes
                //and I'm finding them as I use the analyzer with my code
                members.Add(member);
            }

            return members;
        }
    }
}

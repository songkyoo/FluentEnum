using Microsoft.CodeAnalysis.CSharp;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFacts;

namespace Macaron.FluentEnum;

public static class NamingHelpers
{
    public static string GetCamelCaseName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name[1..] : "");
    }

    public static string GetEscapedKeyword(string keyword)
    {
        return GetKeywordKind(keyword) != SyntaxKind.None || GetContextualKeywordKind(keyword) != SyntaxKind.None
            ? "@" + keyword
            : keyword;
    }
}

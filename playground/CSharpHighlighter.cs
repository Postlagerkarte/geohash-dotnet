using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace Geohash.Playground;

/// <summary>
/// Minimal C# tokenizer for the live-code panel. The snippets are generated
/// by the app itself, so this only needs to cover the constructs we emit.
/// </summary>
public static partial class CSharpHighlighter
{
    [GeneratedRegex("""
        (?<cm>//[^\n]*)
        |(?<st>"(?:[^"\\]|\\.)*")
        |(?<nu>\b\d[\d_]*(?:\.\d+)?\b)
        |(?<kw>\b(?:var|new|using|int|double|string|bool|true|false|null|out|foreach|in|return|static)\b)
        |(?<ty>\b(?:Geohasher|RadiusHasher|PolygonHasher|GeohashCompressor|BoundingBox|Direction|GeohashInclusionCriteria|Progress|GeometryFactory|Coordinate|Polygon|LinearRing|Precision|HashSet|List|Dictionary)\b)
        |(?<mt>(?<=\.)[A-Z][A-Za-z0-9]*(?=\())
        """, RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex Tokens();

    public static MarkupString Highlight(string code)
    {
        var sb = new StringBuilder(code.Length + 256);
        int pos = 0;

        foreach (Match m in Tokens().Matches(code))
        {
            if (m.Index > pos)
                sb.Append(WebUtility.HtmlEncode(code[pos..m.Index]));

            string cls =
                m.Groups["cm"].Success ? "tok-cm" :
                m.Groups["st"].Success ? "tok-st" :
                m.Groups["nu"].Success ? "tok-nu" :
                m.Groups["kw"].Success ? "tok-kw" :
                m.Groups["ty"].Success ? "tok-ty" : "tok-mt";

            sb.Append("<span class=\"").Append(cls).Append("\">")
              .Append(WebUtility.HtmlEncode(m.Value))
              .Append("</span>");

            pos = m.Index + m.Length;
        }

        if (pos < code.Length)
            sb.Append(WebUtility.HtmlEncode(code[pos..]));

        return new MarkupString(sb.ToString());
    }
}

using System.Text;

namespace HDRPilot.GameDetection;

// Small, bounded reader for Valve KeyValues text manifests; no launcher binaries loaded.
public static class VdfReader
{
    public static IReadOnlyList<(string Key, string Value)> Pairs(string text)
    {
        if (text.Length > 4 * 1024 * 1024) throw new InvalidDataException("Manifest too large.");
        var tokens = new List<string>();
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsWhiteSpace(text[i])) continue;
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/') { while (i < text.Length && text[i] != '\n') i++; continue; }
            if (text[i] is '{' or '}') { tokens.Add(text[i].ToString()); continue; }
            if (text[i] != '"') continue;
            var value = new StringBuilder();
            for (i++; i < text.Length && text[i] != '"'; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '\\' or '"') i++;
                value.Append(text[i]);
            }
            tokens.Add(value.ToString());
        }
        var result = new List<(string, string)>();
        for (var i = 0; i + 1 < tokens.Count; i++)
            if (tokens[i] is not ("{" or "}") && tokens[i + 1] is not ("{" or "}")) { result.Add((tokens[i], tokens[i + 1])); i++; }
        return result;
    }
}

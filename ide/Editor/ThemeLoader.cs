using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace OCIDE.Editor
{
    public class JsonTheme
    {
        [JsonPropertyName("language")]
        public string Language { get; set; }

        [JsonPropertyName("extensions")]
        public string[] Extensions { get; set; }

        [JsonPropertyName("syntax_colors")]
        public JsonSyntaxColor[] SyntaxColors { get; set; }
    }

    public class JsonSyntaxColor
    {
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("regex")]
        public string Regex { get; set; }

        [JsonPropertyName("colors")]
        public JsonColor Colors { get; set; }
    }

    public class JsonColor
    {
        [JsonPropertyName("dark")]
        public string Dark { get; set; }

        [JsonPropertyName("light")]
        public string Light { get; set; }
    }

    public static class ThemeLoader
    {
        public static IHighlightingDefinition LoadFromJson(string filePath, bool isDarkTheme = true)
        {
            // Ensure absolute path so it works regardless of working directory
            if (!System.IO.Path.IsPathRooted(filePath))
            {
                filePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
            }

            if (!File.Exists(filePath)) return null;

            JsonTheme theme = null;
            try
            {
                string jsonString = File.ReadAllText(filePath);
                theme = JsonSerializer.Deserialize<JsonTheme>(jsonString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse theme JSON {filePath}: {ex.Message}");
                return null;
            }

            if (theme == null) return null;

            string extString = string.Join(";", theme.Extensions);
            string themeSuffix = isDarkTheme ? "Dark" : "Light";
            string defName = $"{theme.Language}{themeSuffix}";

            string xml = $@"<?xml version=""1.0""?>
            <SyntaxDefinition name=""{defName}"" extensions=""{extString}"" xmlns=""http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008"">
";
            
            // Define colors
            foreach (var rule in theme.SyntaxColors)
            {
                string colorHex = isDarkTheme ? rule.Colors.Dark : rule.Colors.Light;
                xml += $@"<Color name=""{rule.TokenType}"" foreground=""{colorHex}"" />" + "\n";
            }

            xml += "<RuleSet>\n";
            
            // Hardcode spans for known multiline elements as regex in JSON is typically single-line interpreted by AvalonEdit Rule
            foreach (var rule in theme.SyntaxColors)
            {
                // Simple heuristic to make common multilines work in AvalonEdit
                if (rule.TokenType == "comment" && rule.Regex.Contains(@"\*/"))
                {
                    xml += $@"<Span color=""{rule.TokenType}"" multiline=""true""><Begin>/\*</Begin><End>\*/</End></Span>" + "\n";
                    if (rule.Regex.Contains(@"//")) {
                        xml += $@"<Span color=""{rule.TokenType}"" begin=""//"" />" + "\n";
                    }
                }
                else if (rule.TokenType == "comment" && rule.Regex.Contains(@"<!--"))
                {
                    xml += $@"<Span color=""{rule.TokenType}"" multiline=""true""><Begin>&lt;!--</Begin><End>--&gt;</End></Span>" + "\n";
                }
                else if (rule.TokenType == "comment" && rule.Regex.Contains(@"#"))
                {
                    xml += $@"<Span color=""{rule.TokenType}"" begin=""#"" />" + "\n";
                }
                else if (rule.TokenType == "string" && theme.Language == "python")
                {
                    xml += $@"<Span color=""{rule.TokenType}"" multiline=""true""><Begin>\""\""\""</Begin><End>\""\""\""</End></Span>" + "\n";
                    xml += $@"<Span color=""{rule.TokenType}"" multiline=""true""><Begin>'''</Begin><End>'''</End></Span>" + "\n";
                    xml += $@"<Span color=""{rule.TokenType}""><Begin>\""</Begin><End>\""</End></Span>" + "\n";
                    xml += $@"<Span color=""{rule.TokenType}""><Begin>'</Begin><End>'</End></Span>" + "\n";
                }
                else if (rule.TokenType == "string" && theme.Language == "javascript")
                {
                    xml += $@"<Span color=""{rule.TokenType}"" multiline=""true""><Begin>`</Begin><End>`</End></Span>" + "\n";
                    xml += $@"<Span color=""{rule.TokenType}""><Begin>\""</Begin><End>\""</End></Span>" + "\n";
                    xml += $@"<Span color=""{rule.TokenType}""><Begin>'</Begin><End>'</End></Span>" + "\n";
                }
                else if (rule.TokenType == "string" && theme.Language == "html")
                {
                    xml += $@"<Span color=""{rule.TokenType}""><Begin>\""</Begin><End>\""</End></Span>" + "\n";
                    xml += $@"<Span color=""{rule.TokenType}""><Begin>'</Begin><End>'</End></Span>" + "\n";
                }
                else if (rule.TokenType == "code_block" && theme.Language == "markdown")
                {
                    xml += $@"<Span color=""{rule.TokenType}"" multiline=""true""><Begin>```</Begin><End>```</End></Span>" + "\n";
                }
                else if (rule.TokenType == "inline_code" && theme.Language == "markdown")
                {
                    xml += $@"<Span color=""{rule.TokenType}"" multiline=""false""><Begin>`</Begin><End>`</End></Span>" + "\n";
                }
                else 

                {
                    // Regular Regex rule mapping
                    // Escape special XML characters in regex like < > &
                    string safeRegex = SecurityElement.Escape(rule.Regex);
                    xml += $@"<Rule color=""{rule.TokenType}"">{safeRegex}</Rule>" + "\n";
                }
            }

            xml += "</RuleSet>\n</SyntaxDefinition>";

            try 
            {
                using (var reader = new System.Xml.XmlTextReader(new System.IO.StringReader(xml)))
                {
                    return HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme {filePath}: {ex.Message}");
                return null;
            }
        }
    }
}

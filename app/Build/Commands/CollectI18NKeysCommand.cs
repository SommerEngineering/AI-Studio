using System.Text.RegularExpressions;

using SharedTools;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Build.Commands;

public sealed partial class CollectI18NKeysCommand
{
    [Command("collect-i18n", Description = "Collect I18N keys")]
    public async Task CollectI18NKeys()
    {
        if(!Environment.IsWorkingDirectoryValid())
            return;

        Console.WriteLine("=========================");
        Console.Write("- Collecting I18N keys ...");
        
        var cwd = Environment.GetAIStudioDirectory();
        var binPath = Path.Join(cwd, "bin");
        var objPath = Path.Join(cwd, "obj");
        var wwwrootPath = Path.Join(cwd, "wwwroot");
        var allFiles = Directory.EnumerateFiles(cwd, "*", SearchOption.AllDirectories);
        var counter = 0;
        
        var allI18NContent = new Dictionary<string, string>();
        foreach (var filePath in allFiles)
        {
            counter++;
            if(filePath.StartsWith(binPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            if(filePath.StartsWith(objPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            if(filePath.StartsWith(wwwrootPath, StringComparison.OrdinalIgnoreCase))
                continue;
            
            var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var matches = this.FindAllTextTags(content);
            if (matches.Count == 0)
                continue;
            
            var ns = this.DetermineNamespace(filePath);
            var fileInfo = new FileInfo(filePath);
            var name = fileInfo.Name.Replace(fileInfo.Extension, string.Empty);
            var langNamespace = $"{ns}.{name}".ToUpperInvariant();
            foreach (var match in matches)
            {
                // The key in the format A.B.C.D.T{hash}:
                var key = $"UI_TEXT_CONTENT.{langNamespace}.T{match.ToFNV32()}";
                allI18NContent.TryAdd(key, match);
            }
        }
        
        Console.WriteLine($" {counter:###,###} files processed, {allI18NContent.Count:###,###} keys found.");
        
        Console.Write("- Creating Lua code ...");
        var luaCode = this.ExportToLuaTable(allI18NContent);
        
        // Build the path, where we want to store the Lua code:
        var luaPath = Path.Join(cwd, "Assistants", "I18N", "allTexts.lua");
        
        // Store the Lua code:
        await File.WriteAllTextAsync(luaPath, luaCode, Encoding.UTF8);
        
        Console.WriteLine(" done.");
    }
    
    private string ExportToLuaTable(Dictionary<string, string> keyValuePairs)
    {
        // Collect all nodes:
        var root = new Dictionary<string, object>();
        
        //
        // Split all collected keys into nodes:
        //
        foreach (var key in keyValuePairs.Keys.Order())
        {
            var path = key.Split('.');
            var current = root;
            for (var i = 0; i < path.Length - 1; i++)
            {
                // We ignore the AISTUDIO segment of the path:
                if(path[i] == "AISTUDIO")
                    continue;
                
                if (!current.TryGetValue(path[i], out var child) || child is not Dictionary<string, object> childDict)
                {
                    childDict = new Dictionary<string, object>();
                    current[path[i]] = childDict;
                }
                
                current = childDict;
            }
            
            current[path.Last()] = keyValuePairs[key];
        }

        //
        // Inner method to build Lua code from the collected nodes:
        //
        void ToLuaTable(StringBuilder sb, Dictionary<string, object> innerDict, int indent = 0)
        {
            sb.AppendLine("{");
            var prefix = new string(' ', indent * 4);
            foreach (var kvp in innerDict)
            {
                if (kvp.Value is Dictionary<string, object> childDict)
                {
                    sb.Append($"{prefix}    {kvp.Key}");
                    sb.Append(" = ");
                    
                    ToLuaTable(sb, childDict, indent + 1);
                }
                else if (kvp.Value is string s)
                {
                    sb.AppendLine($"{prefix}    -- {s.Trim().Replace("\n", " ")}");
                    sb.Append($"{prefix}    {kvp.Key}");
                    sb.Append(" = ");
                    sb.Append($"""
                               "{s}"
                               """);
                    sb.AppendLine(",");
                    sb.AppendLine();
                }
            }
            
            sb.AppendLine(prefix + "},");
            sb.AppendLine();
        }
        
        //
        // Write the Lua code:
        //
        var sbLua = new StringBuilder();
        
        // To make the later parsing easier, we add the mandatory plugin
        // metadata:
        sbLua.AppendLine(
        """
        -- The ID for this plugin:
        ID = "77c2688a-a68f-45cc-820e-fa8f3038a146"
        
        -- The icon for the plugin:
        ICON_SVG = ""
        
        -- The name of the plugin:
        NAME = "Collected I18N keys"
        
        -- The description of the plugin:
        DESCRIPTION = "This plugin is not meant to be used directly. Its a collection of all I18N keys found in the project."
        
        -- The version of the plugin:
        VERSION = "1.0.0"
        
        -- The type of the plugin:
        TYPE = "LANGUAGE"
        
        -- The authors of the plugin:
        AUTHORS = {"MindWork AI Community"}
        
        -- The support contact for the plugin:
        SUPPORT_CONTACT = "MindWork AI Community"
        
        -- The source URL for the plugin:
        SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"
        
        -- The categories for the plugin:
        CATEGORIES = { "CORE" }
        
        -- The target groups for the plugin:
        TARGET_GROUPS = { "EVERYONE" }
        
        -- The flag for whether the plugin is maintained:
        IS_MAINTAINED = true
        
        -- When the plugin is deprecated, this message will be shown to users:
        DEPRECATION_MESSAGE = ""
        
        -- The IETF BCP 47 tag for the language. It's the ISO 639 language
        -- code followed by the ISO 3166-1 country code:
        IETF_TAG = "en-US"
        
        -- The language name in the user's language:
        LANG_NAME = "English (United States)"
        
        """);
        
        sbLua.Append("UI_TEXT_CONTENT = ");
        if(root["UI_TEXT_CONTENT"] is Dictionary<string, object> dict)
            ToLuaTable(sbLua, dict);
        
        return sbLua.ToString();
    }
    
    private List<string> FindAllTextTags(ReadOnlySpan<char> fileContent)
    {
        const string START_TAG = """
                                 T("
                                 """;
        
        const string END_TAG = """
                               ")
                               """;

        var matches = new List<string>();
        var startIdx = fileContent.IndexOf(START_TAG);
        var content = fileContent;
        while (startIdx > -1)
        {
            content = content[(startIdx + START_TAG.Length)..];
            var endIdx = content.IndexOf(END_TAG);
            if (endIdx == -1)
                break;
            
            var match = content[..endIdx];
            matches.Add(match.ToString());
            
            startIdx = content.IndexOf(START_TAG);
        }
        
        return matches;
    }
    
    private string? DetermineNamespace(string filePath)
    {
        // Is it a C# file? Then we can read the namespace from it:
        if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return this.ReadNamespaceFromCSharp(filePath);

        // Is it a Razor file? Then, it depends:
        if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
        {
            // Check if the file contains a namespace declaration:
            var blazorNamespace = this.ReadNamespaceFromRazor(filePath);
            if (blazorNamespace != null)
                return blazorNamespace;
            
            // Alright, no namespace declaration. Let's check the corresponding C# file:
            var csFilePath = $"{filePath}.cs";
            if (File.Exists(csFilePath))
            {
                var csNamespace = this.ReadNamespaceFromCSharp(csFilePath);
                if (csNamespace != null)
                    return csNamespace;
                
                Console.WriteLine($"- Error: Neither the blazor file '{filePath}' nor the corresponding C# file '{csFilePath}' contain a namespace declaration.");
                return null;
            }

            Console.WriteLine($"- Error: The blazor file '{filePath}' does not contain a namespace declaration and the corresponding C# file '{csFilePath}' does not exist.");
            return null;
        }
        
        // Not a C# or Razor file. We can't determine the namespace:
        Console.WriteLine($"- Error: The file '{filePath}' is neither a C# nor a Razor file. We can't determine the namespace.");
        return null;
    }
    
    private string? ReadNamespaceFromCSharp(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var matches = CSharpNamespaceRegex().Matches(content);
        
        if (matches.Count == 0)
            return null;

        if (matches.Count > 1)
        {
            Console.WriteLine($"The file '{filePath}' contains multiple namespaces. This scenario is not supported.");
            return null;
        }
        
        var match = matches[0];
        return match.Groups[1].Value;
    }
    
    private string? ReadNamespaceFromRazor(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var matches = BlazorNamespaceRegex().Matches(content);
        
        if (matches.Count == 0)
            return null;

        if (matches.Count > 1)
        {
            Console.WriteLine($"The file '{filePath}' contains multiple namespaces. This scenario is not supported.");
            return null;
        }
        
        var match = matches[0];
        return match.Groups[1].Value;
    }

    [GeneratedRegex("""@namespace\s+([a-zA-Z0-9_.]+)""")]
    private static partial Regex BlazorNamespaceRegex();
    
    [GeneratedRegex("""namespace\s+([a-zA-Z0-9_.]+)""")]
    private static partial Regex CSharpNamespaceRegex();
}
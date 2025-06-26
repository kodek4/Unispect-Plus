using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Unispect.SDK
{
    public static class Utilities
    {
        private static readonly Dictionary<ulong, string> UnknownClassNameCache = new Dictionary<ulong, string>();
        private static Dictionary<string, int> _prefixIndexer;

        private static Dictionary<string, int> PrefixIndexer
        {
            get
            {
                if (_prefixIndexer != null)
                    return _prefixIndexer;

                _prefixIndexer = new Dictionary<string, int>();
                foreach (var e in Enum.GetNames(typeof(UnknownPrefix)))
                    _prefixIndexer.Add(e, 0);

                return _prefixIndexer;
            }
        }

        public static string ToUnknownClassString(this byte[] _, UnknownPrefix prefix, uint token)
        {
            var hash = (token - 0x2000000) * (uint)prefix;
            if (UnknownClassNameCache.ContainsKey(hash))
                return UnknownClassNameCache[hash];

            var prefixName = Enum.GetName(typeof(UnknownPrefix), prefix);
            var str = $"{prefixName}{hash:X4}";
            UnknownClassNameCache.Add(hash, str);

            return str;
        }

        public static string GetSimpleTypeKeyword(this string text)
        {
            var ret = text.Replace("System.", "");
            switch (ret)
            {
                case "Void": return "void";
                case "Object": return "object";
                case "String": return "string";
                case "Boolean": return "bool";
                case "Single": return "float";
                case "Double": return "double";
                case "Byte": return "byte";
                case "SByte": return "sbyte";
                case "Int16": return "short";
                case "Int32": return "int";
                case "Int64": return "long";
                case "UInt16": return "ushort";
                case "UInt32": return "uint";
                case "UInt64": return "ulong";
            }

            return ret;
        }

        public static IEnumerable<int> Step(int fromInclusive, int toExclusive, int step)
        {
            for (var i = fromInclusive; i < toExclusive; i += step)
            {
                yield return i;
            }
        }

        public static string ToAsciiString(this byte[] buffer, int start = 0)
        {
            var length = 0;
            for (var i = start; i < buffer.Length; i++)
            {
                if (buffer[i] != 0) continue;

                length = i - start;
                break;
            }

            return Encoding.ASCII.GetString(buffer, start, length);
        }

        public static string LowerChar(this string str, int index = 0)
        {
            if (index < str.Length && index > -1)
            {
                if (index == 0)
                    return char.ToLower(str[index]) + str.Substring(index + 1);

                return str.Substring(0, index - 1) + char.ToLower(str[index]) + str.Substring(index + 1);
            }

            return str;
        }

        public static string FormatFieldText(this string text)
        {
            var ret = text.Replace("[]", "Array");
            var lessThanIndex = ret.IndexOf('<');
            if (lessThanIndex > -1)
            {
                ret = ret.Substring(0, lessThanIndex);
            }

            return ret;
        }

        public static string SanitizeFileName(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;
                
            var invalidChars = Path.GetInvalidFileNameChars();
            // Use a safer approach to replace invalid characters
            var result = fileName;
            foreach (var c in invalidChars)
            {
                result = result.Replace(c, '_');
            }
            
            return result;
        }

        public static int ToInt32(this byte[] buffer, int start = 0) => BitConverter.ToInt32(buffer, start);

        /// <summary>
        /// Gets the version of the entry assembly (e.g., the running .exe).
        /// Falls back to the SDK assembly version if the entry assembly is unavailable.
        /// </summary>
        public static string CurrentVersion
        {
            get
            {
                var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            }
        }

        public static string GithubLink => "https://github.com/kodek4/Unispect-Plus";

        /// <summary>
        /// SDK-friendly URL launcher without WPF dependencies
        /// </summary>
        public static void LaunchUrl(string url)
        {
            try
            {
                // Using cmd.exe's 'start' command is a robust way to open a URL
                // in the user's default browser, avoiding file-not-found issues.
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") 
                { 
                    CreateNoWindow = true 
                });
            }
            catch (Exception ex)
            {
                // In SDK context, just log the error instead of showing dialog
                Log.Exception($"Couldn't open URL: {url}", ex);
            }
        }
    }
}
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace Unispect.SDK
{
    public static class Serializer
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented
        };

        private static readonly JsonSerializerSettings JsonSettingsCompressed = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.None
        };

        public static void Save(string filePath, object objectToSerialize)
        {
            try
            {
                // Ensure target directory exists before attempting to write
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                Log.Add($"DEBUG: Serializer.Save - filePath = '{filePath}'");
                var json = JsonConvert.SerializeObject(objectToSerialize, JsonSettings);
                File.WriteAllText(filePath, json, Encoding.UTF8);
                Log.Add("DEBUG: Serializer.Save - File write successful");
            }
            catch (Exception ex)
            {
                // Log and re-throw so callers can handle/report the failure appropriately
                Log.Add($"ERROR: Serializer.Save - {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }

        public static void SaveCompressed(string filePath, object objectToSerialize)
        {
            // Todo: maybe implement a progress indicator by wrapping the stream
            try
            {
                var json = JsonConvert.SerializeObject(objectToSerialize, JsonSettingsCompressed);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                using (Stream fileStream = File.Open(filePath, FileMode.Create))
                using (var compressedStream = new GZipStream(fileStream, CompressionMode.Compress))
                {
                    compressedStream.Write(bytes, 0, bytes.Length);
                }
            }
            catch (IOException)
            {
            }
        }

        public static T Load<T>(string filePath) where T : new()
        {
            var result = new T();

            try
            {
                if (File.Exists(filePath))
                {
                    Log.Add($"DEBUG: Loading file: {filePath}");
                    var json = File.ReadAllText(filePath, Encoding.UTF8);
                    Log.Add($"DEBUG: File read successfully, JSON length: {json.Length}");
                    result = JsonConvert.DeserializeObject<T>(json) ?? new T();
                    Log.Add($"DEBUG: JSON deserialization successful");
                }
                else
                {
                    Log.Add($"DEBUG: File does not exist: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Log.Add($"ERROR: Exception in Load: {ex.GetType().Name}: {ex.Message}");
                Log.Add($"ERROR: StackTrace: {ex.StackTrace}");
                try
                {
                    if (File.Exists(filePath))
                    {
                        var snippet = File.ReadAllText(filePath, Encoding.UTF8);
                        var preview = snippet.Length > 1024 ? snippet.Substring(0, 1024) : snippet;
                        Log.Add($"ERROR: File preview (first 1024 chars):\n{preview}");
                    }
                }
                catch (Exception fileEx)
                {
                    Log.Add($"ERROR: Could not read file for preview: {fileEx.Message}");
                }
                throw;
            }

            return result;
        }
        
        public static T LoadCompressed<T>(string filePath) where T : new()
        {
            var result = new T();

            try
            {
                using (Stream fileStream = File.Open(filePath, FileMode.Open)) 
                using (var decompressStream = new GZipStream(fileStream, CompressionMode.Decompress))
                using (var reader = new StreamReader(decompressStream, Encoding.UTF8))
                {
                    var json = reader.ReadToEnd();
                    result = JsonConvert.DeserializeObject<T>(json) ?? new T();
                }
            }
            catch (IOException)
            {
            }

            return result;
        }
    }
}
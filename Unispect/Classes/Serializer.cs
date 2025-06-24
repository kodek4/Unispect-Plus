using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;

namespace Unispect
{
    public static class Serializer
    {
        public static void Save(string filePath, object objectToSerialize)
        {
            try
            {
                var json = JsonConvert.SerializeObject(objectToSerialize, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (IOException)
            {
            }
        }

        public static void SaveCompressed(string filePath, object objectToSerialize)
        {
            // Todo: maybe implement a progress indicator by wrapping the stream
            try
            {
                var json = JsonConvert.SerializeObject(objectToSerialize, Formatting.None);
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
                    var json = File.ReadAllText(filePath, Encoding.UTF8);
                    result = JsonConvert.DeserializeObject<T>(json) ?? new T();
                }
            }
            catch (IOException)
            {
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
namespace Unispect.SDK.Models
{
    public class CacheFile
    {
        public int RawClassCount { get; set; }
        public System.Collections.Generic.List<Unispect.SDK.TypeDefWrapper> TypeDefinitions { get; set; } = new System.Collections.Generic.List<Unispect.SDK.TypeDefWrapper>();
    }
} 
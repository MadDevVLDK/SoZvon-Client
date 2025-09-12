namespace SoZvon.SubClasses
{
    public static class DictionaryExtensions
    {
        public static bool TryGetValue<T>(this Dictionary<string, object> dict, string key, out T value)
        {
            value = default!;
            if (dict.TryGetValue(key, out var obj) && obj is T typedValue)
            {
                value = typedValue;
                return true;
            }
            return false;
        }
    }
}

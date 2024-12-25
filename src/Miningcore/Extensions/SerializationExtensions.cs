using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Miningcore.Extensions;

public static class SerializationExtensions
{
    private static readonly JsonSerializerSettings settings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        }
    };

    private static readonly JsonSerializer serializer = JsonSerializer.Create(settings);

    public static T SafeExtensionDataAs<T>(this IDictionary<string, object> extra, params string[] wrappers)
    {
        if(extra == null)
            return default;

        try
        {
            object o = extra;
            Console.WriteLine($"SafeExtensionDataAs input: {JsonConvert.SerializeObject(o)}");

            // Если есть вложенный объект extra, используем его
            if (o is IDictionary<string, object> extraDict && extraDict.ContainsKey("extra"))
            {
                o = extraDict["extra"];
            }

            foreach (var key in wrappers)
            {
                if(o is IDictionary<string, object> dict)
                {
                    o = dict[key];
                    Console.WriteLine($"After dict access with key {key}: {JsonConvert.SerializeObject(o)}");
                }
                else if(o is JObject jo)
                {
                    o = jo[key];
                    Console.WriteLine($"After JObject access with key {key}: {JsonConvert.SerializeObject(o)}");
                }
                else
                    throw new NotSupportedException("Unsupported child element type");
            }

            var json = JsonConvert.SerializeObject(o);
            Console.WriteLine($"JSON: {json}");
            var result = JsonConvert.DeserializeObject<T>(json, settings);
            Console.WriteLine($"Result: {JsonConvert.SerializeObject(result)}");
            return result;
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error in SafeExtensionDataAs: {ex}");
            return default;
        }
    }
}

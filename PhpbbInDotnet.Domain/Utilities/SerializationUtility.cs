using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PhpbbInDotnet.Domain.Utilities
{
    public static class SerializationUtility
    {
        public static string ToCamelCaseJson<T>(T @object)
            => JsonConvert.SerializeObject(
                @object,
                Formatting.None,
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
            );
    }
}

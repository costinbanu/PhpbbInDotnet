using PhpbbInDotnet.Utilities;
using Serilog;
using System;

namespace PhpbbInDotnet.Languages
{
    public class EnumTranslation : Translation
    {
        public EnumTranslation(ILogger logger) : base("Enums", logger)
        {
        }

        public string this[string language, Enum key, Casing casing = Casing.None]
            => base[language, $"{key.GetType().Name}.{Enum.GetName(key.GetType(), key)}", casing];
    }
}

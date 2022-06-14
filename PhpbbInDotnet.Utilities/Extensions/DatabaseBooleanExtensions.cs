namespace PhpbbInDotnet.Utilities.Extensions
{
    public static class DatabaseBooleanExtensions
    {
        public static bool ToBool(this byte @byte)
            => @byte == 1;

        public static byte ToByte(this bool @bool)
            => (byte)(@bool ? 1 : 0);
    }
}

using System;

namespace PhpbbInDotnet.Database
{
    public class DatabaseException : Exception
    {
        private readonly string? _newStackTrace;

        public override string? StackTrace
            => string.IsNullOrWhiteSpace(_newStackTrace) ? base.StackTrace : _newStackTrace;

        public DatabaseException(string message, string? stackTrace = null) : base(message)
        {
            _newStackTrace = stackTrace;
        }

        public DatabaseException(string message, Exception inner, string? stackTrace = null) : base(message, inner)
        {
            _newStackTrace = stackTrace;
        }
    }
}

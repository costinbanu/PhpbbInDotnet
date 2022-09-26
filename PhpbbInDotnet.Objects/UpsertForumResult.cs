using PhpbbInDotnet.Database.Entities;

namespace PhpbbInDotnet.Objects
{
    public class UpsertForumResult
    {
        public UpsertForumResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message;
        }

        public bool IsSuccess { get; }
        public string Message { get; }
        public PhpbbForums? Forum { get; set; }
    }
}

using PhpbbInDotnet.Utilities;

namespace PhpbbInDotnet.Objects
{
    public class ExtendedPostDto : PostDto
    {
        public string UserAvatar { get; set; }

        private long _postTime;
        public long PostTime
        {
            get => _postTime;
            set
            {
                _postTime = value;
                PostCreationTime = value.ToUtcTime();
            }
        }
    }
}

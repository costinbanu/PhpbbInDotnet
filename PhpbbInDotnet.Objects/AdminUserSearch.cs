namespace PhpbbInDotnet.Objects
{
    public class AdminUserSearch
    {
        public string Username { get; set; }
        
        public string Email { get; set; } 
        
        public int? UserId { get; set; }
        
        public string RegisteredFrom { get; set; }
        
        public string RegisteredTo { get; set; } 
        
        public string LastActiveFrom { get; set; }
        
        public string LastActiveTo { get; set; }
        
        public bool NeverActive { get; set; }
    }
}

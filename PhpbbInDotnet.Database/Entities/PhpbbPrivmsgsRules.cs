namespace PhpbbInDotnet.Database.Entities
{
    public partial class PhpbbPrivmsgsRules
    {
        public int RuleId { get; set; }
        public int UserId { get; set; }
        public int RuleCheck { get; set; }
        public int RuleConnection { get; set; }
        public string RuleString { get; set; }
        public int RuleUserId { get; set; }
        public int RuleGroupId { get; set; }
        public int RuleAction { get; set; }
        public int RuleFolderId { get; set; }
    }
}

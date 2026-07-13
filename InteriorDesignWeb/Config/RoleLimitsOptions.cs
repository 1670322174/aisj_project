namespace InteriorDesignWeb.Config
{
    public class RoleLimitsOptions
    {
        public Dictionary<string, int> SchemeCreation { get; set; } = new();
        public Dictionary<string, int> ImagesPerScheme { get; set; } = new();
        public Dictionary<string, int> AIGenerationUnits { get; set; } = new();
        public Dictionary<string, int> AssistantTokens5Hours { get; set; } = new();

        // 可选：添加配置验证
        //public bool IsValid()
        //{
        //    var requiredKeys = new[] { "FreeUser", "Member", "PremiumMember" };
        //    return requiredKeys.All(key =>
        //        SchemeCreation.ContainsKey(key) &&
        //        ImagesPerScheme.ContainsKey(key)
        //    );
        //}
    }
}

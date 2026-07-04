using COSXML.Auth;
using TencentCloud.Sts.V20180813.Models;

namespace InteriorDesignWeb
{

    using COSXML.Auth;
    using COSXML.Common;

    public class CustomQCloudCredentialProvider : DefaultSessionQCloudCredentialProvider
    {
        private readonly string _tmpSecretId;
        private readonly string _tmpSecretKey;
        private readonly string _tmpToken;

        public CustomQCloudCredentialProvider(
            string tmpSecretId,
            string tmpSecretKey,
            string tmpToken) : base(null, null, 0L, null)
        {
            _tmpSecretId = tmpSecretId;
            _tmpSecretKey = tmpSecretKey;
            _tmpToken = tmpToken;
            Refresh(); // 初始设置
        }

        public override void Refresh()
        {
            // 实际项目应从STS服务获取新密钥
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long expiredTime = startTime + 3600; // 有效期1小时

            SetQCloudCredential(
                _tmpSecretId,
                _tmpSecretKey,
                $"{startTime};{expiredTime}",
                _tmpToken
            );
        }
    }
}

using Nop.Core;
using Nop.Core.Data;
using Nop.Core.Infrastructure;
using WddcApiClient.Infrastructure;
using WddcApiClient.Services;

namespace Nop.Plugin.Wddc.Core
{
    public class EfStartUpTask : IStartupTask
    {
        public void Execute()
        {
            var dataSettings = EngineContext.Current.Resolve<DataSettings>();

            Urls.DefaultBaseUrl = dataSettings.TestMode ? dataSettings.WddcTestApiUrl : dataSettings.WddcApiUrl;
            if (string.IsNullOrEmpty(ApiConfiguration.GetRefreshToken()))
            {
                var username = dataSettings.TestMode ? dataSettings.WddcTestApiUsername : dataSettings.WddcApiUsername;
                var password = dataSettings.TestMode ? dataSettings.WddcTestApiPassword : dataSettings.WddcApiPassword;

                if (username == null || password == null) // needed for install
                    return;

                var tokenResult = TokenService.LoginAsync(
                    dataSettings.ClientId,
                    dataSettings.ClientSecret,
                    username,
                    password
                ).Result;
                if (tokenResult.IsError)
                    throw new NopException($"Error logging into api: {tokenResult.Error}");
            }
        }

        public int Order
        {
            get { return 0; }
        }
    }
}

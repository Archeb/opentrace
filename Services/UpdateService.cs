using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenTrace.Infrastructure;

namespace OpenTrace.Services
{
    /// <summary>
    /// 应用程序更新检查服务
    /// </summary>
    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/Archeb/opentrace/releases/latest";

        /// <summary>
        /// 检查是否有新版本可用
        /// </summary>
        /// <param name="currentVersion">当前版本号（不含 v 前缀）</param>
        /// <returns>如果有更新返回新版本号，否则返回 null</returns>
        public async Task<string> CheckForUpdateAsync(string currentVersion)
        {
            if (!UserSettings.checkUpdateOnStartup) return null;

            try
            {
                var httpClient = new System.Net.Http.HttpClient
                {
                    BaseAddress = new Uri(GitHubApiUrl)
                };
                httpClient.DefaultRequestHeaders.Add("User-Agent", "OpenTrace");
                
                var response = await httpClient.GetAsync("");
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var definition = new { tag_name = "" };
                    var json = JsonConvert.DeserializeAnonymousType(result, definition);
                    string latestVersion = json.tag_name;
                    string currentVersionWithPrefix = "v" + currentVersion;
                    
                    if (latestVersion != currentVersionWithPrefix)
                    {
                        return latestVersion;
                    }
                }
            }
            catch
            {
                // 静默处理更新检查错误
            }

            return null;
        }
    }
}

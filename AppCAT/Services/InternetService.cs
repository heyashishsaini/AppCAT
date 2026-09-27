using System.Net.NetworkInformation;
using System.Net.Http;
using System.Threading.Tasks;

namespace AppCAT.Services
{
    public static class InternetService
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = System.TimeSpan.FromSeconds(5)
        };

        /// <summary>
        /// Internet available check
        /// </summary>
        public static async Task<bool> IsInternetAvailableAsync()
        {
            try
            {
                // First check: Network interface available 
                if (!NetworkInterface.GetIsNetworkAvailable())
                    return false;

                // Second check: Actually internet pe reach ho raha hai?
                var response = await _httpClient.GetAsync("https://www.microsoft.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Synchronous quick check
        /// </summary>
        public static bool IsInternetAvailable()
        {
            return IsInternetAvailableAsync().GetAwaiter().GetResult();
        }
    }
}
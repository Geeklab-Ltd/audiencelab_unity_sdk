using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;
using Newtonsoft.Json;


namespace Geeklab.AudiencelabSDK
{
    public class TokenHandler : MonoBehaviour
    {
        private const string TOKEN_KEY = "GeeklabCreativeToken";
        private const int MaxFetchRetries = 10;
        private const float InitialBackoffSeconds = 5f;
        private const float MaxBackoffSeconds = 300f; // 5 minutes cap
        
        private static string creativeToken = "";
        private static bool isRetryRunning;
        private static DateTime? lastFetchAttemptUtc;
        private static string lastFetchStatus;
        private static bool hasLoggedMissingToken;
        private static bool hasLoggedBinToken;
        private static TokenHandler instance;
        private static CancellationTokenSource retryCancellation;
        private static int fetchRetryCount;

        public static event Action<string> OnTokenAvailable;

        
        private void Start()
        {
            CheckToken();
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnApplicationQuit()
        {
            retryCancellation?.Cancel();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                retryCancellation?.Cancel();
            }
        }
        
        
        private void OnApplicationFocus(bool hasFocus)
        {
            // if (hasFocus)
            //     CheckToken();
        }
        

        private void CheckToken()
        {
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Checking token");

            if (HasValidToken())
            {
                OnTokenAvailable?.Invoke(GetValidToken());
                return;
            }

            StartRetryLoop();
        }
        
        
        private static string GetTokenFromText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
    
            var pattern = @"(?:.*:\/\/.*\?geeklab_ct:|\bgeeklab_ct:)\s*(\w+)\s*";
            var match = Regex.Match(input, pattern);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return "";
        }


        private static bool ContainsToken(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var pattern = @"(?:.*:\/\/.*\?geeklab_ct:|\bgeeklab_ct:)\s*(\w+)\s*";
            var match = Regex.Match(input, pattern);

            return match.Success;
        }


        public static string GetCreativeToken()
        {
            creativeToken = PlayerPrefs.GetString(TOKEN_KEY);
            return creativeToken;
        }

        public static bool HasValidToken()
        {
            var token = GetCreativeToken();
            return IsValidToken(token);
        }

        public static string GetValidToken()
        {
            var token = GetCreativeToken();
            return IsValidToken(token) ? token.Trim() : null;
        }


        public static void SetToken(string newToken)
        {   
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Setting token: {newToken}");
            if (!IsValidToken(newToken))
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Ignoring invalid token value");
                return;
            }
            creativeToken = newToken.TrimStart('?');
            SaveTokenLocally();
            lastFetchStatus = "ok";
            OnTokenAvailable?.Invoke(creativeToken);
        }


        private static void SaveTokenLocally()
        {
            PlayerPrefs.SetString(TOKEN_KEY, creativeToken);
            PlayerPrefs.Save();
        }

        public static DateTime? GetLastFetchAttemptUtc()
        {
            return lastFetchAttemptUtc;
        }

        public static string GetLastFetchStatus()
        {
            return lastFetchStatus;
        }

        public static void StartRetryLoop()
        {
            if (isRetryRunning || !Application.isPlaying)
            {
                return;
            }

            if (instance == null)
            {
                return;
            }

            _ = instance.StartRetryLoopAsync();
        }

        private async Task StartRetryLoopAsync()
        {
            isRetryRunning = true;
            retryCancellation?.Cancel();
            retryCancellation = new CancellationTokenSource();
            var cancellationToken = retryCancellation.Token;
            var retryDelaySeconds = InitialBackoffSeconds;

            while (!HasValidToken() && !cancellationToken.IsCancellationRequested)
            {
                if (!SDKSettingsModel.Instance.SendStatistics)
                {
                    lastFetchStatus = "disabled";
                    await Task.Delay(5000, cancellationToken);
                    continue; // Don't count as retry - just waiting for setting to change
                }

                // Don't retry when offline - wait for connectivity to be restored
                // This doesn't count as a retry attempt
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    lastFetchStatus = "offline";
                    if (!hasLoggedMissingToken && SDKSettingsModel.Instance.ShowDebugLog)
                    {
                        Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} No internet; token fetch deferred.");
                        hasLoggedMissingToken = true;
                    }
                    await Task.Delay(5000, cancellationToken);
                    continue;
                }

                // Check max retries (only count actual fetch attempts, not offline waits)
                if (fetchRetryCount >= MaxFetchRetries)
                {
                    lastFetchStatus = "max_retries";
                    if (SDKSettingsModel.Instance.ShowDebugLog)
                    {
                        Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Token fetch stopped after {MaxFetchRetries} attempts. Will retry when connectivity changes.");
                    }
                    break;
                }

                fetchRetryCount++;
                lastFetchAttemptUtc = DateTime.UtcNow;
                lastFetchStatus = "fetching";
                
                if (SDKSettingsModel.Instance.ShowDebugLog)
                {
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Token fetch attempt {fetchRetryCount}/{MaxFetchRetries}");
                }

                var token = await GetTokenFromGeeklab();
                if (IsBinToken(token) && !hasLoggedBinToken)
                {
                    Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Received invalid token value \"bin\" from backend.");
                    hasLoggedBinToken = true;
                }

                if (IsValidToken(token))
                {
                    SetToken(token);
                    fetchRetryCount = 0; // Reset on success
                    break;
                }

                lastFetchStatus = "failed";
                // Exponential backoff with jitter, capped at MaxBackoffSeconds
                var jitter = UnityEngine.Random.Range(0.8f, 1.2f);
                retryDelaySeconds = Mathf.Min(retryDelaySeconds * 2f * jitter, MaxBackoffSeconds);
                await Task.Delay((int)(retryDelaySeconds * 1000f), cancellationToken);
            }

            isRetryRunning = false;
        }

        /// <summary>
        /// Reset retry count - call when connectivity is restored to allow fresh retry attempts.
        /// </summary>
        public static void ResetRetryCount()
        {
            fetchRetryCount = 0;
            hasLoggedMissingToken = false;
        }

        private static bool IsValidToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var trimmed = token.Trim();
            if (IsBinToken(trimmed))
            {
                return false;
            }

            return true;
        }

        private static bool IsBinToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return token.Trim().Equals("bin", StringComparison.OrdinalIgnoreCase);
        }

        
        private static async Task<bool> VerifyCreativeToken(string token)
        {
            if (string.IsNullOrEmpty(token) || !SDKSettingsModel.Instance.SendStatistics) return false;
            
            var taskCompletionSource = new TaskCompletionSource<bool>();

            WebRequestManager.Instance.VerifyCreativeTokenRequest(token,
                (response) =>
                {
                    taskCompletionSource.SetResult(true);
                },
                (error) =>
                {
                    taskCompletionSource.SetResult(false);
                }
            );
            
            return await taskCompletionSource.Task;
        }
        

        public static async Task<string> GetTokenFromGeeklab()
        {
            if (!SDKSettingsModel.Instance.SendStatistics)
                return null;
            
            var taskCompletionSource = new TaskCompletionSource<string>();

            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Fetching token from Geeklab");

            WebRequestManager.Instance.FetchTokenRequest(
                (response) =>
                {
                    try
                    {
                        var tokenResponse = JsonConvert.DeserializeObject<TokenResponseModel>(response);
                        if (tokenResponse != null && tokenResponse.whitelisted_properties != null)
                        {
                            UserPropertiesManager.MergeWhitelistedProperties(tokenResponse.whitelisted_properties);
                        }
                        taskCompletionSource.SetResult(tokenResponse?.token);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"{SDKSettingsModel.GetColorPrefixLog()} Failed to parse token response: {ex.Message}");
                        taskCompletionSource.SetResult(null);
                    }
                },
                (error) =>
                {
                    Debug.LogError($"{error}");
                    taskCompletionSource.SetResult(null);
                }
            );
            
            return await taskCompletionSource.Task;
        }
    }
}
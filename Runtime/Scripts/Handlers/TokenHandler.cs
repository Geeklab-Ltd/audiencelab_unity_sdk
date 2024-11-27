using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using System.Text.RegularExpressions;


namespace Geeklab.AudiencelabSDK
{
    public class TokenHandler : MonoBehaviour
    {
        private const string TOKEN_KEY = "GeeklabCreativeToken";
        private static string creativeToken = "";

        
        private void Start()
        {
            CheckToken();
        }
        
        
        private void OnApplicationFocus(bool hasFocus)
        {
            // if (hasFocus)
            //     CheckToken();
        }
        

        private async void CheckToken()
        {
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Checking token");

            if (!string.IsNullOrEmpty(GetCreativeToken().Trim()))
                return;

            if (GetCreativeToken() == "")
            {
                var token = await GetTokenFromGeeklab();
                    
                SetToken(token);
                if (SDKSettingsModel.Instance.ShowDebugLog)
                    Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Token from Geeklab = {token}");
            }
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


        public static void SetToken(string newToken)
        {   
            Debug.Log($"{SDKSettingsModel.GetColorPrefixLog()} Setting token: {newToken}");
            creativeToken = newToken.TrimStart('?');
            SaveTokenLocally();
        }


        private static void SaveTokenLocally()
        {
            PlayerPrefs.SetString(TOKEN_KEY, creativeToken);
            PlayerPrefs.Save();
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
                    var tokenResponse = JsonUtility.FromJson<TokenResponseModel>(response);
                    taskCompletionSource.SetResult(tokenResponse.token);
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
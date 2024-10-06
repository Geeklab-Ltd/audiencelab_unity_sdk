using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    public class SDKTokenModel
    {
        private static SDKTokenModel _instance;

        public static SDKTokenModel Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new SDKTokenModel();
                }
                return _instance;
            }
        }

        public bool IsTokenVerified { get; set; }
        
        public string Token { get; set; }
    }
}
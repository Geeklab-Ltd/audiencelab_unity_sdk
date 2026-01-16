using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Geeklab.AudiencelabSDK
{
    [Serializable]
    public class TokenResponseModel
    {
        public string token;
        [JsonProperty("wp")]
        public Dictionary<string, object> whitelisted_properties;
    }
}
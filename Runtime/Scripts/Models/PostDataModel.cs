using System;


namespace Geeklab.AudiencelabSDK
{
    [Serializable]
    public class PostData
    {
        public string id;
        public string type;
        public string created;
        public object data;
    }
}
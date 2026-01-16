namespace Geeklab.AudiencelabSDK
{
    public class RequestResult
    {
        public string requestKind;
        public string eventType;
        public string eventId;
        public string eventName;
        public string endpoint;
        public string requestBody;
        public string responseBody;
        public int? httpStatus;
        public bool success;
        public string errorMessage;
        public string timestampUtcIso;
    }
}

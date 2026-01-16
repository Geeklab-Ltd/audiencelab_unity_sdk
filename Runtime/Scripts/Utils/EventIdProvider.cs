using System;

namespace Geeklab.AudiencelabSDK
{
    public static class EventIdProvider
    {
        public static string GenerateEventId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}

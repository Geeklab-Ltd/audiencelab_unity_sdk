using System.Collections.Generic;
using Newtonsoft.Json;

public static class JsonConverter
{
    public static string ConvertToJson(object data)
    {
        if (data is string)
        {
            return (string)data;
        }
        
        if (data is string jsonString)
        {
            try
            {
                var parsedObject = JsonConvert.DeserializeObject(jsonString);
                return JsonConvert.SerializeObject(parsedObject);
            }
            catch (JsonException)
            {
                return (string)data;
            }
        }

        if (data is List<object> || data is List<string> || data is Dictionary<string, string>)
        {
            return JsonConvert.SerializeObject(data);
        }
        
        return (string)data;
    }
}
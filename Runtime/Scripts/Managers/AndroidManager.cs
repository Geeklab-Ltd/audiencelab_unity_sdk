using UnityEngine;

public class AndroidManager : MonoBehaviour
{
    private AndroidJavaObject plugin;

    
    private void Start() {
        using (var pluginClass = new AndroidJavaClass("com.example.batterylevelplugin.BatteryLevel")) {
            plugin = pluginClass.CallStatic<AndroidJavaObject>("getInstance");
        }

        GetBatteryLevel();
    }

    private void GetBatteryLevel() {
        var batteryLevel = plugin.Call<string>("getBatteryLevel");
        Debug.Log("Battery level: " + batteryLevel);
    }

    private void SendDebugMessage(string message) {
        plugin.Call("sendToUnity", "GameObject", "Method", message);
    }
}

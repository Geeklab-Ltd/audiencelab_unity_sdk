// DeviceGeneration.java
package com.Geeklab.plugin;

import android.os.Build;

public class DeviceGeneration {
    public static String GetDeviceGeneration() {
        return Build.MODEL;
    }
}

// InstalledFonts.java
package com.Geeklab.plugin;

import android.graphics.Typeface;
import android.os.Build;

public class InstalledFonts {
    public static String GetInstalledFonts() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            String[] fontFamilies = {"sans-serif", "serif", "monospace", "sans-serif-light", "sans-serif-thin", "sans-serif-condensed", "sans-serif-medium", "casual", "cursive", "sans-serif-smallcaps"};
            StringBuilder installedFonts = new StringBuilder();
            for (String fontFamily : fontFamilies) {
                if (Typeface.create(fontFamily, Typeface.NORMAL) != null) {
                    installedFonts.append(fontFamily).append(",");
                }
            }
            return installedFonts.toString();
        }
        return "System doesn't support retrieving fonts";
    }
}

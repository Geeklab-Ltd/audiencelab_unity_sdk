package com.Geeklab.plugin;

import android.content.Context;
import android.provider.Settings;
import android.util.Log;

import java.lang.reflect.Method;
import java.util.concurrent.TimeUnit;

/**
 * Collects device identity information for the AudienceLab SDK.
 * Play Services classes (GAID, App Set ID) are accessed via reflection so this
 * file compiles regardless of whether the Play Services dependencies are present.
 * Dependencies can be provided via EDM (External Dependency Manager) XML or the
 * auto-generated Gradle file — see SDK Settings for details.
 */
public class AudienceLabIdentity {
    private static final String TAG = "AudienceLab";

    private static volatile boolean collecting = false;
    private static volatile boolean collectionComplete = false;
    private static String gaid = null;
    private static Boolean limitAdTracking = null;
    private static String appSetId = null;
    private static String androidId = null;

    public static void StartCollecting(final Context context, final boolean allowGaid) {
        if (collecting || context == null) {
            return;
        }

        collecting = true;
        new Thread(() -> {
            if (allowGaid) {
                collectGaid(context);
            }

            collectAppSetId(context);
            collectAndroidId(context);

            collectionComplete = true;
            Log.d(TAG, "Identity collection complete — GAID: " + (gaid != null)
                    + ", AppSetId: " + (appSetId != null)
                    + ", AndroidId: " + (androidId != null));
        }).start();
    }

    /**
     * Collects GAID via reflection to avoid a compile-time dependency on
     * com.google.android.gms:play-services-ads-identifier.
     */
    private static void collectGaid(Context context) {
        try {
            Class<?> adIdClientClass = Class.forName(
                    "com.google.android.gms.ads.identifier.AdvertisingIdClient");
            Method getInfoMethod = adIdClientClass.getMethod(
                    "getAdvertisingIdInfo", Context.class);
            Object info = getInfoMethod.invoke(null, context);
            if (info != null) {
                gaid = (String) info.getClass().getMethod("getId").invoke(info);
                limitAdTracking = (Boolean) info.getClass()
                        .getMethod("isLimitAdTrackingEnabled").invoke(info);
            }
        } catch (Throwable e) {
            Log.w(TAG, "GAID collection failed: " + e.getClass().getSimpleName()
                    + ": " + e.getMessage());
            Throwable cause = e.getCause();
            if (cause != null) {
                Log.w(TAG, "  caused by: " + cause.getClass().getSimpleName()
                        + ": " + cause.getMessage());
            }
        }
    }

    /**
     * Collects App Set ID via reflection to avoid a compile-time dependency on
     * com.google.android.gms:play-services-appset.
     */
    private static void collectAppSetId(Context context) {
        try {
            Class<?> appSetClass = Class.forName(
                    "com.google.android.gms.appset.AppSet");
            Object client = appSetClass.getMethod("getClient", Context.class)
                    .invoke(null, context);

            // Look up getAppSetIdInfo on the interface, not the runtime impl class,
            // because the concrete class returned by GMS may be obfuscated.
            Class<?> appSetIdClientClass = Class.forName(
                    "com.google.android.gms.appset.AppSetIdClient");
            Object task = appSetIdClientClass.getMethod("getAppSetIdInfo")
                    .invoke(client);

            Class<?> tasksClass = Class.forName(
                    "com.google.android.gms.tasks.Tasks");
            Class<?> taskClass = Class.forName(
                    "com.google.android.gms.tasks.Task");
            Method awaitMethod = tasksClass.getMethod(
                    "await", taskClass, long.class, TimeUnit.class);

            Object info = awaitMethod.invoke(null, task, 1500L, TimeUnit.MILLISECONDS);
            if (info != null) {
                appSetId = (String) info.getClass().getMethod("getId").invoke(info);
            }
        } catch (Throwable e) {
            Log.w(TAG, "App Set ID collection failed: " + e.getClass().getSimpleName()
                    + ": " + e.getMessage());
            Throwable cause = e.getCause();
            if (cause != null) {
                Log.w(TAG, "  caused by: " + cause.getClass().getSimpleName()
                        + ": " + cause.getMessage());
            }
        }
    }

    /**
     * Collects the Android ID. Part of the base Android SDK — no extra dependencies needed.
     */
    private static void collectAndroidId(Context context) {
        try {
            androidId = Settings.Secure.getString(
                    context.getContentResolver(), Settings.Secure.ANDROID_ID);
        } catch (Throwable e) {
            Log.w(TAG, "Android ID collection failed: " + e.getMessage());
        }
    }

    public static boolean IsCollectionComplete() {
        return collectionComplete;
    }

    public static String GetGaid() {
        return gaid;
    }

    public static String GetAppSetId() {
        return appSetId;
    }

    public static String GetAndroidId() {
        return androidId;
    }

    public static Boolean GetLimitAdTracking() {
        return limitAdTracking;
    }
}

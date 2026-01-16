package com.Geeklab.plugin;

import android.content.Context;
import android.provider.Settings;

import com.google.android.gms.ads.identifier.AdvertisingIdClient;
import com.google.android.gms.appset.AppSet;
import com.google.android.gms.appset.AppSetIdInfo;
import com.google.android.gms.appset.AppSetIdClient;
import com.google.android.gms.tasks.Task;
import com.google.android.gms.tasks.Tasks;

import java.util.concurrent.TimeUnit;

public class AudienceLabIdentity {
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
                try {
                    AdvertisingIdClient.Info info = AdvertisingIdClient.getAdvertisingIdInfo(context);
                    if (info != null) {
                        gaid = info.getId();
                        limitAdTracking = info.isLimitAdTrackingEnabled();
                    }
                } catch (Throwable ignored) {
                }
            }

            try {
                AppSetIdClient client = AppSet.getClient(context);
                Task<AppSetIdInfo> task = client.getAppSetIdInfo();
                AppSetIdInfo info = Tasks.await(task, 1500, TimeUnit.MILLISECONDS);
                if (info != null) {
                    appSetId = info.getId();
                }
            } catch (Throwable ignored) {
            }

            try {
                androidId = Settings.Secure.getString(context.getContentResolver(), Settings.Secure.ANDROID_ID);
            } catch (Throwable ignored) {
            }

            collectionComplete = true;
        }).start();
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

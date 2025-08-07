#include "DeviceInfoHandler.h"
#include "AudiencelabSDKModule.h"
#include "Engine/Engine.h"
#include "GenericPlatform/GenericPlatformMisc.h"
#include "RHI.h"
#include "Misc/DateTime.h"

#if PLATFORM_ANDROID
#include "Android/AndroidPlatformMisc.h"
#include "Android/AndroidJNI.h"
#include "Android/AndroidApplication.h"
#endif

#if PLATFORM_IOS
#include "IOS/IOSPlatformMisc.h"
#endif

FAudiencelabDeviceInfo UDeviceInfoHandler::GetDeviceInfo()
{
	FAudiencelabDeviceInfo DeviceInfo;

	// Basic screen information
	if (GEngine && GEngine->GameViewport)
	{
		FVector2D ViewportSize;
		GEngine->GameViewport->GetViewportSize(ViewportSize);
		DeviceInfo.Width = static_cast<int32>(ViewportSize.X);
		DeviceInfo.Height = static_cast<int32>(ViewportSize.Y);
	}
	else
	{
		// Fallback to system resolution
		FDisplayMetrics DisplayMetrics;
		FDisplayMetrics::RebuildDisplayMetrics(DisplayMetrics);
		DeviceInfo.Width = DisplayMetrics.PrimaryDisplayWidth;
		DeviceInfo.Height = DisplayMetrics.PrimaryDisplayHeight;
	}

	// Get native resolution
	GetNativeResolution(DeviceInfo.NativeWidth, DeviceInfo.NativeHeight);

	// DPI information
	FDisplayMetrics DisplayMetrics;
	FDisplayMetrics::RebuildDisplayMetrics(DisplayMetrics);
	DeviceInfo.Dpi = DisplayMetrics.DPIScaleFactor > 0 ? DisplayMetrics.DPIScaleFactor * 96.0f : 96.0f;

	// Device information
	DeviceInfo.DeviceName = FPlatformProcess::ComputerName();
	DeviceInfo.DeviceModel = GetDeviceModel();
	DeviceInfo.OsVersion = FPlatformMisc::GetOSVersion();

	// Graphics information
	DeviceInfo.GraphicsDeviceVendor = GRHIVendorId != 0 ? RHIVendorIdToString() : TEXT("Unknown");
	DeviceInfo.GraphicsDeviceID = FString::Printf(TEXT("%d"), GRHIDeviceId);
	DeviceInfo.GraphicsDeviceVersion = GRHIAdapterName;

	// Low power detection
	DeviceInfo.bLowPower = IsLowPowerMode();

	// Timezone
	FDateTime Now = FDateTime::Now();
	DeviceInfo.Timezone = Now.ToString(TEXT("%Y-%m-%dT%H:%M:%S"));

	// Installed fonts
	DeviceInfo.InstalledFonts = GetInstalledFonts();

	return DeviceInfo;
}

FString UDeviceInfoHandler::GetDeviceModel()
{
	return GetPlatformDeviceModel();
}

TArray<FString> UDeviceInfoHandler::GetInstalledFonts()
{
	return GetPlatformInstalledFonts();
}

void UDeviceInfoHandler::GetNativeResolution(int32& Width, int32& Height)
{
	GetPlatformNativeResolution(Width, Height);
}

bool UDeviceInfoHandler::IsLowPowerMode()
{
#if PLATFORM_IOS
	// On iOS, you could check for low power mode
	// For now, return false as a placeholder
	return false;
#elif PLATFORM_ANDROID
	// On Android, you could check battery level
	// For now, return false as a placeholder
	return false;
#else
	// On desktop platforms, always return false
	return false;
#endif
}

FString UDeviceInfoHandler::GetPlatformDeviceModel()
{
#if PLATFORM_ANDROID
	// Use Android-specific device model detection
	if (JNIEnv* Env = FAndroidApplication::GetJavaEnv())
	{
		jclass BuildClass = FAndroidApplication::FindJavaClass("android/os/Build");
		if (BuildClass)
		{
			jfieldID ModelField = Env->GetStaticFieldID(BuildClass, "MODEL", "Ljava/lang/String;");
			if (ModelField)
			{
				jstring ModelString = (jstring)Env->GetStaticObjectField(BuildClass, ModelField);
				if (ModelString)
				{
					const char* ModelChars = Env->GetStringUTFChars(ModelString, nullptr);
					FString Result(ModelChars);
					Env->ReleaseStringUTFChars(ModelString, ModelChars);
					Env->DeleteLocalRef(ModelString);
					Env->DeleteLocalRef(BuildClass);
					return Result;
				}
			}
			Env->DeleteLocalRef(BuildClass);
		}
	}
	return TEXT("Android Device");
#elif PLATFORM_IOS
	// Use iOS-specific device model detection
	// This would require native iOS code similar to the Unity SDK
	return FIOSPlatformMisc::GetDeviceModel();
#else
	// Desktop platforms
	return FPlatformMisc::GetCPUBrand();
#endif
}

TArray<FString> UDeviceInfoHandler::GetPlatformInstalledFonts()
{
	TArray<FString> FontList;

#if PLATFORM_ANDROID
	// Android common fonts
	FontList.Add(TEXT("sans-serif"));
	FontList.Add(TEXT("serif"));
	FontList.Add(TEXT("monospace"));
	FontList.Add(TEXT("sans-serif-light"));
	FontList.Add(TEXT("sans-serif-thin"));
	FontList.Add(TEXT("sans-serif-condensed"));
	FontList.Add(TEXT("sans-serif-medium"));
	FontList.Add(TEXT("casual"));
	FontList.Add(TEXT("cursive"));
	FontList.Add(TEXT("sans-serif-smallcaps"));
#elif PLATFORM_IOS
	// iOS common fonts - in a real implementation, this would use native iOS APIs
	FontList.Add(TEXT("Helvetica"));
	FontList.Add(TEXT("Helvetica-Bold"));
	FontList.Add(TEXT("Times New Roman"));
	FontList.Add(TEXT("Arial"));
	FontList.Add(TEXT("Courier"));
	FontList.Add(TEXT("Georgia"));
	FontList.Add(TEXT("Verdana"));
#else
	// Desktop platforms - common system fonts
	FontList.Add(TEXT("Arial"));
	FontList.Add(TEXT("Times New Roman"));
	FontList.Add(TEXT("Helvetica"));
	FontList.Add(TEXT("Courier New"));
	FontList.Add(TEXT("Verdana"));
	FontList.Add(TEXT("Georgia"));
	FontList.Add(TEXT("Tahoma"));
#endif

	return FontList;
}

void UDeviceInfoHandler::GetPlatformNativeResolution(int32& Width, int32& Height)
{
	// Get the native screen resolution
	FDisplayMetrics DisplayMetrics;
	FDisplayMetrics::RebuildDisplayMetrics(DisplayMetrics);
	
	Width = DisplayMetrics.PrimaryDisplayWidth;
	Height = DisplayMetrics.PrimaryDisplayHeight;

#if PLATFORM_IOS
	// iOS might need special handling for retina displays
	// This would be implemented with native iOS code
#endif
}
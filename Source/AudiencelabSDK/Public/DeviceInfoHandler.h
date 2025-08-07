#pragma once

#include "CoreMinimal.h"
#include "UObject/NoExportTypes.h"
#include "Models/AudiencelabModels.h"
#include "DeviceInfoHandler.generated.h"

/**
 * Device Info Handler for collecting device information
 */
UCLASS(BlueprintType)
class AUDIENCELABSDK_API UDeviceInfoHandler : public UObject
{
	GENERATED_BODY()

public:
	/** Get comprehensive device information */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static FAudiencelabDeviceInfo GetDeviceInfo();

	/** Get device model */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static FString GetDeviceModel();

	/** Get installed fonts */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static TArray<FString> GetInstalledFonts();

	/** Get native screen resolution */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static void GetNativeResolution(int32& Width, int32& Height);

	/** Check if device is on low power mode */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static bool IsLowPowerMode();

private:
	/** Platform-specific device model detection */
	static FString GetPlatformDeviceModel();

	/** Platform-specific font detection */
	static TArray<FString> GetPlatformInstalledFonts();

	/** Platform-specific native resolution */
	static void GetPlatformNativeResolution(int32& Width, int32& Height);
};
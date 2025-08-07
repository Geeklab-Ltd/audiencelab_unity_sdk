#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Models/AudiencelabModels.h"
#include "AudiencelabSDK.generated.h"

DECLARE_DYNAMIC_DELEGATE_OneParam(FOnSDKOperationComplete, bool, bSuccess);

/**
 * Main Audiencelab SDK class - Game Instance Subsystem
 * Provides the main API for tracking user metrics, ad events, and purchases
 */
UCLASS(BlueprintType)
class AUDIENCELABSDK_API UAudiencelabSDK : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	/** Get the SDK instance */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK", CallInEditor = true)
	static UAudiencelabSDK* Get(const UObject* WorldContext);

	/** Initialize the SDK (automatically called) */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void InitializeSDK();

	// Token and Authentication

	/** Get Creative Token if any */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	FString GetCreativeToken() const;

	/** Get deep link URL if any */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	FString GetDeepLink() const;

	// Settings Management

	/** Enable or disable metrics collection */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void ToggleMetricsCollection(bool bIsEnabled);

	/** Check if metrics collection is enabled */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	bool GetIsMetricsCollection() const;

	// Event Tracking

	/** Send User Metrics to the server */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendUserMetrics(const FString& JsonData, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send custom purchase metrics to the server */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendCustomPurchaseMetrics(const FString& JsonData, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send custom purchase event with structured data */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendCustomPurchaseEvent(const FString& ItemId, const FString& ItemName, double Value, const FString& Currency = TEXT("USD"), const FString& Status = TEXT("Completed"), const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send advertisement metrics to the server */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendCustomAdMetrics(const FString& JsonData, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send custom ad event with structured data */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendCustomAdEvent(const FString& AdId, const FString& Name, const FString& Source, int32 WatchTime, bool bReward, const FString& MediaSource, const FString& Channel, double Value, const FString& Currency = TEXT("USD"), const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Send a simple ad view event with automatic total_ad_value tracking */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendAdViewEvent(const FString& AdId, const FString& AdSource, double Value = 0.0, const FString& Currency = TEXT("USD"), int32 WatchTime = 0, bool bReward = false, const FOnSDKOperationComplete& OnComplete = FOnSDKOperationComplete());

	/** Get the current cumulative total ad value stored locally */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	double GetTotalAdValue() const;

private:
	/** Check if the SDK is fully configured and enabled */
	bool IsConfigFullyEnabled() const;

	/** Add to the total ad value and save it */
	double AddToTotalAdValue(double AdValue);

	/** Creative token cache */
	UPROPERTY()
	FString CachedCreativeToken;

	/** Deep link cache */
	UPROPERTY()
	FString CachedDeepLink;

	/** Total ad value key for saved games */
	static const FString TOTAL_AD_VALUE_KEY;
};
#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "AudiencelabSettings.generated.h"

/**
 * Settings for the Audiencelab SDK
 */
UCLASS(config=Game, defaultconfig, meta=(DisplayName="Audiencelab SDK"))
class AUDIENCELABSDK_API UAudiencelabSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	UAudiencelabSettings();

	// Get the settings instance
	static const UAudiencelabSettings* Get();

	/** Main SDK Settings */
	
	/** API Token provided by AudienceLab */
	UPROPERTY(config, EditAnywhere, Category = "Main Settings", meta = (DisplayName = "API Token"))
	FString Token;

	/** Enable or disable the SDK */
	UPROPERTY(config, EditAnywhere, Category = "Main Settings", meta = (DisplayName = "Enable SDK"))
	bool bIsSDKEnabled = false;

	/** Enable or disable statistics collection */
	UPROPERTY(config, EditAnywhere, Category = "Main Settings", meta = (DisplayName = "Send Statistics", EditCondition = "bIsSDKEnabled"))
	bool bSendStatistics = true;

	/** Enable or disable debug logging */
	UPROPERTY(config, EditAnywhere, Category = "Main Settings", meta = (DisplayName = "Show Debug Log", EditCondition = "bIsSDKEnabled"))
	bool bShowDebugLog = true;

public:
	/** Check if the SDK is fully enabled and configured */
	bool IsFullyEnabled() const;

	/** Check if metrics collection is enabled */
	bool IsMetricsEnabled() const;

	/** Get formatted debug log prefix */
	static FString GetDebugLogPrefix();

#if WITH_EDITOR
	virtual FText GetSectionText() const override;
	virtual FText GetSectionDescription() const override;
#endif
};
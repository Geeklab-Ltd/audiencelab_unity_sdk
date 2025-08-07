#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Interfaces/IHttpRequest.h"
#include "Interfaces/IHttpResponse.h"
#include "Models/AudiencelabModels.h"
#include "WebRequestManager.generated.h"

DECLARE_DYNAMIC_DELEGATE_OneParam(FOnHttpRequestComplete, const FString&, Response);
DECLARE_DYNAMIC_DELEGATE_OneParam(FOnHttpRequestError, const FString&, Error);

/**
 * Web Request Manager for handling HTTP communication with Audiencelab APIs
 */
UCLASS(BlueprintType)
class AUDIENCELABSDK_API UWebRequestManager : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	/** Get the singleton instance */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	static UWebRequestManager* Get(const UObject* WorldContext);

	/** Check data collection status */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void CheckDataCollectionStatus(const FOnHttpRequestComplete& OnSuccess = FOnHttpRequestComplete(), const FOnHttpRequestError& OnError = FOnHttpRequestError());

	/** Send user metrics */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendUserMetricsRequest(const FString& JsonData, const FOnHttpRequestComplete& OnSuccess = FOnHttpRequestComplete(), const FOnHttpRequestError& OnError = FOnHttpRequestError());

	/** Send ad event */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendAdEventRequest(const FString& JsonData, bool bIsCustom, const FOnHttpRequestComplete& OnSuccess = FOnHttpRequestComplete(), const FOnHttpRequestError& OnError = FOnHttpRequestError());

	/** Send purchase metrics */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void SendPurchaseMetricsRequest(const FString& JsonData, bool bIsCustom, const FOnHttpRequestComplete& OnSuccess = FOnHttpRequestComplete(), const FOnHttpRequestError& OnError = FOnHttpRequestError());

	/** Verify creative token */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void VerifyCreativeTokenRequest(const FString& Token, const FOnHttpRequestComplete& OnSuccess = FOnHttpRequestComplete(), const FOnHttpRequestError& OnError = FOnHttpRequestError());

	/** Fetch token */
	UFUNCTION(BlueprintCallable, Category = "Audiencelab SDK")
	void FetchTokenRequest(const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError = FOnHttpRequestError());

private:
	/** Send webhook request */
	void SendWebhookRequest(const FString& Type, const FString& JsonData, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError);

	/** Send generic HTTP request */
	void SendRequest(const FString& Endpoint, const FString& JsonData, const FOnHttpRequestComplete& OnSuccess, const FOnHttpRequestError& OnError, const FString& Method = TEXT("POST"), const TMap<FString, FString>& Headers = TMap<FString, FString>());

	/** Handle HTTP request completion */
	void OnRequestComplete(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bWasSuccessful, FOnHttpRequestComplete OnSuccess, FOnHttpRequestError OnError);

	/** Get UTC offset as string */
	FString GetUtcOffset() const;

	/** Check if internet is available */
	bool IsInternetAvailable() const;

	/** Log debug error */
	void DebugLogError(const FString& Message, const FOnHttpRequestError& OnError);
};
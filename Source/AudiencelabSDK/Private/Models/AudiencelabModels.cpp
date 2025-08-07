#include "Models/AudiencelabModels.h"

// API Endpoints definitions
const FString UAudiencelabApiEndpoints::API_ENDPOINT = TEXT("https://analytics.geeklab.app/");
const FString UAudiencelabApiEndpoints::TEST_TOKEN = TEXT("c86fb1b2-e3bf-4e91-8299-e3d203a8d36d");
const FString UAudiencelabApiEndpoints::CHECK_DATA_COLLECTION_STATUS = UAudiencelabApiEndpoints::API_ENDPOINT + TEXT("CheckCollection");
const FString UAudiencelabApiEndpoints::VERIFY_API_KEY = UAudiencelabApiEndpoints::API_ENDPOINT + TEXT("auth");
const FString UAudiencelabApiEndpoints::VERIFY_TOKEN = UAudiencelabApiEndpoints::API_ENDPOINT + TEXT("verify-token");
const FString UAudiencelabApiEndpoints::DEVICE_METRICS = UAudiencelabApiEndpoints::API_ENDPOINT + TEXT("store-metrics");
const FString UAudiencelabApiEndpoints::FETCH_TOKEN = UAudiencelabApiEndpoints::API_ENDPOINT + TEXT("fetch-token");
const FString UAudiencelabApiEndpoints::WEBHOOK = UAudiencelabApiEndpoints::API_ENDPOINT + TEXT("webhook");
#include "AudiencelabSettings.h"
#include "AudiencelabSDKModule.h"

UAudiencelabSettings::UAudiencelabSettings()
{
	CategoryName = TEXT("Plugins");
	SectionName = TEXT("Audiencelab SDK");
}

const UAudiencelabSettings* UAudiencelabSettings::Get()
{
	return GetDefault<UAudiencelabSettings>();
}

bool UAudiencelabSettings::IsFullyEnabled() const
{
	return bIsSDKEnabled && !Token.IsEmpty();
}

bool UAudiencelabSettings::IsMetricsEnabled() const
{
	return IsFullyEnabled() && bSendStatistics;
}

FString UAudiencelabSettings::GetDebugLogPrefix()
{
	return TEXT("[AudiencelabSDK]");
}

#if WITH_EDITOR
FText UAudiencelabSettings::GetSectionText() const
{
	return NSLOCTEXT("AudiencelabSDK", "SettingsDisplayName", "Audiencelab SDK");
}

FText UAudiencelabSettings::GetSectionDescription() const
{
	return NSLOCTEXT("AudiencelabSDK", "SettingsDescription", "Configure settings for the Audiencelab SDK plugin");
}
#endif
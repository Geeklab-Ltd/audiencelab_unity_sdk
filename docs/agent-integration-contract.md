# AudienceLab Unity agent integration contract

Machine-readable source of truth for GEE-516:

- Contract: [`../contracts/v1/audiencelab-unity-integration.contract.json`](../contracts/v1/audiencelab-unity-integration.contract.json)
- Schema: [`../contracts/v1/audiencelab-unity-integration.schema.json`](../contracts/v1/audiencelab-unity-integration.schema.json)
- Local verifier: `python3 scripts/verify_integration_contract.py --write-evidence artifacts/integration-contract-evidence.json`

## What an agent must do

1. **Pin the package** to the contract `package.version` using UPM git URL with a tag, for example:
   `https://github.com/Geeklab-Ltd/audiencelab_unity_sdk.git#1.1.9`
2. **Inject credentials outside git.** Put the application API token into `Assets/Resources/SDKSettings.asset` (Token field) or an equivalent secret-injected asset. Never commit it.
3. **Enable required settings:** `IsSDKEnabled=true`, `SendStatistics=true`.
4. **Verify token** through `Audiencelab SDK > SDK Settings` (calls `https://analytics.geeklab.app/auth`).
5. **Prove signals** on a supported player build:
   - creative token present (`AudiencelabSDK.GetCreativeToken()`)
   - accepted automatic `session` start webhook
6. **Run the local verifier** and keep `verification.passed=true` evidence.

## Consent

The SDK does not show a consent UI. Gate collection with host-app policy plus:

- `SDKSettings.IsSDKEnabled`
- `SDKSettings.SendStatistics` / `AudiencelabSDK.ToggleMetricsCollection(bool)`
- Android GAID / App Set ID toggles in SDK settings

IDFA is not collected by this SDK version.

## Diagnostics

- Editor: SDK Settings verification + `ShowDebugLog`
- Runtime (Editor/Development Build): debug overlay (`F8` / five-finger tap) when enabled
- Evidence must redact secrets; record only presence/absence

## Compatibility and rollback

- Minimum Unity: `2023.1` (from `package.json`)
- Supported platforms in this contract: iOS, Android (Editor partial)
- Rollback by re-pinning the previous git tag in `Packages/manifest.json`, clearing PackageCache if needed, rebuilding, and confirming `GetSDKVersion()` matches the pin

## Out of scope

Store submit / signed attestations (Phase 3 / GEE-528), backend readiness state machine (GEE-525), and MCP registration tooling (GEE-436) are not part of this package contract.

## Approval basis

GEE-481 approved app integration, credential, and readiness boundaries. Sandbox success never implies analysis-ready.

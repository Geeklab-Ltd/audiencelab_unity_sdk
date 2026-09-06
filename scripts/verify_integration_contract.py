#!/usr/bin/env python3
"""Verify the AudienceLab Unity integration contract (GEE-516).

Produces machine-checkable evidence without requiring the Unity Editor.
Exit codes: 0 = pass, 1 = fail, 2 = usage/runtime error.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple


ROOT = Path(__file__).resolve().parents[1]
CONTRACT_PATH = ROOT / "contracts" / "v1" / "audiencelab-unity-integration.contract.json"
SCHEMA_PATH = ROOT / "contracts" / "v1" / "audiencelab-unity-integration.schema.json"
PACKAGE_JSON = ROOT / "package.json"
SDK_VERSION_CS = ROOT / "Runtime" / "Scripts" / "Models" / "SDKVersion.cs"
GITIGNORE = ROOT / ".gitignore"

REQUIRED_PACKAGE_FILES = [
    "package.json",
    "Runtime/geeklab.audiencelab-sdk.asmdef",
    "Editor/geeklab.audiencelab-sdk.Editor.asmdef",
    "Runtime/Scripts/GeeklabSDK.cs",
    "Runtime/Scripts/Models/SDKVersion.cs",
    "Runtime/Scripts/Models/SDKSettingsModel.cs",
    "Runtime/Scripts/Settings/AudienceLabSettings.cs",
    "Plugins/Android/AndroidManifest.xml",
    "Plugins/Android/AudienceLabIdentity.java",
    "Plugins/iOS/AudienceLabIdentity.m",
    "contracts/v1/audiencelab-unity-integration.contract.json",
    "contracts/v1/audiencelab-unity-integration.schema.json",
    "docs/agent-integration-contract.md",
    "scripts/verify_integration_contract.py",
]


class CheckResult:
    def __init__(self, check_id: str, description: str):
        self.id = check_id
        self.description = description
        self.status = "pass"
        self.details: List[str] = []

    def fail(self, message: str) -> None:
        self.status = "fail"
        self.details.append(message)

    def note(self, message: str) -> None:
        self.details.append(message)

    def as_dict(self) -> Dict[str, Any]:
        return {
            "id": self.id,
            "description": self.description,
            "status": self.status,
            "details": self.details,
        }


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def type_name(value: Any) -> str:
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "boolean"
    if isinstance(value, int) and not isinstance(value, bool):
        return "integer"
    if isinstance(value, float):
        return "number"
    if isinstance(value, str):
        return "string"
    if isinstance(value, list):
        return "array"
    if isinstance(value, dict):
        return "object"
    return type(value).__name__


def validate_against_schema(instance: Any, schema: Dict[str, Any], path: str = "$") -> List[str]:
    """Minimal JSON Schema validator for the contract schema subset we publish."""
    errors: List[str] = []

    schema_type = schema.get("type")
    if schema_type == "object":
        if not isinstance(instance, dict):
            return [f"{path}: expected object, got {type_name(instance)}"]
        required = schema.get("required", [])
        for key in required:
            if key not in instance:
                errors.append(f"{path}: missing required property '{key}'")
        properties = schema.get("properties", {})
        additional = schema.get("additionalProperties", True)
        if additional is False:
            allowed = set(properties.keys())
            for key in instance:
                if key not in allowed:
                    errors.append(f"{path}: unexpected property '{key}'")
        for key, child_schema in properties.items():
            if key in instance:
                errors.extend(validate_against_schema(instance[key], child_schema, f"{path}.{key}"))
        if isinstance(additional, dict):
            for key, value in instance.items():
                if key not in properties:
                    errors.extend(validate_against_schema(value, additional, f"{path}.{key}"))
        return errors

    if schema_type == "array":
        if not isinstance(instance, list):
            return [f"{path}: expected array, got {type_name(instance)}"]
        min_items = schema.get("minItems")
        if min_items is not None and len(instance) < min_items:
            errors.append(f"{path}: expected at least {min_items} items, got {len(instance)}")
        item_schema = schema.get("items")
        if isinstance(item_schema, dict):
            for index, item in enumerate(instance):
                errors.extend(validate_against_schema(item, item_schema, f"{path}[{index}]"))
        return errors

    if schema_type == "string":
        if not isinstance(instance, str):
            return [f"{path}: expected string, got {type_name(instance)}"]
        if "const" in schema and instance != schema["const"]:
            errors.append(f"{path}: expected const {schema['const']!r}, got {instance!r}")
        if "enum" in schema and instance not in schema["enum"]:
            errors.append(f"{path}: value {instance!r} not in enum {schema['enum']}")
        pattern = schema.get("pattern")
        if pattern and re.search(pattern, instance) is None:
            errors.append(f"{path}: value {instance!r} does not match pattern {pattern}")
        if schema.get("format") == "uri" and not re.match(r"^https?://", instance):
            errors.append(f"{path}: expected http(s) URI, got {instance!r}")
        return errors

    if schema_type == "boolean":
        if not isinstance(instance, bool):
            return [f"{path}: expected boolean, got {type_name(instance)}"]
        return errors

    return errors


def read_package_version() -> str:
    package = load_json(PACKAGE_JSON)
    version = package.get("version")
    if not isinstance(version, str):
        raise ValueError("package.json missing string version")
    return version


def read_package_unity() -> str:
    package = load_json(PACKAGE_JSON)
    unity = package.get("unity")
    if not isinstance(unity, str):
        raise ValueError("package.json missing string unity")
    return unity


def read_sdk_version_constant() -> str:
    text = SDK_VERSION_CS.read_text(encoding="utf-8")
    match = re.search(r'public\s+const\s+string\s+VERSION\s*=\s*"([^"]+)"\s*;', text)
    if not match:
        raise ValueError("SDKVersion.VERSION constant not found")
    return match.group(1)


def check_contract_schema(contract: Dict[str, Any], schema: Dict[str, Any]) -> CheckResult:
    result = CheckResult("contract-schema", "Contract validates against published JSON Schema")
    errors = validate_against_schema(contract, schema)
    if errors:
        for error in errors:
            result.fail(error)
    else:
        result.note("Schema validation passed")
    return result


def check_package_version_sync(contract: Dict[str, Any]) -> CheckResult:
    result = CheckResult(
        "package-version-sync",
        "package.json, SDKVersion.VERSION, and contract package.version match",
    )
    try:
        package_version = read_package_version()
        sdk_version = read_sdk_version_constant()
        contract_version = contract["package"]["version"]
    except Exception as exc:  # noqa: BLE001 - surface as check failure
        result.fail(str(exc))
        return result

    result.note(f"package.json={package_version}")
    result.note(f"SDKVersion.VERSION={sdk_version}")
    result.note(f"contract.package.version={contract_version}")

    if not (package_version == sdk_version == contract_version):
        result.fail("Version sources are out of sync")
    return result


def check_install_pin(contract: Dict[str, Any]) -> CheckResult:
    result = CheckResult("install-pin", "Recommended install method pins the package version tag")
    version = contract["package"]["version"]
    methods = contract.get("install", {}).get("methods", [])
    recommended = [method for method in methods if method.get("recommended")]
    if not recommended:
        result.fail("No recommended install method defined")
        return result

    method = recommended[0]
    blob = json.dumps(method)
    expected_tag = f"#{version}"
    if expected_tag not in blob:
        result.fail(f"Recommended install method does not pin {expected_tag}")
    else:
        result.note(f"Found pin {expected_tag} in install method '{method.get('id')}'")

    manifest = method.get("manifestExample", {})
    deps = manifest.get("dependencies", {}) if isinstance(manifest, dict) else {}
    git_dep = deps.get(contract["package"]["name"])
    if isinstance(git_dep, str) and not git_dep.endswith(expected_tag):
        result.fail(f"manifestExample dependency is not pinned: {git_dep}")
    return result


def check_required_files() -> CheckResult:
    result = CheckResult("required-package-files", "Required package and contract files exist")
    for relative in REQUIRED_PACKAGE_FILES:
        path = ROOT / relative
        if path.is_file():
            result.note(f"present: {relative}")
        else:
            result.fail(f"missing: {relative}")
    return result


def check_secrets_hygiene(contract: Dict[str, Any]) -> CheckResult:
    result = CheckResult(
        "secrets-hygiene",
        "Secret-bearing consumer assets are ignored; no tracked token assets present",
    )
    gitignore_text = GITIGNORE.read_text(encoding="utf-8") if GITIGNORE.exists() else ""
    required_patterns = [
        "Assets/Resources/SDKSettings.asset",
        "*.audiencelab.secrets.json",
        ".audiencelab/",
    ]
    for pattern in required_patterns:
        if pattern not in gitignore_text:
            result.fail(f".gitignore missing pattern: {pattern}")
        else:
            result.note(f"gitignore ok: {pattern}")

    tracked_secret_candidates = list(ROOT.glob("**/SDKSettings.asset")) + list(
        ROOT.glob("**/*audiencelab.secrets.json")
    )
    # Ignore anything under artifacts/
    tracked_secret_candidates = [
        path
        for path in tracked_secret_candidates
        if "artifacts" not in path.parts and ".git" not in path.parts
    ]
    if tracked_secret_candidates:
        for path in tracked_secret_candidates:
            result.fail(f"secret-like asset present in package tree: {path.relative_to(ROOT)}")
    else:
        result.note("No SDKSettings.asset or secrets json files found in package tree")

    # Soft check: contract documents consumer gitignore guidance
    consumer = contract.get("secretsPolicy", {}).get("consumerGitignore", [])
    if not consumer:
        result.fail("secretsPolicy.consumerGitignore is empty")
    return result


def check_unity_minimum(contract: Dict[str, Any]) -> CheckResult:
    result = CheckResult("unity-minimum", "Contract Unity minimum matches package.json unity field")
    try:
        package_unity = read_package_unity()
        contract_min = contract["compatibility"]["unity"]["minimum"]
        declared = contract["compatibility"]["unity"]["declaredInPackageJson"]
    except Exception as exc:  # noqa: BLE001
        result.fail(str(exc))
        return result

    result.note(f"package.json unity={package_unity}")
    result.note(f"contract minimum={contract_min}")
    result.note(f"contract declaredInPackageJson={declared}")
    if package_unity != contract_min or package_unity != declared:
        result.fail("Unity minimum mismatch between package.json and contract")
    supported = contract["compatibility"]["unity"].get("supported", [])
    if contract_min not in supported:
        result.fail("Unity minimum is not listed in compatibility.unity.supported")
    return result


def check_issue_ref(contract: Dict[str, Any]) -> CheckResult:
    result = CheckResult("issue-ref-gee-516", "Contract references GEE-516")
    refs = {item.get("id") for item in contract.get("issueRefs", [])}
    if "GEE-516" not in refs:
        result.fail("GEE-516 missing from issueRefs")
    else:
        result.note("GEE-516 present")
    if "GEE-481" not in refs:
        result.fail("GEE-481 approval reference missing")
    return result


def build_evidence(checks: List[CheckResult], contract: Dict[str, Any]) -> Dict[str, Any]:
    failed = [check for check in checks if check.status != "pass"]
    package_version = None
    sdk_version = None
    unity_min = None
    try:
        package_version = read_package_version()
        sdk_version = read_sdk_version_constant()
        unity_min = read_package_unity()
    except Exception:
        pass

    return {
        "contractId": contract.get("contractId"),
        "contractVersion": contract.get("contractVersion"),
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "package": {
            "name": contract.get("package", {}).get("name"),
            "version": package_version,
            "sdkVersionConstant": sdk_version,
            "unityMinimum": unity_min,
        },
        "issueRefs": contract.get("issueRefs", []),
        "verification": {
            "passed": len(failed) == 0,
            "failedCount": len(failed),
            "checks": [check.as_dict() for check in checks],
        },
        "redaction": {
            "applicationApiToken": "absent-from-evidence",
            "note": "Evidence never includes raw credentials",
        },
    }


def parse_args(argv: Optional[List[str]] = None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--write-evidence",
        type=Path,
        default=ROOT / "artifacts" / "integration-contract-evidence.json",
        help="Path to write machine-readable evidence JSON",
    )
    parser.add_argument(
        "--quiet",
        action="store_true",
        help="Only print the final pass/fail line",
    )
    return parser.parse_args(argv)


def main(argv: Optional[List[str]] = None) -> int:
    args = parse_args(argv)
    try:
        contract = load_json(CONTRACT_PATH)
        schema = load_json(SCHEMA_PATH)
    except Exception as exc:  # noqa: BLE001
        print(f"ERROR: failed to load contract/schema: {exc}", file=sys.stderr)
        return 2

    checks = [
        check_contract_schema(contract, schema),
        check_package_version_sync(contract),
        check_install_pin(contract),
        check_required_files(),
        check_secrets_hygiene(contract),
        check_unity_minimum(contract),
        check_issue_ref(contract),
    ]

    evidence = build_evidence(checks, contract)
    evidence_path: Path = args.write_evidence
    if not evidence_path.is_absolute():
        evidence_path = ROOT / evidence_path
    evidence_path.parent.mkdir(parents=True, exist_ok=True)
    evidence_path.write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")

    if not args.quiet:
        for check in checks:
            mark = "PASS" if check.status == "pass" else "FAIL"
            print(f"[{mark}] {check.id}: {check.description}")
            for detail in check.details:
                print(f"       - {detail}")
        print(f"Evidence written to {evidence_path.relative_to(ROOT)}")

    passed = evidence["verification"]["passed"]
    print("INTEGRATION_CONTRACT_VERIFICATION=PASS" if passed else "INTEGRATION_CONTRACT_VERIFICATION=FAIL")
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())

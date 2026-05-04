using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    internal static class WebhookPayloadFactory
    {
        private static readonly HashSet<string> WarnedUnsupportedPropertyTypes = new HashSet<string>();

        internal static Dictionary<string, object> CreatePurchase(
            string itemId,
            string itemName,
            double value,
            string currency,
            string status,
            double totalPurchaseValue,
            string transactionId)
        {
            return new Dictionary<string, object>
            {
                { "item_id", itemId },
                { "item_name", itemName },
                { "value", value },
                { "currency", currency },
                { "status", status },
                { "total_purchase_value", totalPurchaseValue },
                { "tr_id", transactionId }
            };
        }

        internal static Dictionary<string, object> CreateAd(
            string adId,
            string name,
            string source,
            int watchTime,
            bool reward,
            string mediaSource,
            string channel,
            double value,
            string currency,
            double totalAdValue)
        {
            return new Dictionary<string, object>
            {
                { "ad_id", adId },
                { "name", name },
                { "source", source },
                { "watch_time", watchTime },
                { "reward", reward },
                { "media_source", mediaSource },
                { "channel", channel },
                { "value", value },
                { "currency", currency },
                { "total_ad_value", totalAdValue }
            };
        }

        internal static Dictionary<string, object> CreateRetention(string retentionDay, string backfillDay)
        {
            return new Dictionary<string, object>
            {
                { "retentionDay", retentionDay },
                { "backfillDay", backfillDay }
            };
        }

        internal static Dictionary<string, object> CreateSessionStart(string sessionId, int sessionIndex)
        {
            return new Dictionary<string, object>
            {
                { "a", "start" },
                { "sid", sessionId },
                { "si", sessionIndex }
            };
        }

        internal static Dictionary<string, object> CreateSessionEnd(
            string reason,
            string sessionId,
            int sessionIndex,
            double durationSeconds)
        {
            return new Dictionary<string, object>
            {
                { "a", "end" },
                { "r", reason },
                { "sid", sessionId },
                { "si", sessionIndex },
                { "sd", durationSeconds }
            };
        }

        internal static Dictionary<string, object> CreateCustomEvent(string eventName, object properties)
        {
            return new Dictionary<string, object>
            {
                { "en", eventName },
                { "pr", NormalizeCustomPropertyValue(properties) }
            };
        }

        private static object NormalizeCustomPropertyValue(object value)
        {
            if (value == null || IsSafeScalar(value))
            {
                return value;
            }

            if (value is DateTime dateTime)
            {
                return dateTime.ToUniversalTime().ToString("o");
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToUniversalTime().ToString("o");
            }

            if (value is Guid guid)
            {
                return guid.ToString();
            }

            if (value is Enum enumValue)
            {
                return enumValue.ToString();
            }

            if (value is IDictionary dictionary)
            {
                var normalized = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key == null)
                    {
                        continue;
                    }

                    normalized[entry.Key.ToString()] = NormalizeCustomPropertyValue(entry.Value);
                }

                return normalized;
            }

            if (value is IEnumerable enumerable)
            {
                var normalized = new List<object>();
                foreach (var item in enumerable)
                {
                    normalized.Add(NormalizeCustomPropertyValue(item));
                }

                return normalized;
            }

            var typeName = value.GetType().FullName;
            if (WarnedUnsupportedPropertyTypes.Add(typeName))
            {
                Debug.LogWarning($"{SDKSettingsModel.GetColorPrefixLog()} Custom event properties contain unsupported object type {typeName}. Pass Dictionary<string, object>, arrays/lists, strings, numbers, booleans, enums, dates, or GUIDs to avoid stripped-build serialization issues.");
            }

            return value.ToString();
        }

        private static bool IsSafeScalar(object value)
        {
            return value is string ||
                   value is bool ||
                   value is char ||
                   value is byte ||
                   value is sbyte ||
                   value is short ||
                   value is ushort ||
                   value is int ||
                   value is uint ||
                   value is long ||
                   value is ulong ||
                   value is float ||
                   value is double ||
                   value is decimal;
        }
    }
}

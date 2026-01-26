using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Geeklab.AudiencelabSDK.Tests
{
    public class WebhookQueueAndRetentionTests
    {
        private string queuePath;

        [SetUp]
        public void SetUp()
        {
            queuePath = Path.Combine(Application.persistentDataPath, "audiencelab_webhook_queue.json");
            if (File.Exists(queuePath))
            {
                File.Delete(queuePath);
            }

            PlayerPrefs.DeleteKey("GeeklabCreativeToken");
            PlayerPrefs.DeleteKey("retentionDay");
            PlayerPrefs.Save();
        }

        [Test]
        public void Queue_WhenTokenMissing_PersistsEntry()
        {
            WebRequestManager.Instance.SendCustomEventRequest(new { foo = "bar" }, "dk", "evt");

            Assert.IsTrue(File.Exists(queuePath), "Queue file should exist when token is missing.");
            var json = File.ReadAllText(queuePath);
            var array = JArray.Parse(json);
            Assert.AreEqual(1, array.Count, "Queue should have one entry.");
            Assert.AreEqual("custom", array[0]["type"]?.Value<string>());
            Assert.IsFalse(string.IsNullOrEmpty(array[0]["eventId"]?.Value<string>()), "eventId should be present.");
            Assert.IsFalse(string.IsNullOrEmpty(array[0]["createdAtIso"]?.Value<string>()), "createdAtIso should be present.");
        }

        [Test]
        public void RetentionDay_IsNull_WhenMissing()
        {
            PlayerPrefs.SetString("GeeklabCreativeToken", "testtoken");
            PlayerPrefs.Save();

            InvokeSendWebhookRequestInternal();
            var retention = GetLastEnvelopeRetentionDay();

            Assert.IsNull(retention, "retention_day should be null when missing.");
        }

        [Test]
        public void RetentionDay_UsesStoredValue()
        {
            PlayerPrefs.SetString("GeeklabCreativeToken", "testtoken");
            PlayerPrefs.SetInt("retentionDay", 5);
            PlayerPrefs.Save();

            InvokeSendWebhookRequestInternal();
            var retention = GetLastEnvelopeRetentionDay();

            Assert.AreEqual(5, retention, "retention_day should use PlayerPrefs value.");
        }

        private static void InvokeSendWebhookRequestInternal()
        {
            var instance = WebRequestManager.Instance;
            var method = typeof(WebRequestManager).GetMethod(
                "SendWebhookRequestInternal",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(method, "SendWebhookRequestInternal should exist.");

            method.Invoke(instance, new object[]
            {
                "custom",
                new { test = 1 },
                null,
                false,
                "event",
                DateTime.Now,
                null,
                null,
                null
            });
        }

        private static int? GetLastEnvelopeRetentionDay()
        {
            var field = typeof(WebRequestManager).GetField(
                "LastWebhookEnvelope",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

            Assert.IsNotNull(field, "LastWebhookEnvelope should exist.");
            var snapshot = field.GetValue(null);
            Assert.IsNotNull(snapshot, "LastWebhookEnvelope should be set.");

            var retentionField = snapshot.GetType().GetField("retention_day", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(retentionField, "retention_day should exist on snapshot.");
            return (int?)retentionField.GetValue(snapshot);
        }
    }
}

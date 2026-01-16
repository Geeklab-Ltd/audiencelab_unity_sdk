using NUnit.Framework;
using Geeklab.AudiencelabSDK;
using UnityEngine;

namespace Geeklab.AudiencelabSDK.Tests
{
    public class UserPropertiesManagerTests
    {
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("GeeklabSDK_UserProps_Whitelist");
            PlayerPrefs.DeleteKey("GeeklabSDK_UserProps_Blacklist");
            PlayerPrefs.Save();
        }

        [Test]
        public void SetUserProperty_PersistsValue()
        {
            var result = UserPropertiesManager.SetUserProperty("level", 3);
            Assert.IsTrue(result);

            var props = UserPropertiesManager.GetWhitelistedProperties();
            Assert.IsTrue(props.ContainsKey("level"));
            Assert.AreEqual(3L, props["level"]);
        }

        [Test]
        public void SetUserProperty_RejectsLongKey()
        {
            var longKey = new string('a', UserPropertiesManager.MaxKeyLength + 1);
            var result = UserPropertiesManager.SetUserProperty(longKey, "value");
            Assert.IsFalse(result);
        }

        [Test]
        public void SetUserProperty_RejectsLongStringValue()
        {
            var longValue = new string('b', UserPropertiesManager.MaxStringValueLength + 1);
            var result = UserPropertiesManager.SetUserProperty("long_value", longValue);
            Assert.IsFalse(result);
        }

        [Test]
        public void GenerateEventId_ReturnsGuidString()
        {
            var id = EventIdProvider.GenerateEventId();
            Assert.IsFalse(string.IsNullOrEmpty(id));
        }
    }
}

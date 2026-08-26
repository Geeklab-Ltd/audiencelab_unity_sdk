using System;

namespace Geeklab.AudiencelabSDK
{
    /// <summary>
    /// A device or installation identifier using AudienceLab's canonical identity type.
    /// Treat <see cref="Value"/> as sensitive data and avoid writing it to logs.
    /// </summary>
    [Serializable]
    public sealed class AudienceLabIdentifier
    {
        internal AudienceLabIdentifier(string type, string value)
        {
            Type = type;
            Value = value;
        }

        /// <summary>
        /// Canonical identity type accepted by dynamic SDK integrations:
        /// <c>ifv</c>, <c>ga</c>, <c>asid</c>, or <c>aid</c>.
        /// </summary>
        public string Type { get; }

        /// <summary>
        /// The platform-provided identifier value.
        /// </summary>
        public string Value { get; }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Geeklab.AudiencelabSDK
{
    /// <summary>
    /// Culture-independent local calendar dates for retention PlayerPrefs.
    /// Always writes ISO <c>yyyy-MM-dd</c> with <see cref="CultureInfo.InvariantCulture"/>.
    /// Reads accept that format plus unambiguous legacy culture/calendar values.
    /// </summary>
    internal static class RetentionDateStorage
    {
        internal const string StorageFormat = "yyyy-MM-dd";
        private const string LegacyCultureFormat = "dd/MM/yyyy";
        // The legal UTC offset range spans 26 hours (-12 through +14), so the same instant can
        // appear two local calendar dates apart after an extreme time-zone change.
        private const int AllowedTimezoneDateSkewDays = 2;

        // A mobile app installation cannot predate the modern mobile-app ecosystem. Keeping
        // this deliberately conservative rejects non-Gregorian years accidentally interpreted
        // as Gregorian (for example Hijri 1448) without excluding any real SDK installation.
        private static readonly DateTime EarliestPlausibleDate = new DateTime(2000, 1, 1);
        private static readonly CultureInfo[] LegacyCalendarCultures = CreateLegacyCalendarCultures();

        private static readonly string[] LegacyGregorianReadFormats =
        {
            "dd/MM/yyyy",
            "dd.MM.yyyy",
            "dd-MM-yyyy",
        };

        private static readonly string[] DatePrefsKeys =
        {
            "firstLogin",
            "lastLogin",
            "lastSentMetricDate",
        };

        internal static string FormatLocalDate(DateTime localDateTime)
        {
            return localDateTime.Date.ToString(StorageFormat, CultureInfo.InvariantCulture);
        }

        internal static string FormatTodayLocal()
        {
            return FormatLocalDate(DateTime.Now);
        }

        internal static bool TryParse(string value, out DateTime date)
        {
            return TryParseWithContext(value, CultureInfo.CurrentCulture, DateTime.Now.Date, out date);
        }

        /// <summary>
        /// Parses canonical storage first, then mirrors the legacy writer using the supplied
        /// culture and active calendar. Controlled Gregorian and historic-calendar fallbacks
        /// allow migration after a locale change without accepting an ambiguous date.
        /// </summary>
        internal static bool TryParseWithContext(
            string value,
            CultureInfo legacyCulture,
            DateTime currentLocalDate,
            out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            var today = currentLocalDate.Date;

            if (DateTime.TryParseExact(
                    trimmed,
                    StorageFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed) &&
                IsPlausibleStoredDate(parsed, today))
            {
                date = parsed.Date;
                return true;
            }

            if (legacyCulture != null &&
                DateTime.TryParseExact(
                    trimmed,
                    LegacyCultureFormat,
                    legacyCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out parsed) &&
                IsPlausibleStoredDate(parsed, today))
            {
                date = parsed.Date;
                return true;
            }

            if (DateTime.TryParseExact(
                    trimmed,
                    LegacyGregorianReadFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out parsed) &&
                IsPlausibleStoredDate(parsed, today))
            {
                date = parsed.Date;
                return true;
            }

            if (TryParseLegacyCalendarFallback(trimmed, today, out parsed))
            {
                date = parsed.Date;
                return true;
            }

            return false;
        }

        internal static bool TryCalculateElapsedDays(DateTime startDate, DateTime endDate, out int days)
        {
            var difference = (endDate.Date - startDate.Date).Days;
            if (difference < -AllowedTimezoneDateSkewDays)
            {
                days = 0;
                return false;
            }

            // A new installation can temporarily appear to be before its stored local install
            // date after crossing the date line. It is still day zero, never a negative day.
            days = Math.Max(0, difference);
            return true;
        }

        internal static int PreserveMonotonicElapsedDays(
            int calculatedDays,
            int? persistedDays,
            DateTime currentLocalDate)
        {
            if (!persistedDays.HasValue ||
                !IsPlausibleElapsedDays(persistedDays.Value, currentLocalDate))
            {
                return calculatedDays;
            }

            // Retain the small lead caused by crossing the date line, but never let a
            // temporary forward clock jump freeze retention until that value catches up.
            var maximumProtectedDay = calculatedDays + AllowedTimezoneDateSkewDays;
            return Math.Max(calculatedDays, Math.Min(persistedDays.Value, maximumProtectedDay));
        }

        internal static bool IsPlausibleElapsedDays(int days, DateTime currentLocalDate)
        {
            if (days < 0)
                return false;

            var maximumPlausibleDays =
                (currentLocalDate.Date.AddDays(AllowedTimezoneDateSkewDays) - EarliestPlausibleDate).Days;
            return days <= maximumPlausibleDays;
        }

        private static bool IsPlausibleStoredDate(DateTime candidate, DateTime today)
        {
            var date = candidate.Date;
            return date >= EarliestPlausibleDate &&
                   date <= today.AddDays(AllowedTimezoneDateSkewDays);
        }

        private static bool IsPlausibleSequence(DateTime firstLogin, DateTime laterDate)
        {
            return laterDate.Date >= firstLogin.Date.AddDays(-AllowedTimezoneDateSkewDays);
        }

        /// <summary>
        /// Rewrites any legacy-formatted retention date prefs into the canonical ISO form
        /// so string equality checks (e.g. lastLogin vs today) stay reliable after upgrade.
        /// </summary>
        internal static void NormalizeStoredDates()
        {
            NormalizeStoredDatesWithContext(CultureInfo.CurrentCulture, DateTime.Now.Date);
        }

        /// <summary>
        /// Context-aware entry point used by migration validation and the runtime entry point.
        /// </summary>
        internal static void NormalizeStoredDatesWithContext(
            CultureInfo legacyCulture,
            DateTime currentLocalDate)
        {
            var firstLoginRaw = PlayerPrefs.GetString("firstLogin", "");
            var hasParsedFirstLogin = TryParseWithContext(
                firstLoginRaw,
                legacyCulture,
                currentLocalDate,
                out var parsedFirstLogin);

            var changed = false;
            foreach (var key in DatePrefsKeys)
            {
                var raw = PlayerPrefs.GetString(key, "");
                if (string.IsNullOrEmpty(raw))
                    continue;

                if (!TryParseWithContext(
                        raw,
                        legacyCulture,
                        currentLocalDate,
                        out var parsed))
                    continue;

                if (key != "firstLogin" &&
                    !string.IsNullOrEmpty(firstLoginRaw) &&
                    (!hasParsedFirstLogin || !IsPlausibleSequence(parsedFirstLogin, parsed)))
                {
                    continue;
                }

                var normalized = FormatLocalDate(parsed);
                if (raw == normalized)
                    continue;

                PlayerPrefs.SetString(key, normalized);
                changed = true;
            }

            if (changed)
                PlayerPrefs.Save();
        }

        private static bool TryParseLegacyCalendarFallback(
            string value,
            DateTime today,
            out DateTime date)
        {
            date = default;
            DateTime? resolvedDate = null;

            foreach (var culture in LegacyCalendarCultures)
            {
                if (!DateTime.TryParseExact(
                        value,
                        LegacyCultureFormat,
                        culture,
                        DateTimeStyles.AllowWhiteSpaces,
                        out var parsed) ||
                    !IsPlausibleStoredDate(parsed, today))
                {
                    continue;
                }

                parsed = parsed.Date;
                if (!TryMergeCandidate(parsed, ref resolvedDate))
                    return false;
            }

            if (!TryAddJapaneseEraCandidates(value, today, ref resolvedDate))
                return false;

            if (!resolvedDate.HasValue)
                return false;

            date = resolvedDate.Value;
            return true;
        }

        private static CultureInfo[] CreateLegacyCalendarCultures()
        {
            var cultures = new List<CultureInfo>();
            TryAddLegacyCalendarCulture(cultures, "ar-SA", () => new UmAlQuraCalendar());
            for (var adjustment = -2; adjustment <= 2; adjustment++)
            {
                var capturedAdjustment = adjustment;
                TryAddLegacyCalendarCulture(
                    cultures,
                    "ar-SA",
                    () => new HijriCalendar { HijriAdjustment = capturedAdjustment });
            }
            TryAddLegacyCalendarCulture(cultures, "fa-IR", () => new PersianCalendar());
            TryAddLegacyCalendarCulture(cultures, "he-IL", () => new HebrewCalendar());
            TryAddLegacyCalendarCulture(cultures, "th-TH", () => new ThaiBuddhistCalendar());
            TryAddLegacyCalendarCulture(cultures, "zh-TW", () => new TaiwanCalendar());
            TryAddLegacyCalendarCulture(cultures, "ko-KR", () => new KoreanCalendar());
            return cultures.ToArray();
        }

        private static bool TryAddJapaneseEraCandidates(
            string value,
            DateTime today,
            ref DateTime? resolvedDate)
        {
            CultureInfo culture;
            JapaneseCalendar calendar;
            try
            {
                culture = (CultureInfo)new CultureInfo("ja-JP").Clone();
                calendar = new JapaneseCalendar();
                culture.DateTimeFormat.Calendar = calendar;
            }
            catch (Exception)
            {
                return true;
            }

            if (!TryReadLegacyDateParts(value, culture, out var day, out var month, out var year))
                return true;

            foreach (var era in calendar.Eras)
            {
                try
                {
                    var candidate = calendar.ToDateTime(
                        year,
                        month,
                        day,
                        0,
                        0,
                        0,
                        0,
                        era).Date;

                    // Some .NET profiles allow an out-of-range era date to overflow into a
                    // later era. Require an exact calendar round trip before considering it.
                    if (calendar.GetEra(candidate) != era ||
                        calendar.GetYear(candidate) != year ||
                        calendar.GetMonth(candidate) != month ||
                        calendar.GetDayOfMonth(candidate) != day ||
                        !IsPlausibleStoredDate(candidate, today))
                    {
                        continue;
                    }

                    if (!TryMergeCandidate(candidate, ref resolvedDate))
                        return false;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // This numeric date does not exist in the selected era.
                }
            }

            return true;
        }

        private static bool TryReadLegacyDateParts(
            string value,
            CultureInfo culture,
            out int day,
            out int month,
            out int year)
        {
            day = 0;
            month = 0;
            year = 0;

            var separator = culture.DateTimeFormat.DateSeparator;
            if (string.IsNullOrEmpty(separator))
                return false;

            var parts = value.Split(new[] { separator }, StringSplitOptions.None);
            return parts.Length == 3 &&
                   int.TryParse(parts[0], NumberStyles.None, culture, out day) &&
                   int.TryParse(parts[1], NumberStyles.None, culture, out month) &&
                   int.TryParse(parts[2], NumberStyles.None, culture, out year);
        }

        private static bool TryMergeCandidate(DateTime candidate, ref DateTime? resolvedDate)
        {
            candidate = candidate.Date;
            if (resolvedDate.HasValue && resolvedDate.Value != candidate)
                return false;

            resolvedDate = candidate;
            return true;
        }

        private static void TryAddLegacyCalendarCulture(
            ICollection<CultureInfo> cultures,
            string cultureName,
            Func<Calendar> calendarFactory)
        {
            try
            {
                var culture = (CultureInfo)new CultureInfo(cultureName).Clone();
                culture.DateTimeFormat.Calendar = calendarFactory();
                cultures.Add(culture);
            }
            catch (Exception)
            {
                // Some Unity runtimes omit optional culture/calendar pairs.
            }
        }
    }
}

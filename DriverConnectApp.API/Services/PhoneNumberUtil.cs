using System;
using System.Text.RegularExpressions;

namespace DriverConnectApp.API.Services
{
    public static class PhoneNumberUtil
    {
        /// <summary>
        /// Normalizes phone number to WhatsApp format (country code + number without plus)
        /// Handles multiple country codes intelligently
        /// </summary>
        public static string? Normalize(string phone, string? countryCode = null)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            // Remove all non-digit characters
            var digitsOnly = Regex.Replace(phone, @"\D", "");
            
            if (string.IsNullOrEmpty(digitsOnly))
                return null;

            // Remove leading zeros
            digitsOnly = digitsOnly.TrimStart('0');

            // If no country code provided, try to detect it
            if (string.IsNullOrEmpty(countryCode))
            {
                // Try to detect country code from the number itself
                if (digitsOnly.StartsWith("91") && (digitsOnly.Length == 12 || digitsOnly.Length == 13))
                {
                    // Indian number: 91XXXXXXXXXX (12 digits total)
                    return digitsOnly.Substring(0, 12);
                }
                else if (digitsOnly.StartsWith("44") && digitsOnly.Length >= 12)
                {
                    // UK number: 44XXXXXXXXXXX
                    return digitsOnly;
                }
                else if (digitsOnly.StartsWith("1") && digitsOnly.Length == 11)
                {
                    // US/Canada: 1XXXXXXXXXX (11 digits total)
                    return digitsOnly;
                }
                else if (digitsOnly.StartsWith("86") && digitsOnly.Length >= 13)
                {
                    // China: 86XXXXXXXXXXX
                    return digitsOnly;
                }
                
                // If we can't detect, default to India (91) for backwards compatibility
                countryCode = "91";
            }

            // Check if number already has the country code
            if (digitsOnly.StartsWith(countryCode))
            {
                return digitsOnly;
            }

            // Add country code to local number
            return countryCode + digitsOnly;
        }

        public static string NormalizePhoneNumber(string phone, string? countryCode = null)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            var normalized = Normalize(phone, countryCode);
            return normalized ?? string.Empty;
        }

        public static bool ArePhoneNumbersEqual(string phone1, string phone2, string? countryCode = null)
        {
            if (string.IsNullOrWhiteSpace(phone1) || string.IsNullOrWhiteSpace(phone2))
                return false;

            var normalized1 = Normalize(phone1, countryCode);
            var normalized2 = Normalize(phone2, countryCode);

            return normalized1 == normalized2;
        }

        public static string GetPhoneWithoutCountryCode(string phone, string countryCode = "91")
        {
            var normalized = Normalize(phone, countryCode);
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            if (normalized.StartsWith(countryCode) && normalized.Length > countryCode.Length)
            {
                return normalized.Substring(countryCode.Length);
            }

            return normalized;
        }

        /// <summary>
        /// WhatsApp API requires phone numbers in format: 919876543210 (no plus sign)
        /// </summary>
        public static string FormatForWhatsAppApi(string phone, string? countryCode = null)
        {
            var normalized = Normalize(phone, countryCode);
            return normalized ?? string.Empty;
        }

        /// <summary>
        /// Extracts phone number from WhatsApp webhook format
        /// Example: "919876543210@c.us" → "919876543210"
        /// Intelligently handles different country codes
        /// </summary>
        public static string ExtractPhoneFromWhatsAppId(string whatsAppId)
        {
            if (string.IsNullOrWhiteSpace(whatsAppId))
                return string.Empty;

            // Remove @c.us or @g.us suffix
            var atIndex = whatsAppId.IndexOf('@');
            if (atIndex > 0)
            {
                return whatsAppId.Substring(0, atIndex);
            }

            return whatsAppId;
        }

        /// <summary>
        /// Detects the country code from a phone number
        /// Returns the detected country code or null if cannot detect
        /// </summary>
        public static string? DetectCountryCode(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            var digitsOnly = Regex.Replace(phone, @"\D", "").TrimStart('0');

            // Indian numbers: Start with 91 and are 12 digits total
            if (digitsOnly.StartsWith("91") && (digitsOnly.Length == 12 || digitsOnly.Length == 13))
                return "91";

            // UK numbers: Start with 44
            if (digitsOnly.StartsWith("44") && digitsOnly.Length >= 12)
                return "44";

            // US/Canada: Start with 1 and are 11 digits
            if (digitsOnly.StartsWith("1") && digitsOnly.Length == 11)
                return "1";

            // China: Start with 86
            if (digitsOnly.StartsWith("86") && digitsOnly.Length >= 13)
                return "86";

            // Australia: Start with 61
            if (digitsOnly.StartsWith("61") && digitsOnly.Length >= 11)
                return "61";

            return null;
        }

        /// <summary>
        /// Validates if a phone number is valid for WhatsApp
        /// </summary>
        public static bool IsValidWhatsAppNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            var digitsOnly = Regex.Replace(phone, @"\D", "").TrimStart('0');

            // Must be at least 10 digits (local number) or have country code
            if (digitsOnly.Length < 10)
                return false;

            // Should not exceed 15 digits (E.164 standard)
            if (digitsOnly.Length > 15)
                return false;

            return true;
        }
    }
}
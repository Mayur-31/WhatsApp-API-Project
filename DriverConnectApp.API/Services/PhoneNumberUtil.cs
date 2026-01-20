using System;
using System.Text.RegularExpressions;

namespace DriverConnectApp.API.Services
{
    public static class PhoneNumberUtil
    {
        /// <summary>
        /// Normalizes phone number to WhatsApp format (country code + number without plus)
        /// FIXED: Prevents double country code prefixing
        /// </summary>
        public static string? Normalize(string phone, string? teamCountryCode = null)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            // Remove all non-digit characters
            var digitsOnly = Regex.Replace(phone, @"\D", "");
            
            if (string.IsNullOrEmpty(digitsOnly))
                return null;

            // Remove leading zeros
            digitsOnly = digitsOnly.TrimStart('0');

            // CRITICAL FIX: Check if it's already a valid international number
            // Indian: 91 + 10 digits = 12 total
            if (digitsOnly.StartsWith("91") && digitsOnly.Length == 12)
                return digitsOnly; // Already proper Indian number
            
            // UK: 44 + 10-11 digits = 12-13 total
            if (digitsOnly.StartsWith("44") && digitsOnly.Length >= 12 && digitsOnly.Length <= 13)
                return digitsOnly; // Already proper UK number
            
            // US/Canada: 1 + 10 digits = 11 total
            if (digitsOnly.StartsWith("1") && digitsOnly.Length == 11)
                return digitsOnly; // Already proper US/Canada number

            // FIX: Handle double country code issue (4491... → 91...)
            if (digitsOnly.StartsWith("4491") && digitsOnly.Length >= 14)
            {
                // Remove the first country code (44) leaving the second (91)
                return FixDoubleCountryCode(digitsOnly);
            }

            // If we have a team country code preference, use it
            if (!string.IsNullOrEmpty(teamCountryCode))
            {
                // Don't add if already starts with this country code
                if (digitsOnly.StartsWith(teamCountryCode))
                    return digitsOnly;
                    
                // Don't add if it's already an international number with a different country code
                if (IsValidInternationalNumber(digitsOnly))
                    return digitsOnly;
                    
                return teamCountryCode + digitsOnly;
            }

            // Default handling for 10-digit Indian numbers
            if (digitsOnly.Length == 10)
                return "91" + digitsOnly;

            // If we can't determine, return as-is (might be local format)
            return digitsOnly;
        }

        /// <summary>
        /// FIXES: Double country code prefix (like 4491xxxxxxxx → 91xxxxxxxx)
        /// </summary>
        public static string FixDoubleCountryCode(string phoneDigits)
        {
            if (string.IsNullOrEmpty(phoneDigits) || phoneDigits.Length < 4)
                return phoneDigits;

            // Common double prefix patterns
            if (phoneDigits.StartsWith("4491") && phoneDigits.Length >= 14)
            {
                // Example: 44919763083516 → 919763083516
                var remaining = phoneDigits.Substring(4); // Remove "4491"
                return "91" + remaining; // Keep only Indian code
            }
            
            if (phoneDigits.StartsWith("9144") && phoneDigits.Length >= 14)
            {
                // Example: 91449876543210 → 449876543210
                var remaining = phoneDigits.Substring(4); // Remove "9144"
                return "44" + remaining; // Keep only UK code
            }

            // Check for any double country code pattern
            if (phoneDigits.Length >= 15)
            {
                // Look for common country codes at start
                string[] countryCodes = { "91", "44", "1", "86", "61" };
                
                foreach (var code1 in countryCodes)
                {
                    foreach (var code2 in countryCodes)
                    {
                        if (code1 != code2 && phoneDigits.StartsWith(code1 + code2))
                        {
                            // Remove the first country code
                            return phoneDigits.Substring(code1.Length);
                        }
                    }
                }
            }

            return phoneDigits;
        }

        /// <summary>
        /// Main normalization method used throughout the app
        /// </summary>
        public static string NormalizePhoneNumber(string phone, string? teamCountryCode = null)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            // First, fix any existing double country code issues
            var fixedPhone = FixDoubleCountryCode(phone);
            
            // Then normalize properly
            var normalized = Normalize(fixedPhone, teamCountryCode);
            
            return normalized ?? string.Empty;
        }

        /// <summary>
        /// Formats phone for WhatsApp API (no + sign)
        /// </summary>
        public static string FormatForWhatsAppApi(string phone, string? teamCountryCode = null)
        {
            var normalized = NormalizePhoneNumber(phone, teamCountryCode);
            return normalized;
        }

        /// <summary>
        /// Extracts phone from WhatsApp ID (removes @c.us suffix)
        /// </summary>
        public static string ExtractPhoneFromWhatsAppId(string whatsAppId)
        {
            if (string.IsNullOrWhiteSpace(whatsAppId))
                return string.Empty;

            // Remove @c.us or @g.us suffix
            var atIndex = whatsAppId.IndexOf('@');
            if (atIndex > 0)
            {
                var phonePart = whatsAppId.Substring(0, atIndex);
                
                // WhatsApp might give us number without country code
                // If it's 10 digits, assume Indian and add 91
                if (phonePart.Length == 10 && !phonePart.StartsWith("91"))
                    return "91" + phonePart;
                    
                return phonePart;
            }

            return whatsAppId;
        }

        /// <summary>
        /// Detects country code from phone number
        /// </summary>
        public static string? DetectCountryCode(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return null;

            var digitsOnly = Regex.Replace(phone, @"\D", "").TrimStart('0');

            // Indian: 91 followed by 10 digits
            if (digitsOnly.StartsWith("91") && digitsOnly.Length == 12)
                return "91";

            // UK: 44 followed by 10-11 digits
            if (digitsOnly.StartsWith("44") && digitsOnly.Length >= 12 && digitsOnly.Length <= 13)
                return "44";

            // US/Canada: 1 followed by 10 digits
            if (digitsOnly.StartsWith("1") && digitsOnly.Length == 11)
                return "1";

            // China: 86 followed by 11 digits
            if (digitsOnly.StartsWith("86") && digitsOnly.Length == 13)
                return "86";

            // Australia: 61 followed by 9 digits
            if (digitsOnly.StartsWith("61") && digitsOnly.Length == 11)
                return "61";

            return null;
        }

        /// <summary>
        /// Checks if number is already a valid international number
        /// </summary>
        private static bool IsValidInternationalNumber(string digits)
        {
            if (string.IsNullOrEmpty(digits))
                return false;

            // Check for known country codes with proper lengths
            if (digits.StartsWith("91") && digits.Length == 12) return true;  // India
            if (digits.StartsWith("44") && digits.Length >= 12 && digits.Length <= 13) return true;  // UK
            if (digits.StartsWith("1") && digits.Length == 11) return true;   // US/Canada
            if (digits.StartsWith("86") && digits.Length == 13) return true;  // China
            if (digits.StartsWith("61") && digits.Length == 11) return true;  // Australia
            if (digits.StartsWith("49") && digits.Length >= 11 && digits.Length <= 13) return true;  // Germany
            if (digits.StartsWith("33") && digits.Length == 12) return true;  // France

            return false;
        }

        /// <summary>
        /// Validates WhatsApp number format
        /// </summary>
        public static bool IsValidWhatsAppNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            var normalized = NormalizePhoneNumber(phone);
            
            if (string.IsNullOrEmpty(normalized))
                return false;

            // Must be between 10 and 15 digits
            if (normalized.Length < 10 || normalized.Length > 15)
                return false;

            // Must be all digits
            return Regex.IsMatch(normalized, @"^\d+$");
        }

        /// <summary>
        /// Compares two phone numbers after normalization
        /// </summary>
        public static bool ArePhoneNumbersEqual(string phone1, string phone2, string? teamCountryCode = null)
        {
            if (string.IsNullOrWhiteSpace(phone1) || string.IsNullOrWhiteSpace(phone2))
                return false;

            var normalized1 = NormalizePhoneNumber(phone1, teamCountryCode);
            var normalized2 = NormalizePhoneNumber(phone2, teamCountryCode);

            return normalized1 == normalized2;
        }

        /// <summary>
        /// Gets phone without country code
        /// </summary>
        public static string GetPhoneWithoutCountryCode(string phone, string countryCode = "91")
        {
            var normalized = NormalizePhoneNumber(phone, countryCode);
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            if (normalized.StartsWith(countryCode) && normalized.Length > countryCode.Length)
            {
                return normalized.Substring(countryCode.Length);
            }

            return normalized;
        }

        /// <summary>
        /// NEW: Cleans a phone number for storage (prevents duplicates)
        /// </summary>
        public static string CleanForStorage(string phone, int teamId)
        {
            // Default to Indian for now, but you should pass team's actual country code
            var normalized = NormalizePhoneNumber(phone, "91");
            
            // Ensure consistency: if it's 10 digits without country code, add 91
            if (normalized.Length == 10)
                return "91" + normalized;
                
            return normalized;
        }

        /// <summary>
        /// NEW: Gets display format (+91 XXXX XXX XXX)
        /// </summary>
        public static string GetDisplayFormat(string phone)
        {
            var normalized = NormalizePhoneNumber(phone);
            
            if (string.IsNullOrEmpty(normalized) || normalized.Length < 10)
                return phone;

            if (normalized.StartsWith("91") && normalized.Length == 12)
            {
                // Indian format: +91 98765 43210
                var number = normalized.Substring(2);
                return $"+91 {number.Substring(0, 5)} {number.Substring(5)}";
            }
            else if (normalized.StartsWith("44") && normalized.Length >= 12)
            {
                // UK format: +44 7911 123456
                var number = normalized.Substring(2);
                return $"+44 {number.Substring(0, 4)} {number.Substring(4)}";
            }
            else if (normalized.StartsWith("1") && normalized.Length == 11)
            {
                // US format: +1 (123) 456-7890
                var number = normalized.Substring(1);
                return $"+1 ({number.Substring(0, 3)}) {number.Substring(3, 3)}-{number.Substring(6)}";
            }

            // Generic format
            return $"+{normalized}";
        }

        /// <summary>
        /// NEW: Creates WhatsApp ID from phone number
        /// </summary>
        public static string CreateWhatsAppId(string phone, string? teamCountryCode = null)
        {
            var normalized = NormalizePhoneNumber(phone, teamCountryCode);
            return $"{normalized}@c.us";
        }
    }
}
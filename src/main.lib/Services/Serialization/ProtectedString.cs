﻿using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace PKISharp.WACS.Services.Serialization
{
    /// <summary>
    /// Wrapper to handle string encryption and encoding
    /// Strings can be in three forms:
    /// - Clear, prefixed by ClearPrefix
    /// - Base64 encoded, without any prefix
    /// - Base64 encoded *with* encryption, prefixed by EncryptedPrefix
    /// </summary>
    public class ProtectedString : IEquatable<ProtectedString>, IComparable, IComparable<ProtectedString>
    {

        /// <summary>
        /// Indicates encryption
        /// </summary>
        internal const string EncryptedPrefix = "enc-";

        /// <summary>
        /// Indicates clear text
        /// </summary>
        internal const string ClearPrefix = "clear-";

        /// <summary>
        /// Logging service, used only by the JsonConverter
        /// </summary>
        private readonly ILogService? _log;

        /// <summary>
        /// Indicates if there was an error decoding or decrypting the string
        /// </summary>
        public bool Error { get; private set; } = false;

        /// <summary>
        /// Clear value, should be used for operations
        /// </summary>
        public string? Value { get; private set; }

        /// <summary>
        /// Value to save to disk, based on the setting
        /// </summary>
        public string? DiskValue(bool encrypt)
        {
            if (string.IsNullOrEmpty(Value) || Error)
            {
                return Value;
            }
            if (Value.StartsWith(SecretServiceManager.VaultPrefix))
            {
                return Value;
            }
            if (encrypt) 
            {
                if (OperatingSystem.IsWindows())
                {
                    return EncryptedPrefix + Protect(Value);
                }
                else
                {
                    throw new InvalidOperationException("Configuration encryption is only supported on Windows. Disable the setting in settings.json.");
                }
            } 
            else
            {
                return Encode(Value);
            }
        }
        
        /// <summary>
        /// Version of the string safe to be displayed on screen and in logs
        /// </summary>
        /// <returns></returns>
        public string DisplayValue
        {
            get
            {
                if (Value?.StartsWith(SecretServiceManager.VaultPrefix) ?? false)
                {
                    return Value;
                }
                return "********";
            }
        }

        /// <summary>
        /// Constructor for user input, always starting with clear text
        /// </summary>
        /// <param name="clearValue"></param>
        public ProtectedString(string? clearValue) => Value = clearValue;

        /// <summary>
        /// Constructor for deserialisation, may be any format
        /// </summary>
        /// <param name="rawValue"></param>
        /// <param name="log"></param>
        public ProtectedString(string rawValue, ILogService log)
        {
            _log = log;
            Value = rawValue;

            if (!string.IsNullOrEmpty(rawValue))
            {
                if (rawValue.StartsWith(EncryptedPrefix))
                {
                    // Sure to be encrypted
                    try
                    {
                        Value = OperatingSystem.IsWindows() ? 
                            Unprotect(rawValue[EncryptedPrefix.Length..]) : 
                            throw new InvalidOperationException();
                    }
                    catch
                    {
                        _log.Error("Unable to decrypt configuration value, may have been written by a different machine.");
                        Error = true;
                    }
                }
                else if (rawValue.StartsWith(ClearPrefix))
                {
                    // Sure to be clear/unencoded
                    Value = rawValue[ClearPrefix.Length..];
                }
                else if (rawValue.StartsWith(SecretServiceManager.VaultPrefix)) 
                {
                    // Sure to be clear/unencoded
                    Value = rawValue;
                }
                else
                {
                    // Should be Base64
                    try
                    {
                        var clearBytes = Convert.FromBase64String(rawValue);
                        Value = Encoding.UTF8.GetString(clearBytes);
                    }
                    catch
                    {
                        _log.Error("Unable to decode configuration value, use the prefix {prefix} to input clear text", ClearPrefix);
                        Error = true;
                    }
                }
            }
        }

        /// <summary>
        /// Base64 encode a string
        /// </summary>
        /// <param name="clearText"></param>
        /// <returns></returns>
        private static string Encode(string clearText)
        {
            var clearBytes = Encoding.UTF8.GetBytes(clearText);
            return Convert.ToBase64String(clearBytes);
        }

        /// <summary>
        /// Encrypt and Base64-encode a string
        /// </summary>
        /// <param name="clearText"></param>
        /// <param name="optionalEntropy"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        [SupportedOSPlatform("windows")]
        private static string Protect(string clearText, string? optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.LocalMachine)
        {
            var clearBytes = Encoding.UTF8.GetBytes(clearText);
            var entropyBytes = string.IsNullOrEmpty(optionalEntropy)
                ? null
                : Encoding.UTF8.GetBytes(optionalEntropy);
            var encryptedBytes = ProtectedData.Protect(clearBytes, entropyBytes, scope);
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Base64-decode and decrypt a string
        /// </summary>
        /// <param name="clearText"></param>
        /// <param name="optionalEntropy"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        [SupportedOSPlatform("windows")]
        private static string? Unprotect(string encryptedText, string? optionalEntropy = null, DataProtectionScope scope = DataProtectionScope.LocalMachine)
        {
            if (encryptedText == null)
            {
                return null;
            }

            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var entropyBytes = string.IsNullOrEmpty(optionalEntropy)
                ? null
                : Encoding.UTF8.GetBytes(optionalEntropy);
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, entropyBytes, scope);
            return Encoding.UTF8.GetString(clearBytes);
        }

        public override bool Equals(object? obj) => (obj as ProtectedString)?.Value == Value;
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public bool Equals(ProtectedString? other) => other?.Value == Value;
        public int CompareTo(object? obj) => (obj as ProtectedString)?.Value?.CompareTo(Value) ?? -1;
        public int CompareTo(ProtectedString? other) => other?.Value?.CompareTo(Value) ?? -1;
        public static bool operator ==(ProtectedString? a, ProtectedString? b) => a?.Value == b?.Value;
        public static bool operator !=(ProtectedString? a, ProtectedString? b) => a?.Value != b?.Value;
    }
}

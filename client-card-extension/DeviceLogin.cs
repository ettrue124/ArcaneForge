using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ArcaneForge {
    // Password-free sign-in to the private server. Patched into
    // WizardsAccountsClient.RememberMe and .SavedRefreshToken. It applies only when
    // services.conf points Arena at this server, so official sign-in is untouched.
    public static class DeviceLogin {
        static bool? active;
        static string token;

        public static bool Active() {
            if (active.HasValue) return active.Value;
            try {
                var path = Path.Combine(Application.streamingAssetsPath, "services.conf");
                active = File.Exists(path) && File.ReadAllText(path).Contains("\"arena-local\"");
            } catch (Exception) {
                active = false;
            }
            return active.Value;
        }

        // A hash of the hardware ID, so the raw ID never leaves the PC.
        public static string Token() {
            if (token != null) return token;
            using (var sha = SHA256.Create()) {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes("arcaneforge-device:" + SystemInfo.deviceUniqueIdentifier));
                var text = new StringBuilder("local-device-");
                foreach (var b in hash) text.Append(b.ToString("x2"));
                token = text.ToString();
            }
            return token;
        }
    }
}

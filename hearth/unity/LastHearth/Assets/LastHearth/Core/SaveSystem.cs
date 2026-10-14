using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace LastHearth.Core
{
    /// <summary>
    /// GDD 14: local JSON save with a checksum. The checksum catches broken files and casual edits;
    /// real protection comes with the server.
    /// </summary>
    public static class SaveSystem
    {
        const string Salt = "last-hearth/v1";

        sealed class Envelope
        {
            public int Version;
            public string Checksum = "";
            public string State = "";
        }

        public static string ToJson(GameState state)
        {
            string body = JsonConvert.SerializeObject(state, Formatting.None);
            var env = new Envelope { Version = state.SaveVersion, Checksum = Hash(body), State = body };
            return JsonConvert.SerializeObject(env, Formatting.Indented);
        }

        /// <summary>Returns null if the file is broken or was edited.</summary>
        public static GameState FromJson(string json)
        {
            try
            {
                var env = JsonConvert.DeserializeObject<Envelope>(json);
                if (env == null || env.Checksum != Hash(env.State))
                    return null;
                return JsonConvert.DeserializeObject<GameState>(env.State);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static string Hash(string body)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(Salt + body));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte x in bytes)
                    sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}

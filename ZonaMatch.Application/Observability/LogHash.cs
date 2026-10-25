using System.Security.Cryptography;
using System.Text;

namespace ZonaMatch.Application.Observability;

// Short, stable hash that lets us correlate log entries about the same person
// (for example repeated failed logins) without writing their email into the logs.
internal static class LogHash
{
    public static string Of(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
}

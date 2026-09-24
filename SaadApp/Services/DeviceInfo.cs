using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace SaadApp.Services;

/// <summary>
/// معرّف الجهاز واسم مستخدم ويندوز.
/// </summary>
public static class DeviceInfo
{
    private static readonly Lazy<string> LazyDeviceId = new(ComputeDeviceId);

    /// <summary>معرّف ثابت لهذا الجهاز (SHA-256 من MachineGuid + اسم الجهاز).</summary>
    public static string DeviceId => LazyDeviceId.Value;

    public static string WindowsUserName => Environment.UserName;

    private static string ComputeDeviceId()
    {
        string machineGuid = "";
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            machineGuid = key?.GetValue("MachineGuid") as string ?? "";
        }
        catch
        {
            // نكمل بالمصادر الأخرى
        }

        string raw = $"BOOST|{machineGuid}|{Environment.MachineName}|{Environment.ProcessorCount}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}

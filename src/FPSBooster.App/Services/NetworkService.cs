using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

public static class NetworkService
{
    public static async Task FlushDnsAsync()
    {
        await CmdHelper.Ipconfig("/flushdns");
        await CmdHelper.Ipconfig("/registerdns");
    }

    public static async Task SetGamingDnsAsync(string primary = "1.1.1.1", string secondary = "8.8.8.8")
    {
        // Applique le DNS sur toutes les interfaces actives via PowerShell (safe, réversible)
        string ps = "$ifs = Get-DnsClientServerAddress -AddressFamily IPv4 | Where-Object {$_.ServerAddresses.Count -gt 0} | Select-Object -ExpandProperty InterfaceAlias -Unique; " +
                    $"foreach ($i in $ifs) {{ Set-DnsClientServerAddress -InterfaceAlias $i -ServerAddresses ('{primary}','{secondary}') }}";
        await CmdHelper.Powershell(ps, 20_000);
        await FlushDnsAsync();
        Logger.Info($"DNS gaming appliqué: {primary}, {secondary}");
    }

    public static void DisableNagle()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var ifaces = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", writable: true);
            if (ifaces == null) return;
            foreach (var name in ifaces.GetSubKeyNames())
            {
                using var iface = ifaces.OpenSubKey(name, writable: true);
                if (iface == null) continue;
                // Seulement les interfaces avec DHCP/IP
                if (iface.GetValue("DhcpIPAddress") == null && iface.GetValue("IPAddress") == null) continue;
                iface.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                iface.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
            }
            Logger.Info("Nagle désactivé (TcpAckFrequency=1, TCPNoDelay=1)");
        }
        catch (Exception ex) { Logger.Error("DisableNagle", ex); }
    }

    public static async Task OptimizeTcpAsync()
    {
        RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "Tcp1323Opts", 0);
        RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF));
        RegistryHelper.Set(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0, RegistryValueKind.DWord); // QoS 0%

        await CmdHelper.Netsh("int tcp set global autotuninglevel=highlyrestricted");
        await CmdHelper.Netsh("int tcp set global ecncapability=disabled");
        await CmdHelper.Netsh("int tcp set global timestamps=disabled");
        Logger.Info("TCP optimisé pour le gaming");
    }

    public static async Task<string> PingTestAsync(string host = "1.1.1.1")
    {
        var (code, stdout, _) = await CmdHelper.RunAsync("ping.exe", $"-n 4 {host}", 15_000);
        return code == 0 ? stdout : $"Ping {host} échoué";
    }

    /// <summary>Reset complet stack réseau (standard gaming). Reboot requis.</summary>
    public static async Task ResetStackAsync()
    {
        await CmdHelper.Netsh("winsock reset");
        await CmdHelper.Netsh("int ip reset");
        Logger.Info("Stack réseau réinitialisée (reboot requis)");
    }

    /// <summary>Désactive les tunnels de transition (Teredo/6to4/ISATAP) = moins d'overhead.</summary>
    public static async Task DisableTunnelsAsync()
    {
        await CmdHelper.Netsh("interface teredo set state disabled");
        await CmdHelper.Netsh("interface 6to4 set state disabled");
        await CmdHelper.Netsh("interface isatap set state disabled");
        Logger.Info("Tunnels Teredo/6to4/ISATAP désactivés");
    }

    public static async Task FlushArpAsync()
    {
        await CmdHelper.RunAsync("arp.exe", "-d *");
        await CmdHelper.RunAsync("nbtstat.exe", "-R");
        Logger.Info("Cache ARP + NetBIOS vidé");
    }

    /// <summary>Coupe le P2P Windows Update (Delivery Optimization) = bande passante préservée.</summary>
    public static void DeliveryNoP2P()
    {
        RegistryHelper.Set(RegistryHive.LocalMachine,
            @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization",
            "DODownloadMode", 100, RegistryValueKind.DWord); // 100 = Bypass, pas de P2P
        Logger.Info("Delivery Optimization: P2P désactivé (Bypass)");
    }

    /// <summary>Restaure les défauts réseau Windows (annule nos tweaks).</summary>
    public static async Task RestoreNetworkDefaultsAsync()
    {
        await CmdHelper.Netsh("int tcp set global autotuninglevel=normal");
        await CmdHelper.Netsh("int tcp set global ecncapability=disabled");
        await CmdHelper.Netsh("int tcp set global timestamps=default");
        await CmdHelper.Netsh("interface teredo set state client");
        await CmdHelper.Netsh("interface 6to4 set state default");
        await CmdHelper.Netsh("interface isatap set state default");
        await CmdHelper.Powershell(
            "Get-DnsClientServerAddress -AddressFamily IPv4 | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ResetServerAddresses }",
            20_000);
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit");
        RegistryHelper.DeleteValue(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "Tcp1323Opts");
        RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", 10); // défaut Windows = 10
        ResetNagle();
        await FlushDnsAsync();
        Logger.Info("Réseau restauré par défaut");
    }

    private static void ResetNagle()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var ifaces = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", writable: true);
            if (ifaces == null) return;
            foreach (var name in ifaces.GetSubKeyNames())
            {
                using var iface = ifaces.OpenSubKey(name, writable: true);
                if (iface == null) continue;
                iface.DeleteValue("TcpAckFrequency", throwOnMissingValue: false);
                iface.DeleteValue("TCPNoDelay", throwOnMissingValue: false);
            }
        }
        catch (Exception ex) { Logger.Error("ResetNagle", ex); }
    }

    /// <summary>Test gigue (jitter) + perte : 20 pings, min/max/moyenne/jitter/perte.</summary>
    public static async Task<string> JitterTestAsync(string host = "1.1.1.1")
    {
        var (code, stdout, _) = await CmdHelper.RunAsync("ping.exe", $"-n 20 {host}", 40_000);
        if (code != 0) return $"Ping {host} échoué";
        var times = new List<int>();
        foreach (Match m in System.Text.RegularExpressions.Regex.Matches(stdout, @"(?:temps|time)\s*[=<>]\s*(\d+)\s*ms", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            if (int.TryParse(m.Groups[1].Value, out int t)) times.Add(t);
        var loss = System.Text.RegularExpressions.Regex.Match(stdout, @"perte\s*\D*(\d+)\s*%|(\d+)%\s*loss", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        string lossStr = loss.Success ? (loss.Groups[1].Success ? loss.Groups[1].Value : loss.Groups[2].Value) + "%" : "?";
        if (times.Count == 0) return $"Ping {host} : aucune réponse";
        double avg = times.Average();
        double jitter = times.Count > 1
            ? Enumerable.Range(1, times.Count - 1).Average(i => Math.Abs(times[i] - times[i - 1]))
            : 0;
        return $"{host} : {times.Count}/20 reçus, perte {lossStr} | min {times.Min()}ms / moy {avg:0}ms / max {times.Max()}ms / jitter {jitter:0.0}ms";
    }

    /// <summary>Résumé config réseau (complet en logs).</summary>
    public static async Task<string> NetworkInfoAsync()
    {
        var (_, adapters, _) = await CmdHelper.Powershell(
            "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object { '{0} | {1}' -f $_.Name, $_.LinkSpeed }", 15_000);
        var (_, dns, _) = await CmdHelper.Powershell(
            "Get-DnsClientServerAddress -AddressFamily IPv4 | Where-Object {$_.ServerAddresses} | ForEach-Object { ($_.ServerAddresses -join ',') } | Select-Object -First 2", 15_000);
        var (_, tcp, _) = await CmdHelper.Netsh("int tcp show global");
        string full = $"ADAPTERS:\n{adapters}\nDNS:\n{dns}\nTCP:\n{tcp}";
        Logger.Info("NetworkInfo:\n" + full);
        string firstAdapter = adapters.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "—";
        return $"Carte : {firstAdapter} | DNS : {dns.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "—"} (détails en logs)";
    }
}

// GuitarTool.exe - installs the PS2 guitar plugin for Rock Band 4 on a jailbroken PS4 and
// builds a guitar_map.ini for any guitar/adapter plugged into this Windows PC.
// Built with the C# compiler that ships with Windows (no extra installs needed to run).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

static class Native
{
    [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetProductString(SafeFileHandle h, byte[] buf, int len);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr p, out HIDP_CAPS c);
    [DllImport("setupapi.dll", SetLastError = true)] public static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr h, int flags);
    [DllImport("setupapi.dll", SetLastError = true)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr info, ref Guid g, int idx, ref SP_DEVICE_INTERFACE_DATA d);
    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref SP_DEVICE_INTERFACE_DATA d, IntPtr detail, int size, out int req, IntPtr info);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(string f, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr tmpl);

    [StructLayout(LayoutKind.Sequential)] public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID; public ushort ProductID; public ushort VersionNumber; }
    [StructLayout(LayoutKind.Sequential)] public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }
}

class HidDev
{
    public string Path; public ushort Vid, Pid, Page, Usage; public int InLen; public string Name;
    public string Id { get { return Vid.ToString("X4") + ":" + Pid.ToString("X4"); } }

    public static List<HidDev> Enumerate()
    {
        var list = new List<HidDev>();
        Guid g; Native.HidD_GetHidGuid(out g);
        IntPtr set = Native.SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
        if (set == new IntPtr(-1)) return list;
        try
        {
            for (int i = 0; ; i++)
            {
                var d = new Native.SP_DEVICE_INTERFACE_DATA();
                d.cbSize = Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA));
                if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref g, i, ref d)) break;
                int req;
                Native.SetupDiGetDeviceInterfaceDetail(set, ref d, IntPtr.Zero, 0, out req, IntPtr.Zero);
                IntPtr buf = Marshal.AllocHGlobal(req);
                try
                {
                    Marshal.WriteInt32(buf, IntPtr.Size == 8 ? 8 : 6);
                    if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref d, buf, req, out req, IntPtr.Zero)) continue;
                    string path = Marshal.PtrToStringAuto(new IntPtr(buf.ToInt64() + 4));
                    var dev = Probe(path);
                    if (dev != null) list.Add(dev);
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        return list;
    }

    static HidDev Probe(string path)
    {
        using (var h = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
        {
            if (h.IsInvalid) return null;
            var a = new Native.HIDD_ATTRIBUTES(); a.Size = Marshal.SizeOf(typeof(Native.HIDD_ATTRIBUTES));
            if (!Native.HidD_GetAttributes(h, ref a)) return null;
            IntPtr pp; if (!Native.HidD_GetPreparsedData(h, out pp)) return null;
            Native.HIDP_CAPS caps;
            int st = Native.HidP_GetCaps(pp, out caps);
            Native.HidD_FreePreparsedData(pp);
            if (st != 0x110000 || caps.InputReportByteLength == 0) return null;
            bool skip = (caps.UsagePage == 1 && (caps.Usage == 2 || caps.Usage == 6 || caps.Usage == 0x80)) || caps.UsagePage == 0x0C;
            if (skip) return null;
            var nb = new byte[256]; string name = "";
            if (Native.HidD_GetProductString(h, nb, nb.Length)) name = Encoding.Unicode.GetString(nb).TrimEnd('\0');
            return new HidDev { Path = path, Vid = a.VendorID, Pid = a.ProductID, Page = caps.UsagePage, Usage = caps.Usage, InLen = caps.InputReportByteLength, Name = name.Length > 0 ? name : "(unnamed device)" };
        }
    }
}

class Reader : IDisposable
{
    FileStream fs; byte[] buf; Task<int> pending;
    public Reader(HidDev d)
    {
        var h = Native.CreateFile(d.Path, 0x80000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
        if (h.IsInvalid) throw new IOException("Could not open the device for reading.");
        fs = new FileStream(h, FileAccess.Read, 64, true);
        buf = new byte[Math.Max(64, d.InLen)];
    }
    // Returns the report without the leading report-ID byte (what the PS4 plugin sees), or null on timeout.
    public byte[] Next(int ms)
    {
        if (pending == null) pending = fs.ReadAsync(buf, 0, buf.Length);
        try { if (!pending.Wait(ms)) return null; }
        catch (AggregateException) { pending = null; throw new IOException("The device stopped responding. Unplug it, plug it back in, and try again."); }
        int n = pending.Result; pending = null;
        if (n < 2) return new byte[0];
        var r = new byte[n - 1]; Array.Copy(buf, 1, r, 0, n - 1); return r;
    }
    public void Dispose() { if (fs != null) fs.Dispose(); }
}

class Entry { public int B, M, V; public Entry(int b, int m, int v) { B = b; M = m; V = v; } public override string ToString() { return B.ToString("x") + "," + M.ToString("x") + "," + V.ToString("x"); } }

static class Mapper
{
    public static int Pop(int v) { int n = 0; while (v != 0) { n += v & 1; v >>= 1; } return n; }

    // Pressed when (report[byte] & mask) == val. Picks the changed byte with the fewest changed bits, so
    // a single button bit wins over an analog byte that merely jitters.
    public static Entry Btn(byte[] idle, byte[] p)
    {
        Entry best = null; int bp = 99; int n = Math.Min(idle.Length, p.Length);
        for (int i = 0; i < n; i++)
            if (idle[i] != p[i]) { int m = idle[i] ^ p[i]; int pc = Pop(m); if (pc < bp) { bp = pc; best = new Entry(i, m, p[i] & m); } }
        return best;
    }

    // Strum is often a hat value (several states in one nibble), so compare the whole nibble.
    public static bool Strum(byte[] idle, byte[] up, byte[] down, out Entry u, out Entry d)
    {
        int n = Math.Min(idle.Length, Math.Min(up.Length, down.Length));
        for (int i = 0; i < n; i++)
        {
            if (idle[i] == up[i] || idle[i] == down[i] || up[i] == down[i]) continue;
            int ch = (idle[i] ^ up[i]) | (idle[i] ^ down[i]);
            int mask = 0;
            if ((ch & 0xF0) == 0) mask = 0x0F; else if ((ch & 0x0F) == 0) mask = 0xF0;
            if (mask != 0) { u = new Entry(i, mask, up[i] & mask); d = new Entry(i, mask, down[i] & mask); return true; }
        }
        u = Btn(idle, up); d = Btn(idle, down);
        return u != null && d != null;
    }

    public static bool Whammy(byte[] idle, byte[] p, out int b, out int rest, out int full)
    {
        int n = Math.Min(idle.Length, p.Length), best = -1, diff = 0;
        for (int i = 0; i < n; i++) { int dd = Math.Abs(idle[i] - p[i]); if (dd > diff) { diff = dd; best = i; } }
        b = best; rest = best >= 0 ? idle[best] : 0; full = best >= 0 ? p[best] : 0;
        return best >= 0 && diff >= 16;
    }

    public static List<string> Build(string vid, string pid, Dictionary<string, byte[]> c, List<string> problems)
    {
        var lines = new List<string>();
        lines.Add("id=" + vid.ToLower() + "," + pid.ToLower());
        var idle = c["idle"]; var seen = new Dictionary<string, string>();
        foreach (var k in new[] { "green", "red", "yellow", "blue", "orange", "start", "select" })
        {
            var e = Btn(idle, c[k]);
            if (e == null) { problems.Add(k.ToUpper() + " - nothing changed while you held it"); continue; }
            string sig = e.ToString();
            if (seen.ContainsKey(sig)) { problems.Add(k.ToUpper() + " gave the same signal as " + seen[sig].ToUpper() + " - was the right button held?"); continue; }
            seen[sig] = k; lines.Add(k + "=" + sig);
        }
        Entry u, d;
        if (Strum(idle, c["strum_up"], c["strum_down"], out u, out d)) { lines.Add("strum_up=" + u); lines.Add("strum_down=" + d); }
        else problems.Add("STRUM - could not tell up from down");
        int wb, wr, wf;
        if (Whammy(idle, c["whammy"], out wb, out wr, out wf)) lines.Add("whammy=" + wb.ToString("x") + "," + wr.ToString("x") + "," + wf.ToString("x"));
        else problems.Add("WHAMMY - no analog change found (this guitar may have no whammy output)");
        return lines;
    }
}

static class Ftp
{
    static FtpWebRequest Req(string ip, string path, string method)
    {
        var r = (FtpWebRequest)WebRequest.Create("ftp://" + ip + ":2121" + path);
        r.Method = method; r.Credentials = new NetworkCredential("anonymous", "guest@");
        r.UsePassive = true; r.UseBinary = true; r.KeepAlive = false; r.Timeout = 10000; r.ReadWriteTimeout = 15000;
        return r;
    }
    public static string List(string ip, string path)
    {
        using (var resp = (FtpWebResponse)Req(ip, path, WebRequestMethods.Ftp.ListDirectoryDetails).GetResponse())
        using (var sr = new StreamReader(resp.GetResponseStream())) return sr.ReadToEnd();
    }
    public static void Put(string ip, string path, byte[] data)
    {
        var r = Req(ip, path, WebRequestMethods.Ftp.UploadFile);
        using (var s = r.GetRequestStream()) s.Write(data, 0, data.Length);
        r.GetResponse().Close();
    }
    public static byte[] Get(string ip, string path)
    {
        try
        {
            using (var resp = (FtpWebResponse)Req(ip, path, WebRequestMethods.Ftp.DownloadFile).GetResponse())
            using (var s = resp.GetResponseStream()) using (var ms = new MemoryStream()) { s.CopyTo(ms); return ms.ToArray(); }
        }
        catch (WebException ex)
        {
            var fr = ex.Response as FtpWebResponse;
            if (fr != null && fr.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable) return null;
            throw;
        }
    }
}

static class Program
{
    const string Section = "[CUSA02084]";
    const string PluginLine = "/data/GoldHEN/plugins/OrbisInstrumentalizer.prx";
    const string Rb4dxLine = "/data/GoldHEN/plugins/RB4DX-Plugin.prx";
    static string lastIp = "";
    static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

    static string Desktop { get { return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory); } }
    static string Ask(string q) { Console.Write(q); string s = Console.ReadLine(); return s == null ? "" : s.Trim(); }
    static void Say(string s) { Console.WriteLine(s); }

    // Adds missing lines under [CUSA02084] and leaves everything else as it was.
    public static string UpdateIni(string text, string[] need, out bool changed)
    {
        var L = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) text = "[settings]\nshow_load_notification=true";
        foreach (var x in text.Replace("\r\n", "\n").Split('\n')) L.Add(x);
        while (L.Count > 0 && L[L.Count - 1].Trim() == "") L.RemoveAt(L.Count - 1);
        int idx = -1;
        for (int i = 0; i < L.Count; i++) if (L[i].Trim() == Section) { idx = i; break; }
        if (idx < 0) { L.Add(Section); idx = L.Count - 1; }
        int end = idx + 1;
        while (end < L.Count && !L[end].Trim().StartsWith("[")) end++;
        changed = false;
        foreach (var n in need)
        {
            bool has = false;
            for (int i = idx + 1; i < end; i++) { string t = L[i].Trim(); if (t == n || t.StartsWith(n + "=")) has = true; }
            if (!has) { L.Insert(end, n); end++; changed = true; }
        }
        return string.Join("\n", L.ToArray()) + "\n";
    }

    static byte[] PluginBytes()
    {
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("plugin.prx"))
        {
            if (s == null) throw new Exception("The plugin is missing from this program.");
            using (var ms = new MemoryStream()) { s.CopyTo(ms); return ms.ToArray(); }
        }
    }

    static string GetIp(string given)
    {
        if (!string.IsNullOrEmpty(given)) { lastIp = given; return given; }
        Say("");
        Say("Do these on the PS4 first:");
        Say("  1. Turn the PS4 on and run the jailbreak so GoldHEN loads.");
        Say("  2. Open the GoldHEN menu and turn ON the FTP server.");
        Say("  3. Find the PS4's IP address: Settings > Network > View Connection Status.");
        Say("The PC and the PS4 must be on the same network (same router).");
        Say("");
        string def = lastIp.Length > 0 ? " [" + lastIp + "]" : "";
        string ip = Ask("Type the PS4 IP address (example 192.168.1.27)" + def + " and press Enter: ");
        if (ip.Length == 0) ip = lastIp;
        lastIp = ip;
        return ip;
    }

    static bool Connect(string ip)
    {
        try { Ftp.List(ip, "/data/GoldHEN/"); return true; }
        catch (Exception ex)
        {
            Say("");
            Say("COULD NOT CONNECT to " + ip + " on port 2121.");
            Say("Check: the PS4 is on and jailbroken, the GoldHEN FTP server is ON, and the IP is right.");
            Say("(" + ex.Message + ")");
            return false;
        }
    }

    static void Install(string ipArg)
    {
        Say("");
        Say("--- INSTALL THE PLUGIN ON THE PS4 ---");
        string ip = GetIp(ipArg);
        if (ip.Length == 0) { Say("No IP address entered."); return; }
        Say("[1/4] Connecting to the PS4...");
        if (!Connect(ip)) return;
        byte[] prx = PluginBytes();
        Say("[2/4] Sending the plugin (" + prx.Length + " bytes)...");
        Ftp.Put(ip, "/data/GoldHEN/plugins/OrbisInstrumentalizer.prx", prx);
        Say("[3/4] Checking plugins.ini...");

        string plugins = Ftp.List(ip, "/data/GoldHEN/plugins/");
        var need = new List<string>();
        if (plugins.Contains("RB4DX-Plugin.prx")) need.Add(Rb4dxLine);
        need.Add(PluginLine);
        byte[] oldB = Ftp.Get(ip, "/data/GoldHEN/plugins.ini");
        string old = oldB == null ? "" : Utf8.GetString(oldB);
        bool changed; string neu = UpdateIni(old, need.ToArray(), out changed);
        if (changed)
        {
            if (old.Length > 0)
            {
                string bk = Path.Combine(Desktop, "plugins_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".ini");
                File.WriteAllText(bk, old, Utf8);
                Say("Your old plugins.ini was saved to: " + bk);
            }
            Ftp.Put(ip, "/data/GoldHEN/plugins.ini", Utf8.GetBytes(neu));
            Say("plugins.ini updated.");
        }
        else Say("plugins.ini was already correct.");

        Say("[4/4] Checking for a map file...");
        string map = Path.Combine(Desktop, "guitar_map.ini");
        bool sentMap = false;
        if (File.Exists(map))
        {
            Ftp.Put(ip, "/data/GoldHEN/guitar_map.ini", File.ReadAllBytes(map));
            Say("Found guitar_map.ini on your Desktop and sent it to the PS4.");
            sentMap = true;
        }
        else Say("No guitar_map.ini on your Desktop (that is fine).");
        Say("");
        Say("INSTALL COMPLETE. What to do now:");
        Say("  1. Plug the guitar adapter into the PS4.");
        Say("  2. FULLY CLOSE Rock Band 4 (PS button > Close Application), then start it again.");
        Say("  3. Try the guitar in the menus.");
        if (!sentMap) Say("  4. If the game sees the guitar but the buttons do nothing, come back and choose option 2.");
    }

    static Dictionary<string, byte[]> cap;

    static byte[] Capture(Reader r, double seconds, string label)
    {
        var counts = new Dictionary<string, int>(); var keep = new Dictionary<string, byte[]>();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds)
        {
            var b = r.Next(300);
            if (b == null || b.Length == 0) continue;
            string k = BitConverter.ToString(b);
            int c; counts.TryGetValue(k, out c); counts[k] = c + 1; keep[k] = b;
        }
        string best = null; int bc = 0;
        foreach (var kv in counts) if (kv.Value > bc) { bc = kv.Value; best = kv.Key; }
        return best == null ? null : keep[best];
    }

    static void Drain(Reader r, int ms) { var sw = Stopwatch.StartNew(); while (sw.ElapsedMilliseconds < ms) r.Next(50); }

    static void MakeMap()
    {
        Say("");
        Say("--- MAKE A MAP FILE FOR MY GUITAR ---");
        Say("This teaches the plugin which button is which on YOUR guitar. It takes about 2 minutes.");
        Say("");
        Say("Before you start:");
        Say("  1. Unplug the guitar adapter from the PS4 and plug it into THIS PC.");
        Say("  2. Have the guitar in your hands.");
        Say("  3. You will be asked to hold 10 things, one at a time, for about 2 seconds each:");
        Say("     green, red, yellow, blue, orange, strum up, strum down, start, select, whammy.");
        Say("     Only hold the ONE thing it asks for. Let go when it says it got it.");
        Ask("Press Enter when the adapter is plugged into this PC. ");
        var devs = HidDev.Enumerate();
        if (devs.Count == 0) { Say("No game controller found. Plug the guitar adapter into THIS PC (not the PS4) and try again."); return; }
        HidDev dev = null;
        foreach (var d in devs) if ((d.Vid == 0x2563 && d.Pid == 0x0575) || d.Vid == 0x0810) { dev = d; break; }
        if (dev == null)
        {
            Say("Devices found:");
            for (int i = 0; i < devs.Count; i++) Say(string.Format("  [{0}] {1}  ({2})", i + 1, devs[i].Name, devs[i].Id));
            int pick = 1;
            if (devs.Count > 1) { if (!int.TryParse(Ask("Type the number of your guitar adapter and press Enter: "), out pick) || pick < 1 || pick > devs.Count) { Say("Not a valid number."); return; } }
            dev = devs[pick - 1];
        }
        Say("Using: " + dev.Name + "  (USB ID " + dev.Id + ")");
        var raw = new List<string>();
        cap = new Dictionary<string, byte[]>();
        using (var r = new Reader(dev))
        {
            Say("");
            Ask("STEP 0: Put the guitar DOWN on a table, touch NOTHING, then press Enter. ");
            Say("  recording the resting state...");
            Drain(r, 300);
            cap["idle"] = Capture(r, 2.5, "idle");
            if (cap["idle"] == null) { Say("No data came from the adapter. Try another USB port, or press a button while it is plugged in."); return; }
            raw.Add("IDLE: " + BitConverter.ToString(cap["idle"]));
            var steps = new[] {
                new[] { "green", "GREEN fret" }, new[] { "red", "RED fret" }, new[] { "yellow", "YELLOW fret" },
                new[] { "blue", "BLUE fret" }, new[] { "orange", "ORANGE fret" },
                new[] { "strum_up", "STRUM UP (push the strum bar up)" }, new[] { "strum_down", "STRUM DOWN (push the strum bar down)" },
                new[] { "start", "START" }, new[] { "select", "SELECT" }, new[] { "whammy", "WHAMMY bar pushed ALL the way down" } };
            int stepNo = 0;
            foreach (var s in steps)
            {
                stepNo++;
                Say("");
                Say(string.Format("STEP {0} of {1}: {2}", stepNo, steps.Length, s[1]));
                Ask("  Get your hand ready, press Enter, then HOLD it when it says HOLD NOW. ");
                Say("  get ready...");
                Drain(r, 900);
                Say("  >>> HOLD NOW! (keep holding about 2 seconds) <<<");
                var b = Capture(r, 2.5, s[0]);
                Say("  OK, let go.");
                if (b == null) b = cap["idle"];
                cap[s[0]] = b;
                raw.Add(s[0] + ": " + BitConverter.ToString(b));
            }
        }
        var problems = new List<string>();
        var lines = Mapper.Build(dev.Vid.ToString("X4"), dev.Pid.ToString("X4"), cap, problems);
        string safeName = dev.Name.Replace("=", "-").Replace("\r", " ").Replace("\n", " ");
        if (safeName.Length > 40) safeName = safeName.Substring(0, 40);
        var mapText = new List<string>();
        mapText.Add("# Guitar map made by GuitarTool for " + safeName + " on " + DateTime.Now.ToString("yyyy-MM-dd"));
        mapText.Add("# Put this file at /data/GoldHEN/guitar_map.ini on the PS4. Each line: button, byte, mask, value (hex).");
        mapText.AddRange(lines);
        string ini = Path.Combine(Desktop, "guitar_map.ini");
        File.WriteAllText(ini, string.Join("\r\n", mapText.ToArray()).Replace("\r\n", "\n") + "\n", Utf8);
        var report = new List<string>();
        report.Add("GuitarTool capture report - " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        report.Add("Device: " + dev.Name + "   USB ID: " + dev.Id + "   report length: " + (dev.InLen - 1) + " bytes");
        report.Add("");
        report.Add("Raw reports (one per button, hex bytes):");
        report.AddRange(raw);
        report.Add("");
        report.Add("Map that was generated:");
        report.AddRange(lines);
        if (problems.Count > 0) { report.Add(""); report.Add("Not detected:"); foreach (var p in problems) report.Add("  " + p); }
        File.WriteAllText(Path.Combine(Desktop, "guitar_capture.txt"), string.Join("\n", report.ToArray()) + "\n", Utf8);
        Say("");
        Say("Saved on your Desktop:");
        Say("  guitar_map.ini      - the map file (exactly the name the PS4 needs)");
        Say("  guitar_capture.txt  - the raw data (keep it, in case something needs fixing)");
        Say("");
        Say("The map that was made:");
        foreach (var l in lines) Say("  " + l);
        if (problems.Count > 0)
        {
            Say("");
            Say("PROBLEMS - these were not detected, so the map is incomplete:");
            foreach (var p in problems) Say("  - " + p);
            Say("Choose option 2 again and hold each of those the WHOLE time it says HOLD NOW.");
            Say("If it keeps failing, send guitar_capture.txt to the author (Discord @CoronaSend).");
            return;
        }
        Say("");
        Say("Map complete. Now it has to get onto the PS4:");
        string yn = Ask("Send guitar_map.ini to the PS4 now? Type y or n and press Enter: ");
        if (yn.ToLower().StartsWith("y"))
        {
            string ip = GetIp(null);
            if (ip.Length > 0 && Connect(ip))
            {
                Ftp.Put(ip, "/data/GoldHEN/guitar_map.ini", File.ReadAllBytes(ini));
                Say("Sent to /data/GoldHEN/guitar_map.ini on the PS4.");
                Say("");
                Say("What to do now:");
                Say("  1. Unplug the adapter from this PC and plug it into the PS4.");
                Say("  2. FULLY CLOSE Rock Band 4 (PS button > Close Application), then start it again.");
                Say("  3. Try the guitar in the menus.");
            }
        }
        else
        {
            Say("OK. When you are ready, choose option 1 and it will send the map file for you.");
        }
    }

    static int SelfTest()
    {
        var c = new Dictionary<string, byte[]>();
        Func<string, byte[]> B = s => { var p = s.Split(' '); var r = new byte[p.Length]; for (int i = 0; i < p.Length; i++) r[i] = Convert.ToByte(p[i], 16); return r; };
        c["idle"] = B("00 00 06 7F 7F 7F 00 00"); c["green"] = B("80 00 06 7F 7F 7F 00 00"); c["red"] = B("02 00 06 7F 7F 7F 00 00");
        c["yellow"] = B("01 00 06 7F 7F 7F 00 00"); c["blue"] = B("04 00 06 7F 7F 7F 00 00"); c["orange"] = B("08 00 06 7F 7F 7F 00 00");
        c["start"] = B("00 02 06 7F 7F 7F 00 00"); c["select"] = B("00 01 06 7F 7F 7F 00 00");
        c["strum_up"] = B("00 00 07 7F 7F 7F 00 00"); c["strum_down"] = B("00 00 05 7F 7F 7F 00 00"); c["whammy"] = B("00 00 06 7F 00 7F 00 00");
        var pr = new List<string>();
        foreach (var l in Mapper.Build("2563", "0575", c, pr)) Say(l);
        Say("problems: " + pr.Count);
        bool ch; Say("--- ini: empty"); Say(UpdateIni("", new[] { Rb4dxLine, PluginLine }, out ch));
        Say("--- ini: other plugins"); Say(UpdateIni("[settings]\r\nshow_load_notification=true\r\n[CUSA00419]\r\n/a.prx\r\n", new[] { PluginLine }, out ch));
        Say("--- ini: section in middle"); Say(UpdateIni("[CUSA02084]\n" + Rb4dxLine + "\n[CUSA11100]\n/b.prx\n", new[] { Rb4dxLine, PluginLine }, out ch));
        UpdateIni("[CUSA02084]\n" + Rb4dxLine + "\n" + PluginLine + "\n", new[] { Rb4dxLine, PluginLine }, out ch); Say("already complete -> changed: " + ch);
        Say("devices: " + HidDev.Enumerate().Count);
        Say("plugin bytes: " + PluginBytes().Length);
        return 0;
    }

    static int Main(string[] args)
    {
        Console.Title = "Guitar Tool for Rock Band 4 on PS4";
        try
        {
            if (args.Length > 0 && args[0] == "--selftest") return SelfTest();
            if (args.Length > 1 && args[0] == "--install") { Install(args[1]); return 0; }
            while (true)
            {
                Say("");
                Say("==============================================");
                Say("  GUITAR TOOL - Rock Band 4 on PS4");
                Say("==============================================");
                Say("");
                Say("  What do you want to do?");
                Say("");
                Say("  Type 1 = Put the guitar plugin on my PS4");
                Say("           (do this first, the PS4 must be on and jailbroken)");
                Say("");
                Say("  Type 2 = My guitar's buttons are wrong or do nothing");
                Say("           (plug the guitar into this PC, I will learn its buttons)");
                Say("");
                Say("  Type 3 = Close this program");
                Say("");
                string c = Ask("Type 1, 2 or 3, then press the Enter key: ");
                try
                {
                    if (c == "1") Install(null);
                    else if (c == "2") MakeMap();
                    else if (c == "3") return 0;
                }
                catch (Exception ex) { Say(""); Say("ERROR: " + ex.Message); }
            }
        }
        catch (Exception ex) { Say("ERROR: " + ex.Message); Ask("Press Enter to close."); return 1; }
    }
}

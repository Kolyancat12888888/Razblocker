using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HFL.Client.Services
{
    public class TransparentDnsRedirector
    {
        private const int WINDIVERT_LAYER_NETWORK = 0;
        private const ulong WINDIVERT_FLAG_SNIFF = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDIVERT_DATA_NETWORK
        {
            public uint IfIdx;
            public uint SubIfIdx;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct WINDIVERT_ADDRESS
        {
            [FieldOffset(0)]
            public long Timestamp;

            [FieldOffset(8)]
            public byte Layer;

            [FieldOffset(9)]
            public byte Event;

            [FieldOffset(10)]
            public byte Flags;

            [FieldOffset(12)]
            public WINDIVERT_DATA_NETWORK Network;
        }

        [DllImport("WinDivert.dll", EntryPoint = "WinDivertOpen", SetLastError = true)]
        private static extern IntPtr WinDivertOpen(
            [MarshalAs(UnmanagedType.LPStr)] string filter,
            int layer,
            short priority,
            ulong flags);

        [DllImport("WinDivert.dll", EntryPoint = "WinDivertRecv", SetLastError = true)]
        private static extern bool WinDivertRecv(
            IntPtr handle,
            byte[] packet,
            uint packetLen,
            out uint readLen,
            ref WINDIVERT_ADDRESS addr);

        [DllImport("WinDivert.dll", EntryPoint = "WinDivertSend", SetLastError = true)]
        private static extern bool WinDivertSend(
            IntPtr handle,
            byte[] packet,
            uint packetLen,
            out uint writeLen,
            ref WINDIVERT_ADDRESS addr);

        [DllImport("WinDivert.dll", EntryPoint = "WinDivertHelperCalcChecksums", SetLastError = true)]
        private static extern bool WinDivertHelperCalcChecksums(
            byte[] packet,
            uint packetLen,
            ref WINDIVERT_ADDRESS addr,
            ulong flags);

        [DllImport("WinDivert.dll", EntryPoint = "WinDivertClose", SetLastError = true)]
        private static extern bool WinDivertClose(IntPtr handle);

        private IntPtr _divertHandle = IntPtr.Zero;
        private CancellationTokenSource? _cts;
        private readonly HttpClient _httpClient;
        public bool IsRunning => _divertHandle != IntPtr.Zero;

        public TransparentDnsRedirector()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        }

        public bool Start(string serverDohUrl)
        {
            Stop();

            if (string.IsNullOrWhiteSpace(serverDohUrl))
                return false;

            try
            {
                // Ensure WinDivert.dll can be loaded by setting DLL directory
                string binDir = Path.Combine(AppContext.BaseDirectory, "Assets", "bin");
                if (Directory.Exists(binDir))
                {
                    SetDllDirectory(binDir);
                }

                // Intercept all outgoing IPv4 DNS queries (UDP dst port 53)
                // Adapters in Windows settings remain 100% unchanged!
                _divertHandle = WinDivertOpen("outbound and !loopback and ip and udp.DstPort == 53", WINDIVERT_LAYER_NETWORK, 100, 0);
                if (_divertHandle == IntPtr.Zero || _divertHandle == new IntPtr(-1))
                {
                    _divertHandle = IntPtr.Zero;
                    return false;
                }

                _cts = new CancellationTokenSource();
                Task.Factory.StartNew(() => InterceptLoop(serverDohUrl, _cts.Token), TaskCreationOptions.LongRunning);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void InterceptLoop(string dohUrl, CancellationToken token)
        {
            byte[] packet = new byte[65535];
            var addr = new WINDIVERT_ADDRESS();

            while (!token.IsCancellationRequested && _divertHandle != IntPtr.Zero)
            {
                if (!WinDivertRecv(_divertHandle, packet, (uint)packet.Length, out uint readLen, ref addr))
                {
                    if (token.IsCancellationRequested) break;
                    continue;
                }

                // Parse IPv4 + UDP header
                // IPv4 Header is minimum 20 bytes. UDP Header is 8 bytes.
                if (readLen < 28) continue;

                int ipHeaderLen = (packet[0] & 0x0F) * 4;
                if (ipHeaderLen < 20 || readLen < ipHeaderLen + 8) continue;

                int udpPayloadOffset = ipHeaderLen + 8;
                int dnsLength = (int)readLen - udpPayloadOffset;
                if (dnsLength < 12) continue;

                byte[] dnsQuery = new byte[dnsLength];
                Array.Copy(packet, udpPayloadOffset, dnsQuery, 0, dnsLength);

                // Preserve original headers to craft the exact response back to the application
                byte[] origIpHeader = new byte[ipHeaderLen];
                Array.Copy(packet, 0, origIpHeader, 0, ipHeaderLen);
                byte[] origUdpHeader = new byte[8];
                Array.Copy(packet, ipHeaderLen, origUdpHeader, 0, 8);
                var origAddr = addr;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Post, dohUrl);
                        req.Content = new ByteArrayContent(dnsQuery);
                        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/dns-message");

                        var res = await _httpClient.SendAsync(req);
                        if (res.IsSuccessStatusCode)
                        {
                            byte[] dnsResponse = await res.Content.ReadAsByteArrayAsync();
                            if (dnsResponse.Length > 0 && _divertHandle != IntPtr.Zero)
                            {
                                SendCraftedDnsResponse(origIpHeader, origUdpHeader, dnsResponse, origAddr);
                            }
                        }
                    }
                    catch { }
                });
            }
        }

        private void SendCraftedDnsResponse(byte[] origIp, byte[] origUdp, byte[] dnsResponse, WINDIVERT_ADDRESS origAddr)
        {
            int totalLen = origIp.Length + 8 + dnsResponse.Length;
            byte[] respPacket = new byte[totalLen];

            // 1. Swap Source IP (bytes 12..15) and Destination IP (bytes 16..19)
            Array.Copy(origIp, 0, respPacket, 0, origIp.Length);
            Array.Copy(origIp, 12, respPacket, 16, 4); // Dst = Orig Src
            Array.Copy(origIp, 16, respPacket, 12, 4); // Src = Orig Dst

            // Set Total Length in IPv4 Header
            respPacket[2] = (byte)((totalLen >> 8) & 0xFF);
            respPacket[3] = (byte)(totalLen & 0xFF);
            respPacket[8] = 64; // TTL

            // 2. Swap Source Port (0..1) and Destination Port (2..3) in UDP Header
            int udpOffset = origIp.Length;
            respPacket[udpOffset + 0] = origUdp[2];
            respPacket[udpOffset + 1] = origUdp[3];
            respPacket[udpOffset + 2] = origUdp[0];
            respPacket[udpOffset + 3] = origUdp[1];

            // UDP Length
            int udpLen = 8 + dnsResponse.Length;
            respPacket[udpOffset + 4] = (byte)((udpLen >> 8) & 0xFF);
            respPacket[udpOffset + 5] = (byte)(udpLen & 0xFF);
            // Set Checksum fields to 0 before calculating
            respPacket[10] = 0;
            respPacket[11] = 0;
            respPacket[udpOffset + 6] = 0;
            respPacket[udpOffset + 7] = 0;

            // 3. Append DNS payload
            Array.Copy(dnsResponse, 0, respPacket, udpOffset + 8, dnsResponse.Length);

            // 4. Set Address struct for inbound injection (outbound bit must be 0)
            var respAddr = new WINDIVERT_ADDRESS();
            respAddr.Layer = WINDIVERT_LAYER_NETWORK;
            respAddr.Flags = 0; // 0 = Inbound
            respAddr.Network = origAddr.Network;

            // Calculate IPv4 and UDP Checksums
            WinDivertHelperCalcChecksums(respPacket, (uint)totalLen, ref respAddr, 0);

            // Reinject response packet into Windows TCP/IP stack
            WinDivertSend(_divertHandle, respPacket, (uint)totalLen, out _, ref respAddr);
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            if (_divertHandle != IntPtr.Zero)
            {
                WinDivertClose(_divertHandle);
                _divertHandle = IntPtr.Zero;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Web;

namespace HFL.Core.Protocols
{
    public class VlessConfig
    {
        public string Uuid { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int Port { get; set; } = 443;
        public string Network { get; set; } = "tcp"; // tcp, grpc, ws
        public string Security { get; set; } = "reality"; // reality, tls, none
        public string PublicKey { get; set; } = string.Empty;
        public string ShortId { get; set; } = string.Empty;
        public string Sni { get; set; } = string.Empty;
        public string Fingerprint { get; set; } = "chrome";
        public string Flow { get; set; } = "xtls-rprx-vision";
        public string GrpcServiceName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = "3X-UI Server";
    }

    public static class VlessParser
    {
        public static VlessConfig? ParseUri(string uriString)
        {
            if (string.IsNullOrWhiteSpace(uriString) || !uriString.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                return null;

            try
            {
                var uri = new Uri(uriString);
                var config = new VlessConfig
                {
                    Uuid = uri.UserInfo,
                    Address = uri.Host,
                    Port = uri.Port > 0 ? uri.Port : 443,
                    Name = !string.IsNullOrEmpty(uri.Fragment) ? Uri.UnescapeDataString(uri.Fragment.TrimStart('#')) : "3X-UI Server"
                };

                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                config.Network = queryParams["type"] ?? "tcp";
                config.Security = queryParams["security"] ?? "reality";
                config.PublicKey = queryParams["pbk"] ?? "";
                config.ShortId = queryParams["sid"] ?? "";
                config.Sni = queryParams["sni"] ?? "";
                config.Fingerprint = queryParams["fp"] ?? "chrome";
                config.Flow = queryParams["flow"] ?? (config.Network == "tcp" && config.Security == "reality" ? "xtls-rprx-vision" : "");
                config.GrpcServiceName = queryParams["serviceName"] ?? "";
                config.Path = queryParams["path"] ?? "";

                return config;
            }
            catch
            {
                return null;
            }
        }

        public static string GenerateXrayJson(VlessConfig vless, bool directRuRouting = true)
        {
            var streamSettings = new Dictionary<string, object>
            {
                ["network"] = vless.Network,
                ["security"] = vless.Security
            };

            if (vless.Security == "reality")
            {
                streamSettings["realitySettings"] = new Dictionary<string, object>
                {
                    ["show"] = false,
                    ["dest"] = $"{vless.Sni}:{vless.Port}",
                    ["xver"] = 0,
                    ["serverNames"] = new[] { vless.Sni },
                    ["privateKey"] = "",
                    ["shortIds"] = string.IsNullOrEmpty(vless.ShortId) ? new[] { "" } : new[] { vless.ShortId },
                    ["publicKey"] = vless.PublicKey,
                    ["fingerprint"] = vless.Fingerprint,
                    ["serverName"] = vless.Sni,
                    ["spiderX"] = "/"
                };
            }
            else if (vless.Security == "tls")
            {
                streamSettings["tlsSettings"] = new Dictionary<string, object>
                {
                    ["serverName"] = vless.Sni,
                    ["fingerprint"] = vless.Fingerprint,
                    ["allowInsecure"] = false
                };
            }

            if (vless.Network == "grpc")
            {
                streamSettings["grpcSettings"] = new Dictionary<string, object>
                {
                    ["serviceName"] = vless.GrpcServiceName,
                    ["multiMode"] = true
                };
            }
            else if (vless.Network == "ws")
            {
                streamSettings["wsSettings"] = new Dictionary<string, object>
                {
                    ["path"] = vless.Path,
                    ["headers"] = new Dictionary<string, string> { ["Host"] = vless.Sni }
                };
            }

            var outboundVless = new Dictionary<string, object>
            {
                ["tag"] = "proxy",
                ["protocol"] = "vless",
                ["settings"] = new
                {
                    vnext = new[]
                    {
                        new
                        {
                            address = vless.Address,
                            port = vless.Port,
                            users = new[]
                            {
                                new
                                {
                                    id = vless.Uuid,
                                    encryption = "none",
                                    flow = vless.Flow
                                }
                            }
                        }
                    }
                },
                ["streamSettings"] = streamSettings
            };

            var routingRules = new List<object>();

            if (directRuRouting)
            {
                routingRules.Add(new
                {
                    type = "field",
                    outboundTag = "direct",
                    ip = new[] { "geoip:private", "geoip:ru" }
                });
                routingRules.Add(new
                {
                    type = "field",
                    outboundTag = "direct",
                    domain = new[] { "geosite:ru", "domain:ru", "domain:su", "domain:xn--p1ai", "domain:vk.com", "domain:yandex.ru", "domain:kinopoisk.ru" }
                });
            }

            routingRules.Add(new
            {
                type = "field",
                outboundTag = "proxy",
                network = "tcp,udp"
            });

            var fullConfig = new Dictionary<string, object>
            {
                ["log"] = new { loglevel = "warning" },
                ["inbounds"] = new object[]
                {
                    new
                    {
                        tag = "socks-in",
                        port = 10808,
                        listen = "127.0.0.1",
                        protocol = "socks",
                        settings = new { auth = "noauth", udp = true }
                    },
                    new
                    {
                        tag = "http-in",
                        port = 10809,
                        listen = "127.0.0.1",
                        protocol = "http"
                    }
                },
                ["outbounds"] = new object[]
                {
                    outboundVless,
                    new { tag = "direct", protocol = "freedom" },
                    new { tag = "block", protocol = "blackhole" }
                },
                ["routing"] = new
                {
                    domainStrategy = "IPIfNonMatch",
                    rules = routingRules.ToArray()
                }
            };

            return JsonSerializer.Serialize(fullConfig, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}

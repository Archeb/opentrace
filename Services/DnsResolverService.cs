using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Ae.Dns.Client;
using Ae.Dns.Protocol;
using Ae.Dns.Protocol.Records;
using OpenTrace.Infrastructure;

namespace OpenTrace.Services
{
    /// <summary>
    /// DNS 解析服务，支持系统 DNS、DoH 和传统 UDP DNS
    /// </summary>
    public class DnsResolverService
    {
        /// <summary>
        /// 解析主机名到 IP 地址数组
        /// </summary>
        /// <param name="host">要解析的主机名</param>
        /// <param name="resolver">解析器配置（"system"、DoH URL 或 IP 地址）</param>
        /// <returns>解析到的 IP 地址数组</returns>
        public IPAddress[] ResolveHost(string host, string resolver)
        {
            if (resolver == "system")
            {
                // 使用系统解析
                return Dns.GetHostAddresses(host);
            }
            else if (resolver.IndexOf("https://") == 0)
            {
                // 使用DoH
                return ResolveWithDoH(host, resolver);
            }
            else
            {
                // 使用传统 DNS
                return ResolveWithUdp(host, resolver);
            }
        }

        private IPAddress[] ResolveWithDoH(string host, string resolver)
        {
            var httpClient = new System.Net.Http.HttpClient
            {
                BaseAddress = new Uri(resolver)
            };
            IDnsClient dnsClient = new DnsHttpClient(httpClient);

            // 同时查询 A 和 AAAA 记录
            DnsMessage aResult = Task.Run(() => dnsClient.Query(DnsQueryFactory.CreateQuery(host, Ae.Dns.Protocol.Enums.DnsQueryType.A))).Result;
            DnsMessage aaaaResult = Task.Run(() => dnsClient.Query(DnsQueryFactory.CreateQuery(host, Ae.Dns.Protocol.Enums.DnsQueryType.AAAA))).Result;

            if (aResult.Answers.Count == 0 && aaaaResult.Answers.Count == 0)
            {
                throw new SocketException();
            }
            
            return ParseDnsResults(aResult, aaaaResult);
        }

        private IPAddress[] ResolveWithUdp(string host, string resolver)
        {
            IDnsClient dnsClient = new DnsUdpClient(IPAddress.Parse(resolver));
            
            // 同时查询 A 和 AAAA 记录
            DnsMessage aResult = Task.Run(() => dnsClient.Query(DnsQueryFactory.CreateQuery(host, Ae.Dns.Protocol.Enums.DnsQueryType.A))).Result;
            DnsMessage aaaaResult = Task.Run(() => dnsClient.Query(DnsQueryFactory.CreateQuery(host, Ae.Dns.Protocol.Enums.DnsQueryType.AAAA))).Result;

            if (aResult.Answers.Count == 0 && aaaaResult.Answers.Count == 0)
            {
                throw new SocketException();
            }
            
            return ParseDnsResults(aResult, aaaaResult);
        }

        private IPAddress[] ParseDnsResults(DnsMessage aResult, DnsMessage aaaaResult)
        {
            List<IPAddress> addressList = new List<IPAddress>();

            // 处理 A 记录
            foreach (DnsResourceRecord answer in aResult.Answers)
            {
                if (answer.Type == Ae.Dns.Protocol.Enums.DnsQueryType.A)
                {
                    addressList.Add(((DnsIpAddressResource)answer.Resource).IPAddress);
                }
            }

            // 处理 AAAA 记录
            foreach (DnsResourceRecord answer in aaaaResult.Answers)
            {
                if (answer.Type == Ae.Dns.Protocol.Enums.DnsQueryType.AAAA)
                {
                    addressList.Add(((DnsIpAddressResource)answer.Resource).IPAddress);
                }
            }
            
            return addressList.ToArray();
        }

        /// <summary>
        /// 获取 DNS 解析器列表
        /// </summary>
        /// <returns>解析器列表，包含 Key 和 Text</returns>
        public List<(string Key, string Text)> GetResolverList()
        {
            var resolvers = new List<(string Key, string Text)>();
            
            // 系统 DNS 始终是第一个选项
            resolvers.Add(("system", Properties.Resources.SYSTEM_DNS_RESOLVER));
            
            if (!string.IsNullOrEmpty(UserSettings.customDNSResolvers))
            {
                string resolverConfig = UserSettings.customDNSResolvers.Replace("\r", "");
                foreach (string item in resolverConfig.Split('\n'))
                {
                    string[] parts = item.Split('#');
                    IPAddress resolverIP;
                    if (parts[0] != "" && (parts[0].IndexOf("https://") == 0 || IPAddress.TryParse(parts[0], out resolverIP)))
                    {
                        string text = parts.Length == 2 ? parts[1] : parts[0];
                        resolvers.Add((parts[0], text));
                    }
                }
            }
            
            return resolvers;
        }
    }
}

using System;using System.Collections.Generic;using System.Net;using System.Web.Script.Serialization;
public static class TunConfiguration
{
 static Dictionary<string,object> Map(params object[] p){var d=new Dictionary<string,object>();for(int i=0;i<p.Length;i+=2)d[(string)p[i]]=p[i+1];return d;}
 public static string Build(EngineRequest r)
 {
  if(r.WireGuard!=null||(r.DiscordOverlay!=null&&r.DiscordOverlay.WireGuard!=null))return DiscordWireGuard.Build(r);
  RoutingConfiguration.ValidateProxy(r.Host,r.Port,r.User,r.Password);
  bool https=String.Equals(r.Protocol,"HTTPS",StringComparison.Ordinal);
  if(!String.IsNullOrEmpty(r.Protocol)&&r.Protocol!="SOCKS5"&&!https)throw new ArgumentException("Protocolo inválido.");
  IPAddress ip;if(!IPAddress.TryParse(r.Host,out ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)throw new ArgumentException("Informe o IPv4 da proxy; hostnames não são usados para evitar DNS de bootstrap direto.");
  if(r.Route.Ipv6)throw new ArgumentException("IPv6 não é suportado.");
  if(https&&r.Route.Udp)throw new ArgumentException("HTTPS CONNECT não suporta UDP. Chamadas Discord exigem SOCKS5 com UDP; escolha SOCKS5 ou desmarque Chamadas / UDP.");
  var targets=new List<string>(r.Route.SelectedPaths());targets.Add(System.IO.Path.Combine(AppPaths.Root,"probe","PKArouteProbe.exe"));
  var rules=new List<object>();
  rules.Add(Map("port",53,"action","hijack-dns"));
  rules.Add(Map("process_path",new[]{r.OwnerPath??System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName},"action","route","outbound","direct"));
  if(r.Route.DirectDiscordCalls&&r.DiscordOverlay==null){if(!r.Route.Global||r.Route.Udp)throw new ArgumentException("Chamadas diretas exigem modo global TCP.");rules.Add(Map("type","logical","mode","and","rules",new[]{Map("process_name",new[]{"Discord.exe"}),Map("network",new[]{"udp"}),Map("ip_version",4)},"action","route","outbound","direct"));}
  if(r.DiscordOverlay!=null){if(!r.PermanentDiscord)throw new ArgumentException("A rota Discord exige modo permanente.");DiscordProxy.Validate(r.DiscordOverlay);var discord=Map("process_name",new[]{"Discord.exe"});rules.Add(Map("type","logical","mode","and","rules",new[]{discord,Map("network",new[]{"tcp"}),Map("ip_version",4)},"action","route","outbound","discord-proxy"));rules.Add(Map("type","logical","mode","and","rules",new[]{discord,Map("network",new[]{"udp"}),Map("ip_version",4)},"action","route","outbound","direct"));rules.Add(Map("process_name",new[]{"Discord.exe"},"action","reject"));}
  var processes=r.PermanentDiscord&&r.DiscordOverlay==null?Map("type","logical","mode","or","rules",new[]{Map("process_path",targets.ToArray()),Map("process_name",new[]{"Discord.exe"})}):Map("process_path",targets.ToArray());
  if(r.PermanentDiscord&&r.DiscordOverlay==null&&(https||r.Route.Udp||r.Route.Global))throw new ArgumentException("O modo permanente Discord exige SOCKS5 TCP e seleção por aplicativo.");
  if(!r.Route.Global)rules.Add(Map("type","logical","mode","and","rules",new[]{processes,Map("network",r.Route.Udp?new[]{"tcp","udp"}:new[]{"tcp"}),Map("ip_version",4)},"action","route","outbound","proxy"));
  if(r.PermanentDiscord&&r.DiscordOverlay==null)rules.Add(Map("type","logical","mode","and","rules",new[]{processes,Map("network",new[]{"udp"}),Map("ip_version",4)},"action","route","outbound","direct"));
  if(!r.Route.Global){var reject=new Dictionary<string,object>(processes);reject["action"]="reject";rules.Add(reject);}
  if(r.Route.Global){rules.Add(Map("ip_version",6,"action","reject"));rules.Add(Map("network",r.Route.Udp?new[]{"icmp"}:new[]{"udp","icmp"},"action","reject"));}
  var proxy=Map("type",https?"http":"socks","tag","proxy","server",r.Host,"server_port",r.Port);if(https&&r.ProxyTls)proxy["tls"]=Map("enabled",true,"server_name",String.IsNullOrWhiteSpace(r.TlsName)?r.Host:r.TlsName);else if(!https)proxy["version"]="5";if(r.User.Length>0){proxy["username"]=r.User;proxy["password"]=r.Password;}
  var outbounds=new List<object>{proxy,Map("type","direct","tag","direct")};if(r.DiscordOverlay!=null){var d=r.DiscordOverlay;var outbound=Map("type","socks","version","5","tag","discord-proxy","server",d.Host,"server_port",d.Port);if(d.User.Length>0){outbound["username"]=d.User;outbound["password"]=d.Password;}outbounds.Add(outbound);}
  return new JavaScriptSerializer().Serialize(Map(
   "log",Map("level","warn","timestamp",true),
   "dns",Map("servers",new[]{Map("type","https","tag","secure-dns","server","1.1.1.1","path","/dns-query","detour","proxy","tls",Map("enabled",true,"server_name","cloudflare-dns.com"))},"final","secure-dns","strategy","ipv4_only"),
   "inbounds",new[]{Map("type","tun","tag","pka-tun","interface_name","PKAproxy-TUN","address",new[]{"172.27.254.1/30","fd5a:504b:4100::1/126"},"mtu",1400,"auto_route",true,"strict_route",true)},
   "outbounds",outbounds.ToArray(),
   "route",Map("auto_detect_interface",true,"find_process",true,"final",r.Route.Global?"proxy":"direct","rules",rules.ToArray())));
 }
}

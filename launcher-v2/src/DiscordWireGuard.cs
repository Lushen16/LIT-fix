using System;
using System.Net;
using System.Collections.Generic;
using System.Web.Script.Serialization;

public sealed class WireGuardProfile
{
 public string PrivateKey="",PublicKey="",PresharedKey="",Endpoint="";
 public string[] Addresses=new string[0],AllowedIPs=new string[0];public int Port,MTU=1420,Keepalive=25;
 public static WireGuardProfile Parse(string text)
 {
  if(String.IsNullOrWhiteSpace(text)||text.Length>32768)throw new ArgumentException("Arquivo WireGuard vazio ou muito grande.");
  var fields=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);string section="";int interfaces=0,peers=0;
  foreach(string raw in text.Split('\n')){string line=raw.Trim();int comment=line.IndexOf('#');if(comment>=0)line=line.Substring(0,comment).Trim();if(line.Length==0)continue;
   if(line.Equals("[Interface]",StringComparison.OrdinalIgnoreCase)){section="Interface";interfaces++;continue;}if(line.Equals("[Peer]",StringComparison.OrdinalIgnoreCase)){section="Peer";peers++;continue;}
   int equal=line.IndexOf('=');if(equal<1||section.Length==0)throw new ArgumentException("Formato WireGuard inválido.");string key=line.Substring(0,equal).Trim(),value=line.Substring(equal+1).Trim();
   string allowed=section=="Interface"?"|PrivateKey|Address|DNS|MTU|ListenPort|":"|PublicKey|PresharedKey|AllowedIPs|Endpoint|PersistentKeepalive|";
   if(allowed.IndexOf("|"+key+"|",StringComparison.OrdinalIgnoreCase)<0||fields.ContainsKey(section+"/"+key))throw new ArgumentException("Opção WireGuard não suportada ou repetida: "+key);
   fields.Add(section+"/"+key,value);
  }
  if(interfaces!=1||peers!=1)throw new ArgumentException("Use uma configuração WireGuard com um servidor.");
  var p=new WireGuardProfile();p.PrivateKey=Required(fields,"Interface/PrivateKey");p.PublicKey=Required(fields,"Peer/PublicKey");fields.TryGetValue("Peer/PresharedKey",out p.PresharedKey);p.PresharedKey=p.PresharedKey??"";
  p.Addresses=Split(Required(fields,"Interface/Address"));p.AllowedIPs=Split(Required(fields,"Peer/AllowedIPs"));string endpoint=Required(fields,"Peer/Endpoint");int colon=endpoint.LastIndexOf(':');
  if(colon<1||!Int32.TryParse(endpoint.Substring(colon+1),out p.Port))throw new ArgumentException("Endpoint WireGuard inválido.");p.Endpoint=endpoint.Substring(0,colon);
  string option;if(fields.TryGetValue("Interface/MTU",out option)&&!Int32.TryParse(option,out p.MTU))throw new ArgumentException("MTU inválido.");if(fields.TryGetValue("Peer/PersistentKeepalive",out option)&&!Int32.TryParse(option,out p.Keepalive))throw new ArgumentException("Keepalive inválido.");
  Validate(p);return p;
 }
 static string Required(Dictionary<string,string> fields,string name){string value;if(!fields.TryGetValue(name,out value)||String.IsNullOrWhiteSpace(value))throw new ArgumentException("Campo obrigatório ausente: "+name);return value;}
 static string[] Split(string value){return Array.ConvertAll(value.Split(','),x=>x.Trim());}
 static void Key(string value){try{if(Convert.FromBase64String(value).Length!=32)throw new FormatException();}catch{throw new ArgumentException("Chave WireGuard inválida.");}}
 static void Prefix(string value){string[] parts=value.Split('/');IPAddress ip;int bits;if(parts.Length!=2||!IPAddress.TryParse(parts[0],out ip)||!Int32.TryParse(parts[1],out bits)||bits<0||bits>(ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork?32:128))throw new ArgumentException("Endereço de rede WireGuard inválido.");}
 public static void Validate(WireGuardProfile p)
 {
  if(p==null)throw new ArgumentException("Importe o arquivo .conf do Proton.");Key(p.PrivateKey);Key(p.PublicKey);if(!String.IsNullOrEmpty(p.PresharedKey))Key(p.PresharedKey);
  IPAddress ip;if(!IPAddress.TryParse(p.Endpoint,out ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork||p.Port<1||p.Port>65535)throw new ArgumentException("Use uma configuração Proton com endpoint IPv4 e porta válidos.");
  if(p.Addresses==null||p.Addresses.Length==0||p.AllowedIPs==null||Array.IndexOf(p.AllowedIPs,"0.0.0.0/0")<0)throw new ArgumentException("A configuração deve incluir Address e AllowedIPs = 0.0.0.0/0.");
  bool v4=false;foreach(string value in p.Addresses){Prefix(value);v4|=IPAddress.Parse(value.Split('/')[0]).AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork;}if(!v4)throw new ArgumentException("A configuração precisa de um endereço IPv4 interno.");foreach(string value in p.AllowedIPs)Prefix(value);if(p.MTU<1280||p.MTU>1500||p.Keepalive<0||p.Keepalive>65535)throw new ArgumentException("MTU ou keepalive fora do intervalo permitido.");
 }
}

public static class DiscordWireGuard
{
 static Dictionary<string,object> Map(params object[] values){var d=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)d[(string)values[i]]=values[i+1];return d;}
 public static string Build(EngineRequest original)
 {
  bool combined=original.DiscordOverlay!=null;WireGuardProfile wg=combined?original.DiscordOverlay.WireGuard:original.WireGuard;WireGuardProfile.Validate(wg);
  if(!combined&&original.Route.Global)throw new ArgumentException("WireGuard está disponível somente para Discord por aplicativo.");
  var serializer=new JavaScriptSerializer();var r=serializer.Deserialize<EngineRequest>(serializer.Serialize(original));r.WireGuard=null;
  string tag=combined?"discord-proxy":"proxy";
  if(combined){DiscordProxy.Validate(original.DiscordOverlay);r.DiscordOverlay.WireGuard=null;r.DiscordOverlay.Host="192.0.2.1";r.DiscordOverlay.Port=1080;r.DiscordOverlay.User=r.DiscordOverlay.Password="";}
  else{r.Protocol="SOCKS5";r.Host="192.0.2.1";r.Port=1080;r.User=r.Password="";r.Route.Udp=false;r.Route.DirectDiscordCalls=false;foreach(var app in r.Route.Apps)if(!String.Equals(System.IO.Path.GetFileName(app.Path),"Discord.exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("A VPN aceita somente Discord.exe.");}
  var config=(Dictionary<string,object>)serializer.DeserializeObject(TunConfiguration.Build(r));var outputs=new List<object>();foreach(Dictionary<string,object> outbound in (object[])config["outbounds"])if((string)outbound["tag"]!=tag)outputs.Add(outbound);config["outbounds"]=outputs.ToArray();
  
  // Enforce IPv4-only in TUN adapter so games and non-Discord apps don't stall on broken IPv6 dual-stack
  try{((Dictionary<string,object>)((object[])config["inbounds"])[0])["address"]=new[]{"172.27.254.1/30"};}catch{}

  // Filter WireGuard addresses and AllowedIPs strictly to IPv4 to prevent WSAEINVAL on systems without IPv6
  var v4Addresses=new List<string>();
  foreach(string a in wg.Addresses){IPAddress ip;if(IPAddress.TryParse(a.Split('/')[0],out ip)&&ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork)v4Addresses.Add(a);}
  var v4AllowedIPs=new List<string>();
  foreach(string a in wg.AllowedIPs){IPAddress ip;if(IPAddress.TryParse(a.Split('/')[0],out ip)&&ip.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork)v4AllowedIPs.Add(a);}
  if(v4AllowedIPs.Count==0)v4AllowedIPs.Add("0.0.0.0/0");

  var peer=Map("address",wg.Endpoint,"port",wg.Port,"public_key",wg.PublicKey,"allowed_ips",v4AllowedIPs.ToArray(),"persistent_keepalive_interval",wg.Keepalive);if(!String.IsNullOrEmpty(wg.PresharedKey))peer["pre_shared_key"]=wg.PresharedKey;
  config["endpoints"]=new[]{Map("type","wireguard","tag",tag,"system",false,"address",v4Addresses.ToArray(),"private_key",wg.PrivateKey,"mtu",wg.MTU,"peers",new[]{peer})};
  var routing=(Dictionary<string,object>)config["route"];var rules=(object[])routing["rules"];
  // Both TCP and UDP use WireGuard. No direct-voice fallback is retained.
  ((Dictionary<string,object>)rules[3])["outbound"]=tag;
  string discordProbe=System.IO.Path.Combine(AppPaths.Root,"probe","DiscordrouteProbe.exe");
  string pkaProbe=System.IO.Path.Combine(AppPaths.Root,"probe","PKArouteProbe.exe");
  var ordered=new List<object>(rules);
  ordered.Insert(2,Map("process_path",new[]{discordProbe,pkaProbe},"action","route","outbound",tag));
  
  // Scope DNS hijack rule strictly to Discord and the route probes so other apps and games keep normal direct DNS
  for(int i=0;i<ordered.Count;i++){
    var rule=ordered[i] as Dictionary<string,object>;
    if(rule!=null&&rule.ContainsKey("action")&&(string)rule["action"]=="hijack-dns"){
      ordered[i]=Map("type","logical","mode","and","rules",new[]{
        Map("type","logical","mode","or","rules",new[]{
          Map("process_name",new[]{"Discord.exe"}),
          Map("process_path",new[]{discordProbe,pkaProbe})
        }),
        Map("port",53)
      },"action","hijack-dns");
      break;
    }
  }
  routing["rules"]=ordered.ToArray();
  if(!combined){
   // Unrelated applications keep direct DNS; Discord and the route probe use the VPN resolver.
   var dns=(Dictionary<string,object>)config["dns"];dns["servers"]=new[]{Map("type","https","tag","secure-dns","server","1.1.1.1","path","/dns-query","detour",tag,"tls",Map("enabled",true,"server_name","cloudflare-dns.com")),Map("type","udp","tag","direct-dns","server","1.1.1.1")};
   dns["rules"]=new[]{Map("type","logical","mode","or","rules",new[]{Map("process_name",new[]{"Discord.exe"}),Map("process_path",new[]{pkaProbe,discordProbe})},"action","route","server","secure-dns")};dns["final"]="direct-dns";
  }
  else{var dns=(Dictionary<string,object>)config["dns"];var servers=new List<object>((object[])dns["servers"]);servers.Add(Map("type","https","tag","discord-dns","server","1.1.1.1","path","/dns-query","detour",tag,"tls",Map("enabled",true,"server_name","cloudflare-dns.com")));dns["servers"]=servers.ToArray();dns["rules"]=new[]{Map("type","logical","mode","or","rules",new[]{Map("process_name",new[]{"Discord.exe"}),Map("process_path",new[]{discordProbe,pkaProbe})},"action","route","server","discord-dns")};}
  routing["default_domain_resolver"]=combined?"secure-dns":"direct-dns";
  return serializer.Serialize(config);
 }
}

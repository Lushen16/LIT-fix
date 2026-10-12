using System;
using System.IO;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.ServiceProcess;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;

public static class WireGuardIntegration
{
 public static string Name { get { using(var hash=System.Security.Cryptography.SHA256.Create())return "Litfix-"+BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(WindowsIdentity.GetCurrent().User.Value))).Replace("-","").Substring(0,12); } }
 public static string Folder { get { return Path.Combine(AppPaths.Root,"WireGuard"); } }
 public static string Config { get { return Path.Combine(Folder,Name+".conf"); } }
 public static string Executable { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WireGuard","wireguard.exe"); } }
 public static bool Installed { get { return File.Exists(Executable); } }
 public static bool Configured { get { return File.Exists(Config); } }
 public static bool HasService { get { using(var service=new ServiceController("WireGuardTunnel$"+Name)){try{var status=service.Status;return true;}catch(InvalidOperationException){return false;}} } }
 public static bool Running { get { using(var service=new ServiceController("WireGuardTunnel$"+Name)){try{return service.Status==ServiceControllerStatus.Running;}catch(InvalidOperationException){return false;}} } }
 public static string Validate(string text)
 {
  if(String.IsNullOrWhiteSpace(text)||text.Length>32768)throw new InvalidOperationException("Configuração WireGuard vazia ou muito grande.");
  string section="";int interfaces=0,peers=0;bool privateKey=false,publicKey=false,address=false,endpoint=false,v4=false,v6=false;
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var result=new StringBuilder();
  foreach(string raw in text.Split('\n')){
   string line=raw.Trim();int comment=line.IndexOf('#');if(comment>=0)line=line.Substring(0,comment).Trim();if(line.Length==0)continue;
   if(line.Equals("[Interface]",StringComparison.OrdinalIgnoreCase)){section="Interface";interfaces++;result.AppendLine("[Interface]");continue;}
   if(line.Equals("[Peer]",StringComparison.OrdinalIgnoreCase)){section="Peer";peers++;result.AppendLine("[Peer]");continue;}
   int equal=line.IndexOf('=');if(equal<1||section.Length==0)throw new InvalidOperationException("Formato de configuração inválido.");
   string key=line.Substring(0,equal).Trim(),value=line.Substring(equal+1).Trim();
   string allowed=section=="Interface"?"|PrivateKey|Address|DNS|MTU|ListenPort|":"|PublicKey|PresharedKey|AllowedIPs|Endpoint|PersistentKeepalive|";
   if(allowed.IndexOf("|"+key+"|",StringComparison.OrdinalIgnoreCase)<0||!seen.Add(section+"/"+key))throw new InvalidOperationException("Opção não suportada ou repetida: "+key+". Use uma configuração padrão, sem comandos personalizados.");
   if(key.Equals("PrivateKey",StringComparison.OrdinalIgnoreCase)||key.Equals("PublicKey",StringComparison.OrdinalIgnoreCase)||key.Equals("PresharedKey",StringComparison.OrdinalIgnoreCase)){
    try{if(Convert.FromBase64String(value).Length!=32)throw new FormatException();}catch{throw new InvalidOperationException("Chave WireGuard inválida.");}
    if(key.Equals("PrivateKey",StringComparison.OrdinalIgnoreCase))privateKey=true;if(key.Equals("PublicKey",StringComparison.OrdinalIgnoreCase))publicKey=true;
   }
   if(key.Equals("Address",StringComparison.OrdinalIgnoreCase))address=value.Length>0;
   if(key.Equals("Endpoint",StringComparison.OrdinalIgnoreCase))endpoint=value.Length>0;
   if(key.Equals("AllowedIPs",StringComparison.OrdinalIgnoreCase))foreach(string network in value.Split(',')){v4|=network.Trim()=="0.0.0.0/0";v6|=network.Trim()=="::/0";}
   result.AppendLine(key+" = "+value);
  }
  if(interfaces!=1||peers!=1||!privateKey||!publicKey||!address||!endpoint)throw new InvalidOperationException("Importe uma configuração com uma Interface e um Peer completos.");
  if(!v4||!v6)throw new InvalidOperationException("Este teste exige AllowedIPs = 0.0.0.0/0, ::/0 para cobrir IPv4 e IPv6. Solicite uma configuração de VPN completa ao fornecedor.");
  return result.ToString();
 }
 public static void Import(string path)
 {
  if(HasService)throw new InvalidOperationException("Desative a VPN antes de importar outra configuração.");
  if(new FileInfo(path).Length>32768)throw new InvalidOperationException("Arquivo de configuração muito grande.");
  string text=Validate(File.ReadAllText(path));Directory.CreateDirectory(Folder);
  var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
  foreach(var sid in new[]{WindowsIdentity.GetCurrent().User,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null)})security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
  Directory.SetAccessControl(Folder,security);File.WriteAllText(Config,text,new UTF8Encoding(false));
 }
 public static void Change(bool enable)
 {
  if(!enable&&!HasService)return;
  if(!Installed)throw new InvalidOperationException("Instale o WireGuard oficial primeiro.");
  if(enable){if(HasService)throw new InvalidOperationException("A VPN já está configurada; desative antes de ativar novamente.");if(!Configured)throw new InvalidOperationException("Importe o arquivo .conf fornecido pelo servidor VPN.");Validate(File.ReadAllText(Config));
   if(DiscordProxy.HasConfiguration||NetworkGuard.ArmedState!=false||EngineController.Status().StartsWith("Motor ativo"))throw new InvalidOperationException("Remova o Discord permanente, pare a Proxy geral e libere a rede direta antes de testar o WireGuard.");}
  if(!enable&&!HasService)return;
  using(var process=Process.Start(new ProcessStartInfo(Executable,enable?"/installtunnelservice \""+Config+"\"":"/uninstalltunnelservice "+Name){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden})){
   if(!process.WaitForExit(60000))throw new InvalidOperationException("A operação ainda não terminou. Consulte o estado antes de tentar novamente.");
   if(process.ExitCode!=0)throw new InvalidOperationException("O WireGuard não concluiu a operação. Abra o aplicativo oficial para consultar o diagnóstico.");
  }
  if(enable){using(var service=new ServiceController("WireGuardTunnel$"+Name))service.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(15));}
 }
 public static string Test()
 {
  if(!Running)throw new InvalidOperationException("Ative a VPN antes de testar.");
  // Generate traffic even when a newly activated tunnel has not handshaken yet.
  var request=(HttpWebRequest)WebRequest.Create("https://api.ipify.org");request.Proxy=null;request.Timeout=10000;request.ReadWriteTimeout=10000;
  string ip;using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))ip=reader.ReadToEnd().Trim();IPAddress parsed;if(!IPAddress.TryParse(ip,out parsed))throw new InvalidOperationException("A consulta de IP retornou uma resposta inválida.");
  var start=new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(Executable),"wg.exe"),"show "+Name+" latest-handshakes"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
  bool fresh=false;using(var process=Process.Start(start)){
   if(!process.WaitForExit(5000)){process.Kill();throw new InvalidOperationException("O diagnóstico WireGuard demorou demais.");}
   string output=process.StandardOutput.ReadToEnd();if(process.ExitCode!=0)throw new InvalidOperationException("Não foi possível consultar o túnel. Abra o Litfix como administrador para executar este teste.");
   long now=(long)(DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds;
   foreach(string line in output.Split('\n')){string[] fields=line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);long timestamp;if(fields.Length==2&&Int64.TryParse(fields[1],out timestamp)&&timestamp>0&&now-timestamp>=0&&now-timestamp<180)fresh=true;}
  }
  return (fresh?"Servidor WireGuard respondeu recentemente.":"Handshake ainda não confirmado; repita o teste em alguns segundos.")+"\r\nIP público observado: "+ip+"\r\nAgora abra o Discord e teste uma chamada. Este diagnóstico não mede a chamada.";
 }
}

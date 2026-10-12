using System;using System.IO;using System.Text;using System.Diagnostics;using System.Threading;using System.Security.Cryptography;using System.Security.Principal;using System.Security.AccessControl;using System.Web.Script.Serialization;using System.Xml;
public sealed class DiscordProfile
{
 public string Host="",User="",Password="",DiscordPath="";public int Port=1080;public WireGuardProfile WireGuard;
}
public static class DiscordProxy
{
 public static string TaskName {get{return "Litfix-Discord-"+WindowsIdentity.GetCurrent().User.Value;}}
 public static string PrivateRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"LitfixDiscord",WindowsIdentity.GetCurrent().User.Value);}}
 static string Draft {get{return AppPaths.SettingsFile("discord-profile.bin");}}
 static string ProfileFile {get{return Path.Combine(PrivateRoot,"profile.bin");}}
 public static DiscordProfile Load()
 {try{return Decode(File.ReadAllBytes(Draft));}catch{return new DiscordProfile();}}
 public static byte[] Encode(DiscordProfile profile)
 {return ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(profile)),null,DataProtectionScope.CurrentUser);}
 public static DiscordProfile Decode(byte[] bytes)
 {return new JavaScriptSerializer().Deserialize<DiscordProfile>(Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes,null,DataProtectionScope.CurrentUser)));}
 public static void Save(DiscordProfile profile){Validate(profile);Directory.CreateDirectory(AppPaths.Root);File.WriteAllBytes(Draft,Encode(profile));}
 public static void Validate(DiscordProfile p)
 {
  if(p==null)throw new ArgumentException("Configure a conexão do Discord.");if(p.WireGuard!=null)WireGuardProfile.Validate(p.WireGuard);else{RoutingConfiguration.ValidateProxy(p.Host,p.Port,p.User,p.Password);
  System.Net.IPAddress ip;if(!System.Net.IPAddress.TryParse(p.Host,out ip)||ip.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork)throw new ArgumentException("Informe o IPv4 da SOCKS5.");}
  if(!File.Exists(p.DiscordPath)||!String.Equals(Path.GetFileName(p.DiscordPath),"Discord.exe",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Localize o Discord.exe instalado.");
 }
 public static string TaskXml(string sid,string executable)
 {
  var text=new StringBuilder();using(var x=XmlWriter.Create(text,new XmlWriterSettings{Indent=true})){
   x.WriteStartElement("Task","http://schemas.microsoft.com/windows/2004/02/mit/task");x.WriteAttributeString("version","1.2");
   x.WriteStartElement("RegistrationInfo");x.WriteElementString("Description","Litfix: conexão permanente exclusiva do Discord.");x.WriteEndElement();
   x.WriteStartElement("Triggers");x.WriteStartElement("LogonTrigger");x.WriteElementString("Enabled","true");x.WriteElementString("UserId",sid);x.WriteEndElement();x.WriteEndElement();
   x.WriteStartElement("Principals");x.WriteStartElement("Principal");x.WriteAttributeString("id","User");x.WriteElementString("UserId",sid);x.WriteElementString("LogonType","InteractiveToken");x.WriteElementString("RunLevel","HighestAvailable");x.WriteEndElement();x.WriteEndElement();
   x.WriteStartElement("Settings");x.WriteElementString("MultipleInstancesPolicy","IgnoreNew");x.WriteElementString("DisallowStartIfOnBatteries","false");x.WriteElementString("StopIfGoingOnBatteries","false");x.WriteElementString("AllowHardTerminate","true");x.WriteElementString("StartWhenAvailable","true");x.WriteElementString("Enabled","true");x.WriteElementString("ExecutionTimeLimit","PT0S");x.WriteStartElement("RestartOnFailure");x.WriteElementString("Interval","PT1M");x.WriteElementString("Count","3");x.WriteEndElement();x.WriteEndElement();
   x.WriteStartElement("Actions");x.WriteAttributeString("Context","User");x.WriteStartElement("Exec");x.WriteElementString("Command",executable);x.WriteElementString("Arguments","--discord-background");x.WriteEndElement();x.WriteEndElement();x.WriteEndElement();
  }return text.ToString();
 }
 static int Schedule(string arguments,bool required)
 {
  using(var p=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"schtasks.exe"),arguments){UseShellExecute=false,CreateNoWindow=true})){
   if(!p.WaitForExit(15000)){p.Kill();throw new IOException("O Agendador não respondeu.");}if(required&&p.ExitCode!=0)throw new IOException("Não foi possível configurar o início automático do Discord.");return p.ExitCode;
  }
 }
 public static bool HasConfiguration {get{return Configured||File.Exists(ProfileFile)||OwnsGuard;}}
 public static bool Configured {get{try{return Schedule("/Query /TN \""+TaskName+"\"",false)==0;}catch{return false;}}}
 static void SecureDirectory(string path)
 {
  if(Directory.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("A pasta permanente não pode ser um link.");
  Directory.CreateDirectory(path);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
  foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
  acl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));Directory.SetAccessControl(path,acl);
 }
 public static void Elevated(bool enable)
 {
  using(var p=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,enable?"--discord-enable":"--discord-disable"){UseShellExecute=true,Verb="runas"})){
   if(!p.WaitForExit(90000))throw new IOException("A operação não foi confirmada. Confira o aviso do Windows.");if(p.ExitCode!=0){string detail="Confira os dados, UDP, feche o Discord e pare/libere o túnel geral antes de ativar.";try{detail=File.ReadAllText(AppPaths.SettingsFile("discord-operation-error.txt"));}catch{}throw new IOException("Não foi possível configurar a proxy permanente. "+detail);}
  }
 }
 public static int Enable()
 {
  bool installed=false;try{
   RequireAdmin();var profile=Load();Validate(profile);
   if(profile.WireGuard!=null){
    try{
     using(var p=Process.Start(new ProcessStartInfo("powershell", "-NoProfile -NonInteractive -Command \"Enable-NetAdapterBinding -Name * -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){
      p.WaitForExit(7000);
     }
    }catch{}
   }
   if(HasConfiguration)throw new InvalidOperationException("Remova a configuração permanente antes de alterar a proxy.");
   if(EngineController.Status().StartsWith("Motor ativo")||NetworkGuard.ArmedState!=false)throw new InvalidOperationException("Pare e libere o túnel geral primeiro.");
   // Discord can remain open
   if(profile.WireGuard==null)ProxyDiagnostics.ProxyIp(profile.Host,profile.Port,profile.User,profile.Password);
   SecureDirectory(Path.GetDirectoryName(PrivateRoot));SecureDirectory(PrivateRoot);
   string exe=Path.Combine(PrivateRoot,"Litfix-Discord.exe");using(var resource=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PKAengine.exe")){if(resource==null)throw new IOException("O pacote não contém o motor permanente.");using(var target=File.Create(exe))resource.CopyTo(target);}File.WriteAllBytes(ProfileFile,Encode(profile));
   string xml=Path.Combine(PrivateRoot,"task.xml");File.WriteAllText(xml,TaskXml(WindowsIdentity.GetCurrent().User.Value,exe),Encoding.Unicode);
   installed=true;string stopFile=Path.Combine(PrivateRoot,"stop");if(File.Exists(stopFile))File.Delete(stopFile);
   Schedule("/Create /TN \""+TaskName+"\" /XML \""+xml+"\" /F",true);Schedule("/Run /TN \""+TaskName+"\"",true);
   var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<55000){if(EngineController.Mode()=="DISCORD")return 0;Thread.Sleep(500);}
   string detail="O túnel permanente não iniciou.";string ready=Path.Combine(PrivateRoot,"engine-discord.bin.ready");if(File.Exists(ready))detail+=" "+File.ReadAllText(ready);throw new IOException(detail);
  }catch(Exception error){
   try{Directory.CreateDirectory(AppPaths.Root);File.WriteAllText(AppPaths.SettingsFile("discord-operation-error.txt"),error.Message);}catch{}
   // A failed enable must not leave a startup job or an orphaned protection policy.
   if(installed)Disable();
   return 1;
  }
 }
 static void RequireAdmin(){if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("Permita o aviso de administrador.");}
 public static int Disable()
 {
  try{
   RequireAdmin();bool task=Configured,owned=File.Exists(Path.Combine(PrivateRoot,"guard-owner"));
   if(!task&&!owned&&!File.Exists(ProfileFile))return 0;
   File.WriteAllText(Path.Combine(PrivateRoot,"stop"),"");if(task)Schedule("/Delete /TN \""+TaskName+"\" /F",true);
   if(EngineController.Mode()=="DISCORD"||EngineController.Mode()=="COMBINED")EngineController.Command("STOP",15000);
   using(var gate=new Mutex(false,@"Global\PKAproxy-TUN")){bool locked=false;try{try{locked=gate.WaitOne(15000);}catch(AbandonedMutexException){locked=true;}if(!locked)throw new IOException("O motor ainda está ativo.");if(owned)NetworkGuard.Release();}finally{if(locked)gate.ReleaseMutex();}}
   foreach(string name in new[]{"profile.bin","guard-owner","task.xml","dashboard.bin"}){string file=Path.Combine(PrivateRoot,name);if(File.Exists(file))File.Delete(file);}
   if(File.Exists(Draft))File.Delete(Draft);
   foreach(var daemon in Process.GetProcessesByName("Litfix-Discord")){using(daemon){try{if(String.Equals(daemon.MainModule.FileName,Path.Combine(PrivateRoot,"Litfix-Discord.exe"),StringComparison.OrdinalIgnoreCase))daemon.WaitForExit(5000);}catch{}}}
   string binary=Path.Combine(PrivateRoot,"Litfix-Discord.exe");try{if(File.Exists(binary))File.Delete(binary);}catch{}return 0;
  }catch(Exception error){try{Directory.CreateDirectory(AppPaths.Root);File.WriteAllText(AppPaths.SettingsFile("discord-operation-error.txt"),error.Message);}catch{}return 1;}
 }
 public static bool OwnsGuard {get{return File.Exists(Path.Combine(PrivateRoot,"guard-owner"));}}
 static string DashboardFile {get{return Path.Combine(PrivateRoot,"dashboard.bin");}}
 public static void ClearDashboard(){if(File.Exists(DashboardFile))File.Delete(DashboardFile);}
 public static bool DashboardOwnerAlive(EngineRequest request){try{using(var p=Process.GetProcessById(request.DashboardOwnerPid))return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==request.DashboardOwnerStart;}catch{return false;}}
 public static long DashboardRevision {get{return File.Exists(DashboardFile)?File.GetLastWriteTimeUtc(DashboardFile).Ticks:0;}}
 public static void ChangeDashboard(EngineRequest request,bool stop)
 {
  string path=AppPaths.SettingsFile("engine-dashboard-"+Guid.NewGuid().ToString("N")+".bin");
  try{if(!stop){request.OwnerPid=Process.GetCurrentProcess().Id;request.OwnerStart=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;TunConfiguration.Build(request);Directory.CreateDirectory(AppPaths.Root);File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(request)),null,DataProtectionScope.CurrentUser));}
   using(var p=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--discord-dashboard "+(stop?"stop":"\""+path+"\"")){UseShellExecute=true,Verb="runas"})){if(!p.WaitForExit(90000)||p.ExitCode!=0){string error="O motor não confirmou a alteração.";try{error=File.ReadAllText(AppPaths.SettingsFile("discord-operation-error.txt"));}catch{}throw new IOException(error);}}
  }finally{if(File.Exists(path))File.Delete(path);}
 }
 static void WriteDashboard(byte[] bytes){string temp=Path.Combine(PrivateRoot,"dashboard-"+Guid.NewGuid().ToString("N")+".tmp");try{File.WriteAllBytes(temp,bytes);if(File.Exists(DashboardFile))File.Replace(temp,DashboardFile,null);else File.Move(temp,DashboardFile);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static int ApplyDashboard(string[] args)
 {
  try{RequireAdmin();if(args.Length!=2||!Configured||!File.Exists(ProfileFile))throw new InvalidOperationException("Ative o Discord permanente primeiro.");
   if(System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(PrivateRoot,"Litfix-Discord.exe")).Version<new Version("2.0.17.0"))throw new InvalidOperationException("Remova e reative a configuração permanente para atualizar seu motor.");
   bool stop=args[1]=="stop";byte[] previous=File.Exists(DashboardFile)?File.ReadAllBytes(DashboardFile):null;
   string ready=Path.Combine(PrivateRoot,"engine-discord.bin.ready");if(File.Exists(ready))File.Delete(ready);
   if(stop){if(File.Exists(DashboardFile))File.Delete(DashboardFile);}else{string path=Path.GetFullPath(args[1]);if(!path.StartsWith(Path.GetFullPath(AppPaths.Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(path).StartsWith("engine-dashboard-")||new FileInfo(path).Length>65536)throw new ArgumentException("Pedido inválido.");byte[] bytes=File.ReadAllBytes(path);var r=new JavaScriptSerializer().Deserialize<EngineRequest>(Encoding.UTF8.GetString(ProtectedData.Unprotect(bytes,null,DataProtectionScope.CurrentUser)));if(r.PermanentDiscord||r.DiscordOverlay!=null)throw new ArgumentException("Pedido inválido.");TunConfiguration.Build(r);ProxyDiagnostics.ProxyIp(r.Host,r.Port,r.User,r.Password,r.Protocol,r.ProxyTls,r.TlsName);if(r.Route.Udp)ProxyDiagnostics.CheckUdp(r.Host,r.Port,r.User,r.Password,r.Protocol);WriteDashboard(bytes);}
   var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<65000){if(File.Exists(ready)){string result=File.ReadAllText(ready);if(result=="OK"&&EngineController.Mode()==(stop?"DISCORD":"COMBINED"))return 0;if(result!="OK")break;}Thread.Sleep(300);}
   if(previous==null){if(File.Exists(DashboardFile))File.Delete(DashboardFile);}else WriteDashboard(previous);
   throw new IOException("Não foi possível aplicar o Dashboard. A configuração anterior foi restaurada; consulte o diagnóstico.");
  }catch(Exception error){Directory.CreateDirectory(AppPaths.Root);File.WriteAllText(AppPaths.SettingsFile("discord-operation-error.txt"),error.Message);return 1;}
 }
 public static int Background()
 {
  RequireAdmin();AppPaths.ProtectedRoot=PrivateRoot;
  string stop=Path.Combine(PrivateRoot,"stop");if(File.Exists(stop))return 0;
  string requestFile=Path.Combine(PrivateRoot,"engine-discord.bin");
  while(File.Exists(ProfileFile)&&!File.Exists(stop)){long revisionAtStart=DashboardRevision;
   try{
    var profile=Decode(File.ReadAllBytes(ProfileFile));var found=AppDiscovery.Find(true);if(found.Length>0)profile.DiscordPath=found[0];Validate(profile);
    var request=new EngineRequest{PermanentDiscord=true,WireGuard=profile.WireGuard,Protocol="SOCKS5",Route=new RoutingOptions{Udp=false},Host=profile.Host,Port=profile.Port,User=profile.User,Password=profile.Password,OwnerPath=Process.GetCurrentProcess().MainModule.FileName,OwnerPid=Process.GetCurrentProcess().Id,OwnerStart=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks};
    long revision=DashboardRevision;if(revision!=0){request=new JavaScriptSerializer().Deserialize<EngineRequest>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(DashboardFile),null,DataProtectionScope.CurrentUser)));request.DashboardOwnerPid=request.OwnerPid;request.DashboardOwnerStart=request.OwnerStart;if(!DashboardOwnerAlive(request)){ClearDashboard();continue;}request.PermanentDiscord=true;request.DiscordOverlay=profile;request.OwnerPath=Process.GetCurrentProcess().MainModule.FileName;request.OwnerPid=Process.GetCurrentProcess().Id;request.OwnerStart=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;}request.DashboardStamp=revision;
    request.Route.Apps.Add(new RegisteredApp{Path=profile.DiscordPath});
    // Keep every installed Discord version in the persistent per-app guard.
    foreach(string exe in InstalledDiscordPaths(profile.DiscordPath))if(!request.Route.Apps.Exists(a=>String.Equals(a.Path,exe,StringComparison.OrdinalIgnoreCase)))request.Route.Apps.Add(new RegisteredApp{Path=exe});
    if(File.Exists(requestFile+".ready"))File.Delete(requestFile+".ready");
    File.WriteAllBytes(requestFile,ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(request)),null,DataProtectionScope.CurrentUser));
    TunRuntime.Run(new[]{"--engine-apply",requestFile});
   }catch{}
   if(File.Exists(requestFile))File.Delete(requestFile);
   for(int i=0;i<60&&!File.Exists(stop)&&DashboardRevision==revisionAtStart;i++)Thread.Sleep(1000);
  }return 0;
 }
 public static string[] InstalledDiscordPaths(string current)
 {
  var result=new System.Collections.Generic.List<string>{current};string parent=Path.GetDirectoryName(Path.GetDirectoryName(current));
  if(parent!=null&&Directory.Exists(parent))foreach(string dir in Directory.GetDirectories(parent,"app-*")){string exe=Path.Combine(dir,"Discord.exe");if(File.Exists(exe)&&!result.Contains(exe))result.Add(exe);}return result.ToArray();
 }
 public static void MarkGuard(){File.WriteAllText(Path.Combine(PrivateRoot,"guard-owner"),WindowsIdentity.GetCurrent().User.Value);}
 public static bool StopRequested {get{return File.Exists(Path.Combine(PrivateRoot,"stop"));}}
}

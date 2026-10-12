using System;using System.IO;using System.Text;using System.Diagnostics;using System.Security.Cryptography;using System.Security.Principal;using System.Web.Script.Serialization;using System.IO.Pipes;using System.Threading;
public sealed class EngineRequest {public RoutingOptions Route;public string Host,User,Password,OwnerPath;public string Protocol="SOCKS5",TlsName="";public bool ProxyTls=true,PermanentDiscord;public int Port,OwnerPid;public long OwnerStart,DashboardStamp,DashboardOwnerStart;public int DashboardOwnerPid;public DiscordProfile DiscordOverlay;public WireGuardProfile WireGuard;}
public static class EngineController
{
    public static string PipeName {get{return "PKAproxy-TUN-"+WindowsIdentity.GetCurrent().User.Value;}}
    public static string Command(string value,int timeout)
    {
        using(var pipe=new NamedPipeClientStream(".",PipeName,PipeDirection.InOut))
        {pipe.Connect(timeout);using(var writer=new StreamWriter(pipe,Encoding.UTF8,1024,true)){writer.AutoFlush=true;writer.WriteLine(value);using(var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true)){var response=reader.ReadLineAsync();if(!response.Wait(timeout))throw new TimeoutException("O motor não respondeu.");return response.Result;}}}
    }
    public static string Mode(){try{return Command("MODE",500);}catch{return "NONE";}}
    public static string Status()
    {try {return Command("STATUS",150)=="RUNNING"?"Motor ativo • Global TUN":"Motor desconectado";}catch{return "Motor desconectado • rede normal";}}
    public static void RunElevated(EngineRequest request,bool stop)
    {
        if(DiscordProxy.HasConfiguration){DiscordProxy.ChangeDashboard(request,stop);return;}
        if(stop){try {if(Command("STOP",1000)!="STOPPED")throw new IOException("O motor não confirmou a parada.");}catch(TimeoutException){if(Status().StartsWith("Motor ativo"))throw;}return;}
        if(Status().StartsWith("Motor ativo"))throw new InvalidOperationException("Desconecte antes de alterar a sessão.");
        request.OwnerPath=Process.GetCurrentProcess().MainModule.FileName;request.OwnerPid=Process.GetCurrentProcess().Id;request.OwnerStart=Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks;
        TunConfiguration.Build(request);
        Directory.CreateDirectory(AppPaths.Root);
        string path=Path.Combine(AppPaths.Root,"engine-"+Guid.NewGuid().ToString("N")+".bin");
        string helperRoot=Path.Combine(Path.GetTempPath(),"PKA-tun-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(helperRoot);
        string helper=Path.Combine(helperRoot,"PKAengine.exe");
        try
        {
            File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(request)),null,DataProtectionScope.CurrentUser));
            using(var s=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PKAengine.exe"))using(var f=File.Create(helper))s.CopyTo(f);
            using(var p=Process.Start(new ProcessStartInfo(helper,"--engine-apply \""+path+"\""){UseShellExecute=true}))
            {
                var timer=Stopwatch.StartNew();
                while(timer.ElapsedMilliseconds<45000)
                {
                    if(File.Exists(path+".ready")){string result=File.ReadAllText(path+".ready");if(result!="OK")throw new InvalidOperationException(result);return;}
                    if(p.HasExited)throw new InvalidOperationException("O motor encerrou antes de ativar. Veja o diagnóstico TUN.");
                    Thread.Sleep(200);
                }
                // Parent removes the request as a cancellation signal; the host checks it before activating.
                throw new TimeoutException("O motor não confirmou a ativação em 45 segundos. Confira o diagnóstico TUN.");
            }
        }
        finally {if(File.Exists(path))File.Delete(path);if(File.Exists(path+".ready"))File.Delete(path+".ready");try{File.Delete(helper);Directory.Delete(helperRoot);}catch{}}
    }
    public static int Apply(string[] args){return TunRuntime.Run(args);}
    public static void ReleaseGuard()
    {
        using(var process=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--guard-release"){UseShellExecute=true,Verb="runas"}))
        {if(!process.WaitForExit(20000))throw new IOException("A liberação não foi confirmada. Verifique o aviso do Windows.");if(process.ExitCode!=0)throw new IOException("Não foi possível remover o bloqueio. Execute Recuperar-rede.cmd como administrador.");}
    }
    public static string RoutedIp(bool insideEngine=false,bool discordProbe=false)
    {
        if(!insideEngine&&(Mode()=="DISCORD"||Mode()=="COMBINED")){string value=Command(discordProbe?"DISCORDIP":"IP",25000);System.Net.IPAddress ip;if(!System.Net.IPAddress.TryParse(value,out ip))throw new IOException("O túnel Discord não confirmou o IP.");return ip.ToString();}
        string root=Path.Combine(AppPaths.Root,"probe");Directory.CreateDirectory(root);
        string exe=Path.Combine(root,discordProbe?"DiscordrouteProbe.exe":"PKArouteProbe.exe"),result=Path.Combine(root,Guid.NewGuid().ToString("N")+".txt");
        using(var s=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PKArouteProbe.exe"))using(var f=File.Create(exe))s.CopyTo(f);
        try {using(var p=Process.Start(new ProcessStartInfo(exe,"\""+result+"\""){UseShellExecute=false,CreateNoWindow=true})){if(!p.WaitForExit(20000)){p.Kill();throw new IOException("Consulta de roteamento expirou.");}if(p.ExitCode!=0)throw new IOException("Falha HTTPS no processo de teste.");}return File.ReadAllText(result).Trim();}
        finally{if(File.Exists(result))File.Delete(result);}
    }
}

using System;using System.IO;using System.Text;using System.Diagnostics;using System.Threading;using System.Security.Cryptography;using System.Security.Principal;using System.Security.AccessControl;using System.IO.Pipes;using System.ServiceProcess;using System.Web.Script.Serialization;using System.Runtime.InteropServices;
public static class TunRuntime
{
    const string CoreHash="7BBEF1DEA9189EE12799AE834EA4B4658355DA25C47A21AD8804904C0CCD9410";
    static string wireguardKey="",wireguardPreshared="";static string log;static string user="",password="",discordUser="",discordPassword="";static readonly object logLock=new object();
    static void Log(string value)
    {if(value==null)return;if(wireguardKey.Length>0)value=value.Replace(wireguardKey,"[chave-wireguard]");if(wireguardPreshared.Length>0)value=value.Replace(wireguardPreshared,"[chave-compartilhada]");if(user.Length>0)value=value.Replace(user,"[usuario]");if(password.Length>0)value=value.Replace(password,"[senha]");if(discordUser.Length>0)value=value.Replace(discordUser,"[usuario-discord]");if(discordPassword.Length>0)value=value.Replace(discordPassword,"[senha-discord]");lock(logLock){try {if(File.Exists(log)&&new FileInfo(log).Length>262144)File.WriteAllText(log,"");File.AppendAllText(log,value+Environment.NewLine);}catch{}}}
    static void ProtectDirectory(string path)
    {
        if(Directory.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Pasta do motor não pode ser um link.");
        Directory.CreateDirectory(path);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
        foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        Directory.SetAccessControl(path,acl);
    }
    static string InstallCore()
    {
        if(!Environment.Is64BitOperatingSystem||String.Equals(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"),"ARM64",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Este pacote experimental requer Windows x64.");
        string parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PKAproxyV2");ProtectDirectory(parent);
        string root=Path.Combine(parent,"Tun");ProtectDirectory(root);
        string exe=Path.Combine(root,"sing-box.exe");
        using(var resource=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PKA.core.gz"))
        using(var decoded=new System.IO.Compression.GZipStream(resource,System.IO.Compression.CompressionMode.Decompress))using(var target=File.Create(exe))decoded.CopyTo(target);
        using(var sha=SHA256.Create())using(var f=File.OpenRead(exe))if(BitConverter.ToString(sha.ComputeHash(f)).Replace("-","")!=CoreHash)throw new IOException("Integridade do motor inválida.");
        return exe;
    }
    static bool OwnerAlive(EngineRequest request)
    {try{using(var p=Process.GetProcessById(request.OwnerPid))return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==request.OwnerStart;}catch{return false;}}
    static void StopLegacy()
    {try{using(var s=new ServiceController("ProxiFyreService")){if(s.Status!=ServiceControllerStatus.Stopped){s.Stop();s.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));}}}catch(InvalidOperationException){} }
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool AttachConsole(uint pid);
    [DllImport("kernel32.dll")]static extern bool FreeConsole();
    [DllImport("kernel32.dll")]static extern bool SetConsoleCtrlHandler(IntPtr handler,bool add);
    [DllImport("kernel32.dll")]static extern bool GenerateConsoleCtrlEvent(uint code,uint group);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool AllocConsole();
    [DllImport("kernel32.dll")]static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr window,int command);
    static void StartHiddenCore(Process core)
    {
        // Keep a real console for graceful CTRL+C shutdown, but do not display it.
        // WindowStyle alone is not sufficient for console children launched without a shell.
        if(!AllocConsole())throw new IOException("Não foi possível preparar o motor em segundo plano.");
        try
        {
            IntPtr window=GetConsoleWindow();
            if(window==IntPtr.Zero)throw new IOException("Console do motor indisponível.");
            ShowWindow(window,0);
            core.Start();
        }
        finally {FreeConsole();}
    }
    static void StopCore(Process core)
    {
        if(core==null||core.HasExited)return;
        if(AttachConsole((uint)core.Id))
        {try{SetConsoleCtrlHandler(IntPtr.Zero,true);GenerateConsoleCtrlEvent(0,0);core.WaitForExit(8000);}finally{FreeConsole();SetConsoleCtrlHandler(IntPtr.Zero,false);}}
        if(!core.HasExited){Log("Encerramento forçado do motor; confira a rede antes de reconectar.");core.Kill();core.WaitForExit(5000);}
    }
    public static int Run(string[] args)
    {
        if(args.Length>0&&args[0]=="--guard-release"){using(var gate=new Mutex(false,@"Global\PKAproxy-TUN")){bool locked=false;try{try{locked=gate.WaitOne(0);}catch(AbandonedMutexException){locked=true;}if(!locked)return 1;NetworkGuard.Release();return 0;}catch{return 1;}finally{if(locked)gate.ReleaseMutex();}}}
        if(args.Length>0&&args[0]=="--engine-stop"){try{EngineController.Command("STOP",3000);return 0;}catch{return 1;}}
        Process core=null;string config=null,requestPath=null;Mutex singleton=null;bool owns=false;TunJob job=null;
        Directory.CreateDirectory(AppPaths.Root);log=Path.Combine(AppPaths.Root,"tun-diagnostic.log");
        try
        {
            if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("Permita o aviso de administrador do Windows.");
            if(args.Length!=2)throw new ArgumentException("Pedido inválido.");
            requestPath=Path.GetFullPath(args[1]);string prefix=Path.GetFullPath(AppPaths.Root)+Path.DirectorySeparatorChar;
            if(!requestPath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(requestPath).StartsWith("engine-")||new FileInfo(requestPath).Length>65536)throw new ArgumentException("Pedido de conexão inválido.");
            var request=new JavaScriptSerializer().Deserialize<EngineRequest>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(requestPath),null,DataProtectionScope.CurrentUser)));
            var wg=request.DiscordOverlay==null?request.WireGuard:request.DiscordOverlay.WireGuard;wireguardKey=wg==null?"":wg.PrivateKey;wireguardPreshared=wg==null?"":wg.PresharedKey??"";
            user=request.User??"";password=request.Password;discordUser=request.DiscordOverlay==null?"":request.DiscordOverlay.User;discordPassword=request.DiscordOverlay==null?"":request.DiscordOverlay.Password;
            singleton=new Mutex(false,@"Global\PKAproxy-TUN");try{owns=singleton.WaitOne(0);}catch(AbandonedMutexException){owns=true;}
            if(!owns)throw new InvalidOperationException("Já existe um motor Global ativo. Desconecte a outra instância.");
            if(NetworkGuard.ArmedState==null||(NetworkGuard.IsArmed&&!(request.PermanentDiscord&&DiscordProxy.OwnsGuard)))throw new InvalidOperationException("Proteção anterior instalada ou estado indisponível. Libere explicitamente a rede antes de ativar outra sessão.");
            string json=TunConfiguration.Build(request);
            if(!OwnerAlive(request))throw new IOException("A janela que solicitou conexão foi encerrada.");
            if(request.WireGuard==null)ProxyDiagnostics.ProxyIp(request.Host,request.Port,request.User,request.Password,request.Protocol,request.ProxyTls,request.TlsName);
            if(request.WireGuard==null&&request.Route.Udp)ProxyDiagnostics.CheckUdp(request.Host,request.Port,request.User,request.Password,request.Protocol);
            string exe=InstallCore();config=Path.Combine(Path.GetDirectoryName(exe),"active.json");File.WriteAllText(config,json,new UTF8Encoding(false));
            using(var check=Process.Start(new ProcessStartInfo(exe,"check -c \""+config+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true}))
            {string errors=check.StandardError.ReadToEnd();if(!check.WaitForExit(15000)||check.ExitCode!=0){Log(errors);throw new IOException("O motor rejeitou a configuração. Abra Diagnóstico.");}}
            if(!File.Exists(requestPath)||!OwnerAlive(request))throw new IOException("Conexão cancelada.");
            string probe=Path.Combine(AppPaths.Root,"probe","PKArouteProbe.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(probe));
            using(var resource=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PKArouteProbe.exe"))using(var target=File.Create(probe))resource.CopyTo(target);
            if(wg!=null)File.Copy(probe,Path.Combine(AppPaths.Root,"probe","DiscordrouteProbe.exe"),true);
            if(request.PermanentDiscord)DiscordProxy.MarkGuard();
            NetworkGuard.Apply(request,exe,probe,false);
            core=new Process {StartInfo=new ProcessStartInfo(exe,"run -c \""+config+"\""){UseShellExecute=false,CreateNoWindow=false,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true},EnableRaisingEvents=true};
            job=new TunJob();core.ErrorDataReceived+=(s,e)=>Log(e.Data);core.OutputDataReceived+=(s,e)=>Log(e.Data);StartHiddenCore(core);job.Assign(core);core.BeginErrorReadLine();core.BeginOutputReadLine();
            if(core.WaitForExit(3500))throw new IOException("Motor não iniciou. Abra Diagnóstico para ver o motivo.");
            NetworkGuard.Apply(request,exe,probe,true);
            // Probe is included in both the routing policy and persistent guard.
            bool isDiscordOnly = (request.PermanentDiscord || request.WireGuard != null) && request.DiscordOverlay == null;
            EngineController.RoutedIp(true, isDiscordOnly || wg != null);
            var access=new PipeSecurity();access.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
            bool first=true,stop=false;DateTime lastDiscordScan=DateTime.UtcNow;
            while(!core.HasExited&&OwnerAlive(request)&&!stop&&!(request.PermanentDiscord&&(DiscordProxy.StopRequested||DiscordProxy.DashboardRevision!=request.DashboardStamp)))
            {
                using(var pipe=new NamedPipeServerStream(EngineController.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,access))
                {
                    var pending=pipe.BeginWaitForConnection(null,null);
                    if(first){if(!File.Exists(requestPath))throw new IOException("Conexão cancelada.");File.WriteAllText(requestPath+".ready","OK");first=false;}
                    while(!pending.AsyncWaitHandle.WaitOne(300)){if(request.DiscordOverlay!=null&&!DiscordProxy.DashboardOwnerAlive(request))DiscordProxy.ClearDashboard();if(core.HasExited||!OwnerAlive(request)||(request.PermanentDiscord&&(DiscordProxy.StopRequested||DiscordProxy.DashboardRevision!=request.DashboardStamp))){stop=true;break;}if(request.PermanentDiscord&&(DateTime.UtcNow-lastDiscordScan).TotalSeconds>=15){lastDiscordScan=DateTime.UtcNow;bool changed=false;foreach(string app in DiscordProxy.InstalledDiscordPaths(request.DiscordOverlay!=null?request.DiscordOverlay.DiscordPath:request.Route.Apps[0].Path))if(!request.Route.Apps.Exists(a=>String.Equals(a.Path,app,StringComparison.OrdinalIgnoreCase))){request.Route.Apps.Add(new RegisteredApp{Path=app});changed=true;}if(changed)NetworkGuard.Apply(request,exe,probe,true);}}
                    if(stop)break;pipe.EndWaitForConnection(pending);
                    using(var reader=new StreamReader(pipe,Encoding.UTF8,false,1024,true))using(var writer=new StreamWriter(pipe,Encoding.UTF8,1024,true))
                    {
                        writer.AutoFlush=true;var read=reader.ReadLineAsync();if(!read.Wait(2000))continue;
                        if(read.Result=="STOP"){StopCore(core);writer.WriteLine("STOPPED");stop=true;}
                        else if(read.Result=="MODE")writer.WriteLine(request.DiscordOverlay!=null?"COMBINED":(request.PermanentDiscord||request.WireGuard!=null)?"DISCORD":"NORMAL");
                        else if(read.Result=="DISCORDIP"){try{writer.WriteLine(EngineController.RoutedIp(true,wg!=null));}catch{writer.WriteLine("FAILED");}}
                        else if(read.Result=="IP"){try{writer.WriteLine(EngineController.RoutedIp(true));}catch{writer.WriteLine("FAILED");}}
                        else writer.WriteLine(read.Result=="STATUS"&&!core.HasExited?"RUNNING":"UNKNOWN");
                    }
                }
            }
            return 0;
        }
        catch(Exception e){
            Log(e.Message);
            if(requestPath!=null&&File.Exists(requestPath))try{File.WriteAllText(requestPath+".ready","Falha ao ativar TUN. Abra Diagnóstico: "+e.GetType().Name);}catch{}
            try{NetworkGuard.Release();}catch{}
            return 1;
        }
        finally
        {try{StopCore(core);}catch(Exception e){Log("Falha ao encerrar: "+e.GetType().Name);}if(job!=null)job.Dispose();if(core!=null)core.Dispose();if(config!=null)try{File.Delete(config);}catch{}if(owns)singleton.ReleaseMutex();if(singleton!=null)singleton.Dispose();}
    }
}

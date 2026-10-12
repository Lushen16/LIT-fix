using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
// Persistent WFP filters: no dynamic session, no automatic removal on engine exit.
// Only explicit recovery removes these filters. All mutations are transactional.
public static class NetworkGuard
{
    static Guid SubKey=new Guid("ea96e0ec-4111-4597-bf7c-033d1979a101");
    static Guid V4=new Guid("c38d57d1-05a7-4c33-904f-7fbceee60e82"),V6=new Guid("4a72393b-319f-44bc-84c3-ba54dcb3b6b4");
    static Guid AppId=new Guid("d78e1e87-8644-4ea5-9437-d809ecefc971"),InterfaceId=new Guid("4cd62a49-59c3-4969-b7f3-bda5d32890a4"),RemotePort=new Guid("c35a604d-d22b-4e1a-91b4-68f674ee674b");
    static string Manifest {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"PKAproxyV2","guard.keys");}}
    [StructLayout(LayoutKind.Sequential)]public struct Display { [MarshalAs(UnmanagedType.LPWStr)]public string Name;[MarshalAs(UnmanagedType.LPWStr)]public string Description; }
    [StructLayout(LayoutKind.Explicit,Size=16)]public struct Value { [FieldOffset(0)]public uint Type;[FieldOffset(8)]public IntPtr Pointer;[FieldOffset(8)]public byte Byte;[FieldOffset(8)]public ushort Short; }
    [StructLayout(LayoutKind.Sequential)]public struct Blob {public uint Size;public IntPtr Data;}
    [StructLayout(LayoutKind.Sequential)]public struct Condition {public Guid Key;public uint Match;public Value Value;}
    [StructLayout(LayoutKind.Sequential)]public struct Action {public uint Type;public Guid Key;}
    [StructLayout(LayoutKind.Sequential)]public struct Filter {public Guid Key;public Display Display;public uint Flags;public IntPtr Provider;public Blob Data;public Guid Layer,SubLayer;public Value Weight;public uint Count;public IntPtr Conditions;public Action Action;public Guid Context;public IntPtr Reserved;public ulong Id;public Value EffectiveWeight;}
    [StructLayout(LayoutKind.Sequential)]struct SubLayer {public Guid Key;public Display Display;public uint Flags;public IntPtr Provider;public Blob Data;public ushort Weight;}
    [DllImport("fwpuclnt.dll",CharSet=CharSet.Unicode)]static extern uint FwpmEngineOpen0(string server,uint auth,IntPtr identity,IntPtr session,out IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmEngineClose0(IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmTransactionBegin0(IntPtr engine,uint flags);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmTransactionCommit0(IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmTransactionAbort0(IntPtr engine);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmSubLayerAdd0(IntPtr engine,ref SubLayer layer,IntPtr sd);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterAdd0(IntPtr engine,ref Filter filter,IntPtr sd,out ulong id);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterDeleteByKey0(IntPtr engine,ref Guid key);
    [DllImport("fwpuclnt.dll")]static extern uint FwpmFilterGetByKey0(IntPtr engine,ref Guid key,out IntPtr filter);
    [DllImport("fwpuclnt.dll",CharSet=CharSet.Unicode)]static extern uint FwpmGetAppIdFromFileName0(string file,out IntPtr blob);
    [DllImport("fwpuclnt.dll")]static extern void FwpmFreeMemory0(ref IntPtr value);
    [DllImport("iphlpapi.dll",CharSet=CharSet.Unicode)]static extern uint ConvertInterfaceAliasToLuid(string alias,out ulong luid);
    static void Check(uint code){if(code!=0)throw new InvalidOperationException("Proteção WFP falhou (0x"+code.ToString("X8")+"). O aplicativo não será aberto.");}
    public static Guid Key(string value){using(var sha=SHA256.Create()){byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes("PKA-V2:"+value));byte[] b=new byte[16];Array.Copy(hash,b,16);return new Guid(b);}}
    // Query the actual BFE filter; a profile marker is never proof of protection.
    // Unknown state is treated as protected, requiring explicit recovery.
    public static bool IsArmed {get{return ArmedState!=false;}}
    public static bool? ArmedState {get{
        IntPtr engine;uint code=FwpmEngineOpen0(null,10,IntPtr.Zero,IntPtr.Zero,out engine);if(code!=0)return null;
        IntPtr filter=IntPtr.Zero;try{Guid key=Key("dns:"+V4);code=FwpmFilterGetByKey0(engine,ref key,out filter);return code==0?(bool?)true:code==0x80320003?(bool?)false:null;}
        finally{if(filter!=IntPtr.Zero)FwpmFreeMemory0(ref filter);FwpmEngineClose0(engine);}
    }}
    static void Delete(IntPtr engine,Guid key){uint code=FwpmFilterDeleteByKey0(engine,ref key);if(code!=0&&code!=0x80320003)Check(code);}
    static List<Guid> Previous(){var keys=new List<Guid>();if(File.Exists(Manifest))foreach(string line in File.ReadAllLines(Manifest)){Guid key;if(Guid.TryParse(line,out key))keys.Add(key);}return keys;}
    static void Add(IntPtr engine,Guid key,Guid layer,string app,bool permit,ulong? luid,bool dns=false)
    {
        IntPtr blob=IntPtr.Zero,luidPtr=IntPtr.Zero,conditions=IntPtr.Zero;
        try {
            var list=new List<Condition>();
            if(app!=null){Check(FwpmGetAppIdFromFileName0(app,out blob));list.Add(new Condition{Key=AppId,Match=0,Value=new Value{Type=12,Pointer=blob}});}
            if(luid.HasValue){luidPtr=Marshal.AllocHGlobal(8);Marshal.WriteInt64(luidPtr,unchecked((long)luid.Value));list.Add(new Condition{Key=InterfaceId,Match=10,Value=new Value{Type=4,Pointer=luidPtr}});}
            if(dns)list.Add(new Condition{Key=RemotePort,Match=0,Value=new Value{Type=2,Short=53}});
            int size=Marshal.SizeOf(typeof(Condition));if(list.Count>0){conditions=Marshal.AllocHGlobal(size*list.Count);for(int i=0;i<list.Count;i++)Marshal.StructureToPtr(list[i],IntPtr.Add(conditions,size*i),false);}
            var filter=new Filter{Key=key,Display=new Display{Name="PKA V2 - "+(permit?"motor/diagnóstico":"bloqueio de saída direta")},Flags=1,Layer=layer,SubLayer=SubKey,Weight=new Value{Type=1,Byte=(byte)(permit?15:1)},Count=(uint)list.Count,Conditions=conditions,Action=new Action{Type=permit?0x1002u:0x1001u}};
            ulong id;Check(FwpmFilterAdd0(engine,ref filter,IntPtr.Zero,out id));
        }finally{if(blob!=IntPtr.Zero)FwpmFreeMemory0(ref blob);if(luidPtr!=IntPtr.Zero)Marshal.FreeHGlobal(luidPtr);if(conditions!=IntPtr.Zero)Marshal.FreeHGlobal(conditions);}
    }
    public static void Apply(EngineRequest request,string core,string probe,bool allowTunnel)
    {
        if(IntPtr.Size!=8)throw new InvalidOperationException("A proteção exige processo x64.");
        ulong luid=0;if(allowTunnel)Check(ConvertInterfaceAliasToLuid("PKAproxy-TUN",out luid));
        var targets=new List<string>(request.Route.SelectedPaths());if(!targets.Contains(probe))targets.Add(probe);
        if(request.WireGuard!=null||(request.DiscordOverlay!=null&&request.DiscordOverlay.WireGuard!=null))targets.Add(Path.Combine(AppPaths.Root,"probe","DiscordrouteProbe.exe"));
        var permits=new[]{core,request.OwnerPath};
        foreach(string path in targets)foreach(string permitted in permits)if(String.Equals(path,permitted,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("O motor e a interface não podem ser apps protegidos.");
        IntPtr engine;Check(FwpmEngineOpen0(null,10,IntPtr.Zero,IntPtr.Zero,out engine));
        bool tx=false;try {
            Check(FwpmTransactionBegin0(engine,0));tx=true;
            var sub=new SubLayer{Key=SubKey,Display=new Display{Name="PKA Proxy Launcher V2 - proteção persistente"},Flags=1,Weight=65500};uint result=FwpmSubLayerAdd0(engine,ref sub,IntPtr.Zero);if(result!=0&&result!=0x80320009)Check(result);
            var previous=Previous();foreach(Guid key in previous)Delete(engine,key);
            var keys=new List<Guid>();
            foreach(Guid layer in new[]{V4,V6}){
                foreach(string path in permits){Guid key=Key("permit:"+layer+":"+path.ToLowerInvariant());keys.Add(key);Add(engine,key,layer,path,true,null);}
                if(request.Route.Global){
                    Guid dnsKey=Key("dns:"+layer);keys.Add(dnsKey);Add(engine,dnsKey,layer,null,false,layer==V4&&allowTunnel?(ulong?)luid:null,true);
                    Guid key=Key("global:"+layer);keys.Add(key);Add(engine,key,layer,null,false,layer==V4&&allowTunnel?(ulong?)luid:null);
                } else {
                    for(int i=0;i<targets.Count;i++){
                        string path=targets[i];
                        Guid dnsKey=i==0?Key("dns:"+layer):Key("dns:"+layer+":"+path.ToLowerInvariant());
                        keys.Add(dnsKey);
                        Add(engine,dnsKey,layer,path,false,layer==V4&&allowTunnel?(ulong?)luid:null,true);
                        Guid key=Key("app:"+layer+":"+path.ToLowerInvariant());
                        keys.Add(key);
                        Add(engine,key,layer,path,false,layer==V4&&allowTunnel?(ulong?)luid:null);
                    }
                }
            }
            // Journal includes old and new IDs before commit, allowing recovery after a crash.
            foreach(Guid key in keys)if(!previous.Contains(key))previous.Add(key);File.WriteAllLines(Manifest,previous.ConvertAll(k=>k.ToString()).ToArray());
            Check(FwpmTransactionCommit0(engine));tx=false;
            Directory.CreateDirectory(AppPaths.Root);File.WriteAllText(Path.Combine(AppPaths.Root,"guard-active"),request.Route.Global?"Global":"Aplicativos");
        }finally{if(tx)FwpmTransactionAbort0(engine);FwpmEngineClose0(engine);}
    }
    public static void Release()
    {
        IntPtr engine;Check(FwpmEngineOpen0(null,10,IntPtr.Zero,IntPtr.Zero,out engine));bool tx=false;
        try{Check(FwpmTransactionBegin0(engine,0));tx=true;foreach(Guid key in Previous())Delete(engine,key);Check(FwpmTransactionCommit0(engine));tx=false;if(File.Exists(Manifest))File.Delete(Manifest);string marker=Path.Combine(AppPaths.Root,"guard-active");if(File.Exists(marker))File.Delete(marker);}
        finally{if(tx)FwpmTransactionAbort0(engine);FwpmEngineClose0(engine);}
    }
}

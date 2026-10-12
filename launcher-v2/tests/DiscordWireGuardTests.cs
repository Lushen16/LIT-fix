using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Drawing;
class DiscordWireGuardTests
{
 static int assertions;
 static void Check(bool value,string reason){assertions++;if(!value)throw new Exception(reason);}
 static void Reject(Action action){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid configuration accepted");}
 static Dictionary<string,object> Parse(string json){return (Dictionary<string,object>)new JavaScriptSerializer().DeserializeObject(json);}
 [STAThread]static void Main(string[] args)
 {
  AppPaths.ProtectedRoot=Path.Combine(args[0],"profile");Directory.CreateDirectory(AppPaths.Root);string discord=Path.Combine(AppPaths.Root,"Discord.exe"),game=Path.Combine(AppPaths.Root,"Game.exe");File.WriteAllText(discord,"fixture only");File.WriteAllText(game,"fixture only");
  byte[] keyBytes=new byte[32];for(int i=0;i<32;i++)keyBytes[i]=(byte)(i+1);string key=Convert.ToBase64String(keyBytes);
  string text="# Proton-style fixture, not a working account\n[Interface]\nPrivateKey = "+key+"\nAddress = 10.2.0.2/32\nDNS = 10.2.0.1\n[Peer]\nPublicKey = "+key+"\nAllowedIPs = 0.0.0.0/0\nEndpoint = 192.0.2.40:51820\n";
  var wg=WireGuardProfile.Parse(text);Check(wg.Endpoint=="192.0.2.40"&&wg.Port==51820,"Endpoint parsing");Check(wg.Addresses[0]=="10.2.0.2/32","Address parsing");Check(wg.Keepalive==25,"Default keepalive");
  Reject(()=>WireGuardProfile.Parse(text+"PostUp = cmd.exe\n"));Reject(()=>WireGuardProfile.Parse(text.Replace("192.0.2.40","hostname.example")));Reject(()=>WireGuardProfile.Parse(text.Replace("10.2.0.2/32","not-an-ip")));Reject(()=>WireGuardProfile.Parse(text.Replace("0.0.0.0/0","192.0.2.0/24")));Reject(()=>WireGuardProfile.Parse(text+"Endpoint = 192.0.2.2:80\n"));Reject(()=>WireGuardProfile.Parse(text+"[Peer]\nPublicKey = "+key));Reject(()=>WireGuardProfile.Parse(text.Replace(key,"bad-key")));
  var profile=new DiscordProfile{DiscordPath=discord,WireGuard=wg};DiscordProxy.Validate(profile);byte[] protectedProfile=DiscordProxy.Encode(profile);Check(!Encoding.UTF8.GetString(protectedProfile).Contains(key),"WireGuard keys stored in clear text");Check(DiscordProxy.Decode(protectedProfile).WireGuard.PrivateKey==key,"WireGuard DPAPI roundtrip");
  var request=new EngineRequest{PermanentDiscord=true,WireGuard=wg,Host="",User="",Password="",Route=new RoutingOptions(),OwnerPath=Path.Combine(AppPaths.Root,"owner.exe")};request.Route.Apps.Add(new RegisteredApp{Path=discord});
  string json=TunConfiguration.Build(request);var config=Parse(json);Check(((object[])config["endpoints"]).Length==1,"Missing WireGuard endpoint");Check(((object[])config["outbounds"]).Length==1,"Legacy SOCKS outbound retained");var routing=(Dictionary<string,object>)config["route"];Check((string)routing["final"]=="direct","Other apps routed through VPN");var rules=(object[])routing["rules"];Check((string)((Dictionary<string,object>)rules[3])["outbound"]=="proxy","Discord TCP bypasses VPN");Check((string)((Dictionary<string,object>)rules[4])["outbound"]=="proxy","Discord UDP bypasses VPN");Check((string)((Dictionary<string,object>)rules[5])["action"]=="reject","Unsupported Discord traffic not rejected");Check(!json.Contains("\"type\":\"socks\""),"SOCKS probe wrongly retained");Check((string)((Dictionary<string,object>)config["dns"])["final"]=="direct-dns","Unrelated DNS through VPN");Check(request.WireGuard!=null&&request.Host=="","Request mutated");File.WriteAllText(Path.Combine(args[0],"discord-wireguard.json"),json);
  request.Route.Global=true;Reject(()=>TunConfiguration.Build(request));request.Route.Global=false;request.PermanentDiscord=false;Check(TunConfiguration.Build(request).Length>0,"Non-permanent WireGuard accepted");request.PermanentDiscord=true;request.Route.Apps.Add(new RegisteredApp{Path=game});Reject(()=>TunConfiguration.Build(request));
  request.WireGuard=null;request.DiscordOverlay=profile;request.Host="192.0.2.20";request.Port=1080;request.Protocol="HTTPS";
  foreach(bool global in new[]{false,true}){request.Route.Global=global;json=TunConfiguration.Build(request);config=Parse(json);rules=(object[])((Dictionary<string,object>)config["route"])["rules"];Check((string)((Dictionary<string,object>)rules[3])["outbound"]=="discord-proxy","Combined TCP precedence");Check((string)((Dictionary<string,object>)rules[4])["outbound"]=="discord-proxy","Combined UDP precedence");Check(((object[])config["outbounds"]).Length==2,"General proxy lost");Check(profile.WireGuard!=null,"Saved profile mutated");File.WriteAllText(Path.Combine(args[0],global?"combined-wireguard-global.json":"combined-wireguard-apps.json"),json);}
  DiscordProxy.Save(profile);Application.EnableVisualStyles();using(var form=new MainForm()){form.PreviewPage("discord");form.Show();form.Update();using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));image.Save(Path.Combine(args[0],"discord-wireguard-preview.png"));}}
  File.Delete(discord);File.Delete(game);Console.WriteLine("PASS: "+assertions+" Proton/WireGuard parsing, encrypted storage, TCP/UDP routing, direct other apps, combined routes and UI checks. No live VPN or WFP policy activated.");
 }
}

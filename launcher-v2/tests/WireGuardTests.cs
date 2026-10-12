using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
class WireGuardTests
{
 static int count;
 static void Check(bool condition){count++;if(!condition)throw new Exception("Assertion "+count);}
 static void Reject(string text){bool rejected=false;try{WireGuardIntegration.Validate(text);}catch(InvalidOperationException){rejected=true;}Check(rejected);}
 [STAThread]static void Main(string[] args)
 {
  string key=Convert.ToBase64String(new byte[32]);string config="[Interface]\nPrivateKey = "+key+"\nAddress = 10.60.0.2/32\nDNS = 1.1.1.1\n[Peer]\nPublicKey = "+key+"\nEndpoint = 192.0.2.1:51820\nAllowedIPs = 0.0.0.0/0, ::/0\nPersistentKeepalive = 25\n";
  Check(WireGuardIntegration.Validate(config).Contains("::/0"));
  Reject(config.Replace(", ::/0",""));Reject(config.Replace("0.0.0.0/0, ",""));Reject(config+"PostUp = cmd.exe\n");Reject(config+"[Peer]\nPublicKey = "+key);Reject(config.Replace(key,"bad-key"));Reject(config.Replace("Endpoint = 192.0.2.1:51820", ""));Reject(config+"AllowedIPs = 0.0.0.0/0, ::/0\n");Reject("");Reject(new string('x',32769));
  Check(WireGuardIntegration.Name.Length<32);
  if(!WireGuardIntegration.HasService){AppPaths.ProtectedRoot=Path.Combine(args[0],"isolated-profile");Directory.CreateDirectory(AppPaths.Root);string input=Path.Combine(args[0],"sample.conf");File.WriteAllText(input,config);WireGuardIntegration.Import(input);Check(File.Exists(WireGuardIntegration.Config));Check(!File.ReadAllText(WireGuardIntegration.Config).Contains("PostUp"));Check(Directory.GetAccessControl(WireGuardIntegration.Folder).AreAccessRulesProtected);File.Delete(WireGuardIntegration.Config);File.Delete(input);AppPaths.ProtectedRoot=null;}
  Application.EnableVisualStyles();using(var form=new WireGuardDialog()){form.Show();form.Update();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[0],"wireguard-preview.png"));}}
  Console.WriteLine("PASS: "+count+" WireGuard checks and UI rendering. No VPN activated; no remote server tested.");
 }
}

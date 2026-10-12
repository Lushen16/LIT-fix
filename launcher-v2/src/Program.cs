using System;using System.Drawing;using System.Net;using System.Windows.Forms;
static class Program
{
 [System.Runtime.InteropServices.DllImport("shell32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
 static extern int SetCurrentProcessExplicitAppUserModelID(string id);
 [STAThread]static int Main(string[] args)
 {
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  if(args.Length>0&&args[0]=="--wireguard-remove"){try{WireGuardIntegration.Change(false);if(System.IO.File.Exists(WireGuardIntegration.Config))System.IO.File.Delete(WireGuardIntegration.Config);return 0;}catch{return 1;}}
  if(args.Length>0&&args[0]=="--discord-enable")return DiscordProxy.Enable();
  if(args.Length>0&&args[0]=="--discord-disable")return DiscordProxy.Disable();
  if(args.Length>0&&args[0]=="--discord-dashboard")return DiscordProxy.ApplyDashboard(args);
  if(args.Length>0&&args[0]=="--discord-background")return DiscordProxy.Background();
  if(args.Length>0&&args[0]=="--apply-update")return UpdateInstaller.Apply(args);
  if(args.Length>0&&(args[0]=="--engine-apply"||args[0]=="--engine-stop"||args[0]=="--guard-release"))return EngineController.Apply(args);
  SetCurrentProcessExplicitAppUserModelID("Lushen16.Litfix");
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>=2&&args[0]=="--preview"){
   using(var form=new MainForm()){if(args.Length>2)form.PreviewPage(args[2]);form.Show();form.Update();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[1]);}}return 0;
  }
  Application.Run(new MainForm());return 0;
 }
}

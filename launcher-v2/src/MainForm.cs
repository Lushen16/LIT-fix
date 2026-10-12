using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;
public class MainForm:Form
{
 readonly Color bg=Color.FromArgb(7,12,20),card=Color.FromArgb(16,27,43),blue=Color.FromArgb(37,119,255),light=Color.FromArgb(119,207,255),muted=Color.FromArgb(159,179,205);
 TextBox host=new TextBox(),user=new TextBox(),password=new TextBox(),logs=new TextBox(); NumericUpDown port=new NumericUpDown();
 CheckBox global=new ReadableCheckBox(),auto=new ReadableCheckBox();CheckedListBox apps=new CheckedListBox();ComboBox launch=new ComboBox();CheckBox socks=new ReadableCheckBox(),https=new ReadableCheckBox(),udp=new ReadableCheckBox(),proxyTls=new ReadableCheckBox();TextBox tlsName=new TextBox();ListView appResults=new ListView();string tunnelIp="",activeProtocol="";
 TextBox discordHost=new TextBox(),discordUser=new TextBox(),discordPassword=new TextBox(),discordPath=new TextBox();NumericUpDown discordPort=new NumericUpDown();Label discordState=new Label();Button discordEnable,discordRemove,discordTest,discordBrowse;bool permanentConfigured,discordRunning;
 ProxyPicker generalPicker,discordPicker;System.Collections.Generic.List<ProxyPreset> presets=new System.Collections.Generic.List<ProxyPreset>();ProxyPreset generalManual,discordManual;bool loadingPickers=true,generalWasAutomatic,discordWasAutomatic;
 ComboBox discordConnection=new ComboBox();Button discordImportWireGuard;Label discordWireGuardInfo=new Label(),discordWireGuardHelp=new Label();Panel discordSettingsPanel;WireGuardProfile discordWireGuard;
 Panel discordPage=new Panel(),dashboard=new Panel(),applicationPage=new Panel(),testPage=new Panel(),content=new Panel();
 Label state=new Label(),testState=new Label(),exitIp=new Label(),updateState=new Label();Button connect,stop,release,test,updateButton;PendingUpdate pendingUpdate;bool checkingUpdate;DateTime lastUpdateCheck=DateTime.MinValue;
 RoutingOptions route=new RoutingOptions();bool busy,active,preview,healthy=true,checkingHealth,refreshingApps;int sessionVersion;DateTime lastHealth=DateTime.UtcNow;Timer monitor=new Timer();
 public MainForm()
 {
  preview=Array.IndexOf(Environment.GetCommandLineArgs(),"--preview")>=0;
  Text="Litfix "+UpdateService.Current;ClientSize=new Size(1080,760);MinimumSize=new Size(1096,799);StartPosition=FormStartPosition.CenterScreen;
  Icon=Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);BackColor=bg;ForeColor=Color.White;Font=new Font(UiFont(),10);AutoScaleMode=AutoScaleMode.Dpi;
  var sidebar=new Panel{Dock=DockStyle.Left,Width=205,BackColor=card};Controls.Add(sidebar);
  sidebar.Controls.Add(Label("Litfix",24,28,155,48,30,light));sidebar.Controls.Add(Label("PROXY LAUNCHER",25,82,165,25,10,Color.White));
  string[] names={"Proxy geral","Discord","Aplicativos","Teste"};Panel[] pages={dashboard,discordPage,applicationPage,testPage};
  for(int i=0;i<4;i++){Panel page=pages[i];var b=Button(names[i],20,153+i*56,165,false);b.Click+=(s,e)=>ShowPage(page);sidebar.Controls.Add(b);}
  var wireguard=Button("VPN geral",20,377,165,false);wireguard.BackColor=Color.FromArgb(127,65,211);wireguard.Click+=(s,e)=>{if(busy)return;using(var dialog=new WireGuardDialog())dialog.ShowDialog(this);RefreshState();};sidebar.Controls.Add(wireguard);
  updateState=Label("Atualização automática\nLushen16/LIT-fix",24,430,165,100,9,light);sidebar.Controls.Add(updateState);
  sidebar.Controls.Add(Label("V"+UpdateService.Current,24,544,87,30,9,muted));updateButton=Button("Atualizar",113,540,80,true);updateButton.Height=32;updateButton.Font=new Font(UiFont(),9,FontStyle.Bold);updateButton.BackColor=Color.FromArgb(127,65,211);updateButton.FlatAppearance.BorderColor=Color.FromArgb(168,108,244);updateButton.Visible=false;updateButton.Click+=async(s,e)=>await InstallPendingUpdate();sidebar.Controls.Add(updateButton);sidebar.Controls.Add(Label("WINDOWS x64\nSOCKS5 / HTTPS\n\nTCP / UDP via SOCKS5\nIPv6 bloqueado",24,583,165,110,9,muted));
  content.SetBounds(225,20,835,720);content.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(content);
  foreach(Panel page in pages){page.Dock=DockStyle.Fill;page.AutoScroll=true;page.BackColor=bg;content.Controls.Add(page);}
  BuildDashboard();BuildApps();BuildTests();BuildDiscord();ApplyReadableColors(this);ShowPage(dashboard);host.TextChanged+=(s,e)=>InvalidateResults();port.ValueChanged+=(s,e)=>InvalidateResults();user.TextChanged+=(s,e)=>InvalidateResults();password.TextChanged+=(s,e)=>InvalidateResults();
  if(!preview){var dp=DiscordProxy.Load();discordWireGuard=dp.WireGuard;discordConnection.SelectedIndex=dp.WireGuard==null?0:1;discordHost.Text=dp.Host;discordPort.Value=dp.Port>0&&dp.Port<=65535?dp.Port:1080;discordUser.Text=dp.User;discordPassword.Text=dp.Password;discordPath.Text=dp.DiscordPath;if(!File.Exists(discordPath.Text)){var found=AppDiscovery.Find(true);if(found.Length>0)discordPath.Text=found[0];}permanentConfigured=DiscordProxy.HasConfiguration;var saved=SessionStore.Load();if(saved!=null){proxyTls.Checked=saved.ProxyTls??true;tlsName.Text=saved.TlsName??"";socks.Checked=saved.Protocol!="HTTPS";https.Checked=saved.Protocol=="HTTPS"||saved.Protocol=="BOTH";host.Text=saved.Host??"";port.Value=saved.Port>0&&saved.Port<=65535?saved.Port:1080;user.Text=saved.User??"";password.Text=saved.Password();route=saved.Route??new RoutingOptions();route.Ipv6=false;udp.Checked=route.Udp||route.DirectDiscordCalls;if(route.Apps==null)route.Apps=new System.Collections.Generic.List<RegisteredApp>();if(route.Apps.Count==0&&!String.IsNullOrWhiteSpace(saved.Game)&&File.Exists(saved.Game))route.Apps.Add(new RegisteredApp{Path=saved.Game});}global.Checked=route.Global;auto.Checked=route.AutoLaunch;RefreshApps();}
  LoadPickers();
  monitor.Interval=4000;monitor.Tick+=async(s,e)=>{if(!busy){RefreshState();if(!checkingUpdate&&(DateTime.UtcNow-lastUpdateCheck).TotalMinutes>=15)await AutoUpdateAsync();if(active&&!checkingHealth&&(DateTime.UtcNow-lastHealth).TotalSeconds>=30)await CheckHealth();}};if(!preview)monitor.Start();
  FormClosing+=(s,e)=>{if(busy){e.Cancel=true;return;}if(!preview)Save();};FormClosed+=(s,e)=>monitor.Dispose();RefreshState();
  if(!preview)Shown+=async(s,e)=>await AutoUpdateAsync();
 }
 async Task AutoUpdateAsync()
 {
  if(checkingUpdate)return;checkingUpdate=true;lastUpdateCheck=DateTime.UtcNow;
  if(Array.IndexOf(Environment.GetCommandLineArgs(),"--update-failed")>=0)updateState.Text="Atualização não concluída\nVersão anterior restaurada.";
  try{var update=await Task.Run(()=>UpdateService.Check("Lushen16/LIT-fix"));if(IsDisposed)return;ShowPendingUpdate(update);}
  catch(Exception){if(!IsDisposed)updateState.Text=pendingUpdate!=null?"Nova versão disponível\nV"+pendingUpdate.Manifest.version:"Atualização indisponível\nTente na próxima abertura.";}
  finally{checkingUpdate=false;}
 }
 void ShowPendingUpdate(PendingUpdate update)
 {
  pendingUpdate=update;updateButton.Visible=update!=null;updateButton.Enabled=update!=null&&!busy&&!active&&!permanentConfigured;
  updateState.Text=update==null?"Versão atualizada\nV"+UpdateService.Current:"Nova versão disponível\nV"+update.Manifest.version;
 }
 async Task InstallPendingUpdate()
 {
  if(busy||pendingUpdate==null)return;
  if(active||permanentConfigured||EngineController.Status().StartsWith("Motor ativo")){updateState.Text="Pare o motor antes\nde atualizar.";return;}
  SetBusy(true);var update=pendingUpdate;
  try{updateState.Text="Baixando V"+update.Manifest.version+"…";await Task.Run(()=>UpdateService.Fetch(update));if(EngineController.Status().StartsWith("Motor ativo")||DiscordProxy.HasConfiguration)throw new InvalidOperationException("Pare o motor antes de atualizar.");updateState.Text="Instalando atualização…";Save();await Task.Run(()=>UpdateInstaller.Start(update));SetBusy(false);Application.Exit();}
  catch(Exception ex){updateState.Text="Atualização não concluída\nClique para tentar de novo.";Error(ex);}
  finally{if(!IsDisposed)SetBusy(false);}
 }
 void BuildDashboard()
 {
  dashboard.Controls.Add(Label("Sua conexão, sob controle.",0,4,810,48,24,Color.White));dashboard.Controls.Add(Label("Escolha o protocolo e ative a proteção antes de abrir seus aplicativos.",0,59,810,28,10,muted));
  state=Label("Desconectado",20,18,770,38,14,light);dashboard.Controls.Add(Card(0,100,815,70,state));
  var settings=new Panel{Left=0,Top=190,Width=815,Height=263,BackColor=card};dashboard.Controls.Add(settings);
  settings.Controls.Add(Label("Protocolo",20,15,105,25,10,light));socks.Text="SOCKS5";socks.SetBounds(130,12,110,32);socks.Checked=true;https.Text="HTTPS";https.SetBounds(245,12,100,32);settings.Controls.Add(socks);settings.Controls.Add(https);socks.CheckedChanged+=(s,e)=>InvalidateResults();https.CheckedChanged+=(s,e)=>InvalidateResults();udp.Text="Chamadas / UDP (SOCKS5)";udp.SetBounds(350,12,440,32);udp.CheckedChanged+=(s,e)=>{route.Udp=udp.Checked;InvalidateResults();};settings.Controls.Add(udp);Field(settings,"IPv4 do proxy",host,20,58,550);Field(settings,"Usuário",user,20,142,350);Field(settings,"Senha",password,400,142,390);password.UseSystemPasswordChar=true;proxyTls.Text="TLS até a proxy HTTPS";proxyTls.SetBounds(20,216,265,32);proxyTls.Checked=true;proxyTls.CheckedChanged+=(s,e)=>InvalidateResults();settings.Controls.Add(proxyTls);settings.Controls.Add(Label("Nome TLS / certificado (opcional)",300,202,485,22,9,muted));tlsName.SetBounds(300,227,485,28);Style(tlsName);tlsName.TextChanged+=(s,e)=>InvalidateResults();settings.Controls.Add(tlsName);
  settings.Controls.Add(Label("Porta",600,58,185,24,10,muted));port.SetBounds(600,87,185,32);port.Minimum=1;port.Maximum=65535;port.Value=1080;Style(port);settings.Controls.Add(port);
  global.Text="Modo global — todos os aplicativos";global.SetBounds(0,474,815,28);global.ForeColor=light;global.CheckedChanged+=(s,e)=>{route.Global=global.Checked;udp.Text=global.Checked?"Chamadas Discord":"Chamadas / UDP (SOCKS5)";InvalidateResults();};dashboard.Controls.Add(global);
  auto.Text="Abrir o aplicativo escolhido após ativar e verificar o túnel";auto.SetBounds(0,511,815,28);auto.CheckedChanged+=(s,e)=>route.AutoLaunch=auto.Checked;dashboard.Controls.Add(auto);
  launch.SetBounds(0,550,815,32);launch.DropDownStyle=ComboBoxStyle.DropDownList;launch.FlatStyle=FlatStyle.Flat;Style(launch);launch.ForeColor=light;dashboard.Controls.Add(launch);
  connect=Button("Ativar proxy",0,602,252,true);connect.Click+=async(s,e)=>await ActivateProxy();dashboard.Controls.Add(connect);
  stop=Button("Parar túnel",277,602,252,false);stop.Click+=async(s,e)=>await Stop();dashboard.Controls.Add(stop);
  release=Button("Liberar rede direta",554,602,261,false);release.Click+=async(s,e)=>await Release();dashboard.Controls.Add(release);
  dashboard.Controls.Add(Label("Parar ou fechar mantém o bloqueio de saída direta. Para retornar à rede normal,\npare o túnel e clique em Liberar rede direta. O modo global também afeta serviços do Windows.",0,660,815,50,9,muted));
  AddProxyPicker(settings,false);foreach(Control c in dashboard.Controls)if(c.Top>=474)c.Top+=60;
 }

 void BuildDiscord()
 {
  discordPage.Controls.Add(Label("Discord",0,4,810,48,24,Color.White));
  discordPage.Controls.Add(Label("Proxy ou Proton VPN somente no Discord, mesmo com o Litfix fechado.",0,59,810,30,10,muted));
  discordState=Label("Sem proxy permanente",20,18,770,38,14,light);discordPage.Controls.Add(Card(0,100,815,70,discordState));
  var settings=new Panel{Left=0,Top=190,Width=815,Height=330,BackColor=card};discordSettingsPanel=settings;discordPage.Controls.Add(settings);
  discordConnection.Items.AddRange(new object[]{"Proxy SOCKS5","WireGuard / Proton VPN"});discordConnection.DropDownStyle=ComboBoxStyle.DropDownList;discordConnection.SetBounds(20,12,250,30);Style(discordConnection);settings.Controls.Add(discordConnection);
  discordImportWireGuard=Button("Importar Proton .conf",290,10,220,false);discordImportWireGuard.Height=34;discordImportWireGuard.Click+=(sender,e)=>ImportDiscordWireGuard();settings.Controls.Add(discordImportWireGuard);
  discordWireGuardInfo=Label("Sem arquivo importado",530,14,260,28,9,light);settings.Controls.Add(discordWireGuardInfo);discordConnection.SelectedIndex=0;discordConnection.SelectedIndexChanged+=(sender,e)=>SetDiscordConnectionEditing();
  Field(settings,"IPv4 da proxy",discordHost,20,58,550);
  settings.Controls.Add(Label("Porta",600,58,185,24,10,muted));discordPort.SetBounds(600,87,185,32);discordPort.Minimum=1;discordPort.Maximum=65535;discordPort.Value=1080;Style(discordPort);settings.Controls.Add(discordPort);
  Field(settings,"Usuário",discordUser,20,142,350);Field(settings,"Senha",discordPassword,400,142,385);discordPassword.UseSystemPasswordChar=true;
  Field(settings,"Discord.exe",discordPath,20,224,550);discordPath.ReadOnly=true;
  discordBrowse=Button("Localizar Discord",600,253,185,false);discordBrowse.Click+=(sender,e)=>{using(var picker=new OpenFileDialog{Filter="Discord (Discord.exe)|Discord.exe",CheckFileExists=true})if(picker.ShowDialog(this)==DialogResult.OK)discordPath.Text=picker.FileName;};settings.Controls.Add(discordBrowse);
  discordTest=Button("Testar proxy",0,546,250,false);discordTest.Click+=async(sender,e)=>await TestDiscord();discordPage.Controls.Add(discordTest);
  discordEnable=Button("Ativar VPN Discord",277,546,250,true);discordEnable.Click+=async(sender,e)=>await EnableDiscord();discordPage.Controls.Add(discordEnable);
  discordRemove=Button("Desativar VPN Discord",554,546,261,false);discordRemove.BackColor=Color.FromArgb(183,35,55);discordRemove.ForeColor=Color.White;discordRemove.FlatAppearance.BorderColor=Color.FromArgb(245,82,104);discordRemove.Click+=async(sender,e)=>await RemoveDiscord();discordPage.Controls.Add(discordRemove);
  discordPage.Controls.Add(Label("Inicia ao entrar no Windows e reconecta em segundo plano. O Discord pode estar aberto.\nA configuração permanece até removê-la\nou desinstalar o Litfix. Proxy geral e Discord podem permanecer ativos juntos.",0,612,815,92,10,muted));
  AddProxyPicker(settings,true);discordWireGuardHelp=Label("Mensagens e chamadas passam pelo Proton VPN.\nOs demais aplicativos continuam na conexão direta.\n\nImporte o arquivo .conf e clique em Ativar permanente.\nSistema Liga/Desliga sob demanda. O Discord pode estar aberto ao ativar.",20,100,770,170,11,light);settings.Controls.Add(discordWireGuardHelp);discordWireGuardHelp.Visible=false;foreach(Control c in discordPage.Controls)if(c.Top>=546)c.Top+=60;
 }
 DiscordProfile DiscordSettings(){return new DiscordProfile{Host=discordHost.Text.Trim(),Port=(int)discordPort.Value,User=discordUser.Text,Password=discordPassword.Text,DiscordPath=discordPath.Text,WireGuard=discordConnection.SelectedIndex==1?discordWireGuard:null};}
 void ImportDiscordWireGuard()
 {
  if(busy||permanentConfigured)return;
  using(var picker=new OpenFileDialog{Filter="WireGuard / Proton (*.conf)|*.conf",CheckFileExists=true})if(picker.ShowDialog(this)==DialogResult.OK)try{
   if(new FileInfo(picker.FileName).Length>32768)throw new ArgumentException("Arquivo muito grande.");var imported=WireGuardProfile.Parse(File.ReadAllText(picker.FileName));var profile=DiscordSettings();profile.WireGuard=imported;DiscordProxy.Save(profile);discordWireGuard=imported;discordConnection.SelectedIndex=1;SetDiscordConnectionEditing();discordState.Text="Proton importado • clique em Ativar VPN Discord";
  }catch(Exception ex){Error(ex);}
 }
 void SetDiscordConnectionEditing()
 {
  bool wg=discordConnection.SelectedIndex==1,editable=!busy&&!permanentConfigured;discordConnection.Enabled=editable;discordImportWireGuard.Enabled=editable;discordImportWireGuard.Visible=discordWireGuardInfo.Visible=wg;
  discordWireGuardInfo.Text=discordWireGuard==null?"Sem arquivo importado":"Arquivo importado • só Discord";
  discordWireGuardHelp.Visible=wg;discordHost.Visible=discordUser.Visible=discordPassword.Visible=discordPort.Visible=!wg;if(discordSettingsPanel!=null)foreach(Control control in discordSettingsPanel.Controls)if(control is Label&&(control.Text=="IPv4 da proxy"||control.Text=="Porta"||control.Text=="Usuário"||control.Text=="Senha"))control.Visible=!wg;if(discordTest!=null)discordTest.Text=wg?"Testar VPN Discord":"Testar proxy";
  if(discordPicker!=null){discordPicker.Visible=!wg;discordPicker.SetAvailable(editable&&!wg);}discordHost.Enabled=discordUser.Enabled=discordPassword.Enabled=editable&&!wg;discordPort.Enabled=editable&&!wg&&(discordPicker==null||!discordPicker.Automatic.Checked);
 }
 async Task TestDiscord()
 {
  if(busy)return;SetBusy(true);try{var profile=DiscordSettings();if(discordConnection.SelectedIndex==1&&profile.WireGuard==null)throw new ArgumentException("Importe o arquivo .conf do Proton primeiro.");DiscordProxy.Validate(profile);discordState.Text="Testando autenticação…";
   if(profile.WireGuard!=null){if(!discordRunning){discordState.Text="Arquivo válido • ative permanente para testar o servidor";return;}string vpnIp=await Task.Run(()=>EngineController.RoutedIp(false,true));discordState.Text="VPN Discord OK • IP de saída: "+vpnIp;return;}
   string ip=await Task.Run(()=>{string result=ProxyDiagnostics.ProxyIp(profile.Host,profile.Port,profile.User,profile.Password);return result;});discordState.Text="Proxy OK • IP de saída: "+ip;
  }catch(Exception ex){discordState.Text="Proxy não confirmou conexão";Error(ex);}finally{SetBusy(false);}
 }
 async Task EnableDiscord()
 {
  if(busy)return;SetBusy(true);EngineRequest resume=null;try{if(WireGuardIntegration.HasService)throw new InvalidOperationException("Desative a VPN WireGuard antes de ativar o Discord permanente.");if(active){EnsureAppsClosed(route.SelectedPaths());resume=new EngineRequest{Protocol=activeProtocol.Length>0?activeProtocol:Selection(),ProxyTls=proxyTls.Checked,TlsName=tlsName.Text.Trim(),Route=route.Copy(),Host=host.Text.Trim(),Port=(int)port.Value,User=user.Text,Password=password.Text};await Task.Run(()=>EngineController.RunElevated(null,true));await Task.Run(()=>EngineController.ReleaseGuard());active=false;}else if(NetworkGuard.ArmedState==true)throw new InvalidOperationException("Use Liberar rede direta antes de ativar o Discord permanente.");
   var profile=DiscordSettings();if(discordConnection.SelectedIndex==1&&profile.WireGuard==null)throw new ArgumentException("Importe o arquivo .conf do Proton primeiro.");DiscordProxy.Validate(profile);// Discord can remain openDiscordProxy.Save(profile);discordState.Text="Configurando início automático… permita o aviso do Windows";
   await Task.Run(()=>DiscordProxy.Elevated(true));permanentConfigured=DiscordProxy.HasConfiguration;if(resume!=null)await Task.Run(()=>EngineController.RunElevated(resume,false));Log("Proxy permanente do Discord configurada. A janela pode ser fechada.");
  }catch(Exception ex){permanentConfigured=DiscordProxy.HasConfiguration;if(resume!=null&&!permanentConfigured)Log("Proxy geral parada durante a alteração; ative novamente.");Error(ex);}finally{SetBusy(false);RefreshState();}
 }
 async Task RemoveDiscord()
 {
  if(busy)return;SetBusy(true);try{discordState.Text="Removendo configuração permanente…";await Task.Run(()=>DiscordProxy.Elevated(false));permanentConfigured=DiscordProxy.HasConfiguration;discordWireGuard=null;discordPassword.Clear();Log("Configuração permanente removida; rede do Discord liberada.");}
  catch(Exception ex){permanentConfigured=DiscordProxy.HasConfiguration;Error(ex);}finally{SetBusy(false);RefreshState();}
 }

 void AddProxyPicker(Panel settings,bool discord)
 {
  foreach(Control c in settings.Controls)if(c.Top>=50)c.Top+=60;settings.Height+=60;
  var picker=new ProxyPicker(card,light,new Font(UiFont(),10));settings.Controls.Add(picker);
  if(discord)discordPicker=picker;else generalPicker=picker;
  picker.Changed+=()=>PickerChanged(discord);picker.ImportRequested+=ImportProxies;
 }
 ProxyPreset CaptureProxy(bool discord){return new ProxyPreset{Host=discord?discordHost.Text:host.Text,Port=(int)(discord?discordPort.Value:port.Value),User=discord?discordUser.Text:user.Text,Password=discord?discordPassword.Text:password.Text};}
 void FillProxy(bool discord,ProxyPreset p)
 {
  if(p==null)return;var h=discord?discordHost:host;var n=discord?discordPort:port;var u=discord?discordUser:user;var pw=discord?discordPassword:password;
  h.Text=p.Host;n.Value=Math.Max(1,Math.Min(65535,p.Port));u.Text=p.User;pw.Text=p.Password;
 }
 void LoadPickers()
 {
  loadingPickers=true;presets=ProxyCatalog.Load();
  var saved=preview?new ProxySelectionState():ProxyCatalog.Selection();
  generalManual=saved.GeneralManual??CaptureProxy(false);discordManual=saved.DiscordManual??CaptureProxy(true);
  generalPicker.Fill(presets);discordPicker.Fill(presets);generalPicker.Restore(saved.GeneralAutomatic,saved.GeneralIndex);discordPicker.Restore(saved.DiscordAutomatic,saved.DiscordIndex);
  generalWasAutomatic=generalPicker.Automatic.Checked;discordWasAutomatic=discordPicker.Automatic.Checked;loadingPickers=false;PickerChanged(false);PickerChanged(true);
 }
 void PickerChanged(bool discord)
 {
  if(loadingPickers)return;var picker=discord?discordPicker:generalPicker;bool automatic=picker.Automatic.Checked,previous=discord?discordWasAutomatic:generalWasAutomatic;
  if(automatic&&!previous){if(discord)discordManual=CaptureProxy(true);else generalManual=CaptureProxy(false);}
  if(automatic&&picker.Options.SelectedIndex>=0&&picker.Options.SelectedIndex<presets.Count)FillProxy(discord,presets[picker.Options.SelectedIndex]);
  else if(previous&&!automatic)FillProxy(discord,discord?discordManual:generalManual);
  if(discord)discordWasAutomatic=automatic;else generalWasAutomatic=automatic;
  SetProxyEditing();if(!preview)SavePickerState();
 }
 void SetProxyEditing()
 {
  if(generalPicker==null||discordPicker==null)return;
  host.ReadOnly=user.ReadOnly=password.ReadOnly=generalPicker.Automatic.Checked;port.Enabled=!busy&&!active&&!generalPicker.Automatic.Checked;
  discordHost.ReadOnly=discordUser.ReadOnly=discordPassword.ReadOnly=discordPicker.Automatic.Checked;discordPort.Enabled=!busy&&!permanentConfigured&&!discordPicker.Automatic.Checked;
  generalPicker.SetAvailable(!busy&&!active);discordPicker.SetAvailable(!busy&&!permanentConfigured);
  generalPicker.Import.Enabled=discordPicker.Import.Enabled=!busy&&!active&&!permanentConfigured;SetDiscordConnectionEditing();
 }
 void SavePickerState()
 {
  if(preview||loadingPickers)return;try{ProxyCatalog.SaveSelection(new ProxySelectionState{GeneralAutomatic=generalPicker.Automatic.Checked,DiscordAutomatic=discordPicker.Automatic.Checked,GeneralIndex=generalPicker.Options.SelectedIndex,DiscordIndex=discordPicker.Options.SelectedIndex,GeneralManual=generalPicker.Automatic.Checked?generalManual:CaptureProxy(false),DiscordManual=discordPicker.Automatic.Checked?discordManual:CaptureProxy(true)});}catch{Log("Não foi possível salvar a seleção de proxy.");}
 }
 void ImportProxies()
 {
  if(busy||active||permanentConfigured)return;using(var picker=new OpenFileDialog{Filter="Lista de proxies (*.txt)|*.txt|Todos os arquivos (*.*)|*.*",CheckFileExists=true})if(picker.ShowDialog(this)==DialogResult.OK){try{var imported=ProxyCatalog.Parse(File.ReadAllText(picker.FileName));ProxyCatalog.Save(imported);loadingPickers=true;presets=imported;generalPicker.Fill(presets);discordPicker.Fill(presets);loadingPickers=false;PickerChanged(false);PickerChanged(true);Log(presets.Count+" proxies importadas e protegidas neste computador.");}catch(Exception ex){loadingPickers=false;Error(ex);}}
 }
 void BuildApps()
 {
  applicationPage.Controls.Add(Label("Aplicativos",0,4,800,48,24,Color.White));applicationPage.Controls.Add(Label("Cadastre os executáveis e marque os que devem usar a proxy.",0,59,800,30,10,muted));
  var discord=Button("Adicionar Discord",0,108,250,true);discord.Click+=(s,e)=>AddDetected(true);applicationPage.Controls.Add(discord);
  var pka=Button("Adicionar PKA",277,108,250,true);pka.Click+=(s,e)=>AddDetected(false);applicationPage.Controls.Add(pka);
  apps.SetBounds(0,170,815,290);apps.CheckOnClick=true;apps.BackColor=card;apps.ForeColor=Color.White;apps.BorderStyle=BorderStyle.None;apps.HorizontalScrollbar=true;applicationPage.Controls.Add(apps);
  apps.ItemCheck+=(s,e)=>{if(refreshingApps)return;if(e.Index<route.Apps.Count)route.Apps[e.Index].Selected=e.NewValue==CheckState.Checked;System.Action refresh=()=>{InvalidateResults();RefreshLaunch();};if(IsHandleCreated)BeginInvoke(refresh);else refresh();};
  var add=Button("Adicionar .exe",0,483,250,true);add.Click+=(s,e)=>{using(var picker=new OpenFileDialog{Filter="Executáveis Windows (*.exe)|*.exe",Multiselect=true,CheckFileExists=true})if(picker.ShowDialog(this)==DialogResult.OK){foreach(string p in picker.FileNames){string path=Path.GetFullPath(p);if(!route.Apps.Exists(a=>String.Equals(a.Path,path,StringComparison.OrdinalIgnoreCase)))route.Apps.Add(new RegisteredApp{Path=path});}RefreshApps();Save();}};applicationPage.Controls.Add(add);
  var remove=Button("Remover selecionado",277,483,250,false);remove.Click+=(s,e)=>{if(apps.SelectedIndex>=0){route.Apps.RemoveAt(apps.SelectedIndex);RefreshApps();Save();}};applicationPage.Controls.Add(remove);
  var open=Button("Abrir pelo túnel",554,483,261,false);open.Click+=(s,e)=>{try{if(!active||busy)throw new InvalidOperationException("Ative o túnel primeiro.");if(apps.SelectedIndex<0)throw new InvalidOperationException("Escolha um aplicativo na lista.");var a=route.Apps[apps.SelectedIndex];if(!route.Global&&!a.Selected)throw new InvalidOperationException("Marque este aplicativo e reconecte antes de abrir.");GameLauncher.Open(a.Path,"");Log("Aplicativo aberto: "+Path.GetFileName(a.Path));}catch(Exception ex){Error(ex);}};applicationPage.Controls.Add(open);
  applicationPage.Controls.Add(Label("Cadastre também os .exe auxiliares e o cliente final aberto por um launcher.\nFeche os apps antes de ativar: conexões já existentes não são migradas.\nPor aplicativo, os demais processos continuam diretos; DNS na porta 53 usa o túnel.\nA seleção não se estende automaticamente a processos filhos ou serviços compartilhados.",0,554,815,110,10,muted));
 }
 void AddDetected(bool discord)
 {
  try{
   if(active||busy)throw new InvalidOperationException("Pare o túnel antes de alterar os aplicativos protegidos.");
   string[] paths=AppDiscovery.Find(discord);
   if(paths.Length==0)throw new InvalidOperationException((discord?"Discord":"PokeAlliance")+" não encontrado nos caminhos padrão. Instale o aplicativo ou use Adicionar .exe.");
   foreach(string path in paths){var app=route.Apps.Find(a=>String.Equals(a.Path,path,StringComparison.OrdinalIgnoreCase));if(app==null)route.Apps.Add(new RegisteredApp{Path=path,Selected=true});else app.Selected=true;}
   if(discord)udp.Checked=true;RefreshApps();Save();Log((discord?"Discord":"PokeAlliance")+": "+paths.Length+" executável(is) localizado(s) e marcado(s). Feche o aplicativo e ative o túnel antes de abrir.");
  }catch(Exception ex){Error(ex);}
 }
 void BuildTests()
 {
  testPage.Controls.Add(Label("Teste e diagnóstico",0,4,810,48,24,Color.White));testPage.Controls.Add(Label("Resultados reais da negociação da proxy e de uma consulta HTTPS pelo túnel.",0,59,815,28,10,muted));
  testState=Label("Nenhum teste executado",20,18,760,45,13,light);exitIp=Label("IP público de saída: —",20,75,760,30,11,Color.White);testPage.Controls.Add(Card(0,114,815,128,testState,exitIp));
  test=Button("Testar proxy",0,264,250,true);test.Click+=async(s,e)=>await TestProxy();testPage.Controls.Add(test);
  var routed=Button("Testar túnel",277,264,250,false);routed.Click+=async(s,e)=>await TestTunnel();testPage.Controls.Add(routed);
  var diagnostic=Button("Abrir log do motor",554,264,261,false);diagnostic.Click+=(s,e)=>{string path=Path.Combine(permanentConfigured?DiscordProxy.PrivateRoot:AppPaths.Root,"tun-diagnostic.log");if(File.Exists(path))Process.Start(new ProcessStartInfo("notepad.exe","\""+path+"\""){UseShellExecute=true});else Log("O motor ainda não gerou um log.");};testPage.Controls.Add(diagnostic);
  appResults.SetBounds(0,328,815,210);appResults.View=View.Details;appResults.FullRowSelect=true;appResults.GridLines=false;appResults.BackColor=card;appResults.ForeColor=Color.White;appResults.BorderStyle=BorderStyle.None;appResults.Columns.Add("Aplicativo",190);appResults.Columns.Add("IP de saída do túnel",180);appResults.Columns.Add("Verificação",420);testPage.Controls.Add(appResults);logs.SetBounds(0,556,815,143);logs.Multiline=true;logs.ReadOnly=true;logs.ScrollBars=ScrollBars.Vertical;logs.BackColor=card;logs.ForeColor=light;logs.Font=new Font(UiFont(),10);testPage.Controls.Add(logs);

 }
 void ConfigureGeneralRouting(){route.Global=global.Checked;route.AutoLaunch=auto.Checked;route.DirectDiscordCalls=route.Global&&udp.Checked;route.Udp=udp.Checked&&!route.Global;}
 async Task ActivateProxy()
 {
  if(busy)return;SetBusy(true);try {
   permanentConfigured=DiscordProxy.HasConfiguration;
   if(WireGuardIntegration.HasService)throw new InvalidOperationException("Desative a VPN WireGuard antes de ativar a Proxy geral.");
   if(active)throw new InvalidOperationException("Pare o túnel antes de alterar a configuração.");
   if(NetworkGuard.ArmedState==true&&!permanentConfigured)throw new InvalidOperationException("O bloqueio anterior permanece ativo. Libere a rede direta antes de criar uma nova sessão.");
   ConfigureGeneralRouting();if(!route.Global&&!route.DirectDiscordCalls&&!permanentConfigured&&!route.Udp&&route.Apps.Exists(a=>a.Selected&&String.Equals(Path.GetFileName(a.Path),"Discord.exe",StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("Marque Chamadas / UDP e use SOCKS5 com UDP para proteger o Discord. HTTPS CONNECT não transporta chamadas.");
   var request=new EngineRequest{Protocol=await ChooseProtocol(),ProxyTls=proxyTls.Checked,TlsName=tlsName.Text.Trim(),Route=route.Copy(),Host=host.Text.Trim(),Port=(int)port.Value,User=user.Text,Password=password.Text,OwnerPath=Process.GetCurrentProcess().MainModule.FileName};
   TunConfiguration.Build(request);var closed=new System.Collections.Generic.List<string>(request.Route.SelectedPaths());if(permanentConfigured)closed.RemoveAll(p=>String.Equals(Path.GetFileName(p),"Discord.exe",StringComparison.OrdinalIgnoreCase));EnsureAppsClosed(closed.ToArray());
   string chosen=launch.SelectedItem==null?null:((RegisteredApp)launch.SelectedItem).Path;
   if(route.AutoLaunch&&chosen==null)throw new InvalidOperationException("Escolha um aplicativo marcado para abrir automaticamente.");
   Save();state.Text="Ativando e verificando… permita o aviso do Windows";activeProtocol=request.Protocol;tunnelIp="";RefreshAppResults();Log("Validando "+request.Protocol+" e instalando proteção persistente.");
   await Task.Run(()=>EngineController.RunElevated(request,false));active=true;healthy=true;sessionVersion++;lastHealth=DateTime.UtcNow;Log("Túnel ativo; teste HTTPS do processo protegido concluído.");
   if(request.Route.AutoLaunch){GameLauncher.Open(chosen,"");Log("Aplicativo aberto: "+Path.GetFileName(chosen));}
  }catch(Exception ex){Error(ex);}finally{SetBusy(false);RefreshState();}
 }
 async Task Stop(){if(busy)return;SetBusy(true);try{sessionVersion++;tunnelIp="";RefreshAppResults();await Task.Run(()=>EngineController.RunElevated(null,true));Log(permanentConfigured?"Proxy geral parada; Discord permanente mantido.":"Túnel parado. Bloqueio de saída direta mantido.");}catch(Exception ex){Error(ex);}finally{SetBusy(false);RefreshState();}}
 async Task Release(){if(busy)return;SetBusy(true);try{if(active)throw new InvalidOperationException("Pare o túnel antes de liberar a rede direta.");await Task.Run(()=>EngineController.ReleaseGuard());tunnelIp="";RefreshAppResults();Log("Bloqueio removido. A rede direta está liberada.");}catch(Exception ex){Error(ex);}finally{SetBusy(false);RefreshState();}}
 string Selection(){if(!socks.Checked&&!https.Checked)throw new ArgumentException("Marque SOCKS5, HTTPS ou ambas.");return socks.Checked?(https.Checked?"BOTH":"SOCKS5"):"HTTPS";}
 async Task<string> ChooseProtocol()
 {
  string selection=Selection();if(selection!="BOTH")return selection;
  string h=host.Text.Trim(),u=user.Text,p=password.Text;int n=(int)port.Value;bool needsUdp=udp.Checked&&!global.Checked,secure=proxyTls.Checked;string certificate=tlsName.Text.Trim();
  try{await Task.Run(()=>{ProxyDiagnostics.ProxyIp(h,n,u,p,"SOCKS5");if(needsUdp)ProxyDiagnostics.CheckUdp(h,n,u,p,"SOCKS5");});Log("Ambas selecionadas: SOCKS5 validada e escolhida.");return "SOCKS5";}
  catch(Exception ex){Log("SOCKS5 indisponível: "+ex.Message);if(needsUdp)throw new InvalidOperationException("A SOCKS5 não confirmou UDP. HTTPS não pode substituí-la para chamadas Discord.");}
  await Task.Run(()=>ProxyDiagnostics.ProxyIp(h,n,u,p,"HTTPS",secure,certificate));Log("Ambas selecionadas: HTTPS validada e escolhida.");return "HTTPS";
 }
 async Task TestProxy()
 {
  if(busy)return;SetBusy(true);testState.Text="Testando protocolos selecionados…";
  try{
   string selection=Selection(),h=host.Text.Trim(),u=user.Text,p=password.Text;int n=(int)port.Value;bool needsUdp=udp.Checked&&!global.Checked,secure=proxyTls.Checked;string certificate=tlsName.Text.Trim();int ok=0,total=selection=="BOTH"?2:1;
   string summary="";RoutingConfiguration.ValidateProxy(h,n,u,p);
   foreach(string kind in selection=="BOTH"?new[]{"SOCKS5","HTTPS"}:new[]{selection}){
    try{string ip=await Task.Run(()=>ProxyDiagnostics.ProxyIp(h,n,u,p,kind,secure,certificate));string status="TCP OK";
     if(needsUdp&&kind=="SOCKS5"){await Task.Run(()=>ProxyDiagnostics.CheckUdp(h,n,u,p,kind));status="TCP + UDP OK";}
     if(needsUdp&&kind=="HTTPS")status="TCP OK; sem suporte a chamadas UDP";
     summary+=(summary.Length==0?"":"  |  ")+kind+": "+ip;Log(kind+": "+status+" / IP "+ip);ok++;
    }catch(Exception ex){Log(kind+": "+ex.Message);}
   }
   testState.Text=ok+" de "+total+" protocolos verificados";exitIp.Text=summary.Length==0?"IP de saída: indisponível":summary;RefreshAppResults();
  }catch(Exception ex){testState.Text="Falha na proxy";exitIp.Text="IP de saída: indisponível";Error(ex);}finally{SetBusy(false);}
 }
 void InvalidateResults(){if(active)return;tunnelIp="";if(appResults!=null)RefreshAppResults();if(exitIp!=null)exitIp.Text="IP público de saída: —";}
 void RefreshAppResults()
 {
  appResults.Items.Clear();if(permanentConfigured&&!active){var row=new ListViewItem("Discord");row.SubItems.Add(tunnelIp.Length>0?tunnelIp:"—");row.SubItems.Add(tunnelIp.Length>0?"Túnel Discord OK; app não medido":"Aguardando teste do túnel Discord");row.ToolTipText="IP do processo de diagnóstico na rota permanente do Discord.";appResults.Items.Add(row);appResults.ShowItemToolTips=true;return;}foreach(var a in route.Apps){if(!route.Global&&!a.Selected)continue;
   string status=tunnelIp.Length>0?"Túnel OK; app não medido / "+activeProtocol:"Aguardando teste do túnel";
   var item=new ListViewItem(Path.GetFileNameWithoutExtension(a.Path));item.SubItems.Add(tunnelIp.Length>0?tunnelIp:"—");item.SubItems.Add(status);item.ToolTipText="IP medido pelo processo de diagnóstico na mesma rota. O tráfego deste aplicativo não foi medido individualmente.";appResults.Items.Add(item);
  }
  appResults.ShowItemToolTips=true;
 }

 async Task TestTunnel(){if(busy)return;SetBusy(true);try{if(!active&&!discordRunning)throw new InvalidOperationException("Ative o túnel primeiro.");string ip=await Task.Run(()=>EngineController.RoutedIp());healthy=true;tunnelIp=ip;RefreshAppResults();testState.Text="IP do túnel verificado para a rota selecionada";exitIp.Text="IP público pelo túnel: "+ip;Log(testState.Text+" / "+ip);}catch(Exception ex){healthy=false;tunnelIp="";RefreshAppResults();testState.Text="Túnel não confirmado";exitIp.Text="IP público pelo túnel: indisponível";Error(ex);}finally{SetBusy(false);}}
 async Task CheckHealth(){checkingHealth=true;lastHealth=DateTime.UtcNow;int version=sessionVersion;bool ok=false;try{await Task.Run(()=>EngineController.RoutedIp());ok=true;}catch{}finally{checkingHealth=false;}if(!IsDisposed&&active&&version==sessionVersion){if(healthy!=ok)Log(ok?"Conectividade do processo protegido restabelecida.":"Erro de conectividade do processo protegido. Sem fallback direto.");healthy=ok;RefreshState();}}
 void EnsureAppsClosed(string[] paths){foreach(string path in paths)foreach(Process p in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path))){using(p){try{if(String.Equals(p.MainModule.FileName,path,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Feche "+Path.GetFileName(path)+" antes de ativar.");}catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("Não foi possível verificar um processo aberto. Feche "+Path.GetFileName(path)+" antes de ativar.");}}}}
 void SetBusy(bool value){busy=value;updateButton.Enabled=pendingUpdate!=null&&!value&&!active&&!permanentConfigured;connect.Enabled=!value&&!active;stop.Enabled=!value&&active;release.Enabled=!value&&!active&&!permanentConfigured;ApplyActivationColors();test.Enabled=!value;discordEnable.Enabled=!value&&!permanentConfigured;discordRemove.Enabled=!value&&permanentConfigured&&!active;discordTest.Enabled=!value;discordHost.Enabled=discordUser.Enabled=discordPassword.Enabled=discordPath.Enabled=discordPort.Enabled=discordBrowse.Enabled=!value&&!permanentConfigured;proxyTls.Enabled=tlsName.Enabled=socks.Enabled=https.Enabled=udp.Enabled=global.Enabled=auto.Enabled=host.Enabled=port.Enabled=user.Enabled=password.Enabled=launch.Enabled=!value&&!active;applicationPage.Enabled=!value;apps.Enabled=!value&&!active;foreach(Control c in applicationPage.Controls)if(c is Button&&c.Text!="Abrir pelo túnel")c.Enabled=!value&&!active;SetProxyEditing();}
 void SetDisabledColors(Control root,Color color){foreach(Control c in root.Controls){var check=c as ReadableCheckBox;if(check!=null)check.DisabledTextColor=color;var radio=c as ReadableRadioButton;if(radio!=null)radio.DisabledTextColor=color;SetDisabledColors(c,color);}}
 void ApplyActivationColors()
 {
  Color green=Color.FromArgb(99,242,149),normal=Color.FromArgb(145,181,230);
  SetDisabledColors(dashboard,active?green:light);SetDisabledColors(discordPage,discordRunning?green:light);
  connect.Text=active?"Proxy ativada":"Ativar proxy";connect.ForeColor=active?green:Color.White;((ReadableButton)connect).DisabledTextColor=active?green:normal;
  discordEnable.Text=discordRunning?"VPN Discord Ativada":"Ativar VPN Discord";discordEnable.ForeColor=discordRunning?green:Color.White;((ReadableButton)discordEnable).DisabledTextColor=discordRunning?green:normal;
 }
 void RefreshState()
 {
  bool wasActive=active;string mode=preview?"NONE":EngineController.Mode();active=mode=="NORMAL"||mode=="COMBINED";discordRunning=mode=="DISCORD"||mode=="COMBINED";
  bool? guarded=preview?(bool?)false:NetworkGuard.ArmedState;
  state.Text=active?(healthy?"Proxy geral conectada":"Erro de conectividade • motor ativo"):discordRunning?"Proxy geral disponível • Discord permanente ativo":!guarded.HasValue?"Proteção não consultada":guarded.Value?"Túnel parado • saída direta bloqueada":"Desconectado • rede direta liberada";
  string discordKind=discordWireGuard!=null&&discordConnection.SelectedIndex==1?"VPN Discord":"Proxy Discord";discordState.Text=permanentConfigured?(discordRunning?discordKind+" permanente ativa":discordKind+" configurada • aguardando reconexão"):"Sem conexão permanente";
  state.ForeColor=active?Color.FromArgb(99,242,149):light;discordState.ForeColor=discordRunning?Color.FromArgb(99,242,149):light;
  if(wasActive&&!active){tunnelIp="";RefreshAppResults();}SetBusy(busy);
 }
 void RefreshApps(){refreshingApps=true;try{tunnelIp="";RefreshAppResults();apps.Items.Clear();foreach(var a in route.Apps)apps.Items.Add(a,a.Selected);}finally{refreshingApps=false;}RefreshLaunch();}
 void RefreshLaunch(){string previous=launch.SelectedItem==null?null:((RegisteredApp)launch.SelectedItem).Path;launch.Items.Clear();foreach(var a in route.Apps)if(a.Selected)launch.Items.Add(a);for(int i=0;i<launch.Items.Count;i++)if(((RegisteredApp)launch.Items[i]).Path==previous)launch.SelectedIndex=i;if(launch.SelectedIndex<0&&launch.Items.Count>0)launch.SelectedIndex=0;}
 void Save(){if(preview)return;SavePickerState();try{SessionStore.Save("",host.Text.Trim(),(int)port.Value,user.Text,password.Text,route,Selection(),proxyTls.Checked,tlsName.Text.Trim());}catch{Log("Não foi possível salvar o perfil protegido.");}}
 void Error(Exception ex){string text=ex is AggregateException?"Falha de conexão TCP com o proxy.":ex.Message;Log("ERRO: "+text);MessageBox.Show(this,text,"Litfix",MessageBoxButtons.OK,MessageBoxIcon.Error);}
 void Log(string value){if(discordUser.Text.Length>0)value=value.Replace(discordUser.Text,"[usuario-discord]");if(discordPassword.Text.Length>0)value=value.Replace(discordPassword.Text,"[senha-discord]");if(user.Text.Length>0)value=value.Replace(user.Text,"[usuario]");if(password.Text.Length>0)value=value.Replace(password.Text,"[senha]");if(logs.TextLength>24000)logs.Clear();logs.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+value+Environment.NewLine);}
 void ShowPage(Panel page){foreach(Control child in content.Controls)child.Visible=child==page;page.BringToFront();}
 public void PreviewPage(string name){if(name=="active"){active=true;discordRunning=true;permanentConfigured=true;SetBusy(false);}if(name=="update")ShowPendingUpdate(new PendingUpdate{Manifest=new UpdateManifest{version="2.0.17.0"}});if(name=="tests"){route.Apps.Add(new RegisteredApp{Path=@"C:\Apps\Discord.exe"});route.Apps.Add(new RegisteredApp{Path=@"C:\Apps\PokeAlliance.exe"});RefreshAppResults();}ShowPage(name=="discord"?discordPage:name=="apps"?applicationPage:name=="tests"?testPage:dashboard);}
 static string UiFont(){foreach(var f in FontFamily.Families)if(f.Name=="Segoe UI Variable Text")return f.Name;foreach(var f in FontFamily.Families)if(f.Name=="Bahnschrift")return f.Name;return "Segoe UI";}
 Label Label(string t,int x,int y,int w,int h,int size,Color color){return new Label{Text=t,Left=x,Top=y,Width=w,Height=h,ForeColor=color,Font=new Font(UiFont(),size)};}
 Panel Card(int x,int y,int w,int h,params Control[] children){var p=new Panel{Left=x,Top=y,Width=w,Height=h,BackColor=card};p.Controls.AddRange(children);return p;}
 Button Button(string text,int x,int y,int w,bool primary){var b=new ReadableButton{Text=text,Left=x,Top=y,Width=w,Height=42,BackColor=primary?blue:card,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand,Font=new Font(UiFont(),10,FontStyle.Bold)};b.FlatAppearance.BorderColor=blue;b.FlatAppearance.BorderSize=primary?0:1;return b;}
 void ApplyReadableColors(Control parent){foreach(Control c in parent.Controls){if(c is CheckBox||c is RadioButton)c.ForeColor=light;ApplyReadableColors(c);}}
 void Style(Control c){c.BackColor=bg;c.ForeColor=Color.White;c.Font=new Font(UiFont(),11);}
 void Field(Panel p,string title,TextBox input,int x,int y,int width){p.Controls.Add(Label(title,x,y,width,25,10,muted));input.SetBounds(x,y+29,width,32);Style(input);p.Controls.Add(input);}
}

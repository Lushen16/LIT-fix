using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading.Tasks;

public sealed class WireGuardDialog:Form
{
 Label state=new Label();TextBox result=new TextBox();Button import,activate,remove,test;bool busy;
 public WireGuardDialog()
 {
  Text="Litfix • Teste WireGuard";ClientSize=new Size(740,510);MinimumSize=Size;StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(7,12,20);ForeColor=Color.White;Font=new Font("Segoe UI",10);
  Controls.Add(new Label{Text="VPN para Discord",Left=24,Top=20,Width=680,Height=40,Font=new Font("Segoe UI",22),ForeColor=Color.FromArgb(119,207,255)});
  Controls.Add(new Label{Text="Importe a configuração do seu servidor. Neste teste, toda a conexão do\ncomputador ou VM passa pela VPN, incluindo mensagens e chamadas.",Left=24,Top=76,Width=680,Height=55});
  var download=new LinkLabel{Text="Instalar WireGuard oficial",Left=24,Top=139,Width=300,Height=28,LinkColor=Color.FromArgb(119,207,255)};download.Click+=(s,e)=>Process.Start(new ProcessStartInfo("https://www.wireguard.com/install/"){UseShellExecute=true});Controls.Add(download);
  state.SetBounds(24,182,680,50);Controls.Add(state);
  import=Make("Importar .conf",24,Color.FromArgb(37,119,255));activate=Make("Ativar VPN",196,Color.FromArgb(127,65,211));remove=Make("Desativar VPN",368,Color.FromArgb(183,35,55));test=Make("Testar conexão",540,Color.FromArgb(37,119,255));
  import.Click+=(s,e)=>{using(var picker=new OpenFileDialog{Filter="WireGuard (*.conf)|*.conf",CheckFileExists=true})if(picker.ShowDialog(this)==DialogResult.OK){try{WireGuardIntegration.Import(picker.FileName);result.Text="Configuração importada. A chave fica em uma pasta local com acesso restrito; não é enviada ao GitHub.";}catch(Exception ex){result.Text=ex.Message;}RefreshStatus();}};
  activate.Click+=async(s,e)=>await Run(()=>{WireGuardIntegration.Change(true);return "Serviço iniciado. Clique em Testar conexão para verificar o servidor e depois teste uma chamada.";});
  remove.Click+=async(s,e)=>await Run(()=>{WireGuardIntegration.Change(false);return "VPN desativada. A configuração importada permanece para um próximo teste.";});
  test.Click+=async(s,e)=>await Run(()=>WireGuardIntegration.Test());
  result.SetBounds(24,298,680,118);result.Multiline=true;result.ReadOnly=true;result.ScrollBars=ScrollBars.Vertical;result.BackColor=Color.FromArgb(16,27,43);result.ForeColor=Color.FromArgb(119,207,255);Controls.Add(result);
  Controls.Add(new Label{Text="A VPN continua ativa ao fechar o Litfix e inicia com o Windows.\nUse Desativar VPN antes de voltar à Proxy geral ou ao Discord permanente.\nO arquivo .conf contém uma chave privada: não compartilhe publicamente.",Left=24,Top=438,Width=680,Height=65});
  FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};RefreshStatus();
 }
 Button Make(string text,int x,Color color){var button=new ReadableButton{Text=text,Left=x,Top=242,Width=158,Height=40,BackColor=color,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};Controls.Add(button);return button;}
 void RefreshStatus(){bool service=WireGuardIntegration.HasService,running=WireGuardIntegration.Running;state.Text=running?"VPN ativa • conexão com servidor ainda precisa ser testada":service?"Serviço configurado, mas parado • desative e tente novamente":!WireGuardIntegration.Installed?"WireGuard não instalado":WireGuardIntegration.Configured?"Configuração importada • VPN desativada":"Importe um arquivo .conf para começar";activate.Text=running?"VPN ativada":"Ativar VPN";((ReadableButton)activate).DisabledTextColor=running?Color.FromArgb(100,230,145):Color.FromArgb(210,205,245);state.ForeColor=running?Color.FromArgb(100,230,145):Color.FromArgb(119,207,255);import.Enabled=!busy&&!service;activate.Enabled=!busy&&!service&&WireGuardIntegration.Installed&&WireGuardIntegration.Configured;remove.Enabled=!busy&&service;test.Enabled=!busy&&running;}
 async Task Run(Func<string> operation){if(busy)return;busy=true;RefreshStatus();result.Text="Processando…";try{result.Text=await Task.Run(operation);}catch(Exception ex){result.Text=ex.Message;}finally{busy=false;RefreshStatus();}}
}

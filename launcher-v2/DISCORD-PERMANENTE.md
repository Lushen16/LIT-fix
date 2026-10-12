# Discord permanente — Litfix 2.0.20.0

## Proton / WireGuard somente no Discord

Selecione WireGuard / Proton VPN na aba Discord e use Importar Proton .conf. Localize o Discord.exe, feche o Discord e clique em Ativar permanente. Desconecte o aplicativo Proton VPN e desative qualquer VPN geral anterior antes de ativar. O modo por aplicativo usa o motor já incluído no Litfix; não exige o aplicativo oficial WireGuard.

TCP e UDP IPv4 do Discord passam pelo endpoint WireGuard do arquivo importado. As demais aplicações saem diretamente quando a Proxy geral está parada. IPv6 do Discord permanece bloqueado. A tarefa do Windows mantém a conexão em segundo plano e inicia ao entrar na conta. Para trocar de servidor, remova a configuração e importe outro arquivo. O arquivo e as chaves são guardados no perfil cifrado com DPAPI; não são incluídos no instalador ou enviados ao GitHub.

Testar VPN Discord consulta um processo de diagnóstico roteado pela mesma saída do Discord. No modo combinado, ele usa uma rota separada da Proxy geral. Isso verifica HTTPS de saída pelo túnel, sem medir o processo Discord ou uma chamada real. O servidor precisa estar disponível e aceitar a configuração. DNS tratado pelo motor usa Cloudflare: as opções NetShield do arquivo Proton não são reproduzidas. DNS do resolvedor compartilhado do Windows não permite atribuir toda consulta ao aplicativo que a originou.

A opção lateral VPN geral mantém o teste que afeta todo o computador. Para uso exclusivo no Discord, utilize a aba Discord.

Validação WireGuard: 32 verificações de importação, chaves, formato, armazenamento cifrado, rejeição de comandos, rotas TCP/UDP, saída direta de outras aplicações e composição com Proxy geral. As três configurações geradas foram aceitas pelo motor sing-box 1.14.2 incluído. Testes de abertura, pacote permanente e 69 verificações anteriores passaram. Não foram ativadas VPN, tarefa ou regras WFP para este teste; servidor Proton, chamada, reinicialização e saída por aplicação precisam de teste real com a configuração do usuário.

## Proxy SOCKS5

Na aba Discord, informe IPv4, porta, usuário e senha de uma SOCKS5. O Discord.exe é detectado automaticamente; Localizar Discord permite escolher uma instalação diferente. Feche o Discord e desative/libere qualquer túnel geral antes de ativar.

Testar proxy verifica autenticação e HTTPS de saída pela proxy. Ativar permanente pede autorização de administrador do Windows. Depois da confirmação, a janela do Litfix pode ser fechada.

A ativação instala uma tarefa do Windows para a conta atual: início ao fazer login, execução em segundo plano e tentativa de recuperação após falhas. A cópia do motor e o perfil ficam em uma pasta protegida em ProgramData. As credenciais são cifradas com DPAPI. Uma cópia editável cifrada do perfil fica na pasta de configurações do usuário. Nenhuma senha de login do Windows é armazenada.

Este mecanismo encaminha o tráfego do Discord pelo Litfix; não altera arquivos ou configurações internas do Discord. Dashboard e Discord compartilham um único motor com saídas separadas. O Discord mantém sua própria SOCKS5, mesmo com Dashboard global ou HTTPS. Parar o Dashboard ou fechar sua janela retorna ao modo Discord sozinho. Para remover a configuração permanente, pare o Dashboard primeiro. Ao migrar de versões anteriores à 2.0.17, remova e reative a configuração para instalar o motor novo. A troca de configuração reinicia o motor e pode interromper conexões brevemente. Na aba Discord, TCP (login e mensagens) usa SOCKS5; UDP das chamadas IPv4 usa a conexão normal, pelo outbound direto do motor. A proxy não precisa aceitar UDP. IPv6 e demais tráfegos não suportados continuam bloqueados. O túnel geral mantém seu comportamento anterior.

O motor tenta reconectar a cada 60 segundos enquanto a configuração existe. Os filtros persistentes bloqueiam saída direta dos executáveis já registrados se o motor parar. Regras pelo nome Discord.exe cobrem mudanças de versão enquanto o túnel está ativo, e os filtros são atualizados para novos caminhos detectados a cada 15 segundos. Ainda há uma janela entre a criação de uma versão nova e o registro dos filtros: não foi demonstrada uma garantia de ausência de vazamento em todas as atualizações do Discord.

Remover configuração apaga a tarefa, encerra o motor permanente, remove seus filtros e apaga as credenciais salvas. A desinstalação chama a mesma limpeza e recusa continuar se ela falhar. Logs de diagnóstico podem permanecer. Para configurar outra proxy, remova a anterior e ative novamente.

É necessária uma conta com privilégios de administrador, elevando com a mesma conta que salvou o perfil; a elevação usando outra conta não consegue abrir as credenciais protegidas da primeira.

Validação feita: compilação x64, 69 verificações da suíte, testes de CONNECT e retorno UDP por relay local, criptografia e corrupção do perfil separado, descoberta de versões e validação da definição de tarefa pelo Agendador do Windows com TASK_VALIDATE_ONLY (sem registrar tarefa). O motor aceitou as configurações do Discord e dos protocolos. A nova aba foi renderizada e conferida.

Ainda não foram exercitados neste computador: instalar a tarefa de verdade, ativar WFP/TUN permanente, reiniciar Windows, fazer chamada com uma proxy real e desinstalar com a tarefa ativa. O instalador é uma compilação local de teste, sem assinatura RSA ou Authenticode.

Validação de integração recomendada em VM: instalar; testar proxy; ativar permanente; fechar a janela e verificar chamada/IP; reiniciar e fazer login; verificar retomada e bloqueio com proxy indisponível; simular nova versão do Discord; remover configuração e conferir retorno à rede normal; reativar e desinstalar, confirmando ausência da tarefa e dos filtros.

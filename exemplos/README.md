# Exemplos

Projetos prontos do GordoKore Studio. Abra com **Arquivo > Abrir**, aperte **F5** para compilar e instalar, e abra o jogo: a janela aparece no menu do GordoKore.

Todos usam só as peças do estúdio (widgets, lista de dados, ações e Lua), então servem de ponto de partida para as suas janelas.

| Projeto | O que mostra | Recursos usados |
|---|---|---|
| [`painel_bot.gkproj`](painel_bot.gkproj) | Painel para controlar o bot: IA automática/manual, sentar quando parado, enviar comandos e atalhos. | Botões com comando do OpenKore, liga/desliga e lista com ações em Lua, botão com código C++ ([`painel_bot/user.cpp`](painel_bot/user.cpp)). |
| [`itens.gkproj`](itens.gkproj) | A janela "Itens do jogo": todos os itens com ícone, busca, arrastar para outra janela, descrição e drops do Divine Pride. | Modelo pronto "Janela de itens do jogo": lista de dados com a fonte *Itens do jogo*, botão com legenda, faixa, janela redimensionável com âncoras. |
| [`jogadores_perto.gkproj`](jogadores_perto.gkproj) | Os jogadores do mapa com classe e distância, atualizados sozinhos; o mapa e a sua posição no topo. | Lista de dados *Jogadores no mapa (ao vivo)*, busca, **Ao abrir** e timer em Lua (`gk.me`, `gk.every`). |
| [`esconder_jogadores.gkproj`](esconder_jogadores.gkproj) | Um botão que esconde e mostra os outros jogadores, só na sua tela. | Lua (`gk.hide`, `gk.hidden`); sem código, a ação *Esconder no mapa* faz o mesmo. |
| [`commits_openkore.gkproj`](commits_openkore.gkproj) | Os commits recentes do OpenKore, buscados na API do GitHub. | Lua com internet (`gk.http_get`, `gk.json`), lista *Preenchida pelo código* (`gk.rows`), resultado guardado por 5 minutos. |

Ao compilar, o estúdio cria ao lado do `.gkproj` uma pasta com o nome do projeto, com o código C++ gerado e a DLL. Ela não vai para o git; só o `user.cpp` do `painel_bot` fica, porque tem código do exemplo.

Para escrever os scripts, veja o [manual de Lua](../docs/manual-lua.md).

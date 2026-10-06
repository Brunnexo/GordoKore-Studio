# Histórico de versões

As versões seguem o [versionamento semântico](https://semver.org/lang/pt-BR/). A release do GitHub usa como notas a seção da versão correspondente neste arquivo.

## [0.1.0] - 2026-10-06

Primeira versão pública.

### Editor

- Prévia desenhada com as texturas da GRF do Ragnarok Online LATAM (0x200 e 0x300), com sprites de NPCs e monstros.
- Grade e guias de alinhamento, zoom de 100% a 600% (slider ou Ctrl + roda), desfazer e refazer (Ctrl+Z / Ctrl+Y ou Ctrl+R).
- Grade de propriedades em português, filtrada pelo tipo de widget.
- Widgets: texto, botão, botão de imagem (com legenda), liga/desliga, campo de texto, lista, imagem, sprite, retângulo, área rolável, lista de dados, ícone de item e faixa de 3 imagens.
- Lista de dados com o modelo da linha editado com duplo clique e `{coluna}` nos textos. As fontes de dados são: itens, monstros e mapas do jogo; jogadores, NPCs e monstros do mapa ao vivo; lista fixa; ou código.
- Janela redimensionável com passo e âncoras.
- Modelos prontos: barra de busca, rodapé com contagem e janela de itens do jogo.
- Textos em português, espanhol e inglês.
- Modo escuro; ajuda em **Ajuda > Como usar** (F1).

### Módulos (precisam do GordoKore com SDK 6)

- Ações sem código: comando do OpenKore, arrastar item, descrição do item, drops do Divine Pride, limpar campo e esconder os outros jogadores.
- Lua (`gk.*`):
  - widgets e janelas;
  - listas de dados;
  - dados do mapa (`gk.me`, `gk.players`, `gk.npcs`, `gk.monsters`);
  - timers (`gk.every`, `gk.after`);
  - função "Ao abrir" da janela;
  - internet (`gk.http_get`, `gk.json`).
- C++ no `user.cpp` com o SDK do GordoKore.
- Compilação com MinGW 32 bits e instalação na pasta de módulos do jogo (F5).

### Distribuição

- Executável único e autocontido para Windows 10/11 64 bits: não precisa do .NET instalado.
- Exemplos e manual de Lua no pacote.

# Manual de scripts Lua do GordoKore Studio

Este manual ensina a dar comportamento às janelas do GordoKore Studio com scripts Lua: o que acontece quando o jogador clica num botão, marca uma caixa, escolhe um item numa lista ou clica numa linha de uma lista de dados.

Não é preciso saber programar. Se você nunca escreveu código, leia primeiro [Lua em 10 minutos](#lua-em-10-minutos) e depois siga o [primeiro script](#primeiro-script-passo-a-passo).

## Sumário

1. [Quando usar Lua](#quando-usar-lua)
2. [Primeiro script, passo a passo](#primeiro-script-passo-a-passo)
3. [Como o estúdio liga um widget a uma função](#como-o-estúdio-liga-um-widget-a-uma-função)
4. [Quando o script roda](#quando-o-script-roda)
5. [Referência da API `gk`](#referência-da-api-gk)
6. [Lua em 10 minutos](#lua-em-10-minutos)
7. [Receitas](#receitas)
8. [Erros e depuração](#erros-e-depuração)
9. [Limites](#limites)
10. [Resumo de bolso](#resumo-de-bolso)

---

## Quando usar Lua

Cada widget clicável tem uma **Ação**. Há três jeitos de programar o que ela faz:

| Ação | Quando usar | Onde fica o código |
|---|---|---|
| **Comando do OpenKore** | O botão só manda um comando fixo para o bot, como `ai manual` ou `move prontera`. Na lista de dados, `{coluna}` no comando vira o valor da linha clicada. | Nenhum código: o comando vai na propriedade "Comando ou função". |
| **Lua (no jogo)** | O clique precisa de lógica: ler um campo, decidir com `if`, mudar textos da janela, guardar um contador, preencher uma lista, escolher o comando conforme o estado. | Aba **Script Lua** do estúdio. |
| **C++ (user.cpp)** | Acesso a tudo do SDK em C++ (para quem já programa em C++). | `user.cpp`, na pasta do código gerado. |

Regra prática: se a ação é sempre o mesmo comando, use **Comando do OpenKore**. Se ela depende de alguma coisa (o que foi digitado, se a caixa está marcada, quantas vezes já clicou), use **Lua**.

O script Lua roda **dentro do jogo**, no `bridge.dll`. Ele não roda na prévia do estúdio: para ver o resultado, compile, instale e abra o jogo.

---

## Primeiro script, passo a passo

Vamos fazer uma janela com um texto e um botão. Ao clicar no botão, o texto muda.

1. **Abra o estúdio** e crie um projeto novo (Arquivo > Novo).
2. **Ponha um texto na janela.** Na aba Widgets, clique em **Texto**. Na grade de propriedades, à direita, veja o campo **Id** (em Geral). Anote o número: neste exemplo, `1`.
3. **Ponha um botão.** Clique em **Botão**. Na propriedade **Texto**, escreva `Dizer olá`. Anote o **Id** dele (por exemplo, `2`).
4. **Ligue o botão a uma função Lua.** Com o botão selecionado, em **Ação**:
   - **Ação (clique)**: escolha `Lua (no jogo)`;
   - **Comando ou função**: escreva `dizer_ola` (só o nome, sem parênteses).
5. **Escreva a função.** Abra a aba **Script Lua** (ao lado da aba Janela) e escreva:

   ```lua
   local TEXTO = 1  -- Id do texto no estúdio

   function dizer_ola(id, evento, valor)
     gk.set(TEXTO, 'Olá, mundo!')
   end
   ```

6. **Dê um ícone à janela** para ela aparecer no menu do GordoKore: selecione a janela (clique no fundo dela) e escolha um **Ícone do menu**.
7. **Compile e instale** com **F5**. Se o jogo estiver aberto, feche e abra de novo: módulos novos só carregam quando o jogo abre.
8. **No jogo**, abra o menu do GordoKore, clique no ícone da sua janela e depois no botão. O texto vira "Olá, mundo!".

O que aconteceu:

- `function dizer_ola(id, evento, valor)` cria uma função com o nome que você pôs na ação.
- Quando o jogador clica, o `bridge.dll` chama essa função.
- `gk.set(TEXTO, 'Olá, mundo!')` troca o texto do widget de Id `1`.

---

## Como o estúdio liga um widget a uma função

### O nome da função

Na grade de propriedades do widget, grupo **Ação**:

- **Ação (clique)** = `Lua (no jogo)`;
- **Comando ou função** = o nome da função, exatamente como está no script (maiúsculas e minúsculas contam).

A função precisa ser **global**: escreva `function nome(...)`, e não `local function nome(...)`. Função `local` não pode ser chamada pela ação.

Uma mesma função pode servir a vários widgets. O primeiro parâmetro (`id`) diz qual deles chamou.

### Os parâmetros

Toda função ligada a uma ação recebe três números:

```lua
function minha_acao(id, evento, valor)
  -- id:     Id do widget que disparou (o da grade de propriedades)
  -- evento: o que aconteceu (tabela abaixo)
  -- valor:  um número que depende do evento
end
```

Você não precisa declarar os três. `function enviar()` também funciona: o Lua ignora o que sobra.

### Eventos e valores

| Widget | Quando dispara | `evento` | `valor` |
|---|---|---|---|
| Botão, botão de imagem | clique | `1` | `0` |
| Liga/desliga | ao marcar ou desmarcar | `2` | `1` marcado, `0` desmarcado |
| Lista | **duplo clique** num item | `3` | posição do item, começando em **0** |
| Lista de dados | clique numa linha | `1` | linha clicada, começando em **0** |
| Lista de dados | clique direito numa linha | `4` | linha clicada |
| Lista de dados | duplo clique numa linha | `5` | linha clicada |
| Botão dentro da linha de uma lista de dados | clique | `1` | linha do botão |
| Janela (propriedade **Ao abrir**) | sempre que a janela abre | `6` | `0` (o `id` também é `0`) |

Os números dos eventos podem ser guardados com nomes no começo do script:

```lua
local CLIQUE, MUDOU, ESCOLHEU, CLIQUE_DIREITO, DUPLO_CLIQUE, ABRIU = 1, 2, 3, 4, 5, 6
```

### Ao abrir a janela

Selecione a janela (clique no fundo dela) e, no grupo **Lua**, escreva em **Ao abrir (função Lua)** o nome de uma função. Ela roda toda vez que a janela abre, com os widgets já prontos: é o lugar para preencher textos e listas.

```lua
local MAPA = 1

function ao_abrir()
  local eu = gk.me()
  if eu then
    gk.set(MAPA, 'Mapa: ' .. eu.mapa)
  end
end
```

A lista de dados tem uma ação para cada evento: **Ação (clique)**, **Ação (clique direito)** e **Ação (duplo clique)**, cada uma com a sua função.

Na lista de dados, `valor` é a posição da linha **como ela aparece agora**, já filtrada pela busca. É o número que `gk.value` espera; veja [Lista de dados](#lista-de-dados).

O campo de texto não tem ação: leia o que foi digitado com `gk.get` quando outro widget, como um botão "Enviar", for clicado.

### Ids: como o script acha os widgets

O script não conhece os **Nomes** dos widgets, só os **Ids** (número na grade de propriedades, grupo Geral). Guarde os Ids em variáveis no começo do script:

```lua
local ESTADO, CAMPO, LISTA = 2, 8, 10
```

Assim, se um Id mudar no estúdio, você corrige uma linha só.

### Um script por projeto

O projeto tem um script só, na aba **Script Lua**, e todas as janelas do projeto o compartilham. As funções `gk` que mexem em widgets agem **na janela que disparou o evento**. Para abrir ou fechar outra janela, use `gk.open`, `gk.close` e `gk.toggle` com a **Chave** da janela.

---

## Quando o script roda

1. **Quando o jogo abre**, o `bridge.dll` carrega o módulo e roda o script inteiro, de cima a baixo, uma vez. Nesse momento ele guarda as funções e as variáveis. Nenhuma janela está aberta ainda.
2. **A cada clique** (ou outro evento) num widget com ação Lua, o `bridge.dll` chama a função ligada a ele. **Ao abrir** a janela, chama a função da propriedade "Ao abrir".
3. **Nos timers** (`gk.every` e `gk.after`), a função roda sozinha, de tempos em tempos.
4. **As variáveis continuam vivas** entre um clique e outro, e também quando a janela fecha e abre de novo, até o jogo fechar. Por isso um contador funciona.

Consequências importantes:

- **No corpo do script, `gk.get`, `gk.set` e as outras funções de widget não fazem nada**, porque não há janela aberta. Ali use as funções que não dependem de janela: `gk.log`, `gk.command`, `gk.lang`, `gk.tr`, `gk.hide`, `gk.hidden`, as do mundo (`gk.me`, `gk.players`...) e os timers.
- **Num timer**, as funções de widget agem na primeira janela aberta do módulo; com todas fechadas, não fazem nada.
- **Ao reabrir a janela, os widgets voltam a ser como estão no estúdio.** Textos trocados com `gk.set`, itens postos com `gk.list_add` e registros postos com `gk.rows` somem. As variáveis Lua continuam: guarde nelas o que precisa sobreviver e preencha os widgets de novo em **Ao abrir**.
- Instalar uma versão nova do módulo com o jogo aberto só vale quando o jogo abrir de novo.

---

## Referência da API `gk`

Todas as funções ficam na tabela `gk`. O `id` é sempre o Id do widget no estúdio.

### Textos e campos

| Função | O que faz |
|---|---|
| `gk.get(id)` | Devolve o texto do widget: o que foi digitado num **campo de texto**, ou o texto de um texto, botão ou botão com legenda (no idioma atual). |
| `gk.set(id, texto)` | Troca o texto de um texto, botão, botão com legenda ou campo de texto. Números são convertidos: `gk.set(1, 42)` mostra `42`. Num campo que filtra uma lista de dados, a lista filtra de novo. |

### Liga/desliga

| Função | O que faz |
|---|---|
| `gk.checked(id)` | Devolve `true` se está marcado, senão `false`. |
| `gk.check(id, marcado)` | Marca (`true`) ou desmarca (`false`). **Não** dispara a ação do liga/desliga. |

### Lista (a simples)

| Função | O que faz |
|---|---|
| `gk.list_add(id, texto)` | Põe um item no fim da lista. |
| `gk.selected(id)` | Devolve a posição do item selecionado (o primeiro é `0`), ou `-1` se nenhum. |

### Lista de dados

| Função | O que faz |
|---|---|
| `gk.rows(id, colunas, registros)` | Troca os registros da lista. `colunas` é uma tabela de nomes, `registros` é uma tabela de linhas. Ex.: `gk.rows(10, {'id', 'nome'}, {{1, 'um'}, {2, 'dois'}})`. Valores `nil` viram texto vazio. |
| `gk.source(id, fonte)` | Usa uma tabela do jogo: `'itens'` (colunas `id`, `nome`), `'monstros'` (`id`, `nome`, `mapas`, `sprite`) ou `'mapas'` (`nome`). Ou quem está no mapa agora, ao vivo: `'jogadores_no_mapa'`, `'npcs_no_mapa'` ou `'monstros_no_mapa'` (`id`, `nome`, `x`, `y`, `classe`, `classe_nome`, `distancia`). |
| `gk.count(id)` | Quantas linhas a lista mostra agora (depois da busca). |
| `gk.value(id, linha, coluna)` | Valor da `coluna` na `linha` (a mesma contagem do `valor` dos eventos, começando em `0`). Devolve sempre texto; `''` se não existir. |

### Widgets e janelas

| Função | O que faz |
|---|---|
| `gk.visible(id, visivel)` | Mostra (`true`) ou esconde (`false`) um widget. |
| `gk.open(chave)` | Abre a janela com essa **Chave** (propriedade da janela no estúdio). Serve para janelas de qualquer módulo. |
| `gk.close([chave])` | Fecha a janela da chave. Sem chave, fecha a janela do evento. |
| `gk.toggle([chave])` | Abre se estiver fechada, fecha se estiver aberta. |

### Mapa

| Função | O que faz |
|---|---|
| `gk.hide('jogadores', esconder)` | Esconde (`true`) ou mostra (`false`) os outros jogadores do mapa. Só na sua tela: o servidor e os outros jogadores não mudam, e o seu personagem fica. Vale em todos os mapas até o jogo fechar, mesmo com a janela fechada. |
| `gk.hidden('jogadores')` | `true` se os jogadores estão escondidos agora. |

Por enquanto a única parte do mapa é `'jogadores'`; outro nome dá erro (`invalid option`). Sem código, a ação **Esconder no mapa** faz o mesmo: no botão ela alterna (esconde/mostra) e no liga/desliga marcado ela esconde.

### Quem está no mapa

Lido do jogo na hora em que você chama. Fora do mapa (tela de login, troca de mapa), as listas vêm vazias e `gk.me()` devolve `nil`.

| Função | O que faz |
|---|---|
| `gk.me()` | Você: `{id, nome, tipo, x, y, classe, classe_nome, distancia, mapa}`. |
| `gk.players([raio])` | Os outros jogadores, do mais perto ao mais longe. Com `raio`, só os que estão a até tantas células. |
| `gk.npcs([raio])` | Os NPCs. |
| `gk.monsters([raio])` | Os monstros. |
| `gk.actor(id)` | Um ator pelo `id` (inclusive você), ou `nil` se ele não está no mapa. |
| `gk.map()` | O nome do mapa, como `'prontera'`; `''` fora do mapa. |

Cada ator é uma tabela com:

- `id`: o número do ator no servidor;
- `nome`: o nome, como aparece ao passar o mouse;
- `tipo`: `'jogador'`, `'npc'`, `'monstro'` ou `'outro'` (homúnculo, mercenário);
- `x` e `y`: a célula onde ele está;
- `classe`: o número da classe (para monstros, o ID do monstro);
- `classe_nome`: o nome da classe, quando o jogo tem;
- `distancia`: em células até você (a diagonal conta 1).

O nome de jogadores e NPCs que o jogo ainda não conhece vem vazio (`''`). O `bridge.dll` pede ao servidor, como quando você passa o mouse, e ele aparece numa das próximas chamadas.

```lua
for _, p in ipairs(gk.players(10)) do
  gk.log(p.nome .. ' a ' .. p.distancia .. ' celulas')
end
```

### Timers

| Função | O que faz |
|---|---|
| `gk.every(ms, funcao)` | Chama `funcao` a cada `ms` milissegundos (no mínimo 50), até o jogo fechar ou `gk.cancel`. Devolve o número do timer. |
| `gk.after(ms, funcao)` | Chama `funcao` uma vez, daqui a `ms` milissegundos. Devolve o número do timer. |
| `gk.cancel(t)` | Para o timer `t`. |

A função do timer não recebe parâmetros. Funções de widget dentro dela agem na primeira janela aberta do módulo. Um erro dentro do timer aparece no console, mas o timer continua.

```lua
local contagem = 0
local t = gk.every(1000, function()
  contagem = contagem + 1
end)
gk.after(5000, function()
  gk.cancel(t)
  gk.log('parei em ' .. contagem)
end)
```

### Internet (HTTP e JSON)

| Função | O que faz |
|---|---|
| `gk.http_get(url, funcao)` | Busca a `url` (só `https://`) sem travar o jogo. Quando a resposta chega, chama `funcao(ok, corpo, status)`. Devolve o número do pedido. |
| `gk.json(texto)` | Transforma um texto JSON em tabelas do Lua. Com erro, devolve `nil` e a mensagem do erro. |

Na resposta:

- `ok`: `true` quando o servidor respondeu com sucesso (HTTP 200 a 299);
- `corpo`: o texto da resposta, como o servidor mandou. Se a resposta nem chegou, vem a mensagem do erro (sem conexão, resposta maior que 1 MB);
- `status`: o código HTTP (200, 404...), ou `0` se a resposta não chegou.

No `gk.json`, objetos viram tabelas com chaves (`dados.nome`), listas viram tabelas numeradas a partir de 1 (`dados[1]`), `null` vira `nil`, e números inteiros continuam inteiros. Os textos passam do UTF-8 das APIs para o idioma do jogo: os acentos funcionam, e o que o jogo não sabe mostrar (emoji, outros alfabetos) vira `?`.

Limites, para não travar nada nem abusar das APIs: um pedido por segundo (os outros esperam na fila), 10 segundos de espera, resposta de até 1 MB e no máximo 8 pedidos em andamento por script. A função da resposta roda como um timer: as funções de widget agem na primeira janela aberta do módulo.

```lua
gk.http_get('https://api.github.com/repos/OpenKore/openkore', function(ok, corpo, status)
  if not ok then
    gk.log('falhou: ' .. status)
    return
  end
  local repo = gk.json(corpo)
  gk.log(repo.full_name .. ': ' .. repo.stargazers_count .. ' estrelas')
end)
```

### OpenKore, idioma e log

| Função | O que faz |
|---|---|
| `gk.command(texto)` | Manda um comando para o console do OpenKore, como se fosse digitado lá (ex.: `gk.command('ai manual')`). Precisa do OpenKore conectado com o plugin GordoKore. |
| `gk.lang()` | Idioma do jogo: `'pt'`, `'es'` ou `'en'`. |
| `gk.tr(pt, es, en)` | Devolve o texto do idioma do jogo. `es` e `en` são opcionais: sem eles, vale o português. |
| `gk.log(texto)` | Escreve no console do GordoKore Bridge, com o prefixo `[MOD]`. É o jeito de depurar. |

### Bibliotecas do Lua

O script tem o Lua 5.4 com as bibliotecas **básica**, `string`, `table`, `math` e `utf8`. Não há `io`, `os`, `require` nem `dofile`/`loadfile`: o script não lê arquivos nem roda programas. `print` não aparece em lugar nenhum; use `gk.log`.

---

## Lua em 10 minutos

O suficiente para escrever os scripts deste manual.

### Comentários

```lua
-- Tudo depois de dois traços é comentário: o Lua ignora.
```

### Variáveis

```lua
local nome = 'Poring'   -- texto (aspas simples ou duplas)
local nivel = 10        -- número
local ativo = true      -- verdadeiro ou falso (true / false)
local nada = nil        -- "sem valor"
```

Use `local` em tudo, **menos nas funções que as ações chamam**. Uma variável `local` no topo do script continua viva entre os cliques.

### Contas e textos

```lua
local total = 2 + 3 * 4          -- 14
local metade = 7 // 2            -- 3 (divisão inteira)
local frase = 'Nível ' .. 10     -- '..' junta textos: 'Nível 10'
local tamanho = #'Poring'        -- 6
```

### Comparações e decisões

```lua
local hp = 25
if hp < 30 then
  gk.log('HP baixo')
elseif hp < 70 then
  gk.log('HP médio')
else
  gk.log('HP alto')
end
```

Compare com `==` (igual), `~=` (diferente), `<`, `>`, `<=` e `>=`. Junte condições com `and`, `or` e `not`.

**Atenção:** no Lua, só `false` e `nil` são falsos. O número `0` é **verdadeiro**. Por isso, no liga/desliga, escreva `if valor == 1 then`, e não `if valor then`.

### Funções

```lua
local function dobro(n)
  return n * 2
end

local x = dobro(21)   -- 42
```

Funções auxiliares, que só o próprio script chama, podem e devem ser `local`. As que a ação chama precisam ser globais (sem `local`).

Uma função também pode ser escrita sem nome, direto onde é usada, como nos timers:

```lua
gk.after(3000, function()
  gk.log('passaram 3 segundos')
end)
```

### Tabelas: listas

```lua
local cores = { 'vermelho', 'verde', 'azul' }
local primeira = cores[1]     -- 'vermelho': a contagem começa em 1
local quantas = #cores        -- 3
table.insert(cores, 'preto')  -- põe no fim

for i, cor in ipairs(cores) do
  gk.log(i .. ': ' .. cor)
end
```

As listas do Lua começam em **1**, mas o `valor` dos eventos de lista começa em **0**. Some 1 para achar o item da sua tabela: `cores[valor + 1]`.

### Tabelas: registros

```lua
local monstro = { nome = 'Poring', nivel = 1 }
gk.log(monstro.nome)        -- 'Poring'
monstro.nivel = 2

local perigosos = { quit = true, relog = true }
if perigosos['quit'] then gk.log('cuidado') end
```

### Repetições

```lua
for i = 1, 5 do
  gk.log('volta ' .. i)
end

local n = 3
while n > 0 do
  n = n - 1
end
```

### Textos úteis

```lua
local s = '  ai manual  '
local limpo = s:match('^%s*(.-)%s*$')    -- tira os espaços das pontas: 'ai manual'
local maiusculo = limpo:upper()          -- 'AI MANUAL'
local comeco = limpo:sub(1, 2)           -- 'ai'
local primeira = limpo:match('^%S+')     -- primeira palavra: 'ai'
local numero = tonumber('42')            -- texto para número (nil se não for número)
local texto = tostring(42)               -- número para texto
local achou = limpo:find('manual', 1, true) ~= nil  -- contém 'manual'?
```

---

## Receitas

Cada receita diz quais widgets pôr na janela, com os Ids usados no script. Se os seus Ids forem outros, troque os números no começo do script.

### Contador

**Widgets:** Texto (Id 1); Botão "Contar" (Id 2) com a ação Lua `contar`.

```lua
local TEXTO = 1
local cliques = 0

function contar()
  cliques = cliques + 1
  gk.set(TEXTO, 'Cliques: ' .. cliques)
end
```

`cliques` é `local` no topo, então continua contando entre um clique e outro, mesmo se a janela fechar.

### Liga/desliga que configura o bot

**Widgets:** Liga/desliga (Id 6) com a ação Lua `auto_sentar`; Texto (Id 2) para mostrar o estado.

```lua
local ESTADO = 2

function auto_sentar(id, evento, valor)
  if valor == 1 then
    gk.command('conf sitAuto_idle 1')
    gk.set(ESTADO, 'Senta quando parado')
  else
    gk.command('conf sitAuto_idle 0')
    gk.set(ESTADO, 'Não senta sozinho')
  end
end
```

### Campo de texto com botão Enviar

**Widgets:** Campo de texto (Id 8); Botão "Enviar" (Id 9) com a ação Lua `enviar`; Texto (Id 2) para avisos.

```lua
local ESTADO, CAMPO = 2, 8

function enviar()
  local comando = gk.get(CAMPO):match('^%s*(.-)%s*$')
  if comando == '' then
    gk.set(ESTADO, 'Digite um comando')
    return
  end
  gk.command(comando)
  gk.set(CAMPO, '')
  gk.set(ESTADO, 'Enviado: ' .. comando)
end
```

`return` sai da função na hora: nada abaixo dele roda.

### Pedir confirmação (clicar duas vezes)

Mesmos widgets da receita anterior. Comandos perigosos só vão no segundo clique.

```lua
local ESTADO, CAMPO = 2, 8
local perigosos = { quit = true, relog = true, respawn = true }
local pendente = nil

function enviar_seguro()
  local comando = gk.get(CAMPO):match('^%s*(.-)%s*$')
  if comando == '' then return end
  local palavra = comando:match('^%S+'):lower()
  if perigosos[palavra] and pendente ~= comando then
    pendente = comando
    gk.set(ESTADO, 'Clique de novo para confirmar: ' .. comando)
    return
  end
  pendente = nil
  gk.command(comando)
  gk.set(CAMPO, '')
  gk.set(ESTADO, 'Enviado: ' .. comando)
end
```

### Lista de atalhos

**Widgets:** Lista (Id 10) com a ação Lua `usar_atalho`; Botão "Atalhos" (Id 12) com a ação Lua `carregar_atalhos`; Texto (Id 2).

A lista começa vazia. O botão a preenche e se esconde; o duplo clique num item manda o comando dele.

```lua
local ESTADO, LISTA = 2, 10

local atalhos = {
  { 'Ir para Prontera', 'move prontera' },
  { 'Guardar itens', 'autostorage' },
  { 'Vender itens', 'autosell' },
  { 'Onde estou?', 'where' },
}

function carregar_atalhos(id)
  for _, atalho in ipairs(atalhos) do
    gk.list_add(LISTA, atalho[1])
  end
  gk.visible(id, false)
end

function usar_atalho(id, evento, posicao)
  local atalho = atalhos[posicao + 1]
  if not atalho then return end
  gk.command(atalho[2])
  gk.set(ESTADO, 'Atalho: ' .. atalho[1])
end
```

Ao reabrir a janela, a lista volta vazia e o botão volta a aparecer, então lista e tabela nunca ficam fora de ordem.

### Lista de dados preenchida pelo script

**Widgets:**

- **Lista de dados** (Id 10) com:
  - **Nome** `destinos`;
  - **Fonte de dados** `Preenchida pelo código (Lua/C++)`;
  - **Colunas da busca** `nome;mapa`.
- No modelo da linha (dê duplo clique na lista para editar): um texto `{nome}`, um texto `{mapa} {x},{y}` e um botão "Ir" (Id 13) com a ação Lua `ir`.
- Botão "Carregar" (Id 20) com a ação Lua `carregar`.
- Texto `{destinos.total} destinos`, que se atualiza sozinho.

```lua
local LISTA = 10

local destinos = {
  { 'Prontera (centro)', 'prontera', 150, 180 },
  { 'Geffen (fonte)', 'geffen', 119, 59 },
  { 'Payon (arqueiros)', 'payon', 175, 230 },
  { 'Morroc (oásis)', 'morocc', 156, 93 },
}

function carregar()
  gk.rows(LISTA, { 'nome', 'mapa', 'x', 'y' }, destinos)
end

function ir(id, evento, linha)
  local mapa = gk.value(LISTA, linha, 'mapa')
  local x = gk.value(LISTA, linha, 'x')
  local y = gk.value(LISTA, linha, 'y')
  gk.command('move ' .. x .. ' ' .. y .. ' ' .. mapa)
end
```

No botão da linha, `id` é o Id do **botão** (13), não o da lista. Por isso `gk.value` usa a constante `LISTA`.

Esse botão também funcionaria sem Lua: ação **Comando do OpenKore** com `move {x} {y} {mapa}`. Use Lua quando precisar de lógica, como na próxima receita.

### Clique na linha decide o que fazer

**Widgets:** Lista de dados (Id 10) com **Fonte de dados** `Itens do jogo` e as ações:

- **Ação (clique)**: Lua `item_clicado`;
- **Ação (clique direito)**: `Descrição do item`, sem código.

Também um Texto (Id 2).

```lua
local ESTADO = 2
local POCOES = { [501] = true, [502] = true, [503] = true, [504] = true, [505] = true }

function item_clicado(id, evento, linha)
  local item = tonumber(gk.value(id, linha, 'id'))
  local nome = gk.value(id, linha, 'nome')
  if POCOES[item] then
    gk.set(ESTADO, nome .. ' é uma poção')
  else
    gk.set(ESTADO, nome .. ' (#' .. item .. ')')
  end
end
```

Aqui o evento vem da própria lista, então `id` já é o Id dela e serve para o `gk.value`. `gk.value` devolve texto; `tonumber` converte para comparar com números.

### Trocar a fonte da lista

**Widgets:** Lista de dados (Id 10) com um texto `{nome}` no modelo da linha. Três botões:

- "Itens" (Id 21) com a ação Lua `ver_itens`;
- "Monstros" (Id 22) com a ação Lua `ver_monstros`;
- "Mapas" (Id 23) com a ação Lua `ver_mapas`.

```lua
local LISTA = 10

function ver_itens()    gk.source(LISTA, 'itens')    end
function ver_monstros() gk.source(LISTA, 'monstros') end
function ver_mapas()    gk.source(LISTA, 'mapas')    end
```

### Mostrar e esconder partes da janela

**Widgets:** Liga/desliga "Opções avançadas" (Id 30) com a ação Lua `avancado`; widgets 31, 32 e 33, que só aparecem quando ele está marcado.

```lua
local AVANCADOS = { 31, 32, 33 }

function avancado(id, evento, valor)
  for _, widget in ipairs(AVANCADOS) do
    gk.visible(widget, valor == 1)
  end
end
```

`valor == 1` já é `true` ou `false`, que é o que `gk.visible` espera.

### Ler outros widgets ao clicar

**Widgets:** Liga/desliga (Id 6, sem ação); Lista (Id 10) com itens; Botão "Aplicar" (Id 11) com a ação Lua `aplicar`.

```lua
local CAIXA, LISTA = 6, 10
local modos = { 'auto', 'manual', 'off' }

function aplicar()
  local posicao = gk.selected(LISTA)
  if posicao < 0 then
    gk.log('nada escolhido')
    return
  end
  gk.command('ai ' .. modos[posicao + 1])
  if gk.checked(CAIXA) then
    gk.command('conf sitAuto_idle 1')
  end
end
```

`gk.checked` devolve `true`/`false`, então aqui `if gk.checked(CAIXA) then` está certo. A cilada do `0` verdadeiro é só com o `valor` numérico do evento.

### Esconder os outros jogadores

**Widgets:** Botão "Esconder / mostrar jogadores" (Id 2) com a ação Lua `alternar_jogadores`; Texto (Id 3) para o estado. É o exemplo `exemplos/esconder_jogadores.gkproj`.

```lua
local ESTADO = 3

function alternar_jogadores()
  local esconder = not gk.hidden('jogadores')
  gk.hide('jogadores', esconder)
  if esconder then
    gk.set(ESTADO, 'Jogadores escondidos')
  else
    gk.set(ESTADO, 'Jogadores visíveis')
  end
end
```

`not gk.hidden(...)` inverte o estado atual: escondidos passam a visíveis e vice-versa. O estado fica guardado no jogo, não na janela; por isso a função pergunta com `gk.hidden` em vez de guardar uma variável própria.

### Quem está por perto (lista ao vivo)

**Widgets:**

- Lista de dados (Id 2, **Nome** `perto`) com **Fonte de dados** `Jogadores no mapa (ao vivo)` e a ação Lua `clicou`.
- No modelo da linha: textos `{nome}`, `{classe_nome}` e `{distancia}`.
- Campo de texto `busca`, ligado à lista em **Filtrar pelo campo**, com **Colunas da busca** `nome;classe_nome`.
- Texto (Id 1) para o mapa e texto `{perto.total} jogadores`.
- Janela com **Ao abrir** = `ao_abrir`.

É o exemplo `exemplos/jogadores_perto.gkproj`. A lista se atualiza sozinha a cada meio segundo, sem perder a busca nem a rolagem.

```lua
local MAPA, LISTA = 1, 2

function ao_abrir()
  local eu = gk.me()
  if eu then
    gk.set(MAPA, 'Mapa: ' .. eu.mapa .. ' (' .. eu.x .. ', ' .. eu.y .. ')')
  end
end

function clicou(id, evento, linha)
  local alvo = gk.actor(tonumber(gk.value(LISTA, linha, 'id')))
  if alvo then
    gk.log(alvo.nome .. ' em ' .. alvo.x .. ', ' .. alvo.y)
  end
end

gk.every(2000, ao_abrir)
```

### Avisar quando um jogador chegar

Sem widgets: só o script. A cada segundo, compara os jogadores por perto com os da vez anterior.

```lua
local vistos = {}

gk.every(1000, function()
  local agora = {}
  for _, p in ipairs(gk.players(15)) do
    agora[p.id] = true
    if not vistos[p.id] and p.nome ~= '' then
      gk.log(p.nome .. ' chegou perto (' .. p.distancia .. ' celulas)')
    end
  end
  vistos = agora
end)
```

O teste `p.nome ~= ''` deixa para avisar quando o nome já chegou do servidor.

### Monstro mais perto

```lua
local ESTADO = 2

function mais_perto()
  local lista = gk.monsters()
  local m = lista[1]
  if m then
    gk.set(ESTADO, m.nome .. ' a ' .. m.distancia .. ' celulas')
  else
    gk.set(ESTADO, 'Nenhum monstro por perto')
  end
end
```

As listas já vêm do mais perto ao mais longe, então o primeiro (`lista[1]`) é o mais perto.

### Lista com dados de uma API

**Widgets:**

- Texto (Id 1) para o estado e botão "Atualizar" com a ação Lua `buscar`.
- Lista de dados (Id 3, **Nome** `commits`) com **Fonte de dados** `Preenchida pelo código` e a ação Lua `mostrar`. No modelo da linha, textos `{data}`, `{autor}` e `{mensagem}`.
- Campo `busca` ligado à lista, texto `{commits.total} commits` e texto (Id 9) para o commit clicado.
- Janela com **Ao abrir** = `ao_abrir`.

É o exemplo `exemplos/commits_openkore.gkproj`: os commits recentes do OpenKore, pela API pública do GitHub (sem chave).

```lua
local ESTADO, LISTA, DETALHE = 1, 3, 9
local URL = 'https://api.github.com/repos/OpenKore/openkore/commits?per_page=30'
local COLUNAS = { 'data', 'autor', 'mensagem' }

local linhas = nil   -- último resultado: sobrevive à janela fechar
local fresco = false -- buscado há menos de 5 minutos

local function mostrar_linhas()
  gk.rows(LISTA, COLUNAS, linhas)
  gk.set(ESTADO, #linhas .. ' commits recentes')
end

function buscar()
  gk.set(ESTADO, 'Buscando no GitHub...')
  gk.http_get(URL, function(ok, corpo, status)
    if not ok then
      gk.set(ESTADO, 'Erro ' .. status)
      return
    end
    local commits = gk.json(corpo)
    linhas = {}
    for i, c in ipairs(commits) do
      local autor = c.commit.author
      local primeira = c.commit.message:match('^[^\n]*') or ''
      linhas[i] = { autor.date:sub(1, 10), autor.name, primeira }
    end
    fresco = true
    gk.after(5 * 60 * 1000, function() fresco = false end)
    mostrar_linhas()
  end)
end

function ao_abrir()
  if linhas and fresco then
    mostrar_linhas()
  else
    buscar()
  end
end

function mostrar(id, evento, linha)
  gk.set(DETALHE, gk.value(LISTA, linha, 'autor') .. ': ' .. gk.value(LISTA, linha, 'mensagem'))
end
```

Dois cuidados que valem para qualquer API:

- **Guarde o resultado numa variável** (`linhas`). Ao reabrir a janela, a lista volta vazia, e o `ao_abrir` a preenche de novo sem gastar outra consulta.
- **Não consulte a cada abertura.** APIs gratuitas limitam as consultas (a do GitHub, 60 por hora). O `fresco` com `gk.after` só deixa buscar de novo depois de 5 minutos, ou no botão.

### Abrir outra janela

**Widgets:** Botão "Configurações" com a ação Lua `abrir_config`, numa janela. Outra janela do projeto com a **Chave** `config` e um botão "Fechar" com a ação Lua `fechar`.

```lua
function abrir_config()
  gk.open('config')
end

function fechar()
  gk.close()   -- sem chave: fecha a janela do botão
end
```

### Textos em três idiomas

```lua
local ESTADO = 2

function saudar()
  gk.set(ESTADO, gk.tr('Bom dia!', '¡Buenos días!', 'Good morning!'))
  if gk.lang() == 'en' then
    gk.log('cliente em inglês')
  end
end
```

---

## Erros e depuração

### Onde ver as mensagens

`gk.log('texto')` e todos os erros do script aparecem no **console do GordoKore Bridge** (a janela de console que abre com o jogo), em linhas que começam com `[MOD]`.

```lua
function enviar()
  local comando = gk.get(8)
  gk.log('enviar: campo = [' .. comando .. ']')
end
```

Os colchetes em volta do texto mostram espaços sobrando.

### Mensagens comuns

Nos exemplos abaixo, o projeto se chama `meu_modulo`.

| No console | O que é | Como resolver |
|---|---|---|
| `[MOD] meu_modulo.dll: meu_modulo.lua:12: ...` ao abrir o jogo, seguido de `gk_module_init devolveu 2` | Erro de sintaxe ou erro no corpo do script, na linha 12. **O módulo inteiro não carrega**: as janelas dele somem do menu. (A dica sobre o SDK nessa segunda linha não vale aqui: o `2` é o erro no script.) | Corrija a linha indicada. Erros comuns: `end` faltando, `=` no lugar de `==`, aspas sem fechar, `+` no lugar de `..` para juntar texto. |
| `[MOD] meu_modulo.dll: funcao Lua X nao existe` ao clicar | A ação chama `X`, mas o script não tem uma função global com esse nome. | Confira o nome em "Comando ou função" (maiúsculas contam). Tire o `local` da frente da função. |
| `[MOD] meu_modulo.dll: acao Lua X sem script (lua_load)` | A aba Script Lua está vazia. | Escreva o script. |
| `[MOD] meu_modulo.dll: meu_modulo.lua:20: attempt to ...` ao clicar | Erro dentro da função, na linha 20. O jogo continua; só aquele clique falha. | Leia a mensagem (lista abaixo). |

Erros "attempt to" mais comuns:

- **`attempt to concatenate a nil value`**: você juntou com `..` uma variável que vale `nil`. Por exemplo, `atalhos[posicao]` fora da tabela.
- **`attempt to index a nil value`**: você usou `x.campo` ou `x[1]` com `x` igual a `nil`. Teste antes com `if not x then return end`.
- **`attempt to call a nil value (field 'xyz')`**: a função `gk.xyz` não existe. Confira o nome na [referência](#referência-da-api-gk).
- **`attempt to add a 'string' with a 'number'`** (ou `sub`, `mul`, `div`...): conta com um texto que não é número, como o que vem de `gk.get` ou `gk.value`. Converta com `tonumber(...)` e teste se o resultado não é `nil`.
- **`bad argument #1 to 'set' (number expected, got string)`**: o Id precisa ser um número, como `gk.set(2, 'x')`, e não `gk.set('estado', 'x')`.

### O estúdio não confere o Lua

O estúdio só verifica se a ação Lua tem um nome de função. Erros de sintaxe e funções com nome errado só aparecem no console, quando o jogo abre ou no clique. Para testar mais rápido:

- Comece pequeno: uma função, F5, teste, e só então a próxima.
- Ponha um `gk.log('script carregado')` no fim do script. Se ele aparece no console ao abrir o jogo, o script carregou inteiro.

### Desfazer no editor

Na aba Script Lua, **Ctrl+Z** desfaz a digitação do próprio editor. Clicando na prévia, Ctrl+Z desfaz as edições do projeto, e cada trecho digitado em sequência no script conta como um passo.

---

## Limites

- **Timers leves:** o timer roda na linha de execução do jogo. Use intervalos de centenas de milissegundos ou mais; um timer pesado a cada 50 ms deixa o jogo lento.
- **Nomes que ainda não chegaram:** jogadores e NPCs podem vir com `nome` vazio nas primeiras chamadas.
- **Sem acesso ao disco:** o script não lê nem grava arquivos. O que precisa ser lembrado vive em variáveis, até o jogo fechar.
- **Internet só para ler:** `gk.http_get` faz apenas GET em `https://`, sem cabeçalhos próprios. APIs que pedem chave no cabeçalho ou envio de dados (POST) ainda não dão.
- **A janela reaberta volta ao desenho do estúdio.** Veja [Quando o script roda](#quando-o-script-roda).
- **O script roda na linha de execução do jogo:** um laço muito longo (milhares de voltas fazendo `gk.list_add`, por exemplo) trava o jogo enquanto roda.
- **Acentos:** o script vai para a DLL em Windows-1252. Os acentos do português e do espanhol funcionam; emoji e outros alfabetos viram `?`. Como cada letra acentuada ocupa um byte, `#texto` e `string.sub` funcionam normalmente; a biblioteca `utf8` não serve para esses textos.
- **Separadores do `gk.rows`:** tabulação (`\t`) e quebra de linha (`\n`) dentro de um valor quebram o registro. Evite esses caracteres nos dados.
- **Ids:** o script usa números. Mudou o Id de um widget no estúdio, mude também no script.
- **`gk.command`** só chega ao bot com o OpenKore conectado ao jogo e o plugin GordoKore carregado.

---

## Resumo de bolso

```lua
-- Eventos: 1 clique, 2 mudou (liga/desliga), 3 escolheu (lista), 4 clique direito, 5 duplo clique, 6 abriu
local CLIQUE, MUDOU, ESCOLHEU, CLIQUE_DIREITO, DUPLO_CLIQUE, ABRIU = 1, 2, 3, 4, 5, 6

-- Ids dos widgets (grade de propriedades > Geral > Id)
local TEXTO, CAMPO, LISTA = 1, 2, 3

-- Função ligada a uma ação: global, recebe (id, evento, valor)
function exemplo(id, evento, valor)
  local digitado = gk.get(CAMPO)              -- ler texto
  gk.set(TEXTO, 'Você digitou ' .. digitado)  -- escrever texto
  gk.command('ai manual')                     -- comando para o OpenKore
  gk.log('debug: ' .. tostring(valor))        -- console do GordoKore Bridge
end
```

| Quero... | Use |
|---|---|
| ler/escrever texto | `gk.get(id)`, `gk.set(id, texto)` |
| liga/desliga | `gk.checked(id)`, `gk.check(id, bool)` |
| lista simples | `gk.list_add(id, texto)`, `gk.selected(id)` |
| lista de dados | `gk.rows`, `gk.source`, `gk.count`, `gk.value` |
| esconder | `gk.visible(id, bool)` |
| janelas | `gk.open(chave)`, `gk.close([chave])`, `gk.toggle([chave])` |
| esconder os outros jogadores | `gk.hide('jogadores', bool)`, `gk.hidden('jogadores')` |
| quem está no mapa | `gk.me()`, `gk.players([raio])`, `gk.npcs([raio])`, `gk.monsters([raio])`, `gk.actor(id)`, `gk.map()` |
| repetir ou esperar | `gk.every(ms, f)`, `gk.after(ms, f)`, `gk.cancel(t)` |
| buscar numa API | `gk.http_get(url, function(ok, corpo, status) end)`, `gk.json(texto)` |
| rodar ao abrir a janela | propriedade da janela **Ao abrir (função Lua)** |
| bot | `gk.command(texto)` |
| idioma | `gk.lang()`, `gk.tr(pt, es, en)` |
| depurar | `gk.log(texto)` |

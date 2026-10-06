/*
 * GordoKore SDK: janelas dentro do jogo em DLLs separadas (modulos).
 *
 * O bridge.dll carrega cada <pasta do jogo>\GordoKore\modules\*.dll e chama gk_module_init com a tabela
 * GkHost. O modulo so fala com o jogo por essa tabela (nunca por enderecos do cliente), entao continua
 * valendo quando o cliente atualiza. ABI em C: compila com MinGW ou MSVC, 32 bits.
 *
 * Regras:
 *  - Tudo roda na thread do jogo (callbacks e chamadas ao host).
 *  - Textos em CP1252 (portugues/espanhol/ingles do LATAM). Caminhos de imagem em CP949, relativos a
 *    data\texture\ (ex.: "\xC0\xAF\xC0\xFA\xC0\xCE\xC5\xCD\xC6\xE4\xC0\xCC\xBD\xBA\\win_msgbox.bmp").
 *  - O host copia todas as strings recebidas.
 *  - Ids de widget: 1 a 0x7000, unicos por janela.
 *  - A tabela so cresce no fim: modulo novo em host antigo é recusado pela versao (GK_CHECK_HOST).
 */
#ifndef GORDOKORE_SDK_H
#define GORDOKORE_SDK_H

#include <stddef.h>

#define GK_SDK_VERSION 6

#ifdef __cplusplus
#define GK_EXTERN_C extern "C"
#else
#define GK_EXTERN_C
#endif
#define GK_EXPORT GK_EXTERN_C __declspec(dllexport)

/* Janela aberta (opaca). Valida so dentro dos callbacks dela */
typedef struct GkWin_ *GkWin;

/* Texto nos tres idiomas do cliente; es/en nulos = usa pt */
typedef struct GkText
{
    const char *pt;
    const char *es;
    const char *en;
} GkText;

enum GkLanguage
{
    GK_LANG_PT = 0,
    GK_LANG_ES = 1,
    GK_LANG_EN = 2
};

enum GkEvent
{
    GK_EVENT_CLICK = 1,        /* botao ou botao de imagem; lista de dados: linha (value = registro) */
    GK_EVENT_CHANGE = 2,       /* caixa de marcar: value = 0/1; campo de texto (SDK 3): ao digitar, value = tamanho */
    GK_EVENT_SELECT = 3,       /* lista (duplo clique): value = indice */
    GK_EVENT_RIGHT_CLICK = 4,  /* SDK 3: lista de dados, value = registro */
    GK_EVENT_DOUBLE_CLICK = 5, /* SDK 3: lista de dados, value = registro */
    GK_EVENT_OPEN = 6          /* SDK 5: a janela abriu (id 0, depois do on_open); bind_event com id 0 */
};

/* Nas listas de dados, "registro" = indice na lista filtrada (list_value) e o value dos comandos troca {coluna} pelo
 * valor do registro ("talknpc {x} {y}") */
enum GkActionKind
{
    GK_ACTION_COMMAND = 1,    /* value = comando do console do OpenKore ("ai manual") */
    GK_ACTION_LUA = 2,        /* value = funcao Lua global, chamada com (id, evento, valor) */
    GK_ACTION_DRAG_ITEM = 3,  /* SDK 3: arrasta o item do registro; value = coluna do ID ("id") */
    GK_ACTION_ITEM_INFO = 4,  /* SDK 3: descricao do item do registro (a do clique direito no inventario) */
    GK_ACTION_ITEM_DROPS = 5, /* SDK 3: monstros que dropam o item do registro (Divine Pride) */
    GK_ACTION_CLEAR = 6,      /* SDK 3: apaga o campo de texto; value = id do campo */
    GK_ACTION_HIDE = 7        /* SDK 4: esconde partes do mapa; value = "jogadores". Liga/desliga: marcado esconde;
                               * os outros alternam */
};

/* SDK 4: partes do mapa que set_hidden esconde, so na tela do jogador (o servidor e os outros nao mudam) */
enum GkWorldPart
{
    GK_WORLD_PLAYERS = 1 /* os outros jogadores (o seu personagem fica) */
};

/* SDK 5: atores do mapa atual (actors), somaveis */
enum GkActorKind
{
    GK_ACTOR_PLAYER = 1,  /* outros jogadores */
    GK_ACTOR_NPC = 2,
    GK_ACTOR_MONSTER = 4,
    GK_ACTOR_OTHER = 8    /* homunculo, mercenario */
};

typedef struct GkActor
{
    size_t size;       /* sizeof(GkActor) de quem chama */
    unsigned id;       /* AID */
    int kind;          /* GkActorKind */
    int x, y;          /* celula */
    int job;           /* classe exibida */
    int distance;      /* celulas ate o seu personagem */
    char name[32];     /* CP1252; vazio ate o cliente saber (jogador/NPC: pedido ao servidor como o mouse em cima) */
} GkActor;

/* SDK 3: ancoras (set_anchor e GkCell.anchor). So direita: acompanha a borda direita; esquerda + direita: estica */
enum GkAnchor
{
    GK_ANCHOR_LEFT = 1,
    GK_ANCHOR_TOP = 2,
    GK_ANCHOR_RIGHT = 4,
    GK_ANCHOR_BOTTOM = 8
};

/* SDK 3: celula do modelo de linha de uma lista de dados */
enum GkCellKind
{
    GK_CELL_TEXT = 1,
    GK_CELL_ITEM_ICON = 2, /* icone do item como no inventario */
    GK_CELL_IMAGE = 3,
    GK_CELL_BUTTON = 4,    /* um por linha visivel; clique avisa on_event(id da celula, GK_EVENT_CLICK, registro) */
    GK_CELL_RECT = 5
};

enum GkCellFlags
{
    GK_CELL_BOLD = 1,
    GK_CELL_RIGHT = 2,      /* texto alinhado a direita: x = borda direita */
    GK_CELL_FIT = 4,        /* texto cortado com "..." na largura */
    GK_CELL_DROPS_ONLY = 8  /* so com a apiKey do Divine Pride no GordoKore.ini */
};

typedef struct GkCell
{
    size_t size;               /* sizeof(GkCell): o host le so o que conhece */
    int id;                    /* 1..0x7000, unico na janela (botao: id do evento) */
    int kind;                  /* GkCellKind */
    int x, y, width, height;   /* na linha */
    GkText text;               /* texto ou legenda; "{coluna}" vira o valor do registro */
    const char *image;         /* IMAGE: caminho ("{coluna}" vale); BUTTON: bitmap normal (nulo = botao padrao) */
    const char *hover;         /* BUTTON com bitmap: mouse em cima (nulo = normal) */
    const char *press;         /* BUTTON com bitmap: apertado (nulo = normal) */
    const char *item;          /* ITEM_ICON: ID do item, ex.: "{id}" */
    unsigned color;            /* TEXT/RECT: 0xRRGGBB */
    int flags;                 /* GkCellFlags */
    int anchor;                /* GK_ANCHOR_RIGHT acompanha a borda direita da lista; LEFT|RIGHT estica; 0 = esquerda */
} GkCell;

typedef struct GkWindowDef
{
    const char *key; /* unico entre todos os modulos: guarda a posicao (GordoKore.ini) */
    GkText title;
    int width;
    int height;
    /* Botao no menu do GordoKore (opcional): BMP 43x43, #FF00FF transparente; label nulo = titulo */
    const unsigned char *menu_icon_bmp;
    size_t menu_icon_size;
    GkText menu_label;
    /* Callbacks (todos opcionais). on_open: criar os widgets. on_draw: depois dos widgets desenhados */
    void (*on_open)(GkWin win, void *user);
    void (*on_close)(void *user);
    void (*on_event)(GkWin win, int id, int event, int value, void *user);
    void (*on_draw)(GkWin win, void *user);
    void (*on_tick)(GkWin win, void *user); /* a cada frame com a janela aberta */
    void *user;
} GkWindowDef;

typedef struct GkHost
{
    int version; /* GK_SDK_VERSION do host */
    size_t size; /* sizeof(GkHost) do host */

    /* Janelas: define (so no gk_module_init) e devolve o handle; < 0 = erro */
    int (*define_window)(const GkWindowDef *def);
    /* Acao ligada a um widget (vale para todas as aberturas da janela) */
    void (*bind)(int window, int id, int kind, const char *value);
    void (*open)(int window);
    void (*close)(int window);
    void (*toggle)(int window);
    int (*is_open)(int window);

    /* Widgets (no on_open), coordenadas na janela; o corpo comeca em y = 17 (abaixo do titulo) */
    void (*add_label)(GkWin win, int id, int x, int y, GkText text, unsigned color, int bold);
    void (*add_button)(GkWin win, int id, int x, int y, int width, GkText text); /* width 0 = texto + 16; altura 20 */
    void (*add_image_button)(GkWin win, int id, int x, int y, int width, int height, const char *normal, const char *hover,
                             const char *press);
    void (*add_checkbox)(GkWin win, int id, int x, int y, int checked); /* liga/desliga 34x15 */
    void (*add_edit)(GkWin win, int id, int x, int y, int width, int height, int max_chars, GkText hint);
    void (*add_listbox)(GkWin win, int id, int x, int y, int width, int height);
    void (*add_image)(GkWin win, int id, int x, int y, const char *path);
    void (*add_sprite)(GkWin win, int id, int x, int y, int width, int height, const char *name); /* npc\ ou monstro; base no fundo da caixa */
    void (*add_rect)(GkWin win, int id, int x, int y, int width, int height, unsigned color);

    /* Estado dos widgets */
    const char *(*get_text)(GkWin win, int id);           /* campo de texto ou label; valido ate a proxima chamada */
    void (*set_text)(GkWin win, int id, const char *text); /* label ou campo de texto */
    int (*get_checked)(GkWin win, int id);
    void (*set_checked)(GkWin win, int id, int checked); /* sem GK_EVENT_CHANGE */
    void (*list_add)(GkWin win, int id, const char *text);
    int (*list_selected)(GkWin win, int id); /* -1 = nenhum */
    void (*set_visible)(GkWin win, int id, int visible);
    void (*invalidate)(GkWin win);

    /* Desenho livre (no on_draw); cor 0xRRGGBB */
    void (*draw_text)(GkWin win, int x, int y, const char *text, unsigned color, int bold);
    void (*draw_image)(GkWin win, int x, int y, const char *path);
    void (*fill_rect)(GkWin win, int x, int y, int width, int height, unsigned color);
    int (*text_width)(GkWin win, const char *text, int bold);

    /* Utilidades */
    void (*kore_command)(const char *command); /* comando no console do OpenKore */
    int (*language)(void);                     /* GkLanguage */
    const char *(*tr)(GkText text);            /* texto no idioma atual */
    void (*log)(const char *text);

    /* Script Lua do modulo (so no gk_module_init; um por modulo): roda o corpo e guarda as funcoes globais
     * para as acoes GK_ACTION_LUA. 0 = ok. API no script, sempre na janela do evento:
     *   gk.command(s)  gk.get(id)  gk.set(id, s)  gk.checked(id)  gk.check(id, bool)
     *   gk.list_add(id, s)  gk.selected(id)  gk.visible(id, bool)  gk.lang() ("pt"/"es"/"en")
     *   gk.tr(pt, es, en)  gk.log(s)  gk.open(chave)  gk.close([chave])  gk.toggle(chave)
     * SDK 3 (listas de dados): gk.count(id)  gk.value(id, registro, coluna)  gk.source(id, "itens")
     *   gk.rows(id, {"id", "nome"}, {{1, "um"}, {2, "dois"}})
     * SDK 4 (mapa): gk.hide("jogadores", bool)  gk.hidden("jogadores")
     * SDK 5 (mundo): gk.me()  gk.players([raio])  gk.npcs([raio])  gk.monsters([raio])  gk.actor(id)  gk.map()
     *   -> {id, nome, tipo, x, y, classe, classe_nome, distancia}; timers gk.every(ms, f)  gk.after(ms, f)  gk.cancel(t)
     *   (fora de evento, as funcoes de widget agem na primeira janela aberta do modulo)
     * SDK 6 (rede): gk.http_get(url, f) -> f(ok, corpo, status) depois; gk.json(texto) -> valor | nil, erro
     *   (textos do JSON passam de UTF-8 para CP1252)
     * Bibliotecas: base (sem dofile/loadfile), string, table, math, utf8. */
    int (*lua_load)(const char *source, const char *name);

    /* --- Versao 2 --- */

    /* Area rolavel (no on_open): moldura com a barra de rolagem nativa na direita (15 px). Os widgets postos
     * nela com set_parent usam x/y relativos ao conteudo da area; a altura do conteudo vai ate o widget mais
     * baixo. Widget que passa da borda de cima ou de baixo some inteiro (sem corte parcial). A roda do mouse
     * na janela rola a primeira area. */
    void (*add_scroll_area)(GkWin win, int id, int x, int y, int width, int height);
    void (*set_parent)(GkWin win, int id, int area); /* depois de criar os dois; area 0 = volta para a janela */

    /* --- Versao 3: listas de dados, janela redimensionavel e mais widgets --- */

    /* Como bind, so naquele evento (GkEvent): clique, clique direito e duplo clique de uma linha com acoes diferentes */
    void (*bind_event)(int window, int id, int event, int kind, const char *value);

    /* Lista de dados (no on_open): as celulas (add_cell) desenham cada registro numa linha de row_height px; barra
     * nativa na direita (15 px) e roda do mouse. Clique, clique direito e duplo clique numa linha avisam o id da
     * lista com value = registro. Textos da janela com "{id.total}" mostram quantos registros passam no filtro
     * ("{id.all}": todos). */
    void (*add_data_list)(GkWin win, int id, int x, int y, int width, int height, int row_height);
    void (*add_cell)(GkWin win, int list, const GkCell *cell);
    /* Tabela do jogo: "itens" (id, nome), "monstros" (id, nome, mapas, sprite) ou "mapas" (nome) */
    void (*set_source)(GkWin win, int list, const char *source);
    /* Registros do modulo (troca a fonte): colunas separadas por \t, registros por \n, em CP1252 */
    void (*set_rows)(GkWin win, int list, const char *columns, const char *rows);
    /* Filtra ao digitar no campo `edit`: o registro fica se alguma das colunas (separadas por ;) contem o texto sem
     * diferenciar maiusculas; so digitos tambem casam o comeco da coluna "id" */
    void (*set_filter)(GkWin win, int list, int edit, const char *columns);
    int (*list_count)(GkWin win, int list); /* registros que passam no filtro */
    /* Valor de uma coluna (list = lista ou celula dela); valido ate a proxima chamada; "" fora da lista */
    const char *(*list_value)(GkWin win, int list, int record, const char *column);

    /* Botao de imagem com legenda (o "Limpar" da Agencia de Aventura); tamanho do bitmap; hover/press nulos = normal */
    void (*add_caption_button)(GkWin win, int id, int x, int y, const char *normal, const char *hover, const char *press, GkText text);
    /* Faixa de 3 imagens: esquerda, meio repetido e direita (rodapes, barras) */
    void (*add_strip)(GkWin win, int id, int x, int y, int width, const char *left, const char *mid, const char *right);
    /* Icone do item como no inventario (24x24) */
    void (*add_item_icon)(GkWin win, int id, int x, int y, int item);

    /* Janela redimensionavel (no gk_module_init, depois do define_window): redimensionador no canto, altura em passos
     * de `step` px a partir de min_height (0 = livre). O tamanho do define_window é o do projeto: os widgets seguem a
     * ancora (set_anchor) quando a janela muda. */
    void (*set_resizable)(int window, int min_width, int min_height, int max_width, int max_height, int step);
    void (*set_anchor)(GkWin win, int id, int anchor); /* GkAnchor; padrao esquerda + topo */

    /* --- Versao 4: mapa --- */

    /* Esconde (hidden != 0) ou mostra as partes do mapa (GkWorldPart, somadas), so na tela do jogador; vale ate fechar
     * o jogo, em todos os mapas */
    void (*set_hidden)(int parts, int hidden);
    int (*hidden)(void); /* partes escondidas agora (GkWorldPart) */

    /* --- Versao 5: mundo (lido do cliente a cada chamada; thread do jogo) --- */

    /* Atores dos tipos pedidos (GkActorKind somados), sem o seu personagem, do mais perto ao mais longe; preenche ate
     * max e devolve quantos existem. Lista de dados: set_source "jogadores_no_mapa", "npcs_no_mapa" ou
     * "monstros_no_mapa" (ao vivo; colunas id, nome, x, y, classe, classe_nome, distancia) */
    int (*actors)(int kinds, GkActor *out, int max);
    int (*actor)(unsigned id, GkActor *out); /* 1 se esta no mapa (inclusive o seu) */
    int (*me)(GkActor *out);                 /* 1 dentro do mapa */
    const char *(*map_name)(void);           /* "prontera"; "" fora do mapa; vale ate a proxima chamada */

    /* --- Versao 6: rede --- */

    /* GET https:// assincrono: done roda depois, na thread do jogo, com o corpo como veio (ok = HTTP 2xx; status 0 =
     * nao chegou e body = a mensagem do erro). Fila com 1 requisicao por segundo, 10 s de limite, ate 1 MB. Devolve o id
     * (> 0), ou 0 se recusou (URL sem https:// ou fila cheia) */
    int (*http_get)(const char *url, void (*done)(int ok, int status, const char *body, size_t size, void *user), void *user);
} GkHost;

/* Primeira linha do gk_module_init: recusa host mais velho que o header do modulo */
#define GK_CHECK_HOST(host)                                                     \
    do                                                                          \
    {                                                                           \
        if (!(host) || (host)->version < GK_SDK_VERSION || (host)->size < sizeof(GkHost)) \
            return -1;                                                          \
    } while (0)

/* Exportadas pelo modulo. gk_module_init devolve 0 se ok; gk_module_shutdown é opcional */
GK_EXPORT int gk_module_init(const GkHost *host);
GK_EXPORT void gk_module_shutdown(void);

#endif

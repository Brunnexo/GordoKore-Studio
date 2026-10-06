// Codigo do modulo: o GordoKore Studio cria este arquivo uma vez e nunca reescreve.
// Ids dos widgets e indices das janelas: module.gen.h. API: gordokore_sdk.h.
#include "module.gen.h"

namespace
{
    bool g_sentado = false;

    void estado(GkWin win, GkText text)
    {
        g_host->set_text(win, painel_bot::kEstado, g_host->tr(text));
    }
}

int user_init(const GkHost *host)
{
    host->log("painel_bot carregado");
    return 0;
}

// Chega depois das acoes de comando/Lua: os botoes de IA ja mandaram o comando, aqui so atualiza o texto
void user_event(int window, GkWin win, int id, int event, int value)
{
    (void)value;
    if (window != painel_bot::kWindow || event != GK_EVENT_CLICK)
        return;
    switch (id)
    {
    case painel_bot::kIaAuto:
        estado(win, {"Estado: IA autom\xE1tica", "Estado: IA autom\xE1tica", "State: auto AI"});
        break;
    case painel_bot::kIaManual:
        estado(win, {"Estado: IA manual", "Estado: IA manual", "State: manual AI"});
        break;
    case painel_bot::kSentar: // acao C++: nada ligado no estudio, so este codigo
        g_sentado = !g_sentado;
        g_host->kore_command(g_sentado ? "sit" : "stand");
        if (g_sentado)
            estado(win, {"Estado: sentado", "Estado: sentado", "State: sitting"});
        else
            estado(win, {"Estado: em p\xE9", "Estado: de pie", "State: standing"});
        break;
    }
}

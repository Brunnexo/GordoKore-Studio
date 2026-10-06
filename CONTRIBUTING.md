# Como contribuir

Obrigado pelo interesse! Bugs, ideias e pull requests são bem-vindos.

## Antes de abrir uma issue

- **Bug:** diga a versão do estúdio (**Ajuda > Sobre**) e a do GordoKore, o passo a passo para reproduzir e, se der, anexe o `.gkproj`. Se o jogo fechou ou reiniciou, as linhas `[MOD]` do console do GordoKore e o arquivo `Ragexe.*.dmp` da pasta do jogo ajudam muito.
- **Ideia:** conte o que você quer fazer no jogo, e não só a peça que falta. Muitas vezes dá para chegar lá com as peças que já existem.

## Compilar

Precisa do **.NET 10 SDK** no Windows.

```bat
build.bat
src\GordoKore.Studio\bin\Release\net10.0-windows\GordoKore.Studio.exe --check
```

O `--check` precisa terminar com `tudo ok` (saída 0). Com o jogo e o MinGW na máquina, ele também lê a GRF e compila um módulo de verdade. Mudou algo que não dá para conferir na interface? Cubra com uma verificação no `Checks.cs`.

## Convenções

- **Textos da interface:** em português, com acento.
- **Comentários no código:** em português, curtos, sem acento, menos o verbo "é", que leva acento (nunca `e'`).
- **Commits:** em português, no imperativo ou descrevendo a mudança ("Lista de dados: busca pelos nomes das colunas").
- **SDK:** o `src/GordoKore.Studio/sdk/gordokore_sdk.h` é cópia do header do GordoKore. Ele só cresce no fim da tabela `GkHost`, e um módulo compilado com um header mais novo é recusado por um GordoKore mais velho.
- **Lua:** função nova no `gk` vai também para o [manual](docs/manual-lua.md).

## Pull requests

Um assunto por pull request, com o `--check` passando. Para mudança grande, abra uma issue antes para combinarmos o caminho.

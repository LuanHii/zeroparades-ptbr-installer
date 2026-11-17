# Zero Parades PT-BR — Instalador

Instalador gráfico em **C# (.NET 8 + Avalonia UI + AssetsTools.NET)** para aplicar arquivos `.po` de tradução em *Zero Parades: For Dead Spies* (compatível com a versão **Director's Cut** e `catalog.bin`).

Este repositório contém **apenas o código-fonte do instalador** — nenhum arquivo ou texto do jogo é incluído aqui.

## Como usar (usuário final)

1. Baixe o instalador (`ZeroParadesPTBR.exe` + dependências) e o arquivo `my_translation.po` no Nexus Mods.
2. Coloque o `my_translation.po` na **mesma pasta** do `ZeroParadesPTBR.exe`.
3. Execute `ZeroParadesPTBR.exe` e clique em **Instalar Tradução**.
4. Abra o jogo e selecione **Deutsch / Alemão** em **Settings → Language**.

## Compilar e testar localmente

```powershell
dotnet test ZeroParadesPTBR.Tests/ZeroParadesPTBR.Tests.csproj
dotnet publish ZeroParadesPTBR/ZeroParadesPTBR.csproj -c Release -r win-x64 --self-contained true -o dist/ZeroParadesPTBR_Windows
```

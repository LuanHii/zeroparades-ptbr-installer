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

## Assinatura gratuita com SignPath.io

1. Crie um repositório **público** no GitHub contendo apenas o conteúdo desta pasta (`installer/`).
2. Cadastre-se no [SignPath.io](https://signpath.io/) (plano gratuito *Open Source*) e conecte o repositório.
3. No GitHub (`Settings → Secrets and variables → Actions`):
   - **Secret:** `SIGNPATH_API_TOKEN`
   - **Variables:**
     - `SIGNPATH_ENABLED` = `true`
     - `SIGNPATH_ORGANIZATION_ID` = seu Organization ID do SignPath
     - `SIGNPATH_PROJECT_SLUG` = slug do projeto no SignPath (ex.: `zeroparades-ptbr-installer`)
     - `SIGNPATH_SIGNING_POLICY_SLUG` = `release-signing` (ou o slug da sua política)
4. Crie uma tag `v1.0.0` (ou execute o workflow manualmente em **Actions → Build and Sign Installer**).

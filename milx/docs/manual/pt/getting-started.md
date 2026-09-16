---
title: Primeiros passos
section: Start
order: 2
summary: Instalar a aplicação, abri-la pela primeira vez e um primeiro projeto de ponta a ponta.
---

# Primeiros passos

## Onde conseguir

Toda release traz uma versão para cada plataforma, em
[github.com/pedronobrez/mil-x/releases](https://github.com/pedronobrez/mil-x/releases). Todas são
autocontidas: o runtime .NET viaja dentro, então nada precisa ser instalado antes.

| Plataforma | Arquivo | O que é |
| --- | --- | --- |
| macOS, Apple silicon | `MIL-X-<versão>-macos-arm64.dmg` | o bundle da aplicação, para arrastar até Applications |
| Windows x64 | `MIL-X-<versão>-windows-x64.msi` | um instalador, por usuário, sem administrador |
| Windows x64 | `MIL-X-<versão>-windows-x64.zip` | a mesma aplicação sem instalador |
| Linux x86_64 | `MIL-X-<versão>-linux-x86_64.tar.gz` | uma pasta para descompactar e rodar |

Todas elas leem `.wiff` nativamente: os componentes da SCIEX que o Apêndice A da licença deles
nomeia como redistribuíveis viajam em `plugins/sciex`, com essa licença ao lado. Nada mais precisa
ser instalado para isso — nem Analyst, nem ProteoWizard. Veja [[raw-data-formats]].

O `SHA256SUMS` na página da release tem a soma de verificação de cada arquivo.

## Instalar no macOS

O MIL-X é distribuído como um bundle de aplicação autocontido, `MIL-X.app`. Ele carrega o
seu próprio runtime .NET e o leitor nativo de `.wiff` em `Contents/MacOS/plugins/sciex`. Nada
mais precisa ser instalado para ler arquivos mzML ou `.wiff`.

1. Copie `MIL-X.app` para `/Applications`. Se você mesmo o construiu, use `ditto` em vez de
   arrastar no Finder, para que a assinatura fique intacta:
   ```bash
   ditto milx/dist/MIL-X.app /Applications/MIL-X.app
   ```
2. Abra-o. Um bundle construído na mesma máquina abre de imediato. Uma cópia que chegou por
   download ou AirDrop carrega a marca de quarentena do macOS e, como o bundle não é notarizado,
   precisa de **clique direito ▸ Open ▸ Open** uma vez; depois disso abre normalmente.
3. No primeiro lançamento a janela abre vazia, na área de trabalho Samples, com a marca e uma
   dica.

A aplicação registra os tipos de documento que entende, de modo que a partir daí o Finder abre
projetos `.milx` (e `.odproj` de antes da 1.0), projetos `.mdproject`, lotes `.oqproj` e arquivos
brutos (`.mzML`, `.wiff`, `.raw`, …) no MIL-X com um duplo clique ou um arrasto sobre o ícone. Veja [[projects-and-files]] e
[[raw-data-formats]] para o que cada um faz.

## Instalar no Windows

O `.msi` instala em `%LOCALAPPDATA%\MIL-X` **para o usuário atual**, então nenhum administrador
entra na história — o que importa num laboratório onde quem analisa o dado raramente é quem tem a
senha de administrador. Ele cria entrada no menu Iniciar, aparece em **Adicionar ou remover
programas**, e uma atualização substitui a versão anterior em vez de ficar ao lado dela.

1. Dê duplo clique no `.msi`. O Windows mostra **"Windows protected your PC"**, porque a build não
   tem certificado de assinatura de código: **More info ▸ Run anyway**. Só um certificado remove
   esse aviso.
2. Inicie pelo menu Iniciar, ou dê duplo clique em qualquer projeto `.milx` — o instalador
   registra essa extensão, com o ícone da aplicação, e se oferece para o `.odproj`.
3. O `.mdproject` continua com o MS-DIAL, que provavelmente está instalado na mesma máquina; o
   MIL-X só se acrescenta à lista **Abrir com** desse tipo de arquivo.

O `.zip` é a mesma aplicação sem instalador e sem nada escrito no registro: descompacte onde
quiser e rode `MIL-X.exe`. Desbloqueie o zip antes de extrair (clique direito ▸ **Propriedades
▸ Desbloquear**) ou o Windows marca cada arquivo de dentro dele.

Se o OpenDIAL 0.9 estiver instalado, o instalador do MIL-X o substitui: mesmo código de
atualização, nome novo, pasta nova. Nada nos seus projetos precisa mudar.

O próprio MS-DIAL 5 roda no Windows, e lá ele faz mais do que este port faz — mobilidade iônica e
imaging entre as coisas. A versão Windows existe para que uma máquina com Windows possa abrir e
continuar uma revisão começada num Mac ou no Linux, e para que um laboratório misto compartilhe um
conjunto só de arquivos.

## Instalar no Linux

Descompacte o tarball e rode o binário:

```bash
tar -xzf MIL-X-<versão>-linux-x86_64.tar.gz
cd MIL-X-<versão>-linux-x86_64
./MIL-X
```

Se o bit de execução não sobreviveu à cópia, `chmod +x MIL-X` o devolve. O que a distribuição
precisa fornecer são as bibliotecas cliente do X11 e o fontconfig, que todo Linux de desktop já
tem; num servidor ou imagem de container pelada:

```bash
apt-get install -y libx11-6 libice6 libsm6 libfontconfig1 libicu-dev   # Debian, Ubuntu
dnf install -y libX11 libICE libSM fontconfig libicu                   # Fedora, RHEL
```

Não há entrada de desktop no tarball: crie uma apontando para o binário se quiser vê-lo no menu
de aplicações.

Construir a partir do código-fonte, para qualquer uma das três, está em [[building-and-testing]].

## A janela em um minuto

Uma janela, cinco áreas de trabalho ao longo do topo, uma barra de status ao longo da base. `⌘1` a
`⌘5` (ou `Ctrl+1` a `Ctrl+5`) alternam entre elas:

| Área de trabalho | Para que serve |
| --- | --- |
| **Explorer** | os arquivos brutos como cromatogramas e espectros, antes ou depois do processamento |
| **Analytics** | a revisão de um resultado processado: a tabela de íons e a evidência de cada feature |
| **Method** | os parâmetros de processamento |
| **Samples** | o lote: quais arquivos, o que é cada um |
| **Statistics** | o dataset como um todo: componentes principais, correção de deriva, modelos, agrupamento, a rede molecular |

O resto da casca — menus, a barra de status, o log, a faixa de progresso — está descrito em
[[shell]].

## Um primeiro projeto, de ponta a ponta

1. **File ▸ New project…** (`⌘N`). Dê-lhe um nome e uma pasta; o arquivo de projeto e a sua pasta
   de resultados são criados dentro de uma pasta com esse nome. Veja [[new-project-wizard]].
2. **Add data files…** ou **Add folder…**. Todo arquivo bruto suportado na pasta é adicionado, uma
   linha por injeção; um `.wiff` com várias amostras vira uma linha por amostra. Defina o **Type**
   de cada linha (Sample, Blank, QC, Standard) e a sua **Class** — o grupo que as estatísticas
   comparam. Ambos podem ser definidos para uma seleção de uma vez a partir da barra de
   ferramentas. Veja [[samples-workspace]].
3. Escolha o método: os padrões de LC-MS (DDA, positivo, centroide), os padrões de GC-MS, ou um
   arquivo de método de uma corrida anterior. Aponte-o para uma biblioteca espectral MSP se tiver
   uma; sem ela toda feature fica desconhecida. Veja [[method-workspace]] e [[method-parameters]].
4. **Create project**, com **Process the batch right away** marcado, ou pressione `⌘R` mais tarde.
   A faixa de progresso mostra a etapa e o arquivo; o log (`⌘L`) mostra cada linha que o motor
   imprime. Oito aquisições de ZenoTOF levam alguns minutos; a primeira leitura de um arquivo de
   fabricante é a parte lenta e fica em cache para a próxima vez. Veja [[processing]].
5. Quando termina, a janela pousa em **Analytics** com a tabela de íons preenchida. Selecione uma
   feature, olhe o seu pico em cada amostra, o seu MS/MS contra a biblioteca, os seus candidatos;
   marque-a com `⌘⇧1` a `⌘⇧5` ou com **Confirm ▸** e **Reject ▸**. **Save review** grava as
   marcações no arquivo que o MS-DIAL lê. Veja [[analytics-workspace]].
6. **Export reviewed table…** grava o que a tabela está mostrando, com a revisão em colunas.
   **Export to MIL-Q…** transforma a lista num método dirigido. Veja [[exports]].

Tudo o que você fez está no arquivo de projeto: reabra-o a partir de **File ▸ Recent projects** e
os resultados, a revisão e o método voltam.

## Abrir algo que já existe

- Um projeto MIL-X (`.milx`, ou `.odproj` de antes da 1.0) ou um projeto MS-DIAL (`.mdproject`): **File ▸ Open project…** (`⌘O`), ou a partir do Finder.
- Uma pasta de resultados escrita pelo console ou pelo MS-DIAL, sem arquivo de projeto: **File ▸ Open results folder…**. O lote é reconstruído a partir dos arquivos de resultado.
- Um arquivo bruto, ou uma pasta deles, só para olhar: solte-o sobre a aplicação, ou **File ▸ Add data files…**. Ele abre no Explorer, sem processar.
- Um lote do OpenQuant (`.oqproj`): **File ▸ Import MIL-Q batch…** adiciona as suas amostras com os seus tipos e grupos. Veja [[openquant]].

Um projeto processado abre em Analytics; um não processado, em Samples.

## Vindo do OpenDIAL

O MIL-X 1.0 é o OpenDIAL sob o seu nome definitivo. Tudo o que você salvou continua abrindo: um
projeto `.odproj` é lido como está e gravado como `.milx` na próxima vez que você salvar; uma
pasta de resultados não é tocada; os seus projetos recentes, tema e disposição de colunas vêm
junto no primeiro início. No macOS, apague o `OpenDIAL.app` quando o `MIL-X.app` estiver no lugar,
ou o Finder continua oferecendo os dois. A lista completa do que foi renomeado está em
[[versions#1.0.0 — setembro de 2026]].

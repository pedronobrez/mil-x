---
title: Primeiros passos
section: Start
order: 2
summary: Instalar a aplicação, abri-la pela primeira vez e um primeiro projeto de ponta a ponta.
---

# Primeiros passos

## Instalar no macOS

O OpenDIAL é distribuído como um bundle de aplicação autocontido, `OpenDIAL.app`. Ele carrega o
seu próprio runtime .NET e, quando foi construído com o SDK da SCIEX, o leitor nativo de `.wiff`
em `Contents/MacOS/plugins/sciex`. Nada mais precisa ser instalado para ler arquivos mzML ou
`.wiff`.

1. Copie `OpenDIAL.app` para `/Applications`. Se você mesmo o construiu, use `ditto` em vez de
   arrastar no Finder, para que a assinatura fique intacta:
   ```bash
   ditto opendial/dist/OpenDIAL.app /Applications/OpenDIAL.app
   ```
2. Abra-o. Um bundle construído na mesma máquina abre de imediato. Uma cópia que chegou por
   download ou AirDrop carrega a marca de quarentena do macOS e, como o bundle não é notarizado,
   precisa de **clique direito ▸ Open ▸ Open** uma vez; depois disso abre normalmente.
3. No primeiro lançamento a janela abre vazia, na área de trabalho Samples, com a marca e uma
   dica.

A aplicação registra os tipos de documento que entende, de modo que a partir daí o Finder abre
projetos `.odproj` e `.mdproject`, lotes `.oqproj` e arquivos brutos (`.mzML`, `.wiff`, `.raw`, …)
no OpenDIAL com um duplo clique ou um arrasto sobre o ícone. Veja [[projects-and-files]] e
[[raw-data-formats]] para o que cada um faz.

Construir a partir do código-fonte, e as versões Linux e Windows, estão em
[[building-and-testing]].

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
   marque-a com `Ctrl+1` a `Ctrl+5` ou com **Confirm ▸** e **Reject ▸**. **Save review** grava as
   marcações no arquivo que o MS-DIAL lê. Veja [[analytics-workspace]].
6. **Export reviewed table…** grava o que a tabela está mostrando, com a revisão em colunas.
   **Export to OpenQuant…** transforma a lista num método dirigido. Veja [[exports]].

Tudo o que você fez está no arquivo de projeto: reabra-o a partir de **File ▸ Recent projects** e
os resultados, a revisão e o método voltam.

## Abrir algo que já existe

- Um projeto OpenDIAL (`.odproj`) ou um projeto MS-DIAL (`.mdproject`): **File ▸ Open project…** (`⌘O`), ou a partir do Finder.
- Uma pasta de resultados escrita pelo console ou pelo MS-DIAL, sem arquivo de projeto: **File ▸ Open results folder…**. O lote é reconstruído a partir dos arquivos de resultado.
- Um arquivo bruto, ou uma pasta deles, só para olhar: solte-o sobre a aplicação, ou **File ▸ Add data files…**. Ele abre no Explorer, sem processar.
- Um lote do OpenQuant (`.oqproj`): **File ▸ Import OpenQuant batch…** adiciona as suas amostras com os seus tipos e grupos. Veja [[openquant]].

Um projeto processado abre em Analytics; um não processado, em Samples.

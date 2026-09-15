---
title: A área de trabalho Samples
section: Workspaces
order: 11
summary: A tabela do lote — quais arquivos estão no projeto, o que é cada injeção, e como rotular muitas de uma vez.
---

# A área de trabalho Samples

O lote: uma linha por injeção, com tudo o que a corrida e as estatísticas precisam saber sobre
ela. `⌘4` mostra-a. Um projeto sem resultados abre aqui; um projeto processado mantém-na a uma
aba de distância.

![a tabela do lote](images/samples.png)

## A barra de ferramentas

| Controle | O que faz |
| --- | --- |
| **Add data files…** | escolhe arquivos brutos; cada um vira uma linha, um `.wiff` com várias amostras vira várias |
| **Add folder…** | todo arquivo bruto suportado diretamente dentro de uma pasta, por ordem de nome; a única forma de adicionar pastas `.d` da Agilent ou da Bruker |
| **Import OpenQuant batch…** | as amostras de um `.oqproj` com o seu tipo, grupo, diluição e comentário; veja [[openquant]] |
| **Remove** | as linhas selecionadas |
| **Clear all** | todas as linhas |
| **Set type of selected** ▸ **Apply** | o tipo escolhido em todas as linhas selecionadas |
| **Set class of selected** ▸ **Apply** | a classe digitada em todas as linhas selecionadas; a caixa completa a partir das classes já no lote |

As linhas selecionam-se com um clique, `⇧`-clique para um intervalo, `⌘`-clique para acrescentar
uma. A mensagem ao lado da barra de ferramentas relata o que a última ação fez — quantas foram
adicionadas, que arquivos foram ignorados por não suportados, um `.wiff` que não pôde ser aberto.

## As colunas

| Coluna | Significado |
| --- | --- |
| **#** | a ordem analítica, renumerada quando linhas são removidas |
| **Use** | desmarque para manter uma linha no lote mas deixá-la fora da corrida |
| **File** | o nome do arquivo; pairar mostra o caminho completo, e `(file not found)` quando falta |
| **Sample** | o nome que os resultados carregam; por padrão o nome do arquivo, ou o nome da amostra dentro de um lote `.wiff` |
| **Type** | Sample, Blank, QC ou Standard — veja [[concepts#O lote]] |
| **Class** | o grupo que as estatísticas comparam; texto livre, `1` até que você o defina |
| **Acquisition** | DDA, SWATH ou AIF — como o MS/MS foi adquirido; veja [[processing#Tipos de aquisição]] |
| **Polarity** | Positive ou Negative, adivinhada pelo nome do arquivo e editável. Um lote com as duas é processado como duas corridas e os resultados são emparelhados — veja [[polarity-merge]] |
| **Order** | a ordem de injeção, editável, para a correção de deriva |
| **Batch** | a sequência em que a injeção foi corrida, para a correção de deriva |
| **Factor** | o segundo fator de um desenho de dois fatores — o ponto no tempo, a dieta, o genótipo — texto livre, com a mesma grafia em toda injeção de um nível; lido pela página **Two factors** da área Statistics. Deixe vazio num desenho de um fator |
| **Dilution** | um fator guardado para registro e exportado |
| **Comment** | texto livre |
| **Format** | como o arquivo será lido: `mzML`, `wiff · native`, `raw · msconvert`, `d · msconvert`, `ABF`, …; âmbar quando o msconvert é necessário |

A linha de status sob a tabela conta as injeções por tipo, as classes, quantas amostras `.wiff`
são lidas nativamente, quantos arquivos passam pelo msconvert, e quantos faltam.

## Tipos e classes, e por que ambos

O **tipo** diz o que a injeção é fisicamente; a **classe** diz a que grupo pertence na
comparação. Um lote de extratos de fígado contra brancos pode ter tipos `Sample, Sample, Sample,
Blank, Blank` e classes `liver, liver, liver, blank, blank`; um QC *pooled* injetado a cada dez
corridas tem tipo `QC` e, em geral, classe `qc`. A correção de deriva lê o tipo — corrige contra
toda injeção com tipo `QC` — e os modelos leem a classe. Veja
[[statistics-workspace#Drift correction]].

A rotulagem é feita uma vez e guardada no projeto. Um lote OpenQuant traz os seus rótulos
consigo: `Quality Control` vira `QC`, `Blank`, `Double Blank` e `Solvent` viram `Blank`, e o grupo
do OpenQuant vira a classe.

## Arquivos .wiff com várias amostras

Um `.wiff` da SCIEX pode guardar várias injeções. Quando o leitor nativo está presente, o arquivo
é expandido em uma linha por amostra ao ser adicionado; cada linha mantém o nome e o índice da sua
amostra, e a corrida lê a certa. Nos bastidores, uma pasta `wiff-samples` ao lado do arquivo guarda
um link por amostra (`name.s2.wiff`), porque o motor identifica uma injeção pelo seu caminho. Veja
[[raw-data-formats#SCIEX .wiff]].

## Lotes não processados

Um lote pode ser olhado antes de ser processado: o Explorer desenha qualquer arquivo nele.
**Process batch** (`⌘R`) a partir de qualquer área de trabalho inicia a corrida; recusa com uma
mensagem quando não há arquivos, quando a biblioteca nomeada no método não existe, ou quando uma
corrida já está em curso.

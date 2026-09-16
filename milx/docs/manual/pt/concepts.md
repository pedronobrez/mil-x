---
title: Conceitos e vocabulário
section: Start
order: 3
summary: As palavras que a aplicação usa — injeção, classe, feature, alinhamento, nível de anotação, marcação — e o que é igual ao MS-DIAL.
---

# Conceitos e vocabulário

A aplicação usa um vocabulário pequeno e fixo. A maior parte é do MS-DIAL; onde o MIL-X tem uma
palavra própria, isso é dito. O [[glossary]] tem as definições curtas; esta página explica as
ideias.

## O lote

Uma **injeção** — uma **amostra** (*sample*) na área de trabalho Samples — é um arquivo bruto, ou
uma amostra dentro de um arquivo de lote `.wiff` com várias amostras. O lote é a lista de injeções
que um projeto processa em conjunto.

Cada injeção tem um **tipo**, que diz o que ela é fisicamente: **Sample**, **Blank**, **QC** (uma
injeção de controle de qualidade *pooled*, o mesmo material todas as vezes) ou **Standard**. O tipo
é o que a correção de deriva e as vistas de abundância leem: os controles são o tipo `QC`, o fundo
é o tipo `Blank`. Veja [[statistics-workspace#Drift correction]].

Cada injeção também tem uma **classe**, texto livre, que é o grupo que as estatísticas comparam:
`liver`, `plasma`, `treated`, `control`. O MS-DIAL também a chama de classe. Tudo o que é agrupado
"por classe" — as barras de abundância, as estatísticas por classe, os modelos — agrupa por ela.

A **ordem analítica** é a posição na sequência de injeção; o número de **lote** (*batch*) agrupa as
injeções que foram corridas numa mesma sequência. Ambos importam para a correção de deriva e para
mais nada. **Dilution** e **comment** são guardados para registro e escritos nas exportações.

## O método

O **método** é o conjunto completo de parâmetros de processamento — tipo de dados, detecção de
picos, deconvolução, anotação, alinhamento — como um único arquivo de texto de linhas
`Chave: valor`, o formato que o console do próprio MS-DIAL lê. A área de trabalho Method edita-o
como formulário e como texto. Veja [[method-workspace]] e [[method-parameters]].

## O que o processamento produz

Para cada injeção, a **detecção de picos** (*peak picking*) encontra **picos**: um pico
cromatográfico num m/z, com tempo de retenção, altura do ápice, área e início e fim. A
**deconvolução** (o MS2Dec do MS-DIAL) atribui a cada pico um espectro de íons-produto limpo. A
**anotação** compara esse espectro com a biblioteca e dá nome ao pico.

O **alinhamento** então casa os picos entre as injeções em **features** — o MS-DIAL chama-as de
**alignment spots**, e as duas palavras aparecem na interface. Uma feature tem um tempo de retenção
e um m/z, um nome, e um pico por injeção; onde uma injeção não tinha pico, o **preenchimento de
lacunas** (*gap filling*) integra o cromatograma naquele lugar mesmo assim e marca o valor como
preenchido. A **porcentagem de preenchimento** (*fill*) é a fração de injeções em que o pico foi de
fato detectado.

A injeção **representativa** de uma feature é aquela cujo pico pontuou melhor contra a
biblioteca; o seu espectro e o seu nome são os da feature.

## Níveis de anotação

O MS-DIAL escreve a confiança de uma anotação no próprio nome, e o MIL-X lê-a de volta como a
coluna **level** da tabela de íons:

| O nome diz | Nível | Significado |
| --- | --- | --- |
| `PC 34:1` | confident | o MS/MS casou com um registro da biblioteca acima de todos os cortes |
| `low score: PC 34:1` | suggested | um casamento abaixo do corte de pontuação total, guardado como pista |
| `no MS2: PC 34:1` | m/z only | sem espectro de produto; o nome apoia-se só na massa do precursor |
| `Unknown`, `w/o MS2: …` | *(desconhecido)* | nada casou |

O nome do lipídio em si não é o nome do registro da biblioteca: o MS-DIAL reescreve-o a partir dos
fragmentos que de fato viu, de modo que um registro vira `LPC 16:0` quando só a espécie está
suportada e `LPC 16:0/0:0` quando uma cadeia está. Veja [[annotation]].

## A revisão

Uma **marcação** (*tag*) é uma das cinco bandeiras de revisão do MS-DIAL — Confirmed, Low quality
spectrum, Misannotation, Coelution, Overannotation — com os mesmos nomes e os mesmos ids
numéricos, de modo que uma revisão feita aqui aparece no MS-DIAL e uma feita lá aparece aqui. Uma
feature está **revisada** quando carrega qualquer marcação, ou quando o revisor a marcou como tal
sem nenhuma. Veja [[review-tags]].

**Curadoria** é tudo o que o revisor muda: marcações, comentários, um nome escolhido à mão, um pico
reintegrado, um isômero separado. As marcações vão para o arquivo de marcações do próprio MS-DIAL;
o resto vai para um arquivo lateral (*sidecar*) do MIL-X, ou — no caso dos picos — de volta ao
próprio resultado do alinhamento. Veja [[projects-and-files]].

## Projetos e resultados

Um **projeto MIL-X** (`.milx`) é um pequeno arquivo JSON: o lote, o texto do método, a pasta
de resultados e o caminho do projeto MS-DIAL quando há um. Um **projeto MS-DIAL** (`.mdproject`
com o seu `.mddata`) é o que o motor escreve: os parâmetros, a lista de arquivos e a biblioteca,
no formato do próprio MS-DIAL, que a aplicação Windows também abre. A **pasta de resultados**
guarda ambos, mais todo arquivo intermediário e exportado. Veja [[projects-and-files]].

## O que é igual ao MS-DIAL

- O motor: detecção de picos, MS2Dec, os anotadores, o alinhador, o preenchimento de lacunas, os exportadores — tudo código upstream, versão 5.5.260817, sem alterações.
- O arquivo de método e cada parâmetro dele.
- O projeto (`.mdproject`), os resultados por arquivo (`.pai2`, `.dcl`) e o resultado do alinhamento (`.arf2`), legíveis pela aplicação Windows.
- As cinco marcações e o arquivo de marcações.
- As exportações: `.mdpeak`, `.mdmsp`, `.mdalign`, `.qa.tsv`, `.mzTab`.

## O que é próprio do MIL-X

- A camada de dados brutos: o leitor de mzML, o leitor nativo de `.wiff`, a ponte msconvert. Veja [[raw-data-formats]].
- A aplicação desktop e as suas cinco áreas de trabalho.
- O arquivo lateral de comentário, nome escolhido à mão e bandeira de revisado; a exportação da tabela revisada; a interoperabilidade com o OpenQuant.
- A área de trabalho de estatística: componentes principais, agrupamento, rede molecular, correção de deriva, os modelos discriminante e ortogonal.
- O cache de varreduras de survey e o arquivo de projeto MIL-X.

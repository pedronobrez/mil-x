---
title: Projetos e arquivos
section: Data and files
order: 30
summary: O arquivo de projeto do OpenDIAL, o projeto MS-DIAL, todo arquivo de uma pasta de resultados e o que cada um guarda, os arquivos de curadoria, as cópias de segurança, e como os caminhos são mantidos.
---

# Projetos e arquivos

## O projeto OpenDIAL (.odproj)

Um pequeno arquivo JSON — o lote, o método e onde estão os resultados — que reabre a sessão
inteira. Vive ao lado dos resultados, é escrito pelo assistente e por **Save project** (`⌘S`), e
é o que **File ▸ Recent projects** lista.

```json
{
  "Version": 1,
  "Name": "liver-cohort-A",
  "Mode": "LCMS",
  "OutputFolder": "results",
  "MdprojectPath": "results/Project-2609071200.mdproject",
  "MethodText": "#Data type\nMS1 data type: Centroid\n…",
  "Samples": [
    {
      "Path": "../raw/260406-Teste-51-I.wiff",
      "Name": "260406-Teste-51-I",
      "Class": "liver",
      "SampleType": "Sample",
      "Acquisition": "DDA",
      "AnalyticalOrder": 1,
      "Batch": 1,
      "Dilution": 1,
      "Comment": "",
      "Included": true,
      "SampleIndex": 0
    }
  ]
}
```

| Campo | Significado |
| --- | --- |
| `Mode` | `LCMS` ou `GCMS`: que motor a corrida usa |
| `OutputFolder` | onde a corrida grava; relativo ao arquivo de projeto quando está perto |
| `MdprojectPath` | o projeto MS-DIAL da última corrida, quando há um; de onde **Open project** carrega os resultados |
| `MethodText` | o arquivo de método inteiro, literal; veja [[method-workspace]] |
| `Samples[].Path` | o arquivo bruto, relativo quando está a menos de duas pastas do projeto, absoluto senão |
| `Samples[].SampleType` | `Sample`, `Blank`, `QC` ou `Standard` |
| `Samples[].Acquisition` | `DDA`, `SWATH` ou `AIF` |
| `Samples[].SampleIndex` | que amostra de um `.wiff` multiamostra esta linha é (0 para qualquer outro arquivo) |

Os caminhos são escritos relativos quando ficam dentro de uma árvore próxima — um projeto movido
junto com os seus arquivos brutos ainda abre — e absolutos quando um caminho relativo subiria três
pastas ou mais, o que não é mais portátil. Abrir o projeto resolve-os de novo.

## O projeto MS-DIAL (.mdproject + .mddata)

O que o motor grava no fim de uma corrida: `Project-<yyMMddHHmm>.mdproject`, um zip com os
parâmetros, a lista de arquivos, a lista de arquivos de alinhamento e a biblioteca, com o `.mddata`
ao lado. O MS-DIAL no Windows abre-o; o OpenDIAL abre-o com **Open project…** ou pelo Finder, e
reconstrói o lote a partir dele quando não há `.odproj`. É o único arquivo que leva a biblioteca
dentro de si, e por isso um projeto aberto desta forma consegue buscar na sua biblioteca sem o
arquivo MSP estar na máquina.

**Um projeto pode mudar de lugar.** Os caminhos dentro dele são absolutos — onde o conjunto de
dados e os arquivos brutos estavam quando ele foi gravado — e não significam nada em outra máquina:
um nome de usuário diferente já basta, e um caminho do Windows nem sequer é um caminho no macOS ou
no Linux. Quando o caminho gravado não existe, o OpenDIAL procura o conjunto de dados pelo nome ao
lado do arquivo de projeto, um nível acima, e nas pastas vizinhas, que é onde uma corrida copiada o
guarda. Assim uma pasta de resultados entregue num pendrive abre, e uma copiada de uma máquina
Windows também. O que ele não acha é um conjunto de dados deixado para trás: copie a pasta inteira,
não só o `.mdproject`.

## A pasta de resultados

Tudo o que uma corrida grava, numa pasta. `<sample>` é o nome da amostra, `<stamp>` um carimbo de
tempo da corrida.

**Por injeção**

| Arquivo | O que guarda |
| --- | --- |
| `<sample>_<stamp>.pai2` | os picos detectados (a informação de área de pico do MS-DIAL, MessagePack) |
| `<sample>_<stamp>.dcl` | o espectro de MS/MS deconvoluído de todo pico |
| `<sample>_<stamp>_tags.xml` | as marcações por pico, que a corrida grava vazias |
| `<sample>_<stamp>.rtc` | a correção de tempo de retenção, quando uma foi calculada |
| `<sample>.mdpeak` | a exportação da tabela de picos |
| `<sample>.mdmsp` | a exportação dos espectros de MS/MS |
| `<sample>.mdscan` | corridas de GC-MS: a tabela de varreduras |

**O alinhamento**

| Arquivo | O que guarda |
| --- | --- |
| `AlignResult-<stamp>.arf2` | o resultado do alinhamento: toda feature com os seus picos por injeção — o contentor que a revisão edita |
| `AlignResult-<stamp>_PeakProperties.arf2` | as propriedades de pico por injeção das features |
| `AlignResult-<stamp>_DriftSopts.arf2` | os spots de tempo de deriva (mobilidade iônica; vazio senão) |
| `AlignResult-<stamp>.EIC.aef` | os cromatogramas de íon extraído que o alinhador guardou |
| `AlignResult-<stamp>.dcl` | o MS/MS representativo de toda feature |
| `AlignResult-<stamp>_tags.xml` | as marcações da revisão, no esquema do MS-DIAL; veja [[review-tags]] |
| `AlignResult-<stamp>_curation.json` | o arquivo lateral do OpenDIAL: comentários, nomes escolhidos à mão, bandeiras de revisado |
| `AlignResult-<stamp>.mdalign` | a exportação da tabela de alinhamento |
| `AlignResult-<stamp>.qa.tsv` | a matriz de QA, formato longo |
| `AlignResult-<stamp>.mdmsp` | a exportação do MS/MS representativo |
| `AlignResult-<stamp>.mzTab` | mzTab-M |
| `*.before-curation` | os arquivos de alinhamento como estavam antes da primeira edição à mão de uma sessão |

**A corrida**

| Arquivo | O que guarda |
| --- | --- |
| `opendial_method.txt` | o método que a corrida usou |
| `Project-<stamp>.mdproject`, `.mddata` | o projeto MS-DIAL |

O contentor é gravado como `.arf2` enquanto a lista de arquivos do MS-DIAL o nomeia `.arf`; as
duas grafias referem-se ao mesmo arquivo, e as cópias de segurança guardam a que existir.

## O arquivo de marcações

`<alignment>_tags.xml` é o do próprio MS-DIAL:

```xml
<PeakSpotTags>
  <Definitions>
    <Tag><Id>1</Id><Label>Confirmed</Label></Tag>
    …
  </Definitions>
  <Peaks>
    <Peak Id="123"><Tag>1</Tag></Peak>
    <Peak Id="456"><Tag>4</Tag><Tag>5</Tag></Peak>
  </Peaks>
</PeakSpotTags>
```

Os ids são os do MS-DIAL (1 Confirmed, 2 Low quality spectrum, 3 Misannotation, 4 Coelution, 5
Overannotation). Um arquivo de marcações ilegível é ignorado em vez de impedir os resultados de
abrir.

## O arquivo lateral de curadoria

`<alignment>_curation.json` guarda o que o MS-DIAL guarda dentro dos seus arquivos binários, de
modo que uma revisão só de marcações nunca os reescreve:

```json
{ "Version": 1, "Spots": [ { "Id": 123, "Comment": "shoulder", "ManualName": "PC 34:1", "Reviewed": true } ],
  "InternalStandards": { "PC": 88, "PE": 412 } }
```

Só os spots com algo a dizer são listados; o arquivo é apagado quando não resta nada.
`InternalStandards` é o padrão escolhido para cada classe de lipídio na área Statistics, como o
alignment ID da feature confirmada (veja [[internal-standards]]); uma classe definida como nenhum
está ausente.

## Cópias de segurança

A primeira edição à mão de uma sessão — uma reintegração ou uma separação — copia o contentor do
alinhamento, as suas propriedades de pico e os seus spots de deriva para `<name>.before-curation`
antes de **Save review** os sobrescrever. Edições posteriores na mesma sessão não substituem a
cópia, de modo que ela guarda sempre o resultado como a corrida o produziu. Para desfazer toda
edição, renomeie as cópias de volta sobre os originais.

## Abrir uma pasta de resultados sem projeto

**File ▸ Open results folder…** lê o que houver: o `.mdproject` mais recente quando há um, senão
os próprios arquivos `.pai2`. No segundo caso o lote é reconstruído com os nomes das amostras, os
arquivos brutos encontrados ao lado pelo nome, uma classe por arquivo, e o método de
`opendial_method.txt` quando existe — o bastante para revisar, não o bastante para correr de novo
sem conferir o lote primeiro.

## Onde a aplicação guarda os seus próprios arquivos

As configurações (`settings.json`), o cache de varreduras de survey e o cache de mzML convertido
estão descritos em [[caches-and-storage]] e [[settings]].

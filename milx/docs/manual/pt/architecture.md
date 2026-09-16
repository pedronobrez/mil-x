---
title: Arquitetura
section: Under the hood
order: 51
summary: Como o port está montado — o motor upstream, a camada aberta de dados brutos, a biblioteca de pipeline, a aplicação de desktop — e as decisões por trás dele.
---

# Arquitetura

## As peças

| Projeto | O que é |
| --- | --- |
| **MS-DIAL upstream** (`MsdialWorkbench-MSDIAL-v5.5.260817/`) | o motor, mantido quase intocado para que uma versão mais nova possa ser trocada no lugar: seis arquivos são corrigidos, e `upstream-patches/` guarda o diff |
| **MilX.RawData** | a camada aberta de dados brutos, um substituto direto do pacote fechado `RawDataHandler`: os mesmos tipos públicos nos mesmos namespaces, de modo que todo projeto upstream compila sem alteração com um interruptor do MSBuild (`UseOpenRawData=true`). Contém o leitor de mzML, a ponte msconvert, o carregador de plugins e a ponte para a dll legada |
| **MilX.Plugins.SciexWiff** | o leitor nativo de `.wiff` sobre o SDK Clearcore2 da SCIEX, carregado de `plugins/sciex` |
| **MilX.Pipeline** | a biblioteca de orquestração sem janela, adaptada do console do MS-DIAL: corre o fluxo com progresso e cancelamento, abre resultados, lê-os de volta para as vistas, edita e grava o alinhamento, contém o depósito de curadoria, a estatística e o cache de varreduras de survey |
| **MilX.Interop.OpenQuant** | o leitor de CSV de componentes e de `.oqproj` do OpenQuant |
| **MilX.Desktop** | a aplicação Avalonia: view models sobre o pipeline, os gráficos próprios, o manual |
| **tests/** | cinco suítes; veja [[building-and-testing]] |
| **tools/** | as ferramentas de diagnóstico; veja [[command-line]] |

## Por que uma camada aberta de dados brutos

O MS-DIAL lê dados brutos por um pacote NuGet fechado cuja construção com suporte a fabricantes
envolve SDKs só para Windows; o repositório distribui apenas a construção "vendor unsupported", que
lê mzML por um leitor opaco que descarta o último pico de todo espectro. `MilX.RawData` expõe a
mesma API e reproduz tudo de que o código a jusante depende — ordem dos espectros, convenções de
índice, conversões de unidade, espectros acumulados — enquanto corrige o que estava errado e
acrescenta o que faltava (Numpress, inteiros de 64 bits, grupos de parâmetros, mobilidade iônica).
Os formatos de fabricante são tratados como mzML que ainda não foi produzido e passam pelo
msconvert uma vez; o `.wiff` da SCIEX é lido nativamente porque o SDK é código gerenciado. Veja
[[raw-data-formats]].

## Por que uma aplicação de desktop nova

A GUI do MS-DIAL são 66 000 linhas de WPF sobre uma camada de desenho própria, sem implementação
para macOS ou Linux e sem caminho automático para o Avalonia. A aplicação do MIL-X é nova e
menor, em Avalonia 11 com CommunityToolkit.Mvvm, e reutiliza a janela e a paleta do OpenQuant para
que os dois se leiam como uma suíte. Reutiliza o motor, as classes de parâmetros, os arquivos de
projeto e os exportadores; não reutiliza a camada de modelo upstream.

## A cultura invariante

O MS-DIAL lê e formata números com a cultura da thread em cerca de 170 lugares. Numa localização
de vírgula decimal o leitor fechado transformava um tempo de varredura de `0.6` s em `6000` s e o
pipeline encontrava silenciosamente zero picos. Todo ponto de entrada do MIL-X — a aplicação, o
console, os testes — força primeiro a cultura invariante, e a camada de dados brutos lê sempre de
forma invariante.

## As seis correções upstream

| Arquivo | Alteração |
| --- | --- |
| `CommonStandard/MessagePack/LargeListMessagePack.cs` | lê até a contagem pedida estar no buffer; um `Stream.Read` curto de uma entrada zip chegava ao decodificador LZ4 inseguro e derrubava o processo quando um projeto com uma biblioteca grande era aberto |
| `MsdialCore/MsdialCore.csproj` | as referências ao pacote `RawDataHandler` são condicionais a `UseOpenRawData`, que acrescenta o projeto aberto no lugar |
| `MsdialCore/DataObj/EadLipidSqliteDatabase.cs` | compila contra `Microsoft.Data.Sqlite` sob o interruptor, porque `System.Data.SQLite` não tem biblioteca nativa para Apple Silicon |
| `MsdialCoreTestApp/Program.cs` | força a cultura invariante |
| `MsdialCoreTestApp/Process/CommonProcess.cs` | os arquivos importados de uma pasta recebem o tipo de aquisição do projeto; deixado em `None`, um quarto das atribuições de MS/MS de um conjunto IDA se perdia |
| `MsdialCoreTestApp/Parser/AnalysisFilesParser.cs` | `Path.Combine` em vez de uma barra invertida, que é um caractere de nome de arquivo no macOS |

## Ler e gravar resultados

As vistas nunca tocam diretamente nos objetos do motor. `ResultLoader` lê os arquivos `.pai2`,
`.dcl` e `.arf2` em linhas planas (`AlignmentSpotRow`, `AlignedSamplePeak`, `SampleInfo`) que a
tabela de íons, os painéis e a estatística partilham, guardando uma referência aos objetos do
próprio motor para que uma edição à mão possa ser gravada de volta pelo serializador do MS-DIAL. A
curadoria vive em `CurationStore`, que lê e grava o arquivo de marcações e o arquivo lateral.

## A sonda e o canal de comandos

Para o smoke test a janela consegue dizer o que está mostrando: com `MILX_UI_PROBE` definida,
um instantâneo JSON — título, área de trabalho, estado da corrida, a feature selecionada, onde
estão os controles nomeados — é gravado sempre que algo muda, e um arquivo `.commands` ao lado é
vigiado para as poucas ações que passam pelos diálogos do próprio sistema operativo. Nada disto
corre no uso comum. Veja [[building-and-testing#O smoke test]].

## Mais

O relatório de engenharia reversa da árvore upstream, o grafo de dependências, a superfície do
componente fechado e os desvios deliberados estão em `docs/ARCHITECTURE.md`; as decisões em
`docs/adr/`; as descobertas sobre IDA da SCIEX em `docs/SCIEX-IDA.md`; o estado do port em
`docs/PORTING.md`.

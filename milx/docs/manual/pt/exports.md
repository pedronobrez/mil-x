---
title: Exportações
section: Reviewing
order: 25
summary: Toda forma de um resultado sair da aplicação — a tabela revisada, a matriz reexportada, a lista de componentes do OpenQuant, as tabelas de análise e os gráficos, as tabelas da rede, e o que a própria corrida grava.
---

# Exportações

## A tabela revisada

**Export reviewed table…** na barra de ferramentas de Analytics grava as features que o filtro
está mostrando, na ordem da tabela, como texto separado por tabulações, com a revisão em colunas
próprias. A exportação de alinhamento do próprio MS-DIAL não tem coluna de marcações — dobra as
marcações no comentário de texto livre e não consegue exprimir *revisada* — de modo que este é o
arquivo a levar para uma planilha ou um script de estatística.

As colunas: `Alignment ID`, `Average RT(min)`, `Average m/z`, `Metabolite name` (o escolhido à mão
quando há um), `Annotation level` (confident, suggested, m/z only, ou vazio), `Adduct`,
`Ontology`, `Formula`, `INCHIKEY`, `Isotope` (`M`, `M+1`, …), `Fill %`, `MS/MS assigned`, `S/N
average`, `Total score`, `Representative file`, `Reviewed`, `Tags` (rótulos separados por ponto e
vírgula), `Comment`, `Manually annotated`, `Manually quantified`, e depois uma coluna por injeção
com a sua altura de pico. A segunda linha nomeia a classe de cada injeção, como as matrizes do
MS-DIAL fazem. Os números são escritos com ponto como separador decimal seja qual for a
localização da máquina.

O nome de arquivo sugerido é `<project>_reviewed.txt`, na pasta de resultados.

## A matriz de alinhamento

**Process ▸ Re-export alignment matrix…** (também **Re-export…** na barra de ferramentas) grava
toda feature, com filtro ou sem, com a sua identidade — `Alignment ID`, `Name`, `RT (min)`, `m/z`,
`Adduct`, `Formula`, `Ontology`, `Score`, `Fill %` — seguida de uma coluna de altura e uma de área
por injeção, em TSV. É o caminho mais rápido para uma matriz completa sem as colunas de
estatística do MS-DIAL.

## A lista de componentes do OpenQuant

**Export to OpenQuant…** transforma as features listadas num CSV de componentes do OpenQuant —
precursor, fragmento mais forte, janela de retenção — de modo que uma lista de descoberta vira um
método dirigido num passo. As opções e as colunas estão em [[openquant]].

## As tabelas de análise

Toda página da [[one-factor-analysis]] que produz uma tabela tem um botão **Export table…** ao
lado das suas opções, e a página **Data processing** tem **Export normalised data…**. Todas gravam
texto separado por tabulações com uma linha de cabeçalho, números com ponto decimal, num arquivo
com o nome da tabela:

| Arquivo | Colunas |
| --- | --- |
| `normalized-data.tsv` | `Feature id`, `Feature`, `Class`, depois uma coluna por injeção com o valor transformado — o conjunto de dados que toda página lê, pronto para outra ferramenta |
| `comparison.tsv` | `Feature id`, `Feature`, `Class`, as médias das duas classes, `Fold change`, `log2 FC`, `Statistic`, `p`, e o p ajustado sob o nome do ajuste (`FDR`, `Holm`, `Bonferroni`) |
| `anova.tsv` | `Feature id`, `Feature`, `Class`, `Statistic`, `p`, o p ajustado, e `Post hoc` — todo par com o seu p, separados por ponto e vírgula |
| `pattern-search.tsv` | `Feature id`, `Feature`, `Class`, `Correlation`, `p` |
| `random-forest.tsv` | `Feature id`, `Feature`, `Class`, `Mean decrease accuracy`, `Mean decrease Gini` |
| `enrichment.tsv` | `Set`, `Kind`, `Size`, `Hits`, `Expected`, `Enrichment ratio`, `p`, o p ajustado, e `Members` — as features significativas do conjunto, separadas por ponto e vírgula |
| `two-way-anova.tsv` | `Feature id`, `Feature`, `Class`, depois `F`, `p` e o p ajustado do fator A, do fator B e (quando testada) da interação, e a média de toda célula do desenho |
| `reactions.tsv` | `Reaction` (o id da tabela), `Reactant`, `Product`, a variação log2 do peso (primeira classe sobre a segunda), `p`, `Z`, `Status`, `Genes`, `Enzyme` — da página [[pathways]] |
| `pathways.tsv` | `Pathway` (a cadeia), `Reactions` (o seu comprimento), `Z`, `Status`, `Genes` |
| `predicted-reactions.tsv` | `Reaction`, `Measured`, `Not measured`, `Genes` — as reações que mais uma classe confirmada abriria |

`Feature id` é o alignment ID do MS-DIAL, o mesmo número da tabela revisada e da tabela de íons,
de modo que as tabelas se juntam. Os próprios padrões internos não estão nas tabelas de razões.

## O relatório da corrida

**Run report…** na barra da Analytics escreve um arquivo HTML: as contagens, o que a biblioteca
traz, as injeções com a sua classe e tipo, o método inteiro, o fim do log da corrida, e as figuras
que estavam na tela — o espelho, o cromatograma e o espectro do Explorer, e todo gráfico emoldurado
da área Statistics. As figuras vão em vetor embutido, de modo que o arquivo se basta e continua
nítido em qualquer tamanho. Abra no navegador e imprima em PDF.

Um arquivo de método sozinho não é um relatório: o que um leitor confere primeiro é quantas features
saíram, quantas ganharam nome, e como estavam as injeções.

## Os gráficos

Todo gráfico da aplicação grava-se como **SVG** ou como **PNG** a duas até seis vezes o seu tamanho
em tela — por **Export…** no canto superior direito de um gráfico de Statistics, ou por **Export as
a picture…** no menu de botão direito de qualquer outro. O diálogo também escolhe o tema em que a
figura é desenhada, de modo que uma figura clara sai de uma janela escura; veja [[chart-export]].

## As tabelas da rede

**Export for Cytoscape…** na área Statistics grava a tabela de arestas da rede molecular (id de
origem, id de destino, similaridade, diferença de massa) no arquivo escolhido e a tabela de nós
(id, nome, classe, tempo de retenção, m/z, altura, anotada) ao lado com um sufixo `_nodes`, ambas
separadas por tabulações, prontas para a importação do Cytoscape.

## O que a corrida grava

Toda corrida grava as exportações do próprio MS-DIAL na pasta de resultados sem que se peça:

| Arquivo | O que é |
| --- | --- |
| `<sample>.mdpeak` | a tabela de picos de uma injeção: todo pico detectado com o seu tempo de retenção, m/z, altura, área, aduto, isótopo, anotação e pontuações |
| `<sample>.mdmsp` | os espectros de MS/MS deconvoluídos dessa injeção, em formato MSP |
| `AlignResult-<stamp>.mdalign` | a tabela de alinhamento: toda feature com a sua identidade, o MS/MS representativo, e a altura de cada injeção, com média e desvio padrão por classe |
| `AlignResult-<stamp>.qa.tsv` | a matriz de QA em formato longo — altura, RT, m/z, S/N, MS/MS e bandeiras de casamento com referência por feature e injeção; desligada quando *Export QA height matrix* do método está desmarcado |
| `AlignResult-<stamp>.mdmsp` | o MS/MS representativo de toda feature |
| `AlignResult-<stamp>.mzTab` | o resultado como mzTab-M |
| `milx_method.txt` | o método que a corrida usou |
| `Project-<stamp>.mdproject` + `.mddata` | o projeto MS-DIAL |

As corridas de GC-MS gravam tabelas de varreduras `<sample>.mdscan` em vez de tabelas de picos.
Todo arquivo está listado em [[projects-and-files#A pasta de resultados]].

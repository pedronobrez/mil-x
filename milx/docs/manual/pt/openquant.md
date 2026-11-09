---
title: MIL-Q (OpenQuant)
section: Data and files
order: 32
summary: O que o MIL-X partilha com o MIL-Q — o OpenQuant de hoje —, importar um lote dele, e exportar uma lista de componentes para quantificação dirigida.
---

# MIL-Q (OpenQuant)

**MIL-Q** é a metade dirigida da família: a aplicação que foi o OpenQuant até a sua 1.0, hoje em
1.0.x sob o próprio nome. Os botões dizem MIL-Q; os formatos de arquivo (`.oqproj`, o CSV de
componentes) foram definidos sob o nome OpenQuant e não mudaram, e é por isso que o código de
interoperação e alguns nomes de arquivo ainda o carregam. O MIL-Q é uma aplicação de quantificação
dirigida; o MIL-X é processamento untargeted sobre o motor do MS-DIAL. Foram feitos para parecer
duas áreas de trabalho de uma só suíte — a mesma paleta, a mesma janela de abas e faixas, a mesma
leitura nativa de `.wiff`, a mesma calculadora de massas e a mesma convenção de sinal-ruído — e o
fluxo entre eles é *descobrir no MIL-X, quantificar no MIL-Q*. Os dois também partilham um contrato
de [[command-line]], de modo que um script pode pedir a qualquer um o mesmo cromatograma e
interpretar a mesma resposta.

## Importar um lote do OpenQuant

**File ▸ Import MIL-Q batch (.oqproj)…**, o botão **Import MIL-Q batch…** na área Samples,
ou abrir um `.oqproj` ou `.opvproj` pelo Finder, adiciona ao lote as amostras do projeto OpenQuant
com o que o OpenQuant sabia delas:

| OpenQuant | MIL-X |
| --- | --- |
| caminho, índice de amostra | o arquivo bruto e, para um `.wiff` multiamostra, a amostra certa |
| nome | o nome da amostra |
| tipo de amostra `Quality Control` | tipo **QC** |
| `Blank`, `Double Blank`, `Solvent` | tipo **Blank** |
| `Standard` | tipo **Standard** |
| qualquer outro | tipo **Sample** |
| grupo de amostra | a **classe**; o tipo de amostra quando não há grupo |
| fator de diluição, comentário | as mesmas colunas |

Os caminhos no projeto são resolvidos relativamente a ele. As linhas cujos arquivos não existem são
adicionadas mesmo assim e listadas num aviso, para que o lote possa ser corrigido em vez de
redigitado. O projeto toma o nome do `.oqproj` quando ainda não tem nenhum.

## Exportar uma lista de componentes

**Export to MIL-Q…** na barra de ferramentas de Analytics, ou **Process ▸ Export to
OpenQuant…**, grava as features que a tabela de íons está mostrando como um CSV de componentes do
OpenQuant, com os cabeçalhos que a própria tabela de componentes do OpenQuant grava e lê. O arquivo
abre no OpenQuant por **Method ▸ Import components**.

As opções:

| Opção | Padrão | Significado |
| --- | --- | --- |
| **Annotated spots only** | ligada | salta os desconhecidos |
| **Fill the fragment column from the representative MS/MS** | ligada | o íon-produto mais intenso pelo menos 2 Da abaixo do precursor vira o `fragment` — um componente ao estilo MRM-HR; sem ela o componente é só precursor |
| **RT half-window (min)** | 0,3 | a coluna `window`: metade da janela de retenção em que o OpenQuant integra |
| **Mass tolerance** | 0,02 Da | as colunas `tolerance` e `unit`, Da ou ppm |

Cada feature vira uma linha: `name` (a anotação, ou `m/z 760.5851 @ 11.40 min` para um
desconhecido), `group` (a ontologia, `annotated`, ou `unknown`), `precursor`, `fragment`, `rt`,
`window`, `tolerance`, `unit`, `formula`, `adduct`, `response` (`area`), e as restantes colunas do
OpenQuant (`is`, `internal_standard`, `concentration_unit`, `qualifier_of`, `ion_ratio`,
`ion_ratio_tolerance`, `regression`, `weighting`, `lm_id`, `min_response`) deixadas vazias para o
MIL-Q preencher, e `provenance` (`MIL-X alignment feature 17`, para que a trilha de auditoria do
MIL-Q diga de onde veio o componente). O cabeçalho tem toda coluna que o leitor de componentes do
MIL-Q tem na 1.0.6; um teste mantém-no igual à lista do próprio MIL-Q, e carrega um arquivo
exportado pelo leitor do MIL-Q quando há um checkout à mão (`MILQ_REPO`).

Filtre primeiro: a lista é o que a tabela mostra, de modo que um filtro de classe ou **Confirmed**
no estado da revisão dá uma lista de componentes com exatamente as features por que você
respondeu.

## O que deliberadamente não é partilhado

Curvas de calibração, razões com padrão interno, qualificadores de razão de íons e critérios de
aceitação definem a quantificação dirigida e ficam no OpenQuant. O índice LIPID MAPS e o buscador
de fórmulas do OpenQuant também ficam lá: o MS-DIAL traz a sua própria anotação de lipídios, e duas
respostas seriam pior do que uma.

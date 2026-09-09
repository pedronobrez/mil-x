---
title: OpenQuant
section: Data and files
order: 32
summary: O que o OpenDIAL partilha com o OpenQuant, importar um lote do OpenQuant, e exportar uma lista de componentes para quantificação dirigida.
---

# OpenQuant

O OpenQuant é uma aplicação de quantificação dirigida; o OpenDIAL é processamento untargeted sobre
o motor do MS-DIAL. Foram feitos para parecer duas áreas de trabalho de uma só suíte — a mesma
paleta, a mesma janela de abas e faixas, a mesma leitura nativa de `.wiff` — e o fluxo entre eles
é *descobrir no OpenDIAL, quantificar no OpenQuant*.

## Importar um lote do OpenQuant

**File ▸ Import OpenQuant batch (.oqproj)…**, o botão **Import OpenQuant batch…** na área Samples,
ou abrir um `.oqproj` ou `.opvproj` pelo Finder, adiciona ao lote as amostras do projeto OpenQuant
com o que o OpenQuant sabia delas:

| OpenQuant | OpenDIAL |
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

**Export to OpenQuant…** na barra de ferramentas de Analytics, ou **Process ▸ Export to
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
`ion_ratio_tolerance`, `regression`, `weighting`, `lm_id`) deixadas vazias para o OpenQuant
preencher.

Filtre primeiro: a lista é o que a tabela mostra, de modo que um filtro de classe ou **Confirmed**
no estado da revisão dá uma lista de componentes com exatamente as features por que você
respondeu.

## O que deliberadamente não é partilhado

Curvas de calibração, razões com padrão interno, qualificadores de razão de íons e critérios de
aceitação definem a quantificação dirigida e ficam no OpenQuant. O índice LIPID MAPS e o buscador
de fórmulas do OpenQuant também ficam lá: o MS-DIAL traz a sua própria anotação de lipídios, e duas
respostas seriam pior do que uma.

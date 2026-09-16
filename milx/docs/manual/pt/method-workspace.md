---
title: A área de trabalho Method
section: Workspaces
order: 12
summary: Os parâmetros de processamento como formulário e como o arquivo de método por trás dele; carregar, salvar e restaurar.
---

# A área de trabalho Method

Cada parâmetro que a corrida usa, como um formulário à esquerda e — com **Advanced text** ligado —
como o arquivo de método em texto de que o formulário é uma vista. `⌘3` mostra-a. Os dois são uma
coisa só: uma edição no formulário reescreve o texto, e uma edição no texto é lida de volta para o
formulário quando a caixa perde o foco, com qualquer erro mostrado por baixo em vermelho.

![o formulário do método](images/method.png)

## A barra de ferramentas

| Controle | O que faz |
| --- | --- |
| **Load method file…** | substitui o método por um arquivo `.txt` ou `.method`; um arquivo que diz `Machine category: GCMS` muda o modo |
| **Save method file…** | grava o método atual, compatível com o console |
| **Reset to defaults** | os padrões do modo atual |
| **Mode** | LC-MS ou GC-MS; muda que seções o arquivo carrega e que motor corre |
| **Advanced text** | mostra o arquivo de método ao lado do formulário |

A linha de status sob o formulário resume-o: `LC-MS · DDA · positive · library.msp · 3 parameters
changed from defaults`.

## A biblioteca

Sob o grupo de identificação, um painel diz o que o `.msp` escolhido realmente traz — quantos
registros, que adutos, que faixa de massa, e se carrega tempos de retenção e em que janela. É lido
do próprio arquivo, em segundo plano, sempre que o caminho muda; uma biblioteca de quatrocentos mil
registros leva um ou dois segundos.

| Controle | O que faz |
| --- | --- |
| **Re-read** | lê o arquivo de novo, depois de ele ter sido trocado em disco |
| **Ignore its retention times** | deixa os tempos da própria biblioteca fora da pontuação e da filtragem. É o padrão de um método novo |
| **Calibrate to this run…** | ajusta os tempos da biblioteca aos que o resultado atual mediu para os compostos que nomeou, grava `<biblioteca>-rt-calibrated.msp` ao lado da original, aponta o método para ela e devolve a retenção à pontuação |

Um aviso aparece em vermelho quando a janela de retenção da biblioteca não se sobrepõe à do método.
É o caso comum com biblioteca pública, e pontuar com ela custa nomes em vez de comprar confiança —
veja [[annotation#Por que tão poucas features são nomeadas]]. A calibração precisa de um resultado
processado com pelo menos vinte nomes em comum com a biblioteca; ela informa em quantos compostos
foi ajustada e o R² do ajuste.

## O formulário

As seções, na ordem em que aparecem. Cada campo é explicado, com o seu padrão e o que faz ao
resultado, em [[method-parameters]].

**Data type** — modo de ionização, tipo de dados de MS1 e MS2 (centroide ou perfil), o tipo de
aquisição padrão para arquivos novos, a ômica alvo (metabolômica ou lipidômica).

**Data collection** — as faixas de tempo de retenção e de massa que a corrida lê, e as tolerâncias
de centroide.

**Peak detection** — suavização, largura e altura mínimas de pico, largura da fatia de massa,
carga máxima, os adutos procurados.

**Deconvolution** — os parâmetros do MS2Dec: janela sigma, corte de amplitude de MS/MS, faixa de
isótopos mantida, e **Exclude fragments after precursor**.

**GC-MS** (só no modo GC-MS) — tipo de retenção, tipo de composto do índice de retenção, tipo de
índice do alinhamento, a tabela do dicionário de RI.

**Identification** — a biblioteca espectral MSP e uma biblioteca de texto opcional, as tolerâncias
e cortes de pontuação da anotação, **Use RT for scoring**, **Use RT for filtering** e **Only
report top hit**.

**Alignment** — se alinhar depois da detecção de picos (**Align after peak picking**), o arquivo de referência, as tolerâncias e
os seus pesos, os filtros, o preenchimento de lacunas.

**Export and process** — **Export QA height matrix (.qa.tsv)**, e o número de threads.

## O arquivo de método

O texto é o que o console do MS-DIAL lê: linhas `Chave: valor`, chaves sem distinção de
maiúsculas, agrupadas sob cabeçalhos de comentário `#`. Chaves que o formulário não modela são
mantidas sob `#Other` e escritas de volta sem alteração, de modo que um arquivo vindo do console
sobrevive a uma ida e volta pelo formulário. O mesmo arquivo é gravado na pasta de resultados como
`milx_method.txt` no início de toda corrida, que é como um resultado registra como foi feito.

Uma exportação de parâmetros do MS-DIAL no Windows (`<project>_param_<stamp>.txt`) pode ser
transformada num arquivo de método com `tools/msdial_param_to_method.py`; veja [[command-line]].

## Onde o método vive

No projeto. O assistente grava-o, toda alteração marca o projeto como alterado, e **Save project**
grava-o de volta. Abrir uma pasta de resultados sem projeto lê `milx_method.txt` dela quando
existe, e cai nos padrões do modo caso contrário.

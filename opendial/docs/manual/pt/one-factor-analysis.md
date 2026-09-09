---
title: Análise de um fator
section: Workspaces
order: 17
summary: As páginas da área Statistics que seguem o módulo de um fator do MetaboAnalyst — o processamento dos dados, a verificação da normalização, fold change, os testes, o volcano plot, ANOVA, correlações, busca de padrão, o random forest, o mapa de calor, k-means e o enriquecimento de lipídios.
---

# Análise de um fator

*Um fator* significa que uma coisa varia entre as injeções — a classe: tratado contra controle,
fígado contra branco, três doses. Estas páginas da [[statistics-workspace]] levam um conjunto de
dados com esse único fator pela sequência que o módulo de um fator do MetaboAnalyst percorre:
limpá-lo, normalizá-lo, transformá-lo, e depois perguntar, uma feature de cada vez, quais mudaram,
e, sobre todas de uma vez, que padrão a mudança tem. Toda página lê o único conjunto construído na
primeira página, de modo que nada aqui é calculado sobre números que outra página não viu. Os
métodos estão em [[algorithms#Estatística de um fator]].

## Processamento dos dados

![a página de processamento dos dados](images/statistics-data.png)

A página que faz o conjunto de dados. A caixa **Source** no topo diz o que as features são (veja
[[internal-standards]] para as razões); os dois painéis abaixo dizem o que lhes é feito, na ordem
em que é feito:

**Valores ausentes e filtro**

| Opção | O que faz | Padrão |
| --- | --- | --- |
| **Drop features missing in more than … % of the injections** | uma feature ausente (sem pico, ou zero) em mais do que esta fração das injeções é descartada antes de tudo | 50 % |
| **Replace the rest by** | o que um valor ausente vira: **1/5 of the minimum** da sua feature (o padrão do MetaboAnalyst, um substituto para um valor abaixo do limite de detecção), **half of the minimum**, o **minimum**, a **mean**, a **median**, a média das **K nearest features** por correlação (KNN), ou **keep the gaps** e deixar cada método saltá-las | 1/5 do mínimo |
| **Variance filter** | descartar as features mais planas, as que têm menos a dizer: por **interquartile range**, **standard deviation**, **median absolute deviation**, **relative standard deviation** (as mais ruidosas, em vez das mais planas), **mean intensity** ou **median intensity** (as mais fracas); ou **none** | amplitude interquartil |
| **Removing the bottom … %** | quantas descartar; deixado vazio, aplica-se a regra do MetaboAnalyst — nenhuma abaixo de 250 features, 5 % até 500, 10 % até 1000, 25 % além | vazio |
| **Drop features with QC RSD above … %** | uma feature cujo desvio padrão relativo sobre os controles de qualidade é pior do que isto não é reprodutível o bastante para testar; 0 desliga, e precisa de injeções com tipo `QC` na [[samples-workspace]] | 0 |

**Normalização, transformação, escalonamento**

| Opção | O que faz | Padrão |
| --- | --- | --- |
| **Sample normalisation** | tornar as injeções comparáveis entre si: **None**; **Sum** (cada injeção dividida pelo seu total, para um total constante); **Median**; **Probabilistic quotient** (cada injeção escalada pela mediana das suas razões com o perfil mediano — PQN, a escolha habitual para um efeito de diluição); **Quotient to the controls** (o mesmo, contra o perfil médio das injeções de QC); ou **Reference feature** (dividir por uma feature, escolhida na caixa seguinte — um padrão interno que não é específico de classe) | None |
| **Reference feature** | a feature por que a normalização de referência divide | — |
| **Transformation** | **Log10** (a escolha habitual; todo valor estritamente positivo depois da imputação), **Log2**, **Natural log**, **Square root**, **Cube root**, ou **None**. Um valor não positivo que sobreviveu é elevado a um décimo do menor positivo, como o MetaboAnalyst faz | Log10 |
| **Scaling (models only)** | o que os modelos multivariados veem: **Auto (unit variance)** dá a toda feature o mesmo peso; **Pareto** (dividido pela raiz quadrada do desvio padrão) fica entre; **Mean centre only** deixa as abundantes liderar. Os fold changes, os testes e as caixas nunca veem o escalonamento | Auto |

As razões a um padrão já estão normalizadas, injeção a injeção, pelo padrão: a fonte padrão não
precisa de normalização por amostra, e está em **None** por essa razão. A vista untargeted de toda
feature é onde **Probabilistic quotient** ou **Sum** ganham o seu lugar.

**Apply** corre a sequência e atualiza toda página; **Apply when the source changes** (ligado por
padrão) faz isso sempre que a fonte, o valor ou os padrões mudam, de modo que as páginas nunca
mostram um conjunto que a faixa não descreve. **Export normalised data…** grava a tabela
transformada — uma linha por feature, uma coluna por injeção — como arquivo separado por
tabulações, que é a tabela a levar para outra ferramenta. As duas linhas no fundo dizem o que
aconteceu: `61 features in · 0 dropped for missing values · 3 value(s) imputed · 0 dropped by the
filter · 61 out · no sample normalisation · log10 · auto scaling`, e que valor virou qual.

## Verificação da normalização

![a verificação da normalização](images/statistics-normalisation.png)

A vista antes-e-depois do MetaboAnalyst, desenhada dos mesmos dados. Em cima, uma caixa por
injeção — a dispersão do valor de toda feature nessa injeção — antes (na escala log, para as
caixas serem legíveis) e depois. As injeções devem alinhar-se depois da normalização; uma caixa que
ainda fica à parte é uma injeção a olhar, e muitas vezes uma injeção que foi diluída ou falhou.
Embaixo, vinte features desenhadas ao longo das injeções, antes e depois: devem dispersar-se de
forma parecida depois da transformação, e uma dispersão ainda enviesada para um lado é uma
transformação que não fez o seu trabalho. Com a fonte de razões e sem normalização, antes e depois
diferem só pela transformação.

## Fold change

O tamanho da mudança entre duas classes, uma feature de cada vez, sem teste. Escolha as duas
classes (a primeira **over** a segunda: `treated` sobre `control` significa que um fold change de 3
é três vezes mais alto no tratado) e o **threshold ×** que conta como mudança (2 por padrão),
depois **Compare**. O gráfico põe toda feature na horizontal em ordem de tabela e o seu log2 fold
change na vertical, colorido quando passa o limiar — *up* é mais alto na primeira classe, *down* é
mais baixo — e a tabela lista o fold change na escala linear e na log2. O fold change é calculado
sobre os valores normalizados antes da transformação, como razão das médias das classes, que é o
que o MetaboAnalyst faz; na escala log uma razão de médias e uma diferença de médias não são a
mesma coisa. Clicar num ponto ou numa linha desenha as caixas da feature na página **Statistical
test**.

## Teste estatístico

![a página do teste estatístico](images/statistics-test.png)

Que features diferem entre duas classes, e com que certeza. As opções:

| Opção | O que faz |
| --- | --- |
| as duas classes | quais são comparadas; só classes com duas ou mais injeções são oferecidas |
| **Non-parametric** | o teste U de Mann–Whitney sobre postos em vez do teste t — para quando os valores não são aproximadamente normais, ou alguns são selvagens. Com **Paired** vira o teste de postos sinalizados de Wilcoxon |
| **Equal variances** | o teste t de Student de variância agrupada em vez do de Welch, que não assume que as duas classes se dispersam da mesma forma. Welch é o padrão porque custa quase nada quando as variâncias são iguais e está certo quando não são |
| **Paired** | as injeções das duas classes correspondem uma a uma, em ordem — o mesmo animal antes e depois; precisa do mesmo número em cada classe |
| **adjust** | o que fazer quanto a testar centenas de features de uma vez: **FDR (Benjamini–Hochberg)**, o padrão e o do MetaboAnalyst; **Holm**; **Bonferroni**; ou **None (raw p)** |
| **α** | o nível que uma feature tem de atingir, no p ajustado, para contar como significativa (0,05) |

**Test** corre; a linha sob a faixa diz o que foi feito — `liver (4) against blank (2) over 61
features by Welch's t-test · 27 at FDR ≤ 0.05`. A tabela dá, por feature, o fold change, a
estatística (t, ou U), o p e o p ajustado, ordenados como quiser. Escolher uma linha desenha a
feature à direita, uma caixa por classe com as suas injeções como pontos, e **Open in the ion
table** salta para ela na [[analytics-workspace]] para olhar os próprios picos. **Export table…**
grava a tabela inteira; veja [[exports#As tabelas de análise]].

A mesma comparação, com as mesmas opções, alimenta o fold change, o volcano e o enriquecimento:
são um cálculo mostrado de quatro formas.

## Volcano plot

![o volcano plot](images/statistics-volcano.png)

As duas coisas de uma vez: o log2 fold change na horizontal, −log10 do p na vertical, de modo que
as features que mudaram muito *e* com confiança ficam nos cantos superiores. As linhas verticais
pontilhadas são o limiar de fold change para os dois lados; a horizontal é α no p ajustado; os
cantos além das duas são coloridos — vermelho *up* (mais alto na primeira classe), azul *down* — e
nomeados. O **fold change ≥**, o ajuste e o nível **≤** são as mesmas opções da página de teste, e
**Redraw** aplica-as. Clicar num ponto desenha as suas caixas à direita. Uma feature além da linha
de fold change mas abaixo da linha de p mudou muito numa ou duas injeções e não nas outras; uma
além da linha de p mas perto do meio mudou com confiança e pouco. As duas valem a pena saber;
nenhuma é um achado.

## ANOVA

Três ou mais classes. A análise de variância de um fator pergunta, por feature, se as médias das
classes diferem de todo; **Kruskal–Wallis** pergunta o mesmo sobre postos. **adjust** e **α** são
como na página de teste; **Compute** corre. A tabela dá a estatística F (ou H), o p e o p ajustado;
o gráfico embaixo põe toda feature na horizontal e −log10 p na vertical, de modo que as altas são as
que olhar. Escolher uma feature desenha as suas caixas, uma por classe, e o painel sob elas dá o
teste **post-hoc** — o LSD de Fisher entre todo par de classes (um Mann–Whitney entre cada par, para
o teste de postos) — de modo que uma feature que a ANOVA sinaliza pode ser lida como *dose alta
contra controle, p 0,002; dose baixa contra controle, não*. Com exatamente duas classes a página
diz para usar o teste de dois grupos.

## Correlações

Que features se movem juntas ao longo das injeções, ou que injeções se parecem. O mapa de calor é
desenhado sobre as **top** *n* features (25 por padrão) escolhidas por **most variable**, **lowest
p in the comparison** ou **lowest p in the ANOVA**, com o coeficiente de **Pearson**, **Spearman**
ou **Kendall**; **Between injections instead** desenha a matriz injeção por injeção, onde as
réplicas devem ser os blocos quentes na diagonal e uma injeção que não se correlaciona com nada é
a que verificar. **Compute** constrói; clicar numa linha da matriz de features abre essa feature na
tabela de íons.

## Busca de padrão

A pergunta inversa: dado um perfil, que features o seguem. **A feature's profile** toma uma
feature — um padrão interno, um marcador em que você confia — e ordena toda outra pela sua
correlação com ela ao longo das injeções; **The class order** toma uma tendência escrita como as
classes em ordem (`control, low, high`, na caixa) e ordena as features pela sua correlação com essa
rampa, de modo que as que sobem com a dose vêm primeiro. Pearson, Spearman ou Kendall, como antes.
**Search** corre; as barras são as correlações, a tabela tem o p de cada uma, e clicar numa barra
abre a feature. **Export table…** grava o ranking.

## Random forest

Um tipo diferente de ranking. Quinhentas árvores de decisão (**Trees**), cada uma crescida sobre
uma amostra bootstrap das injeções com um punhado aleatório das features tentado em toda divisão
(**features per split**, √p por padrão), votam a classe de toda injeção que não viram; a fração que
erram é o **out-of-bag error**, uma estimativa honesta que não precisa de validação cruzada
separada, e a tabela de confusão à direita diz que classes foram confundidas com quais. A
importância de uma feature é a queda nessa acurácia quando os seus valores são embaralhados —
**mean decrease in accuracy** — com a redução de Gini ao lado na tabela. **Grow the forest** corre;
leva um ou dois segundos. Clicar numa barra abre a feature.

A floresta é não linear e não se importa com o escalonamento, de modo que uma feature que ela
ordena e o modelo discriminante não vale uma olhada. Com poucas injeções e muitas features
ajustará o lote; o erro fora do saco é o número que diz se ajustaria o próximo, e deve ser lido
antes do ranking.

## Heatmap

![o mapa de calor agrupado](images/statistics-heatmap.png)

As **top** *n* features (25 por padrão) escolhidas por **lowest p in the comparison**, **lowest p
in the ANOVA**, **most variable** ou **every feature**, desenhadas contra toda injeção, os dois
lados agrupados com a **distance** e a **linkage** à sua escolha (euclidiana e média por padrão,
como o MetaboAnalyst). **Standardise rows** põe toda feature no seu próprio z-score ao longo das
injeções, de modo que as cores comparam features e não abundâncias — ligado, por regra; desligado,
as features abundantes afogam o resto. **Cluster injections** agrupa as colunas também; desligado,
ficam em ordem de tabela, que é a escolha certa quando a ordem da corrida importa. **Build**
desenha.

As faixas de cor nomeiam a classe de cada injeção ao longo do topo e a classe de lipídio de cada
feature ao longo do lado; as árvores são os dendrogramas. Clicar numa linha abre a feature na
tabela de íons. A escala de cores, os rótulos, as árvores e os valores estão nas opções do gráfico
— veja [[chart-export]].

## K-means

As injeções partidas em *k* grupos pelos seus perfis inteiros, sem que as suas classes sejam ditas
(**Clusters (k)**, 2 por padrão; **Run**). O gráfico põe-nas nos dois primeiros componentes
principais coloridas por grupo; o painel embaixo lista os membros de cada grupo com a sua classe.
Quando os grupos reproduzem as classes as classes são reais nos dados; quando um grupo as mistura,
ou uma injeção fica no grupo errado, é assim que o desenho parece sem os seus rótulos. Vinte
reinícios com semeadura k-means++, o melhor guardado, de modo que não depende de sorte.

## Enriquecimento de lipídios

![a página de enriquecimento de lipídios](images/statistics-enrichment.png)

O enriquecimento do MetaboAnalyst pergunta se os metabólitos que mudaram caem numa via mais vezes
do que o acaso os poria lá. Os lipídios não têm lista de vias digna do nome ao nível de espécie,
mas têm as suas classes, os seus comprimentos de cadeia e a sua insaturação, e esses são os
conjuntos aqui:

| Tipo de conjunto | Exemplo | O que significa quando enriquecido |
| --- | --- | --- |
| classe de lipídio | `PC`, `TG`, `Cer` | uma classe inteira se moveu — uma via de síntese, ou uma remodelação de membrana |
| comprimento de cadeia | `34 carbons`, `38 carbons` | lipídios construídos sobre os mesmos ácidos graxos se moveram juntos |
| insaturação | `saturated`, `monounsaturated`, `4 double bonds` | uma dessaturase, ou os ácidos graxos da dieta |
| espécie | `PC 34:1` (com **Species sets too**) | a mesma composição somada entre classes — vários adutos ou isômeros de um lipídio, sobretudo uma verificação |

O **Significant set** são as features que a comparação encontrou (**Significant in the
comparison**), que a ANOVA encontrou, ou o topo da floresta; **minimum set size** descarta
conjuntos pequenos demais para testar (3 por padrão). **Compute** corre a análise de
sobre-representação: um teste hipergeométrico dos acertos em cada conjunto contra as features
testadas, ajustado como na página de teste. As barras são −log10 p por conjunto, a linha
pontilhada p 0,05; a tabela dá os acertos, o tamanho, a razão de enriquecimento (acertos sobre
esperado) e os p. **Export table…** grava-a com os membros de cada conjunto.

Mais duas leituras da mesma comparação ficam abaixo. **Each class as a whole** é o log2 fold change
médio de toda feature da classe, de modo que uma classe que subiu ou desceu em bloco aparece como
uma barra. O **chain map** é uma grade das espécies da classe — carbonos para baixo, duplas
ligações para o lado — cada célula o log2 fold change médio dos lipídios com essa composição, com o
número impresso: o lugar para ver que as espécies mais longas e mais insaturadas subiram enquanto as
curtas saturadas desceram, o que uma lista de valores de p não mostra. **Chain map of** escolhe a
classe, ou todas juntas.

Os conjuntos são lidos dos nomes que o MS-DIAL deu às features, de modo que um desconhecido não
pertence a conjunto nenhum e conta só no total testado; a leitura está em [[algorithms#Nomes de lipídios]]. A página [[pathways]] faz a pergunta seguinte — que reações moveram as classes que se
moveram.

## O que as páginas não fazem

Efeitos de lote entre estudos, séries temporais, e dois fatores de uma vez. Os testes assumem que
as injeções são independentes (exceto **Paired**) e que as classes são a única coisa que difere de
propósito; a correção de deriva na página [[statistics-workspace#Drift correction]] é a única
concessão ao instrumento.

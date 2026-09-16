---
title: A área de trabalho Statistics
section: Workspaces
order: 16
summary: O conjunto de dados visto por inteiro — vinte páginas à esquerda, dos dados e do seu pré-processamento aos testes, ao volcano, aos modelos, ao agrupamento e ao enriquecimento, todas lendo o mesmo conjunto.
---

# A área de trabalho Statistics

A revisão trabalha uma feature de cada vez. Esta área de trabalho é a outra metade: o conjunto de
dados visto por inteiro, de modo que uma injeção mal rotulada, um lote que derivou, uma classe de
lipídios que subiu junta ou um analito que separa os grupos aparece num só lugar. `⌘5` mostra-a;
precisa de um resultado com pelo menos duas injeções. As páginas seguem o módulo *Statistical
Analysis (one factor)* do MetaboAnalyst, que é o que a maioria das pessoas da área já terá usado,
com os mesmos nomes para as mesmas coisas sempre que foi possível, e com as partes específicas de
lipídios — os padrões internos, o enriquecimento sobre classes e cadeias — acrescentadas onde um
resultado de lipidômica precisa delas. Os métodos por trás de cada página estão em [[algorithms]];
os gráficos, as suas opções e a sua exportação em [[chart-export]].

## Um conjunto de dados, muitas páginas

Toda página lê o mesmo conjunto de dados, e o conjunto é construído na primeira página, **Data
processing** — que features, como que números, pré-processados como. Mude a fonte, a normalização
ou a transformação lá e toda página muda com ela; não há página cujos números venham de outra
tabela. A faixa no topo diz o que o conjunto é neste momento:

| Controle | O que faz |
| --- | --- |
| **Source** | o que as features são: **Confirmed analytes, as ratios to a standard** (o padrão de lipidômica, veja [[internal-standards]]), **Confirmed analytes, raw**, ou **Every feature** (a vista untargeted) |
| **Internal standards…** | o diálogo que escolhe o padrão por que cada classe é dividida |
| **Area** | área de pico em vez de altura de pico |
| **Annotated only** | quando toda feature é a fonte, descartar os desconhecidos |
| **Drift corrected** | alimentar toda página com os valores corrigidos contra os controles de qualidade; aparece depois de uma correção ter sido corrida na página **Drift correction** |
| **Reload from the review** | aparece quando a revisão mudou depois de a análise ter sido carregada — uma feature confirmada ou rejeitada desde então — e toma o conjunto confirmado de novo |

Um resultado sem nenhuma feature marcada **Confirmed** abre em **Every feature**; confirme algumas
na [[analytics-workspace]] e recarregue, e a vista de razões fica disponível. O resumo à direita
diz o que passou: `61 features across 6 injections · area ratio to the class standard · log10`.

## As páginas

Estão listadas à esquerda, na ordem em que a análise costuma ser lida:

| Cabeçalho | Página | O que responde |
| --- | --- | --- |
| Data | **Data processing** | o que o conjunto de dados é, e como é limpo, normalizado, transformado e escalonado |
| | **Normalisation check** | a normalização alinhou as injeções, a transformação fez as features dispersarem-se de forma parecida |
| One feature at a time | **Fold change** | quanto cada feature mudou entre duas classes |
| | **Statistical test** | que features diferem entre duas classes, e com que certeza |
| | **Volcano plot** | as duas coisas de uma vez: o tamanho da mudança na horizontal, a certeza na vertical |
| | **ANOVA** | que features diferem entre três ou mais classes, e entre que pares |
| | **Correlations** | que features se movem juntas, ou que injeções se parecem |
| | **Pattern search** | que features seguem um perfil dado, ou uma ordem dada das classes |
| Models | **Principal components** | as réplicas ficam juntas e as classes separadas, sem que as classes sejam ditas |
| | **Discriminant** | que features separam as classes declaradas, e se essa separação sobrevive à validação cruzada |
| | **Orthogonal** | a mesma separação num só eixo, com o S-plot |
| | **Random forest** | um ranking não linear das features, com o seu próprio erro fora do saco |
| | **Two factors** | um desenho com dois fatores — tratamento e tempo, classe e lote: a two-way ANOVA por feature, com a interação, e ASCA sobre a matriz |
| Clustering | **Dendrogram** | que injeções se juntam, e a que altura |
| | **Heatmap** | as principais features contra toda injeção, ambas agrupadas |
| | **K-means** | as injeções partidas em *k* grupos sem os seus rótulos |
| Enrichment | **Lipid enrichment** | se as features que mudaram caem numa classe de lipídio, num comprimento de cadeia ou numa insaturação mais vezes do que o acaso |
| | **Pathways** | que reações da rede de lipídios correram mais depressa ou mais devagar entre as duas classes, e que cadeias delas — o BioPAN, sobre os analitos revisados |
| Quality | **Drift correction** | a deriva do instrumento traçada nos controles e dividida fora |
| | **Molecular network** | que features fragmentam de forma parecida |

As páginas sob *One feature at a time*, *Clustering* (exceto o dendrograma), o enriquecimento de
lipídios e o random forest estão descritas em [[one-factor-analysis]]; as vias em [[pathways]]; o
desenho de dois fatores em [[two-factor-analysis]]. O resto está abaixo.

## Principal components

![componentes principais](images/statistics-pca.png)

Escores à esquerda, um ponto por injeção, coloridos por classe, com a elipse de confiança de 95 %
de cada classe: as réplicas de uma classe devem ficar juntas e as classes separadas, e uma injeção
que cai entre o grupo errado ou longe de tudo é a primeira a verificar. Loadings à direita, um
ponto por feature, coloridos por classe de lipídio: uma feature longe do centro na direção que
separa os grupos é o que os afasta. As dez mais longe do centro são nomeadas; clicar em qualquer
uma abre-a na tabela de íons. As duas caixas acima dos escores escolhem que componentes são
desenhados (**PC1** contra **PC2** por padrão, até PC5); o scree plot e a tabela embaixo dão a
variância que cada componente carrega e o total acumulado.

**A região de 95 %, e qual delas.** Uma classe precisa de pelo menos três injeções para ter uma —
com menos, não há covariância a estimar, e o gráfico diz isso em vez de não desenhar nada em
silêncio. Há duas definições, as mesmas duas que o MetaboAnalyst oferece, para que uma figura feita
aqui possa ser comparada com outra que o leitor já viu:

| | Raio | O que pressupõe |
| --- | --- | --- |
| padrão | `raiz(χ²(2) a 95 %)` = 2,4477 | a região de amostra grande; ela não sabe de quantas injeções a covariância veio, então quatro réplicas e quarenta recebem o mesmo raio. **É o que o MetaboAnalyst desenha se não lhe disserem outra coisa.** |
| **F region** | `raiz(2 · F(0,95; 2, n − 1))` | alarga conforme a classe encolhe: cerca de 1,4 vez o padrão com seis injeções, e com trinta as duas já quase se encontraram |

A caixa **F region** acima dos escores troca todos os gráficos de escores — componentes
principais, PLS-DA e OPLS-DA — para que a área inteira conte uma história só.

Nenhuma das duas é um teste. Uma elipse descreve onde uma classe está; duas que não se sobrepõem
não são por isso significativamente diferentes, e um modelo ajustado para separar as classes vai
separá-las façam as elipses o que fizerem.

Os componentes são calculados sobre o conjunto escalonado — auto-scaling por padrão, de modo que
toda feature pesa o mesmo; Pareto ou só centrar estão na página **Data processing**.

## Discriminant

![o modelo discriminante](images/statistics-discriminant.png)

O par supervisionado dos componentes principais: pergunta que features separam as classes
declaradas, em vez de quais carregam mais variância. Análise discriminante por mínimos quadrados
parciais (PLS-DA), ajustada por NIPALS. **Fit against the classes** corre-a, com o número de
**Components** (2 por padrão, 1 a 5) e o número de **Permutations** para o teste (200 por padrão;
0 salta-o).

Com um punhado de injeções separa qualquer coisa, ruído incluído, de modo que o ajuste nunca é
reportado sozinho:

| Número | O que diz |
| --- | --- |
| **R²Y** | quanto da pertença de classe o modelo reproduz nas injeções em que foi ajustado |
| **Q²** | quanto reproduz numa injeção que nunca viu, por validação cruzada leave-one-out |
| **permutation p** | com que frequência rótulos embaralhados fizeram tão bem, sobre os embaralhamentos pedidos |

Um R²Y alto com um Q² baixo é um modelo que decorou as suas próprias amostras. A linha sob o
gráfico é o veredito e não o ajuste: diz que a separação *survives cross-validation* só quando Q² é
pelo menos 0,4 e p no máximo 0,05, e senão diz claramente para ler o gráfico como um retrato deste
lote e não como um achado. Dirá isso sobre rótulos sem nada por trás. A centragem e o escalonamento
são refeitos dentro de toda dobra da validação cruzada só a partir das injeções de treino, porque
fazê-lo uma vez sobre a matriz inteira deixa a injeção retida falar pela sua própria previsão e
entrega ao ruído um Q² respeitável.

O gráfico de barras à direita ordena as features por **VIP**, a medida habitual de quanto da
separação assenta em cada uma; acima de um — a linha pontilhada — é a marca costumeira de uma
feature que importa. Ao lado de cada barra, uma célula por classe mostra o nível médio da feature
nessa classe, como o gráfico de VIP do MetaboAnalyst faz, de modo que a direção da mudança se lê
sem sair da página. Com exatamente duas classes, **Higher in** na tabela nomeia a classe com que a
feature vai. Clicar numa barra ou numa linha abre essa feature na tabela de íons.

O teste de permutação reajusta o modelo algumas centenas de vezes, o que sobre uns dois mil
features levaria minutos feito de forma ingênua. Não leva: as features são reduzidas uma vez aos
produtos entre as injeções, e todo reajuste trabalha no espaço que as injeções geram, de modo que
duas mil features e oito injeções ajustam e permutam em menos de meio segundo.

## Orthogonal

![o modelo ortogonal](images/statistics-orthogonal.png)

A mesma pergunta do modelo discriminante, numa forma mais arrumada (OPLS-DA). Um modelo simples
espalha a separação por todo componente que ajusta, misturada com variação que não tem nada a ver
com as classes — a ordem da corrida, a extração, o animal. Este retira isso primeiro, um componente
de cada vez, e o que sobra é um só componente preditivo carregando a separação inteira. O gráfico
de escores lê-se então diretamente: da esquerda para a direita é a diferença entre as suas
classes, para cima e para baixo é tudo o mais que estava no caminho.

Só duas classes, porque a parte preditiva é uma direção e uma direção tem duas pontas. Com três ou
mais diz isso e manda para o modelo simples.

**Fit the orthogonal model** corre-o. **Orthogonal components** é quantas rodadas de retirada
fazer (1 por padrão; 0 a 5). Uma é a resposta habitual. Mais vale a pena tentar quando as injeções
ainda se espalham pelo eixo vertical, e vale a pena desconfiar quando começa a melhorar o ajuste
sem melhorar o Q²: nesse ponto está removendo o sinal junto com o incômodo.

**O S-plot** à direita é o ponto de toda a rotação. Na horizontal está quanto da separação cada
feature carrega (a sua covariância com o componente preditivo), na vertical com que consistência
a carrega (a sua correlação), e só os dois cantos afastados valem uma ação. Uma feature alta mas
perto do meio é confiável e minúscula; uma longe mas baixa é uma injeção barulhenta fingindo ser
uma diferença. Os oito cantos mais fortes são nomeados. Clicar num ponto, ou numa linha da tabela
embaixo, abre essa feature na tabela de íons. A tabela lista os cantos em ordem, com a classe em
que cada feature é mais alta, a sua covariância, a sua correlação e o seu VIP.

A rotação é uma forma de olhar, não um resultado. Não ajusta melhor do que o modelo simples e é
submetida aos mesmos dois números, Q² e o p de permutação, impressos no mesmo lugar. Um modelo que
não sobrevive a eles não sobrevive rodado.

Na corrida de oito injeções dos dados de validação, fígado contra branco dá R²Y 0,98 e Q² 0,91 com
p 0,020, com 42 % das features no componente preditivo e 29 % retiradas; os cantos do S-plot são
ácidos graxos livres por volta de 1,5 min, que é o que separa um extrato de fígado real de um
branco. Os mesmos rótulos embaralhados dão Q² −1,55 e p 0,81.

## Dendrogram

![agrupamento hierárquico](images/statistics-clustering.png)

As injeções agrupadas sobre os seus perfis inteiros. Duas injeções juntam-se à altura em que os
seus perfis deixam de concordar, de modo que as réplicas devem juntar-se baixo e os brancos ficar
pendurados sozinhos; uma que se junta ao grupo errado é uma troca ou uma corrida ruim. **Distance**
é o que "concordar" significa — **Euclidean**, **Pearson** (um menos a correlação, o padrão, que
ignora a intensidade geral), **Spearman** (o mesmo sobre postos) ou **Manhattan** — e **Linkage** é
como a distância de um grupo a outro é tomada das dos seus membros — **Average** (o padrão),
**Complete**, **Single** ou **Ward**. As mesmas escolhas estão na página **Heatmap**, onde as
features também são agrupadas.

## Drift correction

![a correção de deriva](images/statistics-drift.png)

A resposta do instrumento cai à medida que a fonte suja e salta entre lotes, de modo que o mesmo
composto não é o mesmo número na injeção 5 e na injeção 80. Os controles de qualidade são o mesmo
material toda vez, de modo que o que quer que façam ao longo da sequência é o instrumento e não a
biologia: ajuste isso, divida fora, e o que sobra é comparável. Esta é a correção localmente
ponderada que a literatura de metabolômica chama QC-RLSC.

**Correct against the controls** corre-a, com o **Smoothing span** — a fração dos controles que
cada ajuste local vê: largo segue a queda lenta, estreito segue todo tremor incluindo o ruído
(0,75 por padrão; 0,2 a 1,0). Cada lote é corrigido sozinho. Onde um lote tem três ou mais
controles a deriva dentro dele é traçada com um ajuste linear local tricúbico; onde tem só um ou
dois, não há o bastante para traçar uma tendência mas há o bastante para dizer a que nível o lote
corria, de modo que é nivelado aos outros com um fator único.

É medida pelo que serve. A tabela dá o coeficiente de variação de toda feature sobre os controles
antes e depois, o pior primeiro; escolher uma linha desenha a resposta dessa feature ao longo da
corrida, controles separados das amostras, antes em cima e depois embaixo. Um traço de controle que
cai ou dá um degrau na fronteira de um lote é deriva; depois de corrigir deve estar plano, e as
amostras devem ter-se movido com ele. Uma feature cuja dispersão não melhora é uma por que os
controles não puderam falar. A linha sob a tabela diz quantos controles e lotes foram usados e o CV
mediano antes e depois.

Nada é gravado no resultado. Os valores corrigidos vivem na área de trabalho e chegam às outras
páginas só enquanto **Drift corrected** está marcado na faixa, caso em que entram antes do
pré-processamento, de modo que as razões, os testes e os modelos os leem todos. O que precisa está
na [[samples-workspace]]: as injeções marcadas `QC`, a ordem de injeção, e o lote. Sem pelo menos
três controles não corrige nada e diz isso.

## Molecular network

![a rede molecular](images/statistics-network.png)

As features cujos espectros de produto deconvoluídos se parecem são unidas, pelo cosseno ponderado
por intensidade sobre fragmentos casados que o MS-DIAL e o GNPS usam. Uma classe de lipídio
fragmenta da mesma forma e forma um grupo, de modo que uma feature nomeada como uma classe sentada
dentro do grupo de outra merece uma segunda olhada, e um desconhecido ao lado de um grupo nomeado é
candidato à mesma família. Clicar num nó abre essa feature na tabela de íons.

**Similarity ≥** é o corte para uma aresta (0,7 por padrão) e **fragment tol.** a tolerância de
massa para dois fragmentos contarem como o mesmo (0,05 Da). As features unidas a nada são contadas
mas não desenhadas: com alguns milhares delas a imagem é toda pontos soltos e nenhum grupo. **Show
unlinked** põe-nas de volta. **Export for Cytoscape…** grava a tabela de arestas e, ao lado, uma
tabela `_nodes`; veja [[exports]].

Construir a rede lê todo espectro deconvoluído, de modo que é um botão — **Build network** — em vez
de algo que acontece a cada mudança de opção. Só as features com MS/MS participam; a rede lê as
features do resultado diretamente, não o conjunto de análise, de modo que é a única página que a
fonte não muda, além de **Annotated only**.

## O que não está aqui

Desenhos multibloco e multinível — medidas repetidas além do teste **Paired** de duas classes,
vários tipos de medida nos mesmos sujeitos, séries temporais com mais de dois pontos, e três ou
mais fatores. Tudo acima assume um fator e injeções independentes, exceto a página **Two
factors**, que toma dois.

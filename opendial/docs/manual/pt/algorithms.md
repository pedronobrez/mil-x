---
title: Algoritmos
section: Under the hood
order: 50
summary: O que cada etapa do processamento e cada vista estatística calcula, em prosa — detecção de picos, MS2Dec, pontuação da anotação, alinhamento, PCA, QC-RLSC, PLS-DA, OPLS-DA, agrupamento, a rede molecular.
---

# Algoritmos

Os algoritmos de processamento são os do MS-DIAL, sem alteração; estão resumidos aqui para que um
resultado possa ser lido com as expectativas certas. A estatística é do próprio OpenDIAL e está
descrita com detalhe suficiente para reproduzir.

## Processamento

### Detecção de picos

As varreduras de survey de cada arquivo bruto são cortadas em fatias de m/z da *mass slice width*
do método, e um cromatograma de íon extraído é construído para cada uma. O cromatograma é suavizado
(média móvel ponderada linear por padrão, sobre o *smoothing level*), e os picos são detectados
pela forma da primeira derivada: uma subida, um ápice, uma descida, com pelo menos *minimum peak
width* varreduras de largura e *minimum peak height* de altura. A altura de cada pico é a
intensidade do ápice, a área a integral acima de zero, e o sinal-ruído o ápice sobre o ruído
local. Os isótopos são agrupados pelos estados de carga até *max charge number*, e os adutos pela
lista que o método nomeia, de modo que `[M+Na]+` a +21,98 Da de um pico `[M+H]+` é reconhecido como
o mesmo composto.

### Deconvolução (MS2Dec)

Em DDA uma varredura de produto pertence ao pico de cujo m/z o seu precursor está dentro da
tolerância de centroide; em SWATH e AIF, a todo pico dentro da janela de isolamento. Para cada
pico, o MS2Dec modela o perfil cromatográfico de todo íon-produto ao longo das varreduras em torno
do ápice e guarda os íons cujos perfis seguem o do próprio pico — os vizinhos coeluídos, modelados
sobre a *sigma window*, são subtraídos. O resultado é um espectro de produto limpo por pico,
gravado no `.dcl`.

### Anotação

Cada espectro deconvoluído é pontuado contra todo registro da biblioteca dentro da tolerância de
MS1 do seu precursor. O produto escalar ponderado (intensidades em raiz quadrada e ponderadas pela
massa), o produto escalar simples, o produto escalar reverso (só sobre os picos da referência), a
porcentagem de picos casados e a similaridade de massa são combinados na pontuação total; a
similaridade de retenção é somada quando a biblioteca tem tempos de retenção e o método assim diz.
Todo corte de [[method-parameters#Identification]] tem de passar; o melhor registro é a anotação,
e em lipidômica o seu nome é reescrito a partir dos fragmentos observados. Veja [[annotation]].

### Alinhamento e preenchimento de lacunas

Os picos de toda injeção são casados com os da injeção de referência por uma pontuação que pesa a
distância de tempo de retenção e a distância de m/z pelos dois *factors*, dentro das duas
tolerâncias. Os picos casados viram uma feature; um pico que nada casa vira uma feature sozinho.
Para cada feature e injeção onde nenhum pico foi encontrado, o preenchimento de lacunas integra o
cromatograma de íon extraído dentro da janela de retenção da feature mesmo assim, para que a matriz
não tenha buracos, e marca o valor como preenchido. Os filtros descartam depois as features
detectadas em poucas injeções. O resultado é o contentor `.arf2` que a revisão edita.

### Reintegração

Dentro da janela que o revisor desenha, a altura é o ápice, a área o trapézio sob o traço em
intensidade × segundos, e a área corrigida de linha de base subtrai a reta que une as duas bordas;
início, ápice e fim movem-se para a primeira, a mais alta e a última varredura dentro. Veja
[[reintegration]].

## Estatística

Toda página parte da mesma tabela: injeções nas linhas, features nas colunas. O que os valores
são, e o que lhes é feito antes de uma página os ler, é a primeira seção.

### Abundância relativa

Com a fonte de razões, as features são os spots marcados Confirmed; cada um é atribuído à classe
de lipídio que a sua ontologia nomeia (ou à classe lida do seu nome), e toda classe com padrão tem
os seus analitos divididos, injeção a injeção, pela área (ou altura) do padrão nessa injeção:
`ratio[i, j] = area[i, j] / area[i, standard of class(j)]`. Um padrão zero ou ausente torna a razão
ausente. O padrão é deixado de fora; uma classe sem padrão mantém os seus valores brutos, e o
relatório nomeia-a.

### Nomes de lipídios

Um nome é lido em classe, carbonos e duplas ligações: `PC 34:1`, `PC 16:0_18:1` (cadeias somadas),
`SM 18:1;O2/23:0` (cadeias somadas, o oxigênio ignorado), `Cer d18:1/17:0`, `TG 52:2|TG
16:0_18:1_18:1` (a parte antes da barra), com a classe tomada da ontologia quando o MS-DIAL deu
uma. Um padrão é um nome com um rótulo — `d7`, `d9`, `(d7)`, `-d5`, `13C`, `IS` como token — e uma
espécie de cadeia ímpar é aquela cujo total de carbonos é ímpar (cadeias 15:0, 17:0, 19:0). A
pontuação que sugere um padrão é 100 para um padrão marcado da classe, 40 para uma espécie de
cadeia ímpar da classe, 20 para um padrão de outra classe, e uma classe só recebe sugestão a partir
de 40.

### Pré-processamento

Na ordem do MetaboAnalyst. *Valores ausentes*: uma feature ausente em mais do que a fração
permitida das injeções é descartada; as lacunas restantes viram uma fração do mínimo positivo da
feature (um quinto por padrão), a sua média, a sua mediana, ou — KNN — a média das dez features que
melhor se correlacionam com ela sobre as injeções que ambas têm. *Filtro*: as features são
ordenadas pela estatística escolhida (amplitude interquartil, desvio padrão, desvio absoluto
mediano, RSD, média ou mediana) e a fração mais baixa descartada; vazia, a fração é a regra do
MetaboAnalyst por contagem (0 abaixo de 250, 5 % abaixo de 500, 10 % abaixo de 1000, 25 % além).
*RSD de QC*: uma feature cujo desvio padrão relativo sobre as injeções de QC excede o limite é
descartada. *Normalização por amostra*: pela soma ou mediana da injeção; pelo quociente
probabilístico — a mediana das razões da injeção com o perfil mediano de todas as injeções (ou das
injeções de QC); ou por uma feature de referência. *Transformação*: log10, log2, ln, raiz quadrada
ou raiz cúbica, com um valor não positivo elevado primeiro a um décimo do menor positivo.
*Escalonamento*, só para os modelos: centrar, depois dividir pelo desvio padrão (auto) ou pela sua
raiz quadrada (Pareto).

### Estatística de um fator

*Dois grupos.* Teste t de Welch por padrão, com os graus de liberdade de Welch–Satterthwaite; o
teste de Student de variância agrupada a pedido; o teste t pareado sobre as diferenças. O U de
Mann–Whitney com a distribuição exata pela recursão de contagem até vinte por grupo sem empates e a
aproximação normal com correção de empates senão; o teste de postos sinalizados de Wilcoxon da
mesma forma. O fold change é a razão das médias das classes nos valores normalizados antes da
transformação; os testes correm nos transformados. *Vários grupos.* O F da ANOVA de um fator, com
a diferença mínima significativa de Fisher entre todo par como post-hoc; o H de Kruskal–Wallis
com a aproximação qui-quadrado, e Mann–Whitney par a par como seu post-hoc. *Ajuste.*
Benjamini–Hochberg (step-up, monótono), Holm (step-down) e Bonferroni.

*Correlações.* Pearson; Spearman como Pearson sobre postos médios; τ-b de Kendall com a
aproximação normal corrigida de empates para o seu p. A busca de padrão é a mesma correlação de
toda feature com o perfil de uma feature dada, ou com a ordem das classes como inteiros.

*Distribuições.* Log-gama de Lanczos; a gama incompleta regularizada por série e fração contínua;
a beta incompleta regularizada pela fração contínua de Lentz; o t de Student, o F de Fisher e o
qui-quadrado a partir delas; a CDF normal pela função de erro complementar e o seu quantil pela
aproximação racional de Acklam; a cauda superior hipergeométrica por log-binomiais somados. Cada
uma é testada contra valores tabelados.

### Agrupamento, mapa de calor e k-means

Agrupamento aglomerativo sobre qualquer de quatro distâncias — euclidiana, um menos Pearson, um
menos Spearman, Manhattan — com ligação média, completa, simples ou de Ward, as três últimas pela
atualização de Lance–Williams. O mapa de calor agrupa as features pela mesma regra e,
opcionalmente, as injeções, sobre as linhas padronizadas (cada feature centrada e dividida pelo
seu desvio padrão ao longo das injeções). K-means com semeadura k-means++, iterações de Lloyd até
convergir, vinte reinícios e a menor soma de quadrados intra-grupo guardada.

### Random forest

O de Breiman: cada árvore crescida sobre um bootstrap das injeções, em cada nó a melhor divisão de
Gini entre √p features aleatórias, até a pureza; as injeções fora do saco de cada árvore são
classificadas por ela e a maioria sobre as árvores é a previsão, cuja taxa de erro é o erro fora do
saco. A importância é a média sobre as árvores da queda de acurácia fora do saco quando os valores
da feature são permutados, e a redução total de Gini atribuível à feature.

### Dois fatores

*A two-way ANOVA*, por feature sobre os valores transformados, com somas de quadrados do tipo II
por comparação de modelos: ajustes de mínimos quadrados dos modelos B, A, A+B e A+B+A×B
(codificação de tratamento, um intercepto), cada um pelas equações normais com pivotamento para que
uma coluna dependente custe posto em vez de uma falha; SS(A|B) = RSS(B) − RSS(A+B), SS(B|A) =
RSS(A) − RSS(A+B), SS(A×B) = RSS(A+B) − RSS(completo); os graus de liberdade de um efeito são o
posto que ele acrescenta, os do resíduo são n menos o posto do modelo completo, e F e p seguem. Um
desenho sem célula replicada não tem interação a testar e diz isso. O p de cada efeito é ajustado
ao longo das features por si só. *ASCA*: a matriz escalonada é centrada, a parte de cada fator são
as suas médias por nível, a da interação as médias das células menos os efeitos principais, o
resíduo o que resta; a fração de um efeito é a sua soma de quadrados sobre a da matriz; o seu p de
permutação embaralha os rótulos do fator sobre a matriz com os outros efeitos removidos (os da
interação sobre os rótulos das células com os efeitos principais removidos) e conta quantas vezes a
soma de quadrados embaralhada atinge a observada; cada parte recebe uma PCA pela matriz de Gram, e
os escores são a parte mais o resíduo projetados nos seus loadings.

### Vias

A rede e o método do BioPAN sobre os valores lineares normalizados. Toda feature é colocada na rede
pela sua classe — a ontologia dobrada nos nomes do BioPAN, os éteres separados pela marca `O-`/`P-`
no nome, as formas de esfinganina por subclasse ou por composição saturada — e, ao nível de espécie,
pela sua composição somada. A abundância de um nó numa injeção é a soma das suas features; ao nível
de ácido graxo os nós são os ácidos graxos livres medidos, por composição. As arestas são as da
tabela de reações ao nível de classe; ao nível de espécie uma reação preservadora une composições
iguais, uma que remove cadeia une duas espécies cujas composições diferem por um ácido graxo livre
medido no conjunto, e uma que adiciona cadeia une duas cujas composições diferem por um acil-CoA
medido no conjunto (a regra do BioPAN); ao nível de ácido graxo os trinta passos de cadeia da
tabela do BioPAN e nenhum outro. Para toda aresta com as duas pontas presentes, o peso por injeção
é produto sobre reagente (sem peso sem o reagente, zero sem o produto); os pesos das duas classes
são comparados pelo teste t de Welch onde cada uma tem duas ou mais; Z é o quantil normal de 1 −
p/2 com o sinal da diferença das médias — o `qnorm(1 − p)` unilateral do BioPAN — com p elevado no
mínimo a 10⁻¹⁵. Uma via é uma cadeia simples dirigida de arestas testadas até o comprimento
escolhido, encontrada por busca em profundidade a partir de todo nó (limitada a vinte mil
cadeias), e o seu Z é Σ Z_i / √k; ativa e suprimida são |Z| no limiar ou além dele para um lado ou
outro. A lista prevista são as reações ao nível de classe com exatamente uma ponta medida.

### Enriquecimento

Toda feature pertence aos conjuntos que o seu nome lido lhe dá — a sua classe de lipídio, o seu
número de carbonos, o seu grau de insaturação, e, opcionalmente, a sua composição somada. Para
cada conjunto com pelo menos o número mínimo de membros entre as features testadas, a
probabilidade hipergeométrica de pelo menos o número observado de acertos entre as features
significativas, ajustada como a comparação foi. A mudança por classe é a média e a mediana do log2
fold change sobre as features da classe com a contagem para cima e para baixo e um teste t de uma
amostra dos log2 fold changes contra zero; o mapa de cadeias é o log2 fold change médio por célula
(carbonos, duplas ligações) de uma classe.

### Componentes principais

Uma matriz de metabolômica tem muito mais features do que injeções, de modo que os componentes vêm
da matriz de Gram injeção por injeção `X·Xᵀ` em vez da covariância das features: com oito injeções
é um problema de autovalores 8×8 (resolvido por rotação de Jacobi) em vez de um 1600×1600, e dá
exatamente os mesmos componentes. Os escores são os autovetores escalados pelas raízes quadradas
dos seus autovalores; os loadings são `Xᵀ·scores` normalizados; a variância explicada é a fração de
cada autovalor no total.

### Correção de deriva (QC-RLSC)

Para toda feature, lote a lote: os valores das injeções de QC contra a sua ordem de injeção são
ajustados com uma regressão linear localmente ponderada (pesos tricúbicos sobre a fração *span*
mais próxima dos controles), a curva ajustada é avaliada na ordem de toda injeção, e todo valor é
dividido por ela e multiplicado pela média geral de QC, de modo que os valores corrigidos mantêm o
tamanho dos originais. Um lote com menos de três controles é nivelado por um fator único — a sua
média de QC contra a média geral de QC. Uma feature é saltada quando menos de três controles no
total têm valor positivo. O coeficiente de variação sobre os controles antes e depois é a medida.
Nada é gravado no resultado.

### O modelo discriminante (PLS-DA)

As classes são codificadas em variáveis indicadoras em `Y` (uma coluna por classe), e os mínimos
quadrados parciais são ajustados por NIPALS para o número pedido de componentes, dando escores,
pesos e loadings. R²Y é a fração de `Y` que o ajuste reproduz; Q² é a fração prevista para cada
injeção quando o modelo é ajustado nas outras (leave-one-out), com a centragem e o escalonamento
refeitos dentro de cada dobra só a partir das injeções de treino. O p de permutação é a fração de
*k* embaralhamentos dos rótulos de classe cujo Q² atingiu o real. O VIP de cada feature é a soma
ponderada habitual dos quadrados dos pesos sobre os componentes. Com duas classes o lado com que
uma feature vai é lido do sinal do seu peso no primeiro componente contra as médias de classe dos
escores do primeiro componente.

A validação cruzada e as permutações são feitas no espaço que as injeções geram: as features são
reduzidas uma vez por dobra à matriz de Gram das injeções de treino e aos produtos cruzados com a
retida, e todo reajuste são algumas operações sobre matrizes *n*×*n*. Um teste segura este caminho
ao simples com nove decimais; sobre 2 083 features e oito injeções, 200 permutações levam cerca de
400 ms.

### O modelo ortogonal (OPLS-DA)

A correção de sinal ortogonal de Trygg e Wold: com duas classes `Y` é uma coluna; o vetor de pesos
`w` é a direção de `Xᵀy`; para cada componente ortogonal a parte do primeiro loading de NIPALS que
é ortogonal a `w` é tomada como `w_ortho`, o seu escore `t_ortho = X·w_ortho` e loading removidos
de `X`, e isto é repetido o número pedido de vezes. Um componente preditivo é então ajustado sobre
o que resta. O S-plot põe a covariância de cada feature com o escore preditivo na horizontal e a
sua correlação com ele na vertical. R²Y, Q² e o p de permutação são calculados da mesma forma que
para o modelo simples, no mesmo espaço reduzido, de modo que os dois são mantidos ao mesmo padrão.

### A rede molecular

Para todo par de features com espectros de produto, o cosseno modificado que o MS-DIAL e o GNPS
usam: os picos são casados gulosamente dentro da tolerância de fragmento, as intensidades são
postas em raiz quadrada e normalizadas ao pico-base, e o cosseno é tomado sobre os pares casados.
Os pares no corte ou acima dele viram arestas; as features sem aresta são contadas e, por padrão,
não desenhadas.

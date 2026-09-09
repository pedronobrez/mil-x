---
title: Dois fatores
section: Workspaces
order: 20
summary: A página Two factors da área Statistics — a análise de variância de dois fatores por feature, com a interação, e ASCA sobre a matriz inteira, num desenho com dois fatores como tratamento e tempo.
---

# Dois fatores

Tudo o mais na [[statistics-workspace]] assume que uma coisa varia entre as injeções. Muitos
experimentos variam duas: tratamento e ponto no tempo, genótipo e dieta, classe e lote. Esta página
toma o conjunto de dados da [[one-factor-analysis]] e dois fatores da tabela de amostras, e faz-lhe
duas perguntas — uma feature de cada vez, e à matriz como um todo. A aritmética está em
[[algorithms#Dois fatores]].

## De onde vem o segundo fator

O primeiro fator é em geral a **Class**. O segundo é digitado na coluna **Factor** da
[[samples-workspace]] — `day0` e `day7`, `chow` e `HFD`, seja qual for o outro eixo do desenho —
salvo com o projeto, e juntado às injeções do resultado pelo nome da amostra. **Batch** e o
**Sample type** também podem servir de fator, que é como um efeito de lote é testado contra as
classes. Cada fator precisa de pelo menos dois níveis; a página diz quando um não tem.

## A faixa

| Controle | O que faz |
| --- | --- |
| **Factor A**, **Factor B** | que duas colunas da tabela de amostras são o desenho: **Class**, **Factor (Samples workspace)**, **Batch**, **Sample type** |
| **Interaction** | testar se o efeito de um fator depende do nível do outro — o tratamento que funciona no dia 7 e não no dia 0. Precisa de uma réplica em pelo menos uma célula do desenho; com uma injeção por célula a interação não se distingue do erro, e a página diz isso e testa só os efeitos principais |
| **adjust**, **α** | o ajuste de testes múltiplos e o nível, como na página **Statistical test** |
| **permutations** | para o teste de cada efeito no ASCA (200 por padrão) |
| **Compute** | ajustar toda feature, depois partir a matriz |
| **Export table…** | a tabela da ANOVA como texto separado por tabulações, com as médias das células; veja [[exports#As tabelas de análise]] |

A linha sob a faixa diz qual foi o desenho — `2 × 3 design, 6 of 6 cells filled, 24 injections` —
e quantas features cada efeito moveu ao nível escolhido.

## A two-way ANOVA

![a página de dois fatores](images/statistics-two-factor.png)

Uma feature de cada vez, sobre os valores transformados: o F e o p do fator A, do fator B, e da sua
interação, cada p ajustado ao longo das features. A tabela é ordenada pelo menor dos três p
ajustados, de modo que as features que respondem ao desenho vêm primeiro, e as colunas dizem que
efeito foi. As somas de quadrados são do tipo II, por comparação de ajustes de mínimos quadrados
aninhados, de modo que um desenho desbalanceado — três réplicas aqui, duas ali — é tratado como
deve, e uma célula vazia custa à interação os seus graus de liberdade em vez do teste inteiro.

Escolher uma linha desenha as caixas da feature, uma por célula do desenho na ordem do fator A,
coloridas pelo fator B. Este é o gráfico de interação na sua forma honesta: caixas paralelas ao
longo dos níveis de B são um efeito principal, caixas que se cruzam são uma interação, e a linha
sob o gráfico dá os três p.

## ASCA

A ANOVA-simultaneous component analysis (Smilde *et al.*, 2005) faz a mesma pergunta à matriz
inteira de uma vez. A matriz centrada e escalonada é desmontada na parte que cada fator explica —
as médias dos seus níveis — na parte que a interação explica, e no resíduo; o gráfico de barras
diz que fração da variação cada uma contém, e um teste de permutação se essa fração é mais do que
rótulos embaralhados dariam (as barras são coloridas conforme é). Cada parte recebe então um modelo
de componentes principais próprio, e o gráfico de escores põe as injeções nos componentes do efeito
escolhido com o seu resíduo somado de volta, de modo que a dispersão das réplicas aparece contra a
separação que o fator faz: duas nuvens bem separadas são um fator a que o conjunto responde; nuvens
que se sobrepõem são um fator a que não responde, digam o que disserem algumas features soltas.

**Effect** escolhe que parte é desenhada — fator A, fator B ou a interação — e a linha ao lado dá a
fração, o p de permutação, e quanto do efeito os seus dois primeiros componentes carregam. Um fator
com dois níveis faz um efeito com um componente e sem segundo; os seus escores tomam então o
primeiro componente do próprio resíduo como eixo vertical, e a linha diz isso.

## Como ler

A ANOVA encontra as features; o ASCA diz se o desenho é visível no conjunto como um todo. Um fator
com cem features significativas e uma fração ASCA de 3 % a p 0,4 é um fator que moveu poucas
coisas muito e nada mais; um fator com fração ASCA de 30 % a p 0,005 e poucas features
significativas é um que moveu tudo um pouco, o que o teste por feature não consegue ver através do
ruído. Os dois são achados, de tipos diferentes.

Séries temporais com mais de dois pontos, medidas repetidas no mesmo sujeito, e desenhos com três
fatores não estão aqui; o teste **Paired** de duas classes e os dois fatores desta página são até
onde o desenho vai.

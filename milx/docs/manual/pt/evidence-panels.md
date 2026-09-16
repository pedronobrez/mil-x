---
title: Os painéis de evidência
section: Reviewing
order: 22
summary: A grade de picos sempre na tela e as nove abas abaixo dela — MS/MS, Isotopes, Candidates, Abundance, Feature map, Samples, Statistics, Trend, Other polarity — e a pergunta que cada uma responde.
---

# Os painéis de evidência

Nove vistas, cada uma respondendo a uma pergunta que um revisor de fato faz sobre a feature
selecionada. A primeira, o pico em toda amostra, é um painel próprio e está sempre na tela; as
outras oito são abas abaixo dele, com o espectro de produto mostrado por padrão, de modo que um
resultado abre no cromatograma e no espectro juntos. O divisor entre os dois é arrastável.

## Peaks

O pico cromatográfico em toda injeção de uma vez, um painel por injeção, numa grade. A feature
existe, está integrada da mesma forma em todo lado, está alinhada.

Cada painel desenha o cromatograma de íon extraído do m/z da feature à tolerância de centroide do
método, com a janela de retenção esperada sombreada de leve (o tempo de retenção da feature ± cinco
vezes a tolerância de alinhamento), a janela de integração do próprio pico sombreada em tom
quente, e um marcador no ápice. O título diz `sample · 1.23E5 · S/N 40`, com `not detected` para
um pico preenchido ou ausente e `gap-filled` quando foi preenchido. O painel da injeção
selecionada tem moldura na cor de destaque.

| Controle | O que faz |
| --- | --- |
| **Grid** `3 × 2` | colunas e linhas da grade; pagina quando há mais injeções do que células, com **◀** `1/2` **▶** |
| **Same Y** | uma escala de intensidade para todo painel, para que uma injeção fraca pareça fraca |
| **Link X** | uma janela de retenção para todo painel |
| modo de zoom | **Expected window** (em torno do tempo de retenção da feature), **Peak** (a janela de integração de cada injeção com uma margem), **Full trace** |
| **Magnify** | amplia o painel selecionado sobre as abas; duplo clique num painel faz o mesmo, **Close** devolve |

Clicar num painel seleciona a sua injeção (na aba Samples e no gráfico Trend também);
`⇧`-arrastar sobre um desenha uma janela de integração — veja [[reintegration]] para a faixa
acima da grade. Os painéis são carregados uma injeção de cada vez a partir do cache de varreduras
de survey; uma barra fina aparece enquanto ainda estão chegando.

## MS/MS

O espectro de produto deconvoluído da injeção representativa, espelhado contra a referência da
biblioteca quando a feature está anotada: medido em cima, biblioteca embaixo, com o precursor
marcado e os picos mais intensos rotulados. Ao lado, as **pontuações de casamento** — os números
por trás da anotação reportada, na ordem em que um revisor os lê:

| Pontuação | Significado |
| --- | --- |
| **Total score** | a pontuação combinada do MS-DIAL, aquela a que o corte se aplica |
| **Dot product** | o produto escalar ponderado entre o espectro medido e o de referência |
| **Reverse dot product** | o mesmo, contando só os picos da referência — quanto do espectro da biblioteca foi explicado |
| **Simple dot product** | o não ponderado |
| **Matched peaks** e **%** | quantos picos da referência foram encontrados, e que fração |
| **Mass similarity** | quão próxima ficou a massa do precursor |
| **RT similarity** | quando a biblioteca trazia um tempo de retenção e o método o usou |
| **Spectrum match** | sim ou não: se o espectro passou os cortes |
| **Lipid evidence** | class, chains ou sn-position: o nível de estrutura do lipídio que os fragmentos sustentaram |

A referência da biblioteca vem da biblioteca do próprio projeto quando está carregada, senão do
arquivo MSP que o método nomeia, lido uma vez por sessão. Veja [[annotation]].

## Isotopes

O envelope isotópico de MS1 da injeção representativa, com o íon monoisotópico primeiro e a
porcentagem monoisotópica no título. Um envelope que não cabe na fórmula, ou um íon monoisotópico
que não é o pico-base, é a assinatura de um isótopo ou aduto de outra feature — marque-a
Overannotation. `No MS1 isotope pattern stored` significa que a corrida não guardou nenhum para
esta feature.

## Candidates

Todo casamento de biblioteca que a corrida guardou para a feature, o melhor primeiro, com a
decomposição completa da pontuação: Candidate, Total, Dot, Reverse, Matched, Matched %, Mass sim.,
e marcas para a evidência de **Class**, **Chains** e **sn**. O que a corrida reportou está marcado
**Reported**. **Use this annotation** substitui o nome pelo do candidato selecionado; **Back to
the automatic name** desfaz.

![a lista de candidatos](images/review-candidates.png)

Quando o composto certo não está entre eles, a faixa **search library** pergunta de novo à
biblioteca por esta feature com tolerâncias à sua escolha — tolerância de precursor **MS1** em Da,
tolerância de fragmento **MS2** em Da, e opcionalmente **RT** com a sua tolerância em minutos — e
sem corte de pontuação nenhum, porque um revisor procura o que existe, não o que passa. **Search**
executa; **Back to the run's matches** volta à lista guardada. Sem espectro de produto todo
registro à mesma massa empata na pontuação, e a massa mais próxima decide a ordem. A busca corre o
mesmo anotador que o pipeline usou, de modo que uma pontuação aqui significa o mesmo que uma lá.
Veja [[annotation#Buscar de novo na biblioteca]].

![buscando de novo na biblioteca](images/review-search.png)

## Abundance

Uma barra por injeção, colorida por classe. Uma feature tão alta nos brancos quanto nas amostras é
fundo; uma que se dispersa pelas injeções de controle de qualidade não é quantificável.

## Feature map

Toda feature filtrada como um ponto em tempo de retenção contra m/z, colorido por classe; a
feature selecionada está marcada. Filtre para uma classe e os seus membros caem numa linha: numa
coluna de fase reversa a retenção sobe com o número de carbonos acila e desce com cada dupla
ligação. Um membro fora dessa linha é o primeiro a reexaminar. Clicar num ponto seleciona essa
feature na tabela de íons.

![o mapa de features](images/review-feature-map.png)

## Samples

Os números por injeção: **#** ordem, **Sample**, **Class**, **Type**, **RT**, **m/z**,
**Height**, **Area**, **S/N**, **Start** e **End** da janela de integração, e **Gap-filled**.
Selecionar uma linha seleciona essa injeção em todo lado — o seu painel na grade, o seu ponto na
tendência — e faz dela a amostra em foco para **This sample** na faixa de integração.

## Statistics

Por classe: **n**, **Mean height**, **SD**, **% CV**, **Min**, **Max**, **Mean area**. Um CV
sobre as injeções de QC acima de 30 % é a marca habitual de uma feature que não vai quantificar.

## Trend

Qualquer métrica por injeção contra outra, colorida por classe, pontos ligados em ordem: **Y** é
Height, Area, RT, m/z ou S/N; **X** é a ordem analítica ou qualquer das mesmas. Altura contra
ordem é a verificação de deriva para uma feature; a correção de deriva na [[statistics-workspace]]
é a mesma verificação para todas elas. Clicar num ponto seleciona a injeção.

## Other polarity

O mesmo composto como a outra corrida do lote o mediu: o espectro de produto desta corrida em cima,
o da outra embaixo. Vazia até que uma polaridade seja vinculada pela barra e uma feature que as
duas corridas viram seja selecionada.

As duas metades não são um casamento a ser pontuado — uma molécula protonada e uma desprotonada se
fragmentam de formas diferentes. O que concorda é a massa neutra, o tempo de retenção e o perfil de
altura ao longo das injeções, e a linha sob o espelho diz os três, com o nome do parceiro, o m/z, o
aduto, o sinal-ruído e qual corrida quantifica o composto. Veja [[polarity-merge]].

Com **Split evidence** ligado, MS/MS à esquerda e esta aba à direita é o arranjo para o qual se
vincula uma polaridade; a segunda coluna a seleciona sozinha na primeira vez.

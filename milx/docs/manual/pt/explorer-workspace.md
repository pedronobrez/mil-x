---
title: A área de trabalho Explorer
section: Workspaces
order: 14
summary: Os arquivos brutos como cromatogramas e espectros — a árvore de canais, sobreposições TIC/BPC/XIC, espectros varredura a varredura, média, o painel de picos e XICs manuais.
---

# A área de trabalho Explorer

Um olhar qualitativo sobre os arquivos brutos, antes ou depois do processamento: uma árvore de
amostras e os seus canais à esquerda, um cromatograma sobre um espectro no centro, e painéis à
direita. `⌘1` mostra-a. Lê qualquer formato que a aplicação lê — veja [[raw-data-formats]] — e é
onde um arquivo bruto solto sobre a aplicação abre.

![o Explorer](images/explorer.png)

## Amostras e canais

Cada arquivo do lote é um nó; expandi-lo carrega o arquivo (o primeiro é carregado e o seu TIC
marcado quando a área de trabalho se preenche) e lista os seus **canais**: os conjuntos de
espectros que formam um cromatograma cada.

- **TIC** — a corrente iônica total de toda a amostra. Numa aquisição com vários experimentos por ciclo, todos os experimentos de um ciclo são somados num ponto, como o OpenQuant desenha.
- **MS1** ou **TOF MS 100–1500** — as varreduras de survey.
- **MS2 events (n)** — num arquivo dependente de dados, todas as varreduras de íons-produto como um só canal, já que o seu precursor muda de varredura para varredura.
- **MS2 400.0–425.0, CE 30** — num arquivo independente de dados, um canal por janela de isolamento, reconhecido por um alvo de isolamento e uma energia de colisão que se repetem ao longo da corrida.
- **IDA MS2 slot 3 → 50–1000, CE 35** — num arquivo IDA da SCIEX, um canal por slot de experimento dependente; as suas contagens de varreduras caem à medida que os slots posteriores disparam menos vezes.

Marcar um canal desenha-o; a caixa de **filtro** estreita a árvore pelo rótulo do canal
(`MS2 313.2`) e mantém visíveis os canais marcados. **Uncheck all**, **TIC of all** e **Collapse**
no fundo fazem o que dizem; os mesmos três estão no menu View.

![canais IDA](images/explorer-ida.png)

## O cromatograma

Cada canal marcado é desenhado como um traço, mais os XICs manuais da amostra ativa. A faixa
acima escolhe **TIC** ou **BPC** (cromatograma de pico-base) para os traços dos canais, e o
**Active channel** — aquele de que o painel de espectro e os XICs leem; selecionar um nó na
árvore define-o.

A barra de ferramentas:

| Controle | O que faz |
| --- | --- |
| **Select range** | um arrasto simples seleciona um intervalo de tempo de retenção em vez de ampliar; `⇧`-arrasto seleciona sempre |
| **Overview** | ajusta o cromatograma inteiro; duplo clique faz o mesmo |
| **Clear range** | descarta a seleção |
| **Normalise** | escala todo traço a 100 %; o eixo Y passa a intensidade relativa |
| **Stack** | desenha cada traço na sua própria faixa horizontal |
| **Contour** | substitui o cromatograma pelo canal ativo como superfície — veja [[explorer-workspace#O contorno]] |
| **m/z labels** | rotula picos do espectro com o seu m/z — não simplesmente os mais altos: o eixo visível é cortado em oito regiões e cada uma pode nomear até três picos, do mais alto para baixo em rodadas, de modo que um trecho lotado de massas baixas não gasta todo rótulo antes de um pico solitário mais acima ser nomeado; um rótulo que imprimiria sobre outro é descartado. A mesma regra desenha o espelho, os painéis de evidência e a figura exportada |
| **RT labels** | rotula o ápice de cada traço em vista com o seu tempo de retenção |
| **Legend** | |
| **Smooth (σ, scans)** | uma suavização gaussiana de todo traço, em varreduras; 0 desliga |
| **Baseline (min)** | subtrai uma linha de base de mínimo móvel sobre esse número de minutos; 0 desliga |

Os gestos dos gráficos, iguais em todo gráfico da aplicação, estão em
[[keyboard-shortcuts#Gráficos]]: arrastar para ampliar o eixo X, roda para ampliar, arrastar com
o botão direito para deslocar, duplo clique para ajustar, `⇧`-arrasto para selecionar, pairar
para uma dica.

Clicar no cromatograma mostra a varredura mais próxima do canal ativo no painel de espectro e põe
um marcador no seu tempo de retenção.

## O contorno

**Contour** na barra de ferramentas, ou **View ▸ Explorer: contour view**, desenha o canal ativo
como superfície: tempo de retenção na horizontal, m/z na vertical, intensidade como cor. Um
cromatograma soma o eixo de massa para fora e um espectro soma o eixo de tempo para fora; o
contorno são os mesmos dados sem nenhuma das somas, de modo que uma interferência ao lado de um
alvo, ou uma crista que corre a corrida inteira numa massa, fica visível em vez de inferida.

Cada varredura do canal é uma linha e cada bin de m/z uma coluna. O eixo de massa é dividido em
cerca de 1.400 bins — mais ou menos um por pixel da tela num intervalo largo. Uma corrida longa
tem mais varreduras do que a tela tem linhas, e as varreduras além do limite de cerca de 900 linhas
são **médias na sua linha, não puladas**: amostrar cada k-ésima varredura perde por completo um
pico de uma varredura, que é o contrário do que uma figura dos dados serve. A média e não a soma,
porque o último grupo costuma ser curto e uma linha mais escura só por isso seria uma mentira sobre
os dados. A faixa acima da superfície diz quantas linhas e bins foram construídos, de quantas
varreduras, e quanto tempo levou.

| Controle | Efeito |
| --- | --- |
| **Intensity** | como as intensidades viram cor: *sqrt* (padrão), *log* ou *linear*. Uma rampa linear sobre quatro décadas mostra o pico-base e mais nada; a raiz quadrada ergue os íons menores à vista sem o achatamento que deixa um gráfico log uniformemente cinza |
| **Colours** | a paleta: inferno, viridis, magma, turbo, greys |
| **m/z … to** | o intervalo de massa da próxima construção; 0 e 0 é o intervalo do próprio canal |
| **Rebuild** | reconstrói a grade sobre o intervalo de tempo na tela e o intervalo de m/z digitado, em resolução plena para esse intervalo |
| **Extract this view** | o cromatograma e o espectro do retângulo na tela — ambos lidos do leitor, não da grade: o XIC da faixa de m/z entra na lista do Manual XIC e o espectro médio do intervalo de tempo preenche o painel de espectro, e a vista volta ao cromatograma para mostrá-los |
| clique | o espectro da varredura sob o cursor, no painel de espectro |
| passar o mouse | o tempo de retenção, o m/z e a intensidade sob o cursor |

O topo da escala de cor é o percentil 99,7 das células não nulas, não o máximo: uma única
varredura saturada ou com um pico espúrio fixaria a escala para todo o resto e deixaria a
superfície preta. A barra de cor nomeia a lei e o topo. Arraste para ampliar o eixo de tempo, roda
para ampliar, arraste com o botão direito para deslocar, clique duplo para ajustar, como em todo
gráfico; a figura sai como SVG ou PNG pelo menu do botão direito do próprio gráfico, veja
[[chart-export]].

Nada é quantificado a partir do contorno: os seus bins de m/z são mais largos do que os passos do
instrumento e as suas linhas podem ser várias varreduras em média, portanto é uma figura, de
propósito. *Extract this view* volta ao leitor tanto para o XIC quanto para o espectro.

## O espectro

A varredura em que o marcador está, como espectro de barras normalizado a 100 %, com o seu
precursor marcado quando tem um. Para uma varredura de íons-produto, a faixa acima do painel nomeia
o precursor que a própria varredura isolou, a sua janela de isolamento, a energia de colisão e, num
arquivo IDA da SCIEX, o slot de que veio — por varredura, nunca a massa de preenchimento do slot. **◀** e **▶** e a caixa de varredura percorrem as varreduras do
canal ativo; a contagem ao lado é o número de varreduras no canal. **Average selected range**
substitui a varredura pelo espectro médio sobre o intervalo de tempo de retenção selecionado, que
é como se lê um espectro de produto fraco.

Quando um pico do painel Peaks é selecionado, o painel mostra em vez disso o seu MS/MS
deconvoluído, espelhado contra a referência da biblioteca quando o pico está anotado: medido em
cima, biblioteca embaixo. `⇧`-arrastar sobre o próprio espectro seleciona um intervalo de m/z, e **XIC from selection**
extrai-o como traço cuja janela é exatamente o intervalo selecionado — o *Extract XIC from
selection* do MIL-Q.

## Os painéis

**Peaks** — os picos que a corrida encontrou na amostra ativa, filtráveis por nome ou m/z: Name,
RT, m/z, Height, Score. Selecionar um desenha o seu XIC como entrada manual, sombreia a sua janela
de integração no cromatograma, marca o seu ápice e mostra o seu MS/MS deconvoluído. O título diz
quantos picos e quantos estão anotados; `No results for this sample` antes de uma corrida.

**Manual XIC** — cromatogramas de íon extraído da amostra ativa. Digite um ou mais valores de
m/z, separados por vírgulas ou espaços (`183.1391, 313.2384`), uma tolerância em Da ou ppm, e
**Add**; cada entrada é desenhada como traço tracejado e pode ser removida com o seu `✕` ou todas
de uma vez com **Clear all**. O **Source** escolhe sobre que varreduras a janela é somada:

| Source | Varreduras |
| --- | --- |
| **MS1** | o canal de survey da amostra ativa (o padrão) |
| **Active channel** | o canal de que o painel de espectro lê, seja qual for o seu tipo |
| **Every checked channel** | um traço por canal marcado, para comparar a mesma massa entre slots ou janelas |
| **All MS2** | toda varredura de íons-produto do arquivo, de todos os canais, fundidas por tempo de retenção |
| **Gated by precursor** | só as varreduras MS2 cujo próprio precursor está a **± Da** do **Precursor m/z** digitado ao lado, de qualquer slot IDA ou evento DDA, fundidas por tempo de retenção |

A fonte com portão é o que o cromatograma de um íon-produto precisa num arquivo dependente de
dados: o instrumento põe os espectros de um precursor no slot que estava livre naquele ciclo, e o
rótulo do slot carrega só a massa de preenchimento com que o método foi escrito. O portão lê o
precursor que cada varredura de fato isolou — na IDA da SCIEX, o `ParentMZ` por varredura — e
assim um precursor espalhado por doze slots vira um traço. Uma linha sob o formulário diz quantas
varreduras o portão encontrou e de quantos slots vieram. A mesma fonte é `gated:MZ[:TOL]` na
[[command-line]].

**Mass calc** — uma fórmula nos números de que um espectrometrista de massas precisa, e uma massa
medida num erro; as tabelas e a aritmética são as do MIL-Q, de modo que as duas aplicações dão os
mesmos números. Digite uma fórmula molecular: `C18H34O4`. Grupos com multiplicadores,
`C6H4(NO2)2`, e isótopos isolados entre colchetes, `[13C]6H12O6`, são lidos, com `D` aceito para
deutério, de modo que um padrão interno marcado como `C18H30D4O4` é digitado como se escreve. O
painel informa a fórmula na ordem de Hill, a massa monoisotópica, a massa média, o equivalente de
anéis e ligações duplas (RDBE) e o m/z do íon para o aduto escolhido ao lado da fórmula:

| Negativo | Positivo |
| --- | --- |
| [M−H]⁻ | [M+H]⁺ |
| [M+Cl]⁻ | [M+NH₄]⁺ |
| [M+HCOO]⁻ | [M+Na]⁺ |
| [M+CH₃COO]⁻ | [M+K]⁺ |
| [M−2H]²⁻ | [M+2H]²⁺ |

A massa do íon leva em conta o elétron: um cátion é mais leve do que a soma dos seus átomos em uma
massa de elétron e um ânion mais pesado, o que importa na quarta casa. Digite um m/z medido sob o
calculado — ou pressione **Measured from spectrum** para tomar o pico do painel de espectro mais
próximo do íon teórico — e o erro aparece em mDa e em ppm. O padrão isotópico teórico do íon — m/z
e abundância relativa de M, M+1, M+2 e assim por diante, até 0,1 % — é tabulado, e **Overlay on
spectrum** desenha-o no painel de espectro como barras tracejadas na escala do pico medido mais
próximo da massa monoisotópica, para comparar os satélites medidos com os esperados; **Clear
overlay** remove-o, e mostrar outro espectro também. A sobreposição só faz sentido num espectro
que carrega os satélites: uma varredura de íons-produto isola o precursor monoisotópico, portanto
sobreponha no canal de survey.

**Spectrum peaks** — o espectro do painel como tabela: m/z, intensidade, intensidade relativa,
mais intenso primeiro. Clique duplo numa linha extrai a massa daquele pico como XIC com a tolerância
definida na aba Manual XIC, como faz o MIL-Q.

## O que não faz

O Explorer desenha e lê; não edita. A reintegração, as marcações e tudo o mais que altera um
resultado vive na [[analytics-workspace]].

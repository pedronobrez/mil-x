---
title: Gráficos, suas opções e sua exportação
section: Reviewing
order: 26
summary: O que se pode dizer a todo gráfico sobre título, rótulos, tamanhos, paleta e escala de cores, e como ele sai da aplicação como figura — SVG ou PNG, claro ou escuro, sobre papel ou sobre nada.
---

# Gráficos, suas opções e sua exportação

Todo gráfico da [[statistics-workspace]] fica numa moldura com dois controles no canto superior
direito: **⚙** abre as opções do gráfico, e **Export…** grava-o como imagem. A moldura não se
importa com o gráfico que contém; os mesmos dois controles funcionam nos gráficos de dispersão,
nas caixas, nas barras, nos mapas de calor, no dendrograma e na rede.

Todo outro gráfico da aplicação — o cromatograma e o espectro do [[explorer-workspace]], os painéis
de pico, o espelho, o envelope isotópico, o mapa de features e as barras de abundância da
[[analytics-workspace]] — tem a exportação sem a moldura: **clique com o botão direito no gráfico**
e escolha **Export as a picture…**. Aqueles painéis estão cheios de evidência e não têm espaço para
uma faixa de botões, de modo que o comando vive no menu. Num gráfico em que o botão direito
desloca, um *arrasto* com o direito continua deslocando; só um clique direito que não se move
oferece o menu.

## As opções

| Opção | O que muda | Em que gráficos |
| --- | --- | --- |
| **Title** | um título desenhado acima do gráfico; vazio por padrão, porque a linha acima da moldura já diz o que o gráfico é, e uma figura num artigo quer o seu próprio | todos |
| **X label**, **Y label** | os rótulos dos eixos, pré-preenchidos com os do próprio gráfico | dispersão, caixas, barras, rank |
| **Point size** | o raio de um ponto (4,2 por padrão; 1,5 a 12) | dispersão, caixas |
| **Font scale** | todo texto do gráfico de uma vez (1 por padrão; 0,7 a 2,2) — aumente para uma figura que vai ser reduzida | todos |
| **Palette** | as cores dos grupos: **Tableau** (o padrão, dez cores), **Okabe-Ito** (oito, segura para todo tipo de daltonismo), **Grey** (para uma figura em preto e branco) ou **Accent** | dispersão, caixas, barras, rank |
| **Colour scale** | a escala de um mapa de calor: **Blue–white–red** (divergente, para z-scores e fold changes, branco no zero), **Viridis** (sequencial, perceptualmente uniforme), **Greys**, ou **Green–black–red** (a convenção dos microarranjos) | mapa de calor, mapa de cadeias, correlações |
| **Grid** | as linhas de grade atrás do gráfico | dispersão, caixas, barras |
| **Legend** | a legenda dos grupos; fica no canto do gráfico que tem menos coisa por baixo, de modo que nunca cobre os pontos nomeados de um volcano | dispersão, linhas |
| **Labels** | os nomes ao lado dos pontos, ou os rótulos de linha de um mapa de calor | dispersão, mapa de calor |
| **Ellipses** | a elipse de confiança de 95 % de cada classe num gráfico de scores | dispersão |
| **Trees** | os dendrogramas ao lado de um mapa de calor | mapa de calor |
| **Values** | os números impressos nas células | mapa de calor, mapa de cadeias |

Uma opção aplica-se a esse gráfico e permanece enquanto a área de trabalho está aberta; não é
gravada em lado nenhum. As opções que um gráfico não tem ficam em cinza.

## Exportar

**Export…**, ou **Export as a picture…** no menu do próprio gráfico, abre um só diálogo: a figura
como será gravada, ao lado das escolhas que a fazem.

![o diálogo de exportação](images/export-dialog.png)

| Escolha | O que faz |
| --- | --- |
| **Format** | **SVG**, uma figura vetorial, ou **PNG**, uma imagem |
| **Resolution** | só para PNG: 2×, 3×, 4× ou 6× o tamanho do gráfico em tela |
| **Theme** | **Light**, **Dark**, ou **As on screen** |
| **Background** | **Paper** (a superfície do próprio tema), **White**, ou **Transparent** |
| **Type size** | todo o texto da figura junto, de 0,7 a 2,2 do que a tela mostra |

**O tema é o da figura, não o da janela.** Quem revisa no tema escuro ainda quer uma figura clara
para um artigo, um slide ou uma impressora, e não deveria ter de trocar a aplicação inteira para
consegui-la — por isso o diálogo abre em **Light**, esteja a aplicação como estiver. **As on
screen** mantém o tema da janela, que é o que uma figura para um slide escuro quer. Todo o resto do
gráfico — a paleta, os rótulos, a grade, a legenda, as elipses — fica exatamente como as opções do
gráfico o deixaram; só a tinta, as réguas e o papel mudam.

**Transparent** grava um PNG sem nada atrás da figura, para pousar sobre um slide colorido. Um SVG
pedido do mesmo jeito é gravado sem o seu retângulo de fundo.

A pré-visualização é a exportação: o mesmo gráfico desenhado pelo mesmo código com as mesmas
definições, de modo que o que está no diálogo é o que cai no arquivo. A linha ao lado dos botões diz
o tamanho do arquivo — em pixels para um PNG, em pontos para um SVG, que serve depois em qualquer
tamanho. As escolhas ficam guardadas, nas configurações, para a próxima figura.

**SVG** é o que se leva para uma figura. Toda linha é uma linha, todo ponto um círculo, todo rótulo
um elemento de texto na fonte do gráfico, de modo que abre no Illustrator, no Inkscape ou num
navegador em qualquer tamanho sem nada para redesenhar, e os rótulos podem ser editados no lugar.
**PNG** é a mesma imagem rasterizada: um gráfico com 600 pontos de largura em tela tem 2400 pixels
a 4×, o que a 300 dpi é uma figura de oito polegadas. Os dois são desenhados pelo código que
desenha a tela, de modo que uma figura a 6× é a figura da tela, apenas maior.

## De um script

O canal de comando descrito em [[building-and-testing#O smoke test]] exporta um gráfico sem
nenhum dos dois diálogos: `exportChart` recebe o `path`, um `page` e `index` para um gráfico de uma
página de estatística ou um `control` para um gráfico nomeado de qualquer área, e os mesmos
`format`, `scale`, `theme`, `background` e `fontScale` que o diálogo oferece. `ChartExport.Save` é a
única chamada por trás de tudo isso, e os testes gravam figuras dos dois jeitos — clara a partir de
uma janela escura, sobre branco e sobre nada — para provar que saem como pedidas.

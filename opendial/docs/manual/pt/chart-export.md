---
title: Gráficos, suas opções e sua exportação
section: Reviewing
order: 26
summary: O que se pode dizer a todo gráfico da área Statistics sobre título, rótulos, tamanhos, paleta e escala de cores, e como ele é gravado como SVG ou PNG.
---

# Gráficos, suas opções e sua exportação

Todo gráfico da [[statistics-workspace]] fica numa moldura com dois controles no canto superior
direito: **⚙** abre as opções do gráfico, e **Export…** grava-o como imagem. A moldura não se
importa com o gráfico que contém; os mesmos dois controles funcionam nos gráficos de dispersão,
nas caixas, nas barras, nos mapas de calor, no dendrograma e na rede.

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

**Export…** oferece **SVG** e **PNG a 2×, 4× e 6×** o tamanho em tela, e pergunta onde salvar; o
nome sugerido é a linha acima do gráfico.

**SVG** é o que se leva para uma figura. É desenhado pelo mesmo código que desenha a tela — toda
linha é uma linha, todo ponto um círculo, todo rótulo um elemento de texto na fonte do gráfico —
de modo que abre no Illustrator, no Inkscape ou num navegador em qualquer tamanho sem nada para
redesenhar, e os rótulos podem ser editados no lugar. O fundo é o do próprio gráfico (branco no
tema claro, a cor do painel no escuro).

**PNG** é a mesma imagem rasterizada a um múltiplo da resolução da tela, de modo que um gráfico
com 600 pixels de largura em tela tem 2400 pixels a 4×, o que a 300 dpi é uma figura de oito
polegadas. O 6× é para uma de página inteira. Os dois são gravados com as opções como estão nesse
momento: defina a escala de fonte e a paleta primeiro, depois exporte.

## De um script

As molduras são visíveis ao canal de comando descrito em [[building-and-testing#O smoke test]], e
um gráfico pode ser exportado sem o painel de salvar nomeando o caminho; `ChartExport.SaveSvg` e
`ChartExport.SavePng` são as duas chamadas, e os testes gravam todo gráfico de toda página das duas
formas para provar que conseguem.

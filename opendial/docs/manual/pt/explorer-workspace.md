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
| **m/z labels** | rotula os oito picos mais intensos do espectro com o seu m/z |
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

## O espectro

A varredura em que o marcador está, como espectro de barras normalizado a 100 %, com o seu
precursor marcado quando tem um. **◀** e **▶** e a caixa de varredura percorrem as varreduras do
canal ativo; a contagem ao lado é o número de varreduras no canal. **Average selected range**
substitui a varredura pelo espectro médio sobre o intervalo de tempo de retenção selecionado, que
é como se lê um espectro de produto fraco.

Quando um pico do painel Peaks é selecionado, o painel mostra em vez disso o seu MS/MS
deconvoluído, espelhado contra a referência da biblioteca quando o pico está anotado: medido em
cima, biblioteca embaixo.

## Os painéis

**Peaks** — os picos que a corrida encontrou na amostra ativa, filtráveis por nome ou m/z: Name,
RT, m/z, Height, Score. Selecionar um desenha o seu XIC como entrada manual, sombreia a sua janela
de integração no cromatograma, marca o seu ápice e mostra o seu MS/MS deconvoluído. O título diz
quantos picos e quantos estão anotados; `No results for this sample` antes de uma corrida.

**Manual XIC** — cromatogramas de íon extraído do canal MS1 da amostra ativa. Digite um ou mais
valores de m/z (separados por espaço), uma tolerância em Da ou ppm, e **Add**; cada entrada é
desenhada como traço tracejado e pode ser removida com o seu `✕` ou todas de uma vez com **Clear
all**.

**Spectrum peaks** — o espectro do painel como tabela: m/z, intensidade, intensidade relativa,
mais intenso primeiro.

## O que não faz

O Explorer desenha e lê; não edita. A reintegração, as marcações e tudo o mais que altera um
resultado vive na [[analytics-workspace]].

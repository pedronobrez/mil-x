---
title: Atalhos de teclado
section: Reference
order: 41
summary: Todo atalho da aplicação, as teclas de revisão, os gestos dos gráficos, e as teclas do próprio manual.
---

# Atalhos de teclado

`⌘` é a tecla command num Mac; no Linux e no Windows leia-a como `Ctrl`. Onde os dois estão
listados, os dois funcionam em todo lado.

## A janela

| Atalho | Ação |
| --- | --- |
| `⌘1` / `Ctrl+1` | área Explorer |
| `⌘2` / `Ctrl+2` | área Analytics |
| `⌘3` / `Ctrl+3` | área Method |
| `⌘4` / `Ctrl+4` | área Samples |
| `⌘5` / `Ctrl+5` | área Statistics |
| `⌘N` | New project… |
| `⌘O` | Open project… |
| `⌘S` / `Ctrl+S` | Save project |
| `⌘R` | Process batch |
| `⌘L` | mostrar ou esconder o log |
| `F1` | o manual, na página da área de trabalho atual |
| `⌘⇧?` | o mesmo |
| `⌘Q` | Quit |

## Revisão (área Analytics)

| Atalho | Ação |
| --- | --- |
| `⌘⇧1` / `Ctrl+Shift+1` | alternar **Confirmed** |
| `⌘⇧2` / `Ctrl+Shift+2` | alternar **Low quality spectrum** |
| `⌘⇧3` / `Ctrl+Shift+3` | alternar **Misannotation** |
| `⌘⇧4` / `Ctrl+Shift+4` | alternar **Coelution (mixed spectra)** |
| `⌘⇧5` / `Ctrl+Shift+5` | alternar **Overannotation** |
| `⌘⇧0` / `Ctrl+Shift+0` | limpar toda marcação da feature |
| `⌘⇧C` / `Ctrl+Shift+C` | **Confirm ▸** — Confirmed, e a feature seguinte |
| `⌘⇧X` / `Ctrl+Shift+X` | **Reject ▸** — Misannotation, e a feature seguinte |
| `⌘⇧N` / `Ctrl+Shift+N` | **Next unreviewed** |
| `⌘⇧↓` / `Ctrl+Shift+↓` | feature seguinte |
| `⌘⇧↑` / `Ctrl+Shift+↑` | feature anterior |

As teclas de revisão são as teclas de área de trabalho com shift: `⌘1` é uma área de trabalho,
`⌘⇧1` é uma marcação. Funcionam seja qual for o foco — a tabela, um gráfico, uma caixa de texto —
na janela principal enquanto Analytics está na tela e na janela própria da tabela de íons, e não
fazem nada noutro lugar. Veja [[review-tags]].

## Gráficos

Os mesmos gestos em todo gráfico — cromatogramas, espectros, gráficos de dispersão:

| Gesto | Ação |
| --- | --- |
| arrastar | ampliar o eixo X para o intervalo arrastado |
| roda | ampliar e reduzir em torno do ponteiro |
| arrastar com o botão direito | deslocar |
| duplo clique | ajustar tudo de novo (**Overview** no Explorer faz o mesmo) |
| `⇧`-arrastar | selecionar um intervalo: um intervalo de tempo de retenção no Explorer, uma janela de integração num painel de pico |
| clique | selecionar a varredura mais próxima (Explorer), a amostra (grade de picos, tendência) ou a feature (mapa de features, S-plot, loadings) |
| pairar | uma dica com os valores sob o ponteiro |

No Explorer, **Select range** faz um arrasto simples selecionar em vez de ampliar.

## O manual

| Atalho | Ação |
| --- | --- |
| `⌘F` / `Ctrl+F` | focar a caixa de busca |
| `Esc` | limpar a busca |
| `⌘[` / `Alt+←` | voltar |
| `⌘]` / `Alt+→` | avançar |

## A tabela Samples

`⇧`-clique seleciona um intervalo de linhas, `⌘`-clique adiciona uma; **Set type of selected** e
**Set class of selected** aplicam-se então a todas elas.

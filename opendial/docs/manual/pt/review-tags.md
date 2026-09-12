---
title: Marcações e vereditos
section: Reviewing
order: 21
summary: As cinco bandeiras de revisão do MS-DIAL, o teclado que as define, Confirm e Reject, comentários, marcação em massa, e onde a revisão é guardada.
---

# Marcações e vereditos

O veredito sobre uma feature é uma ou mais das cinco bandeiras do MS-DIAL. Elas têm os mesmos
nomes e os mesmos ids numéricos que no MS-DIAL, de modo que uma revisão feita aqui aparece na
aplicação Windows, e uma feita lá aparece aqui.

## As cinco marcações

| Tecla | Marcação | Significa |
| --- | --- | --- |
| `⌘⇧1` | **Confirmed** | verificada, e certa |
| `⌘⇧2` | **Low quality spectrum** | fraca ou ruidosa demais para julgar |
| `⌘⇧3` | **Misannotation** | o nome está errado |
| `⌘⇧4` | **Coelution (mixed spectra)** | dois compostos sob um pico |
| `⌘⇧5` | **Overannotation** | um isótopo, aduto ou fragmento em fonte de outra coisa, ou um nome que afirma mais do que o espectro mostra |
| `⌘⇧0` | *(limpar)* | remove toda marcação da feature |

Num teclado sem tecla de comando, `Ctrl+Shift` com o mesmo dígito. Não são exclusivas: uma feature pode ser Coelution e Overannotation ao mesmo tempo. Cada tecla
alterna a sua marcação; os botões da faixa de veredito — **Confirmed**, **Low quality**,
**Misannotation**, **Coelution**, **Overannotation**, **Clear** — fazem o mesmo com o mouse, e
acendem quando a marcação está ligada. A tabela de íons mostra as marcações em forma curta e, na
sua primeira coluna, `✓` para Confirmed, `✕` para Misannotation e `●` para qualquer coisa
sinalizada para voltar.

## Confirm e Reject

**Confirm ▸** (`⌘⇧C`) põe Confirmed, limpa Misannotation, e passa à feature seguinte.
**Reject ▸** (`⌘⇧X`) põe Misannotation, limpa Confirmed, e segue. Juntamente com **Next
unreviewed** (`⌘⇧N`), que salta o que já está decidido, são tudo o que uma passagem rápida
precisa.

## Revisada

Uma feature conta como **revisada** quando carrega qualquer marcação, ou quando o revisor a marcou
como tal sem nenhuma. Os estados **Reviewed** e **Not reviewed** da faixa de filtros e a contagem
na faixa leem isso. Uma feature com comentário mas sem marcação não está revisada.

## Comentários e nomes escolhidos à mão

A caixa sob a faixa de veredito recebe um comentário em texto livre sobre a feature, mostrado na
coluna Comment da tabela de íons e gravado na exportação da tabela revisada. Um nome escolhido na
lista de candidatos com **Use this annotation** substitui o nome da corrida na tabela e em tudo a
jusante, e mostra uma etiqueta **hand-picked** ao lado da identidade da feature; **Back to the
automatic name** desfaz. Veja [[annotation]].

## Marcar muitas de uma vez

**Confirm all shown** marca como Confirmed toda feature que a faixa de filtros está mostrando e
limpa Misannotation nelas; **Clear all shown** remove toda marcação delas. Revisar uma classe de
lipídios é em geral uma questão de filtrar para ela, conferir a sua tendência de tempo de retenção
no mapa de features, e aceitar o conjunto inteiro.

## Onde a revisão é guardada

**Save review** na barra de ferramentas — ou deixar a área de trabalho com marcações por salvar,
o que também as grava — escreve dois arquivos ao lado do resultado do alinhamento:

- `<alignment>_tags.xml` — as marcações, no esquema do próprio MS-DIAL. O MS-DIAL lê e escreve este arquivo; a curadoria feita no Windows aparece aqui, e a feita aqui aparece lá.
- `<alignment>_curation.json` — os comentários, os nomes escolhidos à mão e as bandeiras de revisado, que o MS-DIAL guarda dentro do seu arquivo binário de alinhamento. Escrevê-los num arquivo lateral deixa os binários intocados por uma revisão só de marcações.

Quando picos foram reintegrados ou separados, **Save review** também grava de volta o próprio
resultado do alinhamento, guardando os arquivos como estavam antes da primeira edição da sessão
com um sufixo `.before-curation`; veja [[reintegration]]. O ponto no botão significa que há algo
por salvar. Os dois arquivos estão descritos em [[projects-and-files]].

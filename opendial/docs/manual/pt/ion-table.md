---
title: A tabela de íons
section: Reviewing
order: 20
summary: Toda feature alinhada como uma linha — as colunas, ordenar e reordenar, os glifos de veredito, as barras de abundância e a janela destacável.
---

# A tabela de íons

Toda feature alinhada que passa a faixa de filtros, uma por linha, ordenável por qualquer coluna.
É a espinha da [[analytics-workspace]]: a linha selecionada é o que todo painel de evidência
mostra e onde toda marcação cai.

## As colunas

| Coluna | O que guarda |
| --- | --- |
| *(veredito)* | a revisão num relance: `✓` Confirmed, `✕` Misannotation, `●` sinalizada para voltar (Low quality, Coelution ou Overannotation); ordenável pelas marcações |
| **ID** | o id de alinhamento da feature, o número que o MS-DIAL e toda exportação usam |
| **RT** | o tempo de retenção médio entre as injeções, em minutos |
| **m/z** | o m/z médio, com quatro decimais |
| **Annotation** | o nome — o do revisor quando um foi escolhido à mão, senão o da corrida; `Unknown  m/z 760.5851` quando não há nenhum |
| **Level** | confident, suggested, m/z only, ou vazio; veja [[concepts#Níveis de anotação]] |
| **Adduct** | `[M+H]+`, `[M+Na]+`, … |
| **Class** | a ontologia que o MS-DIAL deu ao composto, que para um lipídio é a sua classe |
| **Fill %** | a fração de injeções em que o pico foi detectado em vez de preenchido |
| **MS/MS** | `MS/MS` quando pelo menos uma injeção trazia um espectro de produto |
| **S/N** | o sinal-ruído médio |
| **Blank %** | a altura média nas injeções do tipo Blank contra a média nas amostras; vazio quando o lote não tem branco |
| **Ion of** | o que esta linha é, quando não é um composto por si: o aduto, isótopo, fragmento de fonte ou dímero de outra feature, e de qual — veja [[annotation#Um composto, vários íons]] |
| **Score** | a pontuação total de casamento da anotação reportada |
| **Height** | a altura média do ápice |
| **Iso** | `M` para um íon monoisotópico, `M+1`, `M+2` para uma feature que a corrida marcou como isótopo de outra; vazio quando não decidiu |
| **Abundance** | uma barra por classe de amostra, a altura média dessa classe — assim uma feature tão alta nos brancos quanto nas amostras salta à vista ao rolar, sem abrir um painel; ordena por altura |
| **Tags** | as marcações, em forma curta |
| **Comment** | o comentário do revisor |

As colunas podem ser arrastadas para a ordem que convier ao trabalho, redimensionadas e
ordenadas clicando no cabeçalho; a ordem volta no próximo arranque.

## Os botões do cabeçalho

| Botão | O que faz |
| --- | --- |
| **▲** / **▼** | feature anterior e seguinte (`⌘⇧↑`, `⌘⇧↓`) |
| **Next unreviewed** | a próxima feature sem veredito, dando a volta (`⌘⇧N`); diz quando não resta nenhuma no filtro |
| **Confirm all shown** | marca como Confirmed toda feature que o filtro está mostrando, que é como uma classe inteira de lipídios é aceite depois de a sua tendência de retenção conferir |
| **Reject all shown** | a outra metade do par: marca todas como Misannotation, para o filtro que se revelou ruído |
| **Clear all shown** | remove toda marcação das features mostradas |
| **Open in a window** / **Dock table** | destaca a tabela para uma janela própria, ou traz de volta |

## A janela destacável

**Open in a window** move a tabela para uma segunda janela, para uma segunda tela. Ela hospeda o
mesmo controle ligado ao mesmo estado, de modo que a seleção, os filtros e as marcações
continuam em sintonia com os painéis da janela principal; nada da revisão se move com ela.
Enquanto está fora, a grade de picos e a evidência ocupam toda a largura da janela principal; a
coluna da tabela não fica vazia.

Três formas a trazem de volta: **Dock table** em qualquer das janelas, fechar a janela, ou arrastar
a janela pela barra de título sobre a parte esquerda da janela principal. Quando a barra de título
cruza os dois quintos esquerdos da janela principal, o lugar que a tabela vai ocupar acende ali —
*Release to dock the ion table here* — e soltar o mouse aí encaixa-a; soltar em qualquer outro lugar
deixa a janela onde foi largada. Se a tabela estava destacada é lembrado para o próximo arranque,
com a ordem de colunas de cada uma.

## Seleção e navegação

Um clique seleciona; `⌘⇧↓` e `⌘⇧↑` movem; `⌘⇧N` salta para a próxima não revisada. A linha
selecionada é mantida quando o filtro muda e ainda a contém, e passa para a primeira linha quando
não. Clicar num ponto do mapa de features, numa linha das tabelas de VIP ou do S-plot, ou num nó
da rede molecular seleciona essa feature aqui, limpando qualquer filtro que a escondesse.

## O que a tabela não mostra

Os números por injeção — altura, área, tempo de retenção, janela de integração, preenchimento de
lacunas — estão na aba de evidência **Samples**, e a média, o desvio padrão e o CV por classe na
aba **Statistics**; veja [[evidence-panels]].

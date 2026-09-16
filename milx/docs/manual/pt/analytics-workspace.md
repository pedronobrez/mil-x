---
title: A área de trabalho Analytics
section: Workspaces
order: 15
summary: A revisão de um resultado processado — como a área de trabalho está organizada, a sua barra de ferramentas e faixa de filtros, e onde cada parte é explicada.
---

# A área de trabalho Analytics

Processar um lote de lipidômica produz alguns milhares de features alinhadas, a maioria errada de
alguma forma. O que transforma isso num resultado é a passagem de revisão, e a do MS-DIAL é a que
os analistas conhecem. Esta área de trabalho segue-a: a tabela de íons é a espinha à esquerda, a
evidência da feature selecionada está à direita, e o veredito está a uma tecla de distância entre
as duas. `⌘2` mostra-a, e um projeto processado abre nela.

![a área de revisão](images/review-ion-table.png)

## A disposição

- **A barra de ferramentas** — a corrida e as exportações.
- **A faixa de filtros** — o que a tabela de íons mostra.
- **A tabela de íons** — toda feature que passa o filtro; veja [[ion-table]]. Pode ser destacada numa janela própria.
- **A identidade** — o nome, id, tempo de retenção, m/z, aduto, fórmula, classe, pontuação e preenchimento da feature selecionada, com uma etiqueta para o seu nível de anotação e outra que diz **hand-picked** quando o revisor escolheu o nome.
- **O veredito** — as cinco marcações, Confirm e Reject, e uma caixa de comentário; veja [[review-tags]].
- **O pico em toda amostra** — o cromatograma da feature em cada injeção, sempre na tela, com a faixa de integração acima; veja [[evidence-panels#Peaks]] e [[reintegration]].
- **As abas de evidência** abaixo dele — MS/MS primeiro, depois Isotopes, Candidates, Abundance, Feature map, Samples, Statistics, Trend; veja [[evidence-panels]]. Um divisor entre os dois define quanto da altura cada um ocupa, e **Split evidence** na barra de ferramentas põe um segundo conjunto ao lado do primeiro.

Um resultado abre nas três coisas de que um revisor precisa para confirmar um analito: a tabela de
íons, o pico em toda amostra e o espectro de produto contra a biblioteca. Nada precisa ser aberto
com um clique antes.

## A barra de ferramentas

| Controle | O que faz |
| --- | --- |
| **Process batch** | corre o pipeline (`⌘R`); veja [[processing]] |
| **Re-export…** | a matriz de alturas e áreas de toda feature, em TSV |
| **Export to MIL-Q…** | as features listadas como uma lista de componentes do OpenQuant; veja [[openquant]] |
| **Export reviewed table…** | as features listadas com a revisão em colunas; veja [[exports]] |
| **Open output folder** | a pasta de resultados no Finder |
| **Run report…** | escreve a corrida — contagens, biblioteca, injeções, método, log e as figuras da tela — num arquivo HTML com as figuras em vetor embutidas; abra e imprima em PDF. Veja [[exports#O relatório da corrida]] |
| **Link polarity…** / **Unlink polarity** | emparelha este resultado com o mesmo lote corrido na outra polaridade, composto a composto, ou esquece o emparelhamento; veja [[polarity-merge]] |
| **Tag both polarities** | leva a revisão através de um par vinculado: um composto, um veredito, desfazer incluído; veja [[polarity-merge]] |
| **Split evidence** | mostra um segundo conjunto de abas de evidência ao lado do primeiro, cada um na sua aba — o espelho ao lado das estatísticas da classe, os candidatos ao lado do mapa de features. Os dois seguem a feature selecionada; desligue e o conjunto único volta a ocupar toda a largura |
| **Save review** | grava as marcações em `<alignment>_tags.xml` — o arquivo que o MS-DIAL lê — e o resto da curadoria ao lado; um ponto no botão significa que há algo por salvar |

O resumo à direita diz `2235 aligned spots across 8 sample(s) · 1804 annotated`, e, depois de uma
edição ou de um salvamento, o que foi feito.

## A faixa de filtros

Os filtros estreitam a tabela de íons e tudo a jusante dela — o mapa de features, as exportações,
**Confirm all shown** — e combinam-se.

| Filtro | Para que serve |
| --- | --- |
| texto livre | nome, classe (ontologia), aduto, fórmula, comentário, um id de feature exato, ou um m/z escrito com quatro decimais |
| **m/z** de–a, **RT** de–a | a região do mapa que se está percorrendo |
| **S/N ≥** | o sinal-ruído médio mínimo; o jeito mais rápido de pôr de lado as features mais fracas sem mexer no corte de altura da corrida |
| **Blank % ≤** | descarta o que os brancos carregam: a altura média nas injeções do tipo Blank contra a média nas amostras. Vinte é um primeiro corte comum; um lote sem branco não é tocado |
| **One row per compound** | esconde os adutos, isótopos, fragmentos de fonte e dímeros reunidos atrás do íon que representa cada composto |
| anotação | **All**, **Confident**, **Suggested** (um nome de pontuação baixa ou só por m/z), **Annotated** (qualquer nome), **Unknown** |
| classe | uma classe de lipídio ou ontologia de cada vez, que é como uma passagem classe a classe se faz |
| estado da revisão | **All**, **Untagged**, **Reviewed**, **Not reviewed**, ou uma marcação específica |
| polaridade | **All**, **Seen in both** ou **Only in this polarity**; ativa depois de vincular uma polaridade — veja [[polarity-merge]] |
| **MS/MS only** | descarta as features anotadas só pela massa |
| **Molecular ion** | descarta as features que a corrida marcou como isótopo de outra |
| **Hand-edited** | só o que um revisor alterou, na anotação ou na integração |

As contagens à direita são as features confirmadas (pílula de destaque), as anotações erradas
(pílula cinza), e `N of M features · R reviewed`. Numa tela de portátil a faixa rola para o lado
em vez de cobrir as contagens.

A feature selecionada é mantida quando sobrevive a um filtro mais apertado, de modo que estreitar
durante a revisão não tira o revisor do lugar. Saltar para uma feature a partir da área de
estatística ou da rede limpa qualquer filtro que a esconda.

## O ciclo de revisão, em resumo

1. Filtre para uma classe, ou para **Not reviewed**.
2. Leia a grade de picos, o MS/MS, os candidatos.
3. `⌘⇧C` para confirmar e seguir, `⌘⇧X` para rejeitar e seguir, `⌘⇧1` a `⌘⇧5` para uma marcação específica, um comentário se for preciso.
4. `⌘⇧N` para a próxima feature que ninguém olhou.
5. **Save review**, ou simplesmente saia: a revisão também é gravada quando se deixa a área de trabalho com marcações por salvar.

Tudo sobre o ciclo está em [[review-tags]].

## Resultados abertos sem projeto

Uma pasta de resultados aberta com **Open results folder…**, ou um alinhamento lido de uma
exportação `.mdalign` em vez dos arquivos binários, pode ser revisada e marcada mas não editada: a
reintegração e a separação precisam do contentor do alinhamento, e a barra de status diz isso.

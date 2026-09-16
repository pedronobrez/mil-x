---
title: Padrões internos e abundância relativa
section: Workspaces
order: 18
summary: Como os analitos confirmados viram razões de área ao padrão interno da sua classe, o diálogo que escolhe o padrão, como a escolha é sugerida, e onde é guardada.
---

# Padrões internos e abundância relativa

Um resultado de lipidômica não é reportado em áreas de pico. Cada analito é reportado como a razão
da sua área pela área de um padrão interno da mesma classe, adicionado a toda amostra na mesma
quantidade antes da extração, de modo que o que quer que tenha acontecido à amostra — o
rendimento da extração, a diluição, a deriva, a supressão iônica naquele tempo de retenção —
aconteceu ao padrão também e se divide fora. Essa razão é a *abundância relativa*, e é a fonte
padrão da [[statistics-workspace]]: **Confirmed analytes, as ratios to a standard**.

Duas coisas entram nela, e as duas vêm da revisão. Os analitos são as features marcadas
**Confirmed** na [[analytics-workspace]] — nada não revisado entra numa razão, que é o sentido de
revisar. O padrão de cada classe é escolhido no diálogo abaixo, entre as features confirmadas
dessa classe, de modo que um padrão tem de ser confirmado como qualquer outra feature antes de
poder ser usado.

## O diálogo

![o diálogo de padrões](images/standards-dialog.png)

**Internal standards…** na faixa abre-o. Uma linha por classe de lipídio com pelo menos um analito
confirmado, mostrando quantos, e uma lista de onde escolher o padrão: os candidatos da própria
classe primeiro, o melhor primeiro — um **standard of this class** (um composto deuterado ou
marcado de outra forma: o nome diz `d7`, `d9`, `13C`, `(d7)`, `-d5`, `IS`), depois um **odd chain,
this class** (uma cadeia 15:0, 17:0 ou 19:0, que não ocorre naturalmente na maioria dos tecidos e
é o padrão não marcado habitual), depois os **standards of other classes**, depois toda outra
feature confirmada por nome — com o tempo de retenção, o m/z e a altura média de cada candidato
para que se distinga dos vizinhos. Uma classe definida como **none — keep the raw area** passa como
áreas, e o resumo na faixa diz isso (`raw: CAR, EtherPC`).

**Suggest again** repõe o padrão sugerido em toda classe; **Clear all** põe toda classe em nenhum;
**Use these standards** aplica a escolha e reconstrói o conjunto de dados; **Cancel** deixa as
coisas como estavam. A sugestão é feita quando o resultado é carregado, de modo que um resultado
cuja revisão confirmou os padrões abre com as razões já calculadas.

## O que a sugestão faz

Para cada classe, a feature confirmada com a maior pontuação: 100 para um padrão marcado da classe,
40 para uma espécie de cadeia ímpar da classe, e nada senão — um padrão de outra classe pontua 20 e
é oferecido mas nunca sugerido, porque dividir PE por um padrão de PC é uma escolha a fazer de
propósito. O próprio padrão é deixado fora das razões (a sua razão consigo mesmo é um), de modo que
uma classe com um padrão e cinco analitos dá cinco razões. Na revisão de validação do lote de
fígado a regra da cadeia ímpar escolheu `PC 33:1`, `LPC 19:1`, `SM 18:1;O2/23:0`, `PE 17:0_17:0`,
`Cer d18:1/17:0` e `LPE 13:0`, que é o que foi adicionado.

## Onde a escolha é guardada

No arquivo lateral da revisão ao lado do arquivo de alinhamento (`<alignment>_curation.json`,
descrito em [[projects-and-files#O arquivo lateral de curadoria]]), sob `InternalStandards`,
indexado por classe, salvo com o resto da revisão. Reabra o projeto e as razões voltam como
estavam. A escolha faz parte da revisão no mesmo sentido que as marcações: mude-a e a revisão fica
por salvar até ser salva.

## Quando a revisão muda

A análise toma o conjunto confirmado quando é carregada. Confirme ou rejeite uma feature depois e a
faixa mostra **Reload from the review**; pressione-o e o conjunto confirmado, os padrões e o
conjunto de dados são tomados de novo. É um botão e não automático porque os testes e os modelos a
jusante levam um momento num conjunto grande, e um revisor marcando ao longo de uma lista não quer
a estatística recalculada a cada tecla.

## O que verificar

- Toda classe que importa tem um padrão, ou você decidiu que não deve ter. A faixa lista as classes que passam brutas.
- O próprio pico do padrão está bom em toda injeção: abra-o na tabela de íons e olhe a grade de revisão. Um padrão ausente ou preenchido numa injeção torna toda razão dessa classe errada para essa injeção, e nada a jusante consegue perceber.
- Os padrões não diferem entre as classes comparadas: na página **Statistical test** com **Confirmed analytes, raw** como fonte, os padrões devem ter fold change perto de um. Se um não tem, a adição ou a extração diferiu, e as razões estão corrigindo isso — que é para o que servem, mas vale a pena saber.

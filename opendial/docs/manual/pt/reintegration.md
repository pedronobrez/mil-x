---
title: Reintegrar e separar picos
section: Reviewing
order: 23
summary: Redesenhar à mão a janela de integração de um pico numa injeção ou em todas, separar uma feature que contém dois compostos, e como a edição é gravada.
---

# Reintegrar e separar picos

A integração automática erra ombros, caudas e picos partidos com frequência suficiente para que um
resultado não esteja terminado até os piores terem sido redesenhados. A faixa **integrate** fica
acima da grade de picos na aba Peaks dos [[evidence-panels]].

![reintegrando uma feature](images/review-integrate.png)

## Desenhar a janela

Duas formas de a definir:

- `⇧`-arrastar sobre qualquer painel da grade. O painel arrastado passa a ser a injeção em foco, e os dois tempos de retenção caem nas caixas **from** e **to**.
- Digitar os dois tempos de retenção, em minutos, nas caixas.

**Reset** devolve às caixas os limites do próprio pico atual. A dica ao lado da faixa diz o que vai
acontecer a seguir.

## Aplicar

**Apply to all** reintegra a feature sobre a janela em toda injeção. **This sample** faz isso só na
injeção em foco — aquela cujo painel foi arrastado, ou cuja linha está selecionada na aba Samples.
A grade carrega cromatogramas uma injeção de cada vez; uma injeção ainda não desenhada é lida
primeiro, de modo que a edição nunca cai sobre um limite desatualizado.

## O que é recalculado

Dentro da janela, para cada injeção: a **altura** passa a ser o ápice, a **área** o trapézio sob o
traço (em intensidade × segundos, a unidade do MS-DIAL), e a área corrigida de linha de base
subtrai a reta que une as duas bordas. O início, o ápice e o fim são movidos para a primeira, a
mais alta e a última varredura dentro da janela. Estas são as definições que a própria corrida
usou, de modo que um pico redesenhado continua comparável com os que estão ao lado.

Os números da própria feature acompanham: altura média, porcentagem de preenchimento, tempo de
retenção médio e largura de pico média. Ela é marcada **manually modified for quantification**,
que é a bandeira que o MS-DIAL exporta e que a tabela revisada grava como *Manually quantified*,
e agora passa o filtro **Hand-edited**. A barra de status relata o que mudou: `Re-integrated
1.812–1.902 min over all samples: 8 changed, 0 skipped · mean height 1,234,567 · fill 100 %`.

## Separar isômeros coeluídos

Quando uma feature alinhada contém dois compostos, **Split isomer** copia-a no lugar. A cópia fica
logo depois da original com id próprio, leva um comentário dizendo de onde veio (`split from
#123`), e começa como duplicata exata, marcada como modificada manualmente. Dê a cada cópia a sua
janela de integração com a faixa, e depois nomeie-as pela tabela de íons ou pela lista de
candidatos. Este é o fluxo "duplicate peak spot" do MS-DIAL.

## Gravar

As duas edições alteram o próprio resultado do alinhamento, não um arquivo lateral. **Save review**
grava o contentor de volta pelo serializador do próprio MS-DIAL, de modo que o resultado editado
abre no MS-DIAL e alimenta os exportadores sem alteração. Os arquivos como estavam antes da
primeira edição da sessão são guardados ao lado com um sufixo `.before-curation` — o contentor do
alinhamento, as suas propriedades de pico, os seus spots de deriva — para que a edição possa ser
desfeita renomeando-os de volta. Veja [[projects-and-files]].

## Quando não é possível

Um resultado aberto a partir de uma exportação `.mdalign` em vez dos arquivos binários de
alinhamento não tem contentor para editar; a barra de status diz isso, e as marcações continuam a
funcionar. O mesmo vale para uma pasta de resultados cujos arquivos `.arf2` faltam.

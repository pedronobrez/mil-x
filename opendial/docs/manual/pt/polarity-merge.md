---
title: As duas polaridades de um lote
section: Reviewing
order: 27
summary: Emparelhar a corrida positiva e a negativa das mesmas amostras — como os compostos são casados pela molécula neutra, o que a tabela de íons passa a mostrar, e qual lado quantifica.
---

# As duas polaridades de um lote

Um fosfolipídio responde em positivo e um ácido graxo livre em negativo, então as duas corridas de
um lote são duas metades da mesma figura. Enquanto não estão vinculadas, são dois projetos, dois
alinhamentos e duas revisões, conciliados à mão numa planilha.

**Link polarity…** na barra do [[analytics-workspace]] pede o resultado de alinhamento do mesmo lote
corrido na outra polaridade — o `.arf2` na sua pasta de saída — e reconcilia os dois, composto a
composto. O botão passa a ler **Unlink polarity**, que esquece o emparelhamento sem tocar em nada
no disco.

## O que é casado, e por quê

Não o m/z: a mesma molécula tem um m/z diferente em cada polaridade. O que é casado é a **molécula
neutra** por trás do íon. O mesmo composto é `[M+H]+` aqui e `[M-H]-` lá, a 2,0146 dáltons de
distância, e a massa neutra por trás de cada um é o mesmo número.

Três coisas têm de concordar antes de duas features serem chamadas de um composto — as mesmas três
do agrupamento por identidade iônica dentro de uma polaridade, descrito em
[[annotation#Um composto, vários íons]]:

| | |
| --- | --- |
| **as massas neutras se encontram** | dentro de 0,01 Da, depois de cada íon ser convertido em seu neutro pelo aduto que a corrida lhe atribuiu. Um aduto que a tabela não conhece deixa o neutro em branco em vez de chutar um próton |
| **eluem juntos** | dentro de 0,1 minuto, que é o que o mesmo método de LC corrido duas vezes dá |
| **as alturas concordam** | as duas corridas são as *mesmas* amostras, então o perfil de altura de um composto ao longo das injeções é uma assinatura. Perfis que se contradizem são recusados |

Cada feature é falada por uma só vez: a oferta mais forte vence, e a segunda melhor oferta para a
mesma feature é uma coincidência, não uma segunda molécula.

## O que a tabela de íons passa a mostrar

Quatro colunas, descritas com as outras em [[ion-table#As colunas]]:

- **Neutral** — a massa neutra por trás do íon. É preenchida haja ou não polaridade vinculada,
  porque é ela que identifica um composto, não um íon.
- **Pol** — `±` quando as duas corridas viram este composto, `+` ou `−` quando só esta viu.
- **Other polarity** — o que a outra corrida viu aqui: o nome quando tinha um, o seu m/z, o seu
  tempo de retenção, e a correlação entre os dois perfis de altura.
- **Quantify** — qual lado carrega o número.

A caixa de **polaridade** na barra de filtros estreita a tabela para **Seen in both** ou **Only in
this polarity**. Só fica ativa depois de uma polaridade ser vinculada.

## Qual lado quantifica

O que mediu melhor o composto, por relação sinal-ruído, com a altura como desempate.

As alturas **nunca são somadas entre as polaridades**. As eficiências de ionização são diferentes,
então a soma de uma altura positiva com uma negativa não é a quantidade de nada. Um lado carrega o
número e o outro confirma a identificação.

## Onde o emparelhamento fica

Ao lado do alinhamento, como `<alinhamento positivo>_polarity-pairs.json` — JSON simples, no mesmo
hábito do arquivo lateral da curadoria descrito em [[projects-and-files]]. Guarda as tolerâncias com
que foi feito, porque um emparelhamento cujos parâmetros se perderam não é um resultado.

Um resultado que estava vinculado abre vinculado: o emparelhamento é *refeito* a partir dos dois
alinhamentos em vez de ser acreditado do arquivo, porque os ids de feature nele não significam nada
se qualquer um dos resultados foi processado de novo desde então.

O [[exports#O relatório da corrida]] nomeia o emparelhamento quando há um.

## O que isso ainda não faz

Os compostos que só a outra polaridade viu são **contados, não listados**: a corrida que você abriu
continua sendo a espinha da revisão, e a sua tabela de íons mostra as suas próprias features.
Revisar as features da outra corrida significa abrir aquele resultado. O mesmo vale para a
estatística, que ainda é calculada sobre um alinhamento de cada vez.

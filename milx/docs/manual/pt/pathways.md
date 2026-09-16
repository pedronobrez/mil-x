---
title: Vias
section: Workspaces
order: 19
summary: A análise de vias segundo o BioPAN do LIPID MAPS — toda reação da rede de lipídios de mamíferos ponderada por produto sobre reagente, comparada entre duas classes, pontuada como Z, e encadeada em vias com os genes por trás delas.
---

# Vias

A [[one-factor-analysis]] diz que lipídios mudaram. Esta página pergunta o que os *fez* mudar: que
passos enzimáticos que transformam um lipídio noutro correram mais depressa numa classe do que na
outra, e que cadeias de passos correram. É o **BioPAN** do LIPID MAPS (Gaud *et al.*, 2021)
embutido na [[statistics-workspace]], lendo o mesmo conjunto de dados que toda outra página — os
analitos confirmados como razões aos seus padrões — de modo que uma reação aqui é pontuada sobre
os mesmos números que o volcano plot mostra, e nada tem de ser exportado e colado num formulário
web. As notas de projeto estão em [[biopan-plan]]; a aritmética em [[algorithms#Vias]].

## O que calcula

A rede de lipídios de mamíferos é a do próprio BioPAN, transcrita da base da ferramenta: 51
reações entre classes de lipídios (PE → PC pela PEMT, PC → LPC pelas fosfolipases A2, LPC → PC
pelas LPCATs, DG → TG pela DGAT2, Cer → SM pelas esfingomielina sintases, PC → PS pela PTDSS1, PA →
PG, PA → PI, PA → PS, as cinases e fosfatases de fosfoinositídeos, e assim por diante), 13 sobre os
lipídios éter (formas alquil `O-` e alquenil `P-` de PC, PE, as suas espécies liso e LPA), 3 sobre
as bases de esfinganina, e 30 entre ácidos graxos — 97 ao todo, sobre 40 classes, com os genes que
o BioPAN nomeia para cada uma. Para toda reação cujo reagente e produto são ambos medidos:

1. **O peso**, por injeção: a abundância do produto dividida pela do reagente. Uma reação correndo
   mais depressa deixa mais produto por unidade de reagente. Uma injeção sem o reagente não tem
   peso; uma sem o produto tem peso zero.
2. **A comparação**: os pesos da primeira classe contra os pesos da segunda, com o teste t de Welch
   sobre os próprios pesos, como o código do BioPAN faz.
3. **O Z-score**: o p unilateral na direção da mudança transformado num quantil normal, Z = Φ⁻¹(1 −
   p), com o sinal da mudança — o `qnorm(1 - p)` do BioPAN. Z é positivo quando o peso da reação é
   mais alto na primeira classe, negativo quando é mais baixo.
4. **O estado**: *active* quando Z passa o limiar (mais depressa na primeira classe), *suppressed*
   quando o passa para o outro lado, *unchanged* entre.

Depois as **vias**: toda cadeia de reações que pode ser percorrida na rede, até um comprimento
escolhido, pontuada combinando os Z-scores das suas reações — a soma dividida pela raiz quadrada
do número de passos, que é o 1/√(n−1) · Σ Zᵢ do BioPAN sobre os n lipídios da cadeia — de modo que
uma cadeia de passos consistentes pontua mais do que qualquer um deles e uma cadeia cujos passos
se cancelam pontua perto de zero. Uma via é ativa ou suprimida ao mesmo limiar.

## A faixa

| Controle | O que faz |
| --- | --- |
| **Level** | **Lipid classes** — todo PC confirmado somado contra todo PE confirmado; **Molecular species** — PE 34:1 contra PC 34:1, e, pela regra do BioPAN, PC 34:1 contra LPC 16:0 quando o FA 18:1 que liberta é medido, LPC 16:0 contra PC 34:1 quando o acil-CoA 18:1 que toma é; **Fatty acids** — os ácidos graxos livres confirmados no conjunto (a classe `FA`), ligados pelos trinta passos de elongação e dessaturação do BioPAN |
| **\|Z\| ≥** | os limiares do BioPAN, unilaterais: 1,282 (p 0,10), **1,645 (p 0,05, o padrão)**, 2,054 (p 0,02), 2,326 (p 0,01) |
| **chains up to** | quantas reações uma via pode encadear (3 por padrão; 1 a 6) |
| **Paired** | as injeções das duas classes correspondem uma a uma, em ordem — o mesmo sujeito antes e depois — e os pesos são comparados pelo teste t pareado, como a opção pareada do BioPAN faz; precisa do mesmo número de injeções em cada classe |
| **Show unchanged** | desenhar também as reações que não passaram o limiar, em cinza |
| **Beyond BioPAN** | também os passos que o MIL-X acrescenta à rede para classes que uma corrida de lipidômica confirma e o BioPAN não cobre — Cer ↔ HexCer ↔ LacCer, HexCer ↔ SHexCer, Chol ↔ CE, FA ↔ CAR, MG → FA, PE → PA — marcados `extension` na tabela; desligado por padrão, de modo que o resultado padrão é a rede do BioPAN e nada mais |
| **Compute** | pontuar a rede entre as duas classes escolhidas na página **Statistical test** |
| **Export reactions…**, **Export pathways…**, **Export list…** | as tabelas como texto separado por tabulações; veja [[exports#As tabelas de análise]] |

As duas classes comparadas são as que a comparação usa — as páginas **Statistical test**, **Fold
change** e **Volcano plot** partilham-nas. As duas precisam de duas ou mais injeções.

## A rede

![a página de vias](images/statistics-pathways.png)

As classes (ou espécies, ou ácidos graxos) são os nós e as reações as setas, apontando do reagente
para o produto. **Verde** é uma reação correndo mais depressa na primeira classe, **roxo** uma
correndo mais devagar, cinza uma que não passou o limiar, pontilhada uma que não pôde ser testada
porque menos de duas injeções numa classe tinham as duas pontas. Quanto mais grossa a seta, maior
o |Z|. Um nó é colorido pela sua própria mudança — verde onde a classe subiu na primeira classe,
roxo onde desceu — de modo que uma seta verde para um nó verde é uma reação que correu mais depressa
*e* produziu mais, e uma seta verde para um nó cinza é uma cujo produto foi consumido tão depressa
quanto foi feito.

Clique numa seta e as suas injeções são desenhadas abaixo, uma caixa por classe, produto sobre
reagente — os números que o teste comparou — com a enzima da reação, o seu p, o seu Z e os seus
genes na linha embaixo. Escolha uma via na tabela e a sua cadeia acende na rede.

## As tabelas

**Reactions** — toda reação que o nível produziu, a mais decisiva primeiro: a sua mudança log2
(primeira classe sobre a segunda), Z, estado e genes. **Pathways** — as cadeias, por |Z|: a própria
cadeia, o seu comprimento *k*, o seu Z e o seu estado. **Not measured** — as reações com uma ponta
confirmada e a outra não: *PC → PS, PS missing* significa que confirmar um PS na revisão tornaria a
PTDSS1 testável. Esta lista é a forma de a página pedir que a revisão vá uma classe mais longe; é
o modo "predicted" do BioPAN. **Nodes** — o que cada nó somou: quantos analitos confirmados
entraram nele e a sua própria mudança log2.

## Como ler

Os pesos das reações são razões de duas abundâncias, cada uma delas uma razão a um padrão, sobre
poucas réplicas, de modo que os Z-scores são um instrumento grosseiro — o BioPAN diz o mesmo do
seu. Leia a rede como se lê o volcano plot: as setas bem além do limiar, numa cadeia que faz
sentido bioquímico, são o achado; uma reação sozinha logo além de 1,645 com três réplicas é uma
pista. Uma classe que subiu como um todo faz toda reação *para* ela parecer ativa e toda reação
*a partir* dela parecer suprimida — PEMT ativa e PLA2 suprimida juntas dizem "o PC subiu", não
"duas enzimas mudaram"; as barras por classe da página **Lipid enrichment** dizem a mesma coisa
mais diretamente, e as duas páginas foram feitas para serem lidas juntas.

Duas coisas que a página precisa da revisão. Uma reação só é testável quando as suas duas classes
têm um analito confirmado *além do padrão* — o padrão divide-se a si próprio nas razões, de modo
que uma classe cuja única feature confirmada é o seu padrão não aparece. E o nível **Molecular
species** segue a regra do BioPAN para as reações que adicionam ou removem uma cadeia: PC 34:1 →
LPC 16:0 só é desenhada quando o ácido graxo que liberta, FA 18:1, está ele próprio confirmado no
conjunto, e LPC 16:0 → PC 34:1 só quando o acil-CoA que toma, 18:1-CoA, está — de modo que numa
corrida sem ácidos graxos livres o nível de espécie mostra as reações que preservam a composição
(PE 34:1 → PC 34:1) e nada mais, que é o que o BioPAN mostra sobre os mesmos dados. O nível **Fatty
acids** é construído a partir dos ácidos graxos livres confirmados, como o BioPAN o constrói — não
a partir das cadeias dos outros lipídios — de modo que uma corrida que não mediu os seus ácidos
graxos livres não tem grafo de ácidos graxos, e a página diz isso.

## O que não é

Só a rede de mamíferos — um resultado de planta ou levedura recebe as reações que se sobrepõem e
nada mais; a tabela é um arquivo de texto (`reactions.tsv` no motor, uma linha por reação com o
seu id, as duas classes, como mapeia espécies, os seus genes, a enzima e a sua fonte) em que a rede
de outro organismo pode ser posta. Duas classes de cada vez, como o BioPAN. Os lipídios éter são
separados em alquil (`O-PC`, `O-PE`, …) e alquenil (`P-PC`, `P-PE`, …) pelo nome que o MS-DIAL
escreveu (`PC O-34:1` contra `PC P-34:1`), as ceramidas e esfingomielinas nas formas de esfingosina
e de esfinganina (`dhCer`, `dhSM`) pela subclasse (`Cer_NDS`) ou por uma composição sem dupla
ligação. A cardiolipina, os fosfoinositídeos e as bases esfingoides estão na rede mas raramente são
confirmados numa corrida em modo positivo, de modo que costumam aparecer só em **Not measured**.
Todo gene é o símbolo humano; a base do BioPAN nomeia as duas estearoil-CoA dessaturases como as de
camundongo `Scd1` e `Scd3`, que aqui são `SCD` e `SCD5`.

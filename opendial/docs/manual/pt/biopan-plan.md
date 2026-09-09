---
title: O BioPAN no OpenDIAL — notas de projeto
section: Under the hood
order: 53
summary: O que o BioPAN do LIPID MAPS faz, como faz, e como a página Pathways da área Statistics o reproduz sobre os analitos revisados — o plano que foi construído, e onde se afasta do original.
---

# O BioPAN no OpenDIAL — notas de projeto

O **BioPAN** (Bioinformatics Methodology for Pathway Analysis, LIPID MAPS, Gaud *et al.*,
*F1000Research* 2021) é a única ferramenta de vias construída para a lipidômica como ela é de fato
medida. Onde o enriquecimento do MetaboAnalyst pergunta se os lipídios que mudaram partilham um
conjunto, o BioPAN pergunta que *reações* — os passos enzimáticos que transformam um lipídio noutro
— estão correndo mais depressa ou mais devagar entre duas condições, pontua as vias em que essas
reações se encadeiam, e nomeia os genes por trás delas. Esta página é o projeto por trás da página
[[pathways]], que é o método do BioPAN embutido na [[statistics-workspace]].

## O que o BioPAN faz

**Entrada.** Uma tabela de nomes de lipídios contra amostras, com pelo menos duas réplicas por
condição, na forma em que um resultado de lipidômica vem — composição somada (`PC 34:1`) ou
espécie molecular (`PC 16:0_18:1`) — e a condição de cada amostra. Os nomes são normalizados com o
LipidLynxX; as espécies de acila graxa são usadas onde dadas e somadas em espécies senão.

**A rede de reações.** Um conjunto curado de reações sobre as subclasses de lipídios de mamíferos,
em dois níveis: entre classes (PE → PC pela PEMT, PC → LPC por uma fosfolipase A, DG → TG pela
DGAT, Cer → SM por uma esfingomielina sintase, e assim por diante) e entre ácidos graxos
(elongação por dois carbonos, dessaturação por uma dupla ligação). Ao nível de espécie uma reação
liga as espécies cuja composição a enzima preservaria.

**Pontuar uma reação.** Para cada reação e cada amostra, o peso é a razão da abundância do produto
pela do reagente. Os pesos são comparados entre as duas condições com um teste t; o p vira um
Z-score pela CDF normal inversa, com o sinal da mudança. Uma reação é *ativa* quando o seu Z passa
o limiar e *suprimida* quando o passa para o outro lado.

**Pontuar uma via.** Uma via é uma cadeia de reações; a sua pontuação combina os Z-scores das suas
reações, normalizados pelo comprimento da cadeia, de modo que uma cadeia longa de reações modestas
e uma curta de reações fortes se comparam com justiça. As vias além do limiar (|Z| > 1,645 para p
< 0,05 unilateral; o BioPAN oferece vários) são reportadas como ativas ou suprimidas, e cada reação
nelas é mapeada para os seus genes a partir da base de proteoma do LIPID MAPS.

**Saída.** Um grafo interativo das subclasses (ou espécies) de lipídios unidas pelas reações, verde
para as que subiram e roxo para as que desceram, com as tabelas de vias, os Z-scores, os valores de
p e as listas de genes ao lado; também um modo *previsto* que lista as reações cujo produto está
presente e o reagente ausente, como candidatas.

## Como o OpenDIAL o constrói

**A entrada já está lá.** A [[statistics-workspace]] constrói os analitos confirmados como razões
aos padrões da sua classe, com a classe de toda injeção, e os nomes do MS-DIAL trazem tanto a
classe quanto a composição, lidas para o enriquecimento da [[one-factor-analysis]]. Nada é
exportado e reimportado, e a revisão — que features são reais — entra de graça.

**A base de reações** é uma tabela de texto distribuída dentro do motor, `reactions.tsv`: uma linha
por reação com o seu id, classe reagente, classe produto, como mapeia espécies (*preserving*,
*removes* uma cadeia, *adds* uma cadeia, só *class*, ou um passo de ácido graxo), os seus genes, o
nome da enzima e a sua fonte. A parte do BioPAN é uma transcrição da base da própria ferramenta —
as tabelas `biopan_reaction` e `biopan_reaction_gene` do código arquivado no OSF — conferida linha a
linha: 51 reações entre classes, 13 sobre os lipídios éter, 3 sobre as bases de esfinganina e 30
entre ácidos graxos, 97 ao todo, com as 249 ligações reação–gene do BioPAN como estão, com duas
edições: FA 24:6 → FA 26:6, que a tabela do BioPAN escreve como FA(34:6), um lapso; e os Scd1 e
Scd3 de camundongo nas duas dessaturações dados como os humanos SCD e SCD5, de modo que a tabela
tem símbolos humanos do início ao fim. Abaixo delas, marcados `extension`, os doze passos que o
OpenDIAL acrescenta para classes que uma corrida confirma e o BioPAN não cobre, ligados por
**Beyond BioPAN** e desligados por padrão. As ontologias do MS-DIAL dobram-se nos nós do BioPAN:
`Cer_NS`, `Cer_AS` e as outras subclasses de esfingosina em `Cer`, as subclasses `DS` e qualquer
composição saturada em `dhCer` (`dhSM` da mesma forma), `Sph` em `SPB`, `DHSph` em `dhSPB`,
`EtherPC` em `O-PC` ou `P-PC` pelo nome. Um teste segura a tabela ao leitor: toda reação nomeia
classes que o leitor de nomes de lipídios conhece, e as contagens são as da base.

**O nível de espécie** segue a regra do BioPAN (`is_valid_reaction` em `lib_parse_data.r`): uma
reação preservadora liga espécies da mesma composição somada; uma que remove cadeia liga uma
espécie reagente a uma espécie produto quando a diferença das suas composições é um ácido graxo
livre medido no conjunto (`PC 34:1` → `LPC 16:0` precisa de `FA 18:1`); uma que adiciona cadeia
quando a diferença é um acil-CoA medido no conjunto (`LPC 16:0` → `PC 34:1` precisa de `FACoA
18:1`). As cadeias resolvidas não são consultadas, como o BioPAN não as consulta. **O nível de
ácido graxo** são os ácidos graxos livres medidos — a classe `FA`, como o `fa_processed_gr` do
BioPAN — ligados pelos trinta passos da sua tabela.

**A pontuação** segue o código R do BioPAN (`lib_pathway_analysis.r`): os pesos são as próprias
razões, uma injeção sem o reagente é descartada e uma sem o produto pesa zero; as duas classes são
comparadas por `t.test` — Welch, ou pareado quando **Paired** está marcado — e Z é `qnorm(1 - p)`
do p unilateral na direção da mudança, que o OpenDIAL calcula como o quantil do p bilateral com o
sinal da diferença das médias, o mesmo número, limitado a cerca de 8 onde p sofre underflow. As
vias são toda cadeia simples de reações testadas até o comprimento escolhido, pontuadas `sum(z) /
sqrt(size)` sobre as reações da cadeia, como o `get_sub_pathway_zscore` do BioPAN faz; o artigo
escreve 1/√(n−1) · Σ Zᵢ sobre os n lipídios da cadeia, a mesma coisa.

**A página** reutiliza os controles da área de trabalho: a rede é um grafo de molas com semente
fixa (a mesma rede desenha-se sempre da mesma forma), as injeções da reação são o gráfico de caixas
que toda outra página usa, as tabelas exportam pela mesma moldura, e o todo responde ao canal de
comando e ao smoke test como o resto.

## Onde se afasta do BioPAN

- As extensões **Beyond BioPAN**, desligadas por padrão e marcadas na tabela.
- Uma reação só é testável quando as suas duas classes têm um analito confirmado além do padrão, já que o padrão se divide a si próprio nas razões. A tabela **Not measured** diz o que confirmar mais uma classe abriria, o que o modo previsto do BioPAN não faz.
- Só a rede de mamíferos. Duas condições de cada vez; três ou mais classes vão par a par pelas duas classes da comparação.

Fontes: Gaud E. *et al.*, "BioPAN: a web-based tool to explore mammalian lipidome metabolic
pathway on LIPID MAPS", *F1000Research* 2021; as páginas do BioPAN em lipidmaps.org/biopan; Nguyen
A. *et al.*, "Using lipidomics analysis to determine signalling and metabolic changes in cells",
*Current Opinion in Biotechnology* 2017, para a rede; LipidLynxX para a normalização de nomes.
